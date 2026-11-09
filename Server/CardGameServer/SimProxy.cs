namespace CardGameServer;

/// <summary>
/// /sim/* 요청을 같은 서버 안에서 따로 도는 밸런스 시뮬레이터(BalanceSim 웹 모드, 127.0.0.1:5080)로 넘긴다.
/// 시뮬레이터용 포트를 새로 열지 않고 게임 서버 주소(7777) 아래에서 보이게 하려는 것.
/// 비밀번호 확인은 시뮬레이터가 한다 (Authorization 헤더를 그대로 넘김).
/// </summary>
public static class SimProxy
{
    private static readonly string[] SkipResponseHeaders = ["Transfer-Encoding", "Connection", "Keep-Alive"];

    public static void MapSimProxy(this WebApplication app)
    {
        string target = Environment.GetEnvironmentVariable("SIM_URL") ?? "http://127.0.0.1:5080/";
        var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(target),
            Timeout = TimeSpan.FromMinutes(2),
        };

        app.Map("/sim/{**rest}", async (HttpContext ctx, string? rest) =>
        {
            // 페이지 안의 주소가 상대 경로라서 끝에 / 가 있어야 한다 (/sim 과 /sim/ 은 같은 경로로 잡힌다)
            if (ctx.Request.Path.Value == "/sim")
            {
                ctx.Response.Redirect("/sim/" + ctx.Request.QueryString);
                return;
            }

            using var request = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), (rest ?? "") + ctx.Request.QueryString);

            if (ctx.Request.ContentLength > 0 || ctx.Request.Headers.TransferEncoding.Count > 0)
            {
                request.Content = new StreamContent(ctx.Request.Body);
                if (ctx.Request.ContentType != null)
                    request.Content.Headers.TryAddWithoutValidation("Content-Type", ctx.Request.ContentType);
            }

            foreach (string name in new[] { "Authorization", "X-Source", "Accept" })
            {
                if (ctx.Request.Headers.TryGetValue(name, out var value))
                    request.Headers.TryAddWithoutValidation(name, value.ToArray());
            }

            try
            {
                using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
                ctx.Response.StatusCode = (int)response.StatusCode;
                foreach (var header in response.Headers.Concat(response.Content.Headers))
                {
                    if (!SkipResponseHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
                        ctx.Response.Headers[header.Key] = header.Value.ToArray();
                }
                await response.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
            }
            catch (HttpRequestException)
            {
                ctx.Response.StatusCode = StatusCodes.Status502BadGateway;
                await ctx.Response.WriteAsync("밸런스 시뮬레이터가 꺼져 있습니다. (balancesim 서비스)");
            }
        });
    }
}
