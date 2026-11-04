using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace CardGameServer;

/// <summary>
/// WebSocket 하나 = 플레이어 하나.
/// WebSocket.SendAsync는 동시 호출이 안 되므로 보내는 메시지는 큐에 넣고 한 루프에서만 보낸다.
/// </summary>
public sealed class PlayerConnection
{
    private const int MaxMessageBytes = 4096;

    private readonly WebSocket socket;
    private readonly Channel<ServerMessage> outbox =
        Channel.CreateBounded<ServerMessage>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    public string PlayerId { get; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Player";
    public Room? Room { get; set; }

    public PlayerInfo Info => new(PlayerId, Name);

    public PlayerConnection(WebSocket socket)
    {
        this.socket = socket;
    }

    public void Send(ServerMessage message) => outbox.Writer.TryWrite(message);

    public async Task SendLoopAsync(CancellationToken token)
    {
        try
        {
            await foreach (ServerMessage message in outbox.Reader.ReadAllAsync(token))
            {
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(message, Protocol.Json);
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, token);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
    }

    public void CompleteOutbox() => outbox.Writer.TryComplete();

    /// <summary>다음 텍스트 메시지 하나를 읽는다. 연결이 끝나면 null.</summary>
    public async Task<string?> ReceiveAsync(CancellationToken token)
    {
        byte[] buffer = new byte[MaxMessageBytes];
        int length = 0;

        while (true)
        {
            if (length >= buffer.Length)
            {
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "message too big", token);
                return null;
            }

            WebSocketReceiveResult result =
                await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), token);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            length += result.Count;

            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(buffer, 0, length);
            }
        }
    }
}
