using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;

namespace BalanceSim;

/// <summary>
/// 웹 모드(--serve)의 본체. 오라클 서버에서 상시 돈다.
///   - 카드 데이터(데이터 폴더의 cards.json)를 들고 있다가, 파일이 바뀌거나 PC에서 올리면 다시 읽는다
///   - 카드 데이터가 바뀔 때마다(= 기획 수정) 같은 설정으로 자동 실행해서 이전 버전과 비교한다
///   - 버튼으로 요청한 실행을 한 번에 하나씩 순서대로 돌린다 (CPU 1개라 겹치지 않게)
///   - 모드 = 이름 붙인 패치 파일 (데이터 폴더/modes/*.json)
/// </summary>
public sealed class SimService : IDisposable
{
    public const int AutoSeed = 7;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string dataDir;
    private readonly string cardsPath;
    private readonly string modesDir;
    private readonly string historyPath;
    private readonly Channel<RunJob> queue = Channel.CreateUnbounded<RunJob>();
    private readonly List<HistoryEntry> history;
    private readonly FileSystemWatcher watcher;
    private readonly object gate = new();
    private readonly ILogger logger;
    private Timer? reloadDebounce;
    private Stats? lastAuto;
    private string lastAutoVersion = "";

    public string ReportsDir { get; }
    public CardLibrary Library { get; private set; } = null!;
    public CardsInfo Cards { get; private set; } = null!;
    public RunProgress? Current { get; private set; }
    public int Queued => queue.Reader.Count;
    public bool AutoRun { get; set; } = true;
    public int AutoGames { get; }
    public string AutoAis { get; }

    public SimService(string dataDir, string seedCardsPath, ILogger logger, int autoGames, string autoAis)
    {
        this.dataDir = dataDir;
        this.logger = logger;
        AutoGames = autoGames;
        AutoAis = autoAis;
        cardsPath = Path.Combine(dataDir, "cards.json");
        modesDir = Path.Combine(dataDir, "modes");
        ReportsDir = Path.Combine(dataDir, "reports");
        historyPath = Path.Combine(dataDir, "history.json");
        Directory.CreateDirectory(modesDir);
        Directory.CreateDirectory(ReportsDir);

        if (!File.Exists(cardsPath)) File.Copy(seedCardsPath, cardsPath);
        history = File.Exists(historyPath)
            ? JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(historyPath), JsonOptions) ?? new()
            : new();

        LoadCards("서버 시작");

        watcher = new FileSystemWatcher(dataDir, "cards.json") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName };
        watcher.Changed += (_, _) => ScheduleReload();
        watcher.Created += (_, _) => ScheduleReload();
        watcher.Renamed += (_, _) => ScheduleReload();
        watcher.EnableRaisingEvents = true;

        _ = Task.Run(WorkerAsync);
    }

    // =========================================================
    // 카드 데이터
    // =========================================================

    /// <summary>PC(sim-sync)가 올린 cards.json. 읽을 수 있는지 먼저 확인하고 바꾼다.</summary>
    public string UploadCards(string json, string source)
    {
        string temp = Path.Combine(dataDir, "cards.upload.json");
        File.WriteAllText(temp, json);
        CardLibrary.Load(temp); // 깨진 파일이면 여기서 예외, 기존 데이터 유지
        if (Hash(json) == Cards.Hash)
        {
            File.Delete(temp);
            return "변경 없음";
        }
        File.Move(temp, cardsPath, overwrite: true);
        LoadCards(source);
        return "반영됨";
    }

    private void ScheduleReload()
    {
        lock (gate)
        {
            reloadDebounce?.Dispose();
            reloadDebounce = new Timer(_ => LoadCards("파일 변경"), null, TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
        }
    }

    private void LoadCards(string source)
    {
        lock (gate)
        {
            try
            {
                string json = File.ReadAllText(cardsPath);
                string hash = Hash(json);
                if (Cards != null && hash == Cards.Hash) return;

                Library = CardLibrary.Load(cardsPath);
                Cards = new CardsInfo(hash[..8], hash, DateTime.Now, Library.Cards.Count, source);
                logger.LogInformation("cards.json {Version} 로드 ({Source})", Cards.Version, source);

                if (AutoRun && source != "서버 시작")
                    Enqueue(new RunJob { Label = $"카드 데이터 변경 ({source}) → 자동 실행", Games = AutoGames, Ais = AutoAis, Seed = AutoSeed, Auto = true });
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "cards.json을 읽지 못했습니다. 기존 데이터를 유지합니다.");
            }
        }
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));

    // =========================================================
    // 모드 (이름 붙인 패치)
    // =========================================================

    public IReadOnlyList<string> Modes() =>
        Directory.GetFiles(modesDir, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().OrderBy(n => n).ToList();

    public string? ModeJson(string name)
    {
        string path = ModePath(name);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public void SaveMode(string name, string json)
    {
        string path = ModePath(name);
        string temp = path + ".tmp";
        File.WriteAllText(temp, json);
        Patch.Load(temp).ApplyTo(Library); // 없는 카드 이름 등은 여기서 예외
        File.Move(temp, path, overwrite: true);
    }

    public void DeleteMode(string name) => File.Delete(ModePath(name));

    private string ModePath(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains(".."))
            throw new ArgumentException("모드 이름에 쓸 수 없는 문자가 있습니다.");
        return Path.Combine(modesDir, name + ".json");
    }

    private CardLibrary LibraryFor(string? mode) =>
        string.IsNullOrEmpty(mode) ? Library : Patch.Load(ModePath(mode)).ApplyTo(Library);

    // =========================================================
    // 실행
    // =========================================================

    public string Enqueue(RunJob job)
    {
        job.Id = $"{DateTime.Now:yyyyMMdd-HHmmss}-{RandomNumberGenerator.GetHexString(4, true)}";
        queue.Writer.TryWrite(job);
        return job.Id;
    }

    public IReadOnlyList<HistoryEntry> History()
    {
        lock (gate) return history.ToList();
    }

    public Replay Replay(string aiA, string aiB, string? mode, int seed)
    {
        var replay = new Replay { Mode = string.IsNullOrEmpty(mode) ? "현재 수치" : mode };
        Simulator.Play(LibraryFor(mode), AiProfile.All[aiA], AiProfile.All[aiB], seed, replay);
        return replay;
    }

    private async Task WorkerAsync()
    {
        await foreach (RunJob job in queue.Reader.ReadAllAsync())
        {
            try
            {
                Execute(job);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "실행 실패: {Label}", job.Label);
                AddHistory(new HistoryEntry { Id = job.Id, Label = job.Label, Error = exception.Message, CreatedAt = DateTime.Now });
            }
            finally
            {
                Current = null;
            }
        }
    }

    private void Execute(RunJob job)
    {
        var ais = SimRunner.ParseAis(job.Ais);
        string dir = Path.Combine(ReportsDir, job.Id);
        string version = Cards.Version;
        CardLibrary current = Library;
        int total = job.Games * (job.Compare ? 2 : 1);
        var progress = new RunProgress(job.Id, job.Label, total);
        Current = progress;

        var entry = new HistoryEntry
        {
            Id = job.Id, Label = job.Label, CreatedAt = DateTime.Now, Games = job.Games, Ais = job.Ais,
            Mode = job.Mode ?? "", CardsVersion = version, Auto = job.Auto,
        };

        if (job.Compare && !string.IsNullOrEmpty(job.Mode))
        {
            Stats before = SimRunner.Run(current, ais, job.Games, job.Seed, progress.Tick);
            Stats after = SimRunner.Run(LibraryFor(job.Mode), ais, job.Games, job.Seed, progress.Tick);
            Report.Write(Path.Combine(dir, "before"), before, "현재 수치");
            Report.Write(Path.Combine(dir, "after"), after, $"모드: {job.Mode}");
            Report.WriteComparison(dir, before, after, $"현재 → {job.Mode}");
            entry.Fill(after);
            entry.Report = $"reports/{job.Id}/after/report.html";
            entry.Compare = $"reports/{job.Id}/compare.html";
        }
        else
        {
            Stats stats = SimRunner.Run(LibraryFor(job.Mode), ais, job.Games, job.Seed, progress.Tick);
            string title = string.IsNullOrEmpty(job.Mode) ? $"현재 수치 (카드 데이터 {version})" : $"모드: {job.Mode}";
            Report.Write(dir, stats, title);
            entry.Fill(stats);
            entry.Report = $"reports/{job.Id}/report.html";

            // 자동 실행: 같은 설정의 이전 카드 데이터 결과와 비교
            if (job.Auto)
            {
                if (lastAuto != null && lastAutoVersion != version)
                {
                    Report.WriteComparison(dir, lastAuto, stats, $"카드 데이터 {lastAutoVersion} → {version}");
                    entry.Compare = $"reports/{job.Id}/compare.html";
                }
                lastAuto = stats;
                lastAutoVersion = version;
            }
        }

        entry.Seconds = (DateTime.Now - entry.CreatedAt).TotalSeconds;
        AddHistory(entry);
    }

    private void AddHistory(HistoryEntry entry)
    {
        lock (gate)
        {
            history.Insert(0, entry);
            if (history.Count > 200) history.RemoveRange(200, history.Count - 200);
            File.WriteAllText(historyPath, JsonSerializer.Serialize(history, JsonOptions));
        }
    }

    public void Dispose() => watcher.Dispose();
}

public sealed record CardsInfo(string Version, string Hash, DateTime UpdatedAt, int Count, string Source);

public sealed class RunJob
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public int Games { get; set; } = 2000;
    public string Ais { get; set; } = "random,default,greedy";
    public string? Mode { get; set; }
    public bool Compare { get; set; }
    public int Seed { get; set; } = 1;
    public bool Auto { get; set; }
}

public sealed class RunProgress(string id, string label, int total)
{
    private int done;
    public string Id { get; } = id;
    public string Label { get; } = label;
    public int Total { get; } = total;
    public int Done => done;
    public DateTime StartedAt { get; } = DateTime.Now;
    public void Tick() => Interlocked.Increment(ref done);
}

public sealed class HistoryEntry
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public double Seconds { get; set; }
    public int Games { get; set; }
    public string Ais { get; set; } = "";
    public string Mode { get; set; } = "";
    public string CardsVersion { get; set; } = "";
    public bool Auto { get; set; }
    public string? Report { get; set; }
    public string? Compare { get; set; }
    public string? Error { get; set; }
    public double TurnsMedian { get; set; }
    public double DrawRate { get; set; }
    public int Strong { get; set; }
    public int Weak { get; set; }
    public string TopStrong { get; set; } = "";
    public string TopWeak { get; set; } = "";

    public void Fill(Stats s)
    {
        TurnsMedian = s.Summary.TurnsMedian;
        DrawRate = s.Summary.DrawRate;
        var measured = s.Cards.Where(r => !double.IsNaN(r.Delta)).ToList();
        Strong = measured.Count(r => r.Delta * 100 >= BalanceSim.Report.OutlierPp && r.Delta - r.DeltaMargin > 0);
        Weak = measured.Count(r => r.Delta * 100 <= -BalanceSim.Report.OutlierPp && r.Delta + r.DeltaMargin < 0);
        CardRow? top = measured.MaxBy(r => r.Delta), bottom = measured.MinBy(r => r.Delta);
        TopStrong = top == null ? "" : $"{top.Card} {top.Delta * 100:+0.0}%p";
        TopWeak = bottom == null ? "" : $"{bottom.Card} {bottom.Delta * 100:+0.0;-0.0}%p";
    }
}
