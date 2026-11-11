// BalanceSim: AI끼리 카드 선택 10라운드 → 전투를 수천 판 돌려 카드/효과/강화 밸런스를 잰다.
//
// 웹 모드 (오라클 서버에서 상시 실행, 브라우저 버튼으로 실행/모드/관전): --serve  → WebApp.cs
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

if (args.Contains("--serve"))
{
    WebApp.Run(args.Where(a => a != "--serve").ToArray());
    return;
}

var options = ParseArgs(args);
int games = int.Parse(options.GetValueOrDefault("games", "4000"));
int seed = int.Parse(options.GetValueOrDefault("seed", "1"));
var ais = SimRunner.ParseAis(options.GetValueOrDefault("ai", "random,default,greedy"));

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
    var clock = Stopwatch.StartNew();
    Stats stats = SimRunner.Run(library, ais, games, seed);
    Console.Error.WriteLine($"{label}: {games:N0}판 {clock.Elapsed.TotalSeconds:0.0}초");
    return stats;
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
