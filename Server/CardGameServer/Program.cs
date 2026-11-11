using System.Net.WebSockets;
using System.Text.Json;
using CardGameServer;
using CardGameServer.Game;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(CardLibrary.Load(Path.Combine(AppContext.BaseDirectory, "Data", "cards.json")));
builder.Services.AddSingleton<RoomManager>();

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(15),
    KeepAliveTimeout = TimeSpan.FromSeconds(30),
});

app.MapGet("/health", (RoomManager rooms) => Results.Ok(new { status = "ok", rooms = rooms.RoomCount }));

// 밸런스 시뮬레이터 웹 화면 (같은 서버의 balancesim 서비스로 넘김)
app.MapSimProxy();

app.Lifetime.ApplicationStopping.Register(() =>
    app.Logger.LogWarning("Server stopping: 접속 중인 클라이언트는 연결이 끊깁니다 (배포/재시작)."));

app.Map("/ws", async (HttpContext context, RoomManager rooms, ILogger<Program> logger) =>
{
    string remote = $"{context.Connection.RemoteIpAddress}:{context.Connection.RemotePort}";

    if (!context.WebSockets.IsWebSocketRequest)
    {
        // 브라우저로 주소를 열었거나, 중간 프록시가 Upgrade 헤더를 지운 경우
        logger.LogWarning("Non-WebSocket request to /ws from {Remote} (Upgrade='{Upgrade}', Connection='{Connection}', UA='{UserAgent}')",
            remote, context.Request.Headers.Upgrade.ToString(), context.Request.Headers.Connection.ToString(),
            context.Request.Headers.UserAgent.ToString());
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    WebSocket socket;
    try
    {
        socket = await context.WebSockets.AcceptWebSocketAsync();
    }
    catch (Exception exception)
    {
        logger.LogWarning("WebSocket accept failed from {Remote}: {Error}", remote, PlayerConnection.DescribeException(exception));
        return;
    }

    using (socket)
    {
        var player = new PlayerConnection(socket, logger) { RemoteEndpoint = remote };
        CancellationToken token = context.RequestAborted;
        string endReason = "알 수 없음";

        logger.LogInformation("Player {Player} connected from {Remote} (UA='{UserAgent}')",
            player.ShortId, remote, context.Request.Headers.UserAgent.ToString());

        Task sendLoop = player.SendLoopAsync(token);
        player.Send(new ServerMessage("welcome", PlayerId: player.PlayerId));

        try
        {
            while (true)
            {
                string? text = await player.ReceiveAsync(token);
                if (text == null)
                {
                    endReason = $"클라이언트가 닫음 (Close {socket.CloseStatus} '{socket.CloseStatusDescription}')";
                    break;
                }

                ClientMessage? message;
                try
                {
                    message = JsonSerializer.Deserialize<ClientMessage>(text, Protocol.Json);
                }
                catch (JsonException exception)
                {
                    logger.LogWarning("Bad JSON from {Player}: {Error} | 내용: {Text}",
                        player.ShortId, exception.Message, text.Length > 300 ? text[..300] + "…" : text);
                    player.Send(new ServerMessage("error", Message: "잘못된 메시지 형식입니다."));
                    continue;
                }

                player.LastReceivedType = message?.Type ?? "(type 없음)";
                if (message?.Type is not ("ping" or "play"))
                {
                    logger.LogDebug("<- {Player} {Text}", player.ShortId, text);
                }

                switch (message?.Type)
                {
                    case "hello":
                        if (!string.IsNullOrWhiteSpace(message.Name))
                        {
                            player.Name = message.Name.Trim()[..Math.Min(message.Name.Trim().Length, 20)];
                        }
                        break;
                    case "create_room":
                        rooms.CreateRoom(player);
                        break;
                    case "join_room":
                        rooms.JoinRoom(player, message.Code);
                        break;
                    case "leave_room":
                        rooms.LeaveRoom(player);
                        break;
                    case "ping":
                        player.Send(new ServerMessage("pong"));
                        break;
                    case "pick":
                        rooms.Pick(player, message.Index);
                        break;
                    case "skip":
                        rooms.Skip(player);
                        break;
                    case "play":
                        rooms.Play(player, message.Index);
                        break;
                    case "rematch":
                        rooms.Rematch(player);
                        break;
                    case "client_error":
                        // 클라이언트가 지난번에 겪은 네트워크 오류. 서버 기록과 맞춰 보기 위해 그대로 남긴다
                        logger.LogWarning("Client error report from {Player} {Remote}:\n{Report}",
                            player.ShortId, remote, message.Message ?? "(내용 없음)");
                        break;
                    default:
                        logger.LogWarning("Unknown message type '{Type}' from {Player}: {Text}",
                            message?.Type, player.ShortId, text.Length > 200 ? text[..200] : text);
                        player.Send(new ServerMessage("error", Message: $"알 수 없는 메시지: {message?.Type}"));
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // RequestAborted: TCP 연결이 끊김 (Close 인사 없이). 와이파이 끊김, 앱 강제 종료, keep-alive 30초 무응답 등
            endReason = player.SecondsSinceLastReceive > 25
                ? $"연결 끊김 (Close 없음, {player.SecondsSinceLastReceive:0}초 무응답 → keep-alive 시간 초과 추정)"
                : "연결 끊김 (Close 없음: 클라이언트 강제 종료/네트워크 끊김)";
        }
        catch (WebSocketException exception)
        {
            endReason = $"WebSocket 오류: {PlayerConnection.DescribeException(exception)}";
        }
        catch (Exception exception)
        {
            endReason = $"서버 처리 중 예외: {exception.GetType().Name}: {exception.Message}";
            logger.LogError(exception, "Unhandled error for {Player}. {Info}", player.ShortId, player.Describe());
        }
        finally
        {
            string roomCode = player.Room?.Code ?? "-";
            string info = player.Describe();

            rooms.LeaveRoom(player);
            player.CompleteOutbox();
            await sendLoop;

            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    logger.LogDebug("Close failed for {Player}: {Error}", player.ShortId, exception.Message);
                }
            }

            bool normal = endReason.StartsWith("클라이언트가 닫음");
            logger.Log(normal ? LogLevel.Information : LogLevel.Warning,
                "Player {Player} disconnected: {Reason} (방 {Room}){SendFailure} | {Info}",
                player.ShortId, endReason, roomCode,
                player.SendFailure is { } failure && !failure.StartsWith("취소") ? $" 보내기 실패: {failure}" : "",
                info);
        }
    }
});

app.Run();
