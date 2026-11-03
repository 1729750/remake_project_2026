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

app.Map("/ws", async (HttpContext context, RoomManager rooms, ILogger<Program> logger) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
    var player = new PlayerConnection(socket);
    CancellationToken token = context.RequestAborted;

    logger.LogInformation("Player {Player} connected from {Ip}", player.PlayerId, context.Connection.RemoteIpAddress);

    Task sendLoop = player.SendLoopAsync(token);
    player.Send(new ServerMessage("welcome", PlayerId: player.PlayerId));

    try
    {
        while (true)
        {
            string? text = await player.ReceiveAsync(token);
            if (text == null)
            {
                break;
            }

            ClientMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<ClientMessage>(text, Protocol.Json);
            }
            catch (JsonException)
            {
                player.Send(new ServerMessage("error", Message: "잘못된 메시지 형식입니다."));
                continue;
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
                case "play":
                    rooms.Play(player, message.Index);
                    break;
                case "rematch":
                    rooms.Rematch(player);
                    break;
                default:
                    player.Send(new ServerMessage("error", Message: $"알 수 없는 메시지: {message?.Type}"));
                    break;
            }
        }
    }
    catch (OperationCanceledException) { }
    catch (WebSocketException) { }
    finally
    {
        rooms.LeaveRoom(player);
        player.CompleteOutbox();
        await sendLoop;

        if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
        {
            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
            catch (WebSocketException) { }
        }

        logger.LogInformation("Player {Player} disconnected", player.PlayerId);
    }
});

app.Run();
