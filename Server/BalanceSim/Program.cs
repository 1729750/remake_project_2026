// BalanceSim: AI끼리 카드 선택 10라운드 → 전투를 수천 판 돌려 카드/효과/강화 밸런스를 잰다.
//
// dotnet run --project Server/BalanceSim -c Release -- [옵션]
//   --games N        판 수 (기본 4000)
//   --ai a,b,c       AI 목록: random, default, greedy (기본 셋 다, 모든 조합을 돌아가며)
//   --seed S         시드 (기본 1). 같은 시드면 같은 결과
//   --patch FILE     패치 파일. 주면 패치 전/후를 같은 시드로 돌려 비교한다
//   --out DIR        결과 폴더 (기본 Server/BalanceSim/reports/<시각>-<이름>)
// 결과 폴더의 report.html(패치 비교는 compare.html)을 브라우저로 열면 차트와 표로 볼 수 있다.
using System.Diagnostics;
using BalanceSim;

var options = ParseArgs(args);
int games = int.Parse(options.GetValueOrDefault("games", "4000"));
int seed = int.Parse(options.GetValueOrDefault("seed", "1"));
var ais = options.GetValueOrDefault("ai", "random,default,greedy").Split(',', StringSplitOptions.RemoveEmptyEntries)
    .Select(name => AiProfile.All.TryGetValue(name.Trim(), out var ai) ? ai : throw new ArgumentException($"모르는 AI: {name}")).ToList();

CardLibrary baseline = CardLibrary.Load(Path.Combine(AppContext.BaseDirectory, "Data", "cards.json"));
Patch? patch = options.TryGetValue("patch", out string? patchPath) ? Patch.Load(patchPath) : null;

string name = patch?.Name ?? "baseline";
string outDir = options.GetValueOrDefault("out",
    Path.Combine(FindReportsRoot(), $"{DateTime.Now:yyyyMMdd-HHmm}-{Safe(name)}"));

Stats before = Run("패치 전 (현재 수치)", baseline);
Report.Write(Path.Combine(outDir, patch == null ? "" : "before"), before, patch == null ? "현재 밸런스" : "패치 전");

if (patch != null)
{
    Stats after = Run($"패치 후: {patch.Name}", patch.ApplyTo(baseline));
    Report.Write(Path.Combine(outDir, "after"), after, $"패치 후: {patch.Name}");
    Report.WriteComparison(outDir, before, after, patch.Name);
    Console.WriteLine(File.ReadAllText(Path.Combine(outDir, "compare.md")));
}
else
{
    Console.WriteLine(Report.Summary(before, "현재 밸런스"));
}

Console.WriteLine($"결과: {Path.GetFullPath(outDir)}");
Console.WriteLine($"브라우저로 보기: {Path.GetFullPath(Path.Combine(outDir, patch == null ? "report.html" : "compare.html"))}");

Stats Run(string label, CardLibrary library)
{
    // 판의 절반은 같은 AI끼리(카드 지표용), 절반은 다른 AI끼리(상성용)가 되도록 조합을 배정. 시드 = 기본 시드 + 판 번호
    var cross = ais.SelectMany(x => ais.Where(y => y != x).Select(y => (x, y))).ToList();
    var mirror = ais.SelectMany(x => Enumerable.Repeat((x, x), Math.Max(1, ais.Count - 1))).ToList();
    var pairs = mirror.Concat(cross).ToList();
    var records = new GameRecord[games];
    var clock = Stopwatch.StartNew();

    Parallel.For(0, games, i =>
    {
        var (aiA, aiB) = pairs[i % pairs.Count];
        records[i] = Simulator.Play(library, aiA, aiB, unchecked(seed * 1_000_003 + i));
    });

    Console.Error.WriteLine($"{label}: {games:N0}판 {clock.Elapsed.TotalSeconds:0.0}초");
    return Stats.From(records, library);
}

static Dictionary<string, string> ParseArgs(string[] args)
{
    var result = new Dictionary<string, string>();
    for (int i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--")) throw new ArgumentException($"옵션은 --이름 값 형태: {args[i]}");
        result[args[i][2..]] = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true";
    }
    return result;
}

// 실행 위치와 상관없이 Server/BalanceSim/reports 를 찾는다
static string FindReportsRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "BalanceSim.csproj")))
            return Path.Combine(dir.FullName, "reports");
    }
    return Path.Combine(Directory.GetCurrentDirectory(), "reports");
}

static string Safe(string s) => string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));
