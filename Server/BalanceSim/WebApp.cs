using System.Text;
using Microsoft.Extensions.FileProviders;

namespace BalanceSim;

/// <summary>
/// 웹 모드: dotnet BalanceSim.dll --serve
/// 오라클 서버에서는 게임 서버가 /sim/ 아래로 이 앱을 그대로 넘겨 준다 (페이지 안의 주소는 전부 상대 경로).
/// 환경 변수: SIM_DATA(데이터 폴더), SIM_PASSWORD(접속 비밀번호, 비우면 없음), SIM_AUTO_GAMES, SIM_AUTO_AIS, ASPNETCORE_URLS
/// </summary>
public static class WebApp
{
    public static void Run(string[] args)
    {
        // 정적 파일(wwwroot): 배포본은 실행 파일 옆, 개발 중(dotnet build)에는 프로젝트 폴더
        string baseDir = AppContext.BaseDirectory;
        string webRoot = Path.Combine(baseDir, "wwwroot");
        for (var dir = new DirectoryInfo(baseDir); !Directory.Exists(webRoot) && dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "BalanceSim.csproj"))) webRoot = Path.Combine(dir.FullName, "wwwroot");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = baseDir, WebRootPath = webRoot });
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        var app = builder.Build();

        string dataDir = Environment.GetEnvironmentVariable("SIM_DATA") ?? Path.Combine(AppContext.BaseDirectory, "simdata");
        string password = Environment.GetEnvironmentVariable("SIM_PASSWORD") ?? "";
        int autoGames = int.TryParse(Environment.GetEnvironmentVariable("SIM_AUTO_GAMES"), out int g) ? g : 1500;
        string autoAis = Environment.GetEnvironmentVariable("SIM_AUTO_AIS") ?? "random,default,greedy";

        var sim = new SimService(dataDir, Path.Combine(AppContext.BaseDirectory, "Data", "cards.json"),
            app.Logger, autoGames, autoAis);

        // 간단한 비밀번호 (HTTP Basic). 사용자 이름은 아무거나
        if (password.Length > 0)
        {
            app.Use(async (ctx, next) =>
            {
                string header = ctx.Request.Headers.Authorization.ToString();
                bool ok = header.StartsWith("Basic ") && TryDecode(header[6..], out string pair) && pair[(pair.IndexOf(':') + 1)..] == password;
                if (!ok)
                {
                    ctx.Response.StatusCode = 401;
                    ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"BalanceSim\", charset=\"UTF-8\"";
                    return;
                }
                await next();
            });
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(sim.ReportsDir), RequestPath = "/reports" });

        app.MapGet("/api/status", () => new
        {
            cards = sim.Cards,
            running = sim.Current == null ? null : new { sim.Current.Id, sim.Current.Label, sim.Current.Done, sim.Current.Total, sim.Current.StartedAt },
            queued = sim.Queued,
            autoRun = sim.AutoRun,
            autoGames = sim.AutoGames,
            ais = AiProfile.All.Keys,
            modes = sim.Modes(),
            history = sim.History().Take(60),
        });

        app.MapPost("/api/autorun", (AutoRunRequest req) => { sim.AutoRun = req.Enabled; return Results.Ok(); });

        app.MapPost("/api/run", (RunJob job) =>
        {
            job.Games = Math.Clamp(job.Games, 100, 50_000);
            SimRunner.ParseAis(job.Ais);
            if (string.IsNullOrWhiteSpace(job.Label))
                job.Label = string.IsNullOrEmpty(job.Mode) ? "현재 수치" : job.Compare ? $"현재 vs {job.Mode}" : $"모드: {job.Mode}";
            job.Auto = false;
            return Results.Ok(new { id = sim.Enqueue(job) });
        });

        // 현재 카드 데이터 (모드 편집 화면이 쓴다)
        app.MapGet("/api/cards", () => new
        {
            info = sim.Cards,
            cards = sim.Library.Cards.Select(c => new
            {
                c.Name, c.Cost, c.Cooldown, cardType = c.CardType.ToString(),
                effects = c.Effects.Select(e => new { type = e.Type.ToString(), e.Magnitude, target = e.Target.ToString() }),
                inStartDeck = sim.Library.StartDeck.Count(s => s.Name == c.Name),
            }),
            prices = Enum.GetValues<EffectType>().Select(t => new
            {
                effect = t.ToString(), sim.Library.PriceOf(t).Price, sim.Library.PriceOf(t).MagnitudeMin,
                sim.Library.PriceOf(t).MagnitudeMax, sim.Library.PriceOf(t).MagnitudeUnit,
            }),
        });

        // PC의 sim-sync가 Unity 에셋에서 만든 cards.json을 올린다
        app.MapPost("/api/cards", async (HttpRequest request) =>
        {
            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            string json = await reader.ReadToEndAsync();
            // 헤더에는 한글을 못 넣으므로 URL 인코딩해서 보낸다
            string source = Uri.UnescapeDataString(request.Headers["X-Source"].FirstOrDefault() ?? "업로드");
            try
            {
                return Results.Ok(new { result = sim.UploadCards(json, source), version = sim.Cards.Version });
            }
            catch (Exception exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        app.MapGet("/api/modes/{name}", (string name) => sim.ModeJson(name) is string json ? Results.Text(json, "application/json") : Results.NotFound());
        app.MapPost("/api/modes/{name}", async (string name, HttpRequest request) =>
        {
            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            try
            {
                sim.SaveMode(name, await reader.ReadToEndAsync());
                return Results.Ok();
            }
            catch (Exception exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });
        app.MapDelete("/api/modes/{name}", (string name) => { sim.DeleteMode(name); return Results.Ok(); });

        // AI 한 판 관전
        app.MapGet("/api/replay", (string? aiA, string? aiB, string? mode, int? seed) =>
        {
            aiA ??= "default";
            aiB ??= "greedy";
            if (!AiProfile.All.ContainsKey(aiA) || !AiProfile.All.ContainsKey(aiB)) return Results.BadRequest(new { error = "모르는 AI" });
            return Results.Ok(sim.Replay(aiA, aiB, mode, seed ?? Random.Shared.Next()));
        });

        app.Lifetime.ApplicationStopping.Register(sim.Dispose);
        app.Run();
    }

    private static bool TryDecode(string base64, out string text)
    {
        try
        {
            text = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            return text.Contains(':');
        }
        catch (FormatException)
        {
            text = "";
            return false;
        }
    }

    public sealed record AutoRunRequest(bool Enabled);
}
