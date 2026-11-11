using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace CardGameServer;

/// <summary>
/// WebSocket 하나 = 플레이어 하나.
/// WebSocket.SendAsync는 동시 호출이 안 되므로 보내는 메시지는 큐에 넣고 한 루프에서만 보낸다.
/// 연결이 왜 끊겼는지 알 수 있게 주고받은 양, 마지막 메시지, 끊긴 이유를 기록한다.
/// </summary>
public sealed class PlayerConnection
{
    private const int MaxMessageBytes = 4096;
    private const int OutboxCapacity = 256;
    private const int OutboxWarnBacklog = 64;

    private readonly WebSocket socket;
    private readonly ILogger logger;
    private readonly Stopwatch age = Stopwatch.StartNew();
    private readonly Channel<ServerMessage> outbox =
        Channel.CreateBounded<ServerMessage>(new BoundedChannelOptions(OutboxCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    private long lastReceivedTicks;
    private long lastSentTicks;
    private int backlogWarned;

    public string PlayerId { get; } = Guid.NewGuid().ToString("N");
    public string ShortId => PlayerId[..8];
    public string Name { get; set; } = "Player";
    public Room? Room { get; set; }
    public string RemoteEndpoint { get; init; } = "?";

    public PlayerInfo Info => new(PlayerId, Name);

    // ---- 진단용 ----
    public long MessagesReceived { get; private set; }
    public long MessagesSent { get; private set; }
    public long BytesReceived { get; private set; }
    public long BytesSent { get; private set; }
    public string LastReceivedType { get; set; } = "-";
    public string LastSentType { get; private set; } = "-";

    /// <summary>보내기 루프가 왜 멈췄는지 (정상 종료면 null).</summary>
    public string? SendFailure { get; private set; }

    public PlayerConnection(WebSocket socket, ILogger logger)
    {
        this.socket = socket;
        this.logger = logger;
    }

    public double AgeSeconds => age.Elapsed.TotalSeconds;

    public double SecondsSinceLastReceive =>
        lastReceivedTicks == 0 ? AgeSeconds : (age.ElapsedTicks - lastReceivedTicks) / (double)Stopwatch.Frequency;

    public double SecondsSinceLastSend =>
        lastSentTicks == 0 ? AgeSeconds : (age.ElapsedTicks - lastSentTicks) / (double)Stopwatch.Frequency;

    /// <summary>로그 한 줄용 요약.</summary>
    public string Describe() =>
        $"{ShortId}({Name}) {RemoteEndpoint} 방={Room?.Code ?? "-"} 연결 {AgeSeconds:0.0}s, " +
        $"수신 {MessagesReceived}개/{BytesReceived}B(마지막 {LastReceivedType} {SecondsSinceLastReceive:0.0}s 전), " +
        $"송신 {MessagesSent}개/{BytesSent}B(마지막 {LastSentType} {SecondsSinceLastSend:0.0}s 전), " +
        $"대기열 {outbox.Reader.Count}, 소켓 {socket.State}";

    public void Send(ServerMessage message)
    {
        if (!outbox.Writer.TryWrite(message))
        {
            // 이미 연결이 끝나 큐가 닫힌 경우
            logger.LogDebug("Send after close: {Player} {Type}", ShortId, message.Type);
            return;
        }

        // 보내기가 밀리면 (상대 네트워크가 느리거나 막힘) 경고. 가득 차면 오래된 것부터 버려진다
        int backlog = outbox.Reader.Count;
        if (backlog >= OutboxWarnBacklog && Interlocked.Exchange(ref backlogWarned, 1) == 0)
        {
            logger.LogWarning("Outbox backlog {Backlog}/{Capacity} for {Player}: 클라이언트가 메시지를 제때 받지 못함 (느린/막힌 네트워크). {Info}",
                backlog, OutboxCapacity, ShortId, Describe());
        }
        else if (backlog < OutboxWarnBacklog / 2)
        {
            Interlocked.Exchange(ref backlogWarned, 0);
        }
    }

    public async Task SendLoopAsync(CancellationToken token)
    {
        try
        {
            await foreach (ServerMessage message in outbox.Reader.ReadAllAsync(token))
            {
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(message, Protocol.Json);
                long started = Stopwatch.GetTimestamp();
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, token);

                double ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                if (ms > 1000)
                {
                    logger.LogWarning("Slow send {Ms:0}ms to {Player} ({Type}, {Bytes}B): 클라이언트 쪽 수신이 막힘. {Info}",
                        ms, ShortId, message.Type, bytes.Length, Describe());
                }

                MessagesSent++;
                BytesSent += bytes.Length;
                LastSentType = message.Type;
                lastSentTicks = age.ElapsedTicks;
            }
        }
        catch (OperationCanceledException)
        {
            SendFailure ??= "취소됨 (연결 종료)";
        }
        catch (WebSocketException exception)
        {
            SendFailure = DescribeException(exception);
            logger.LogWarning("Send loop failed for {Player}: {Error}. {Info}", ShortId, SendFailure, Describe());
        }
        catch (Exception exception)
        {
            SendFailure = DescribeException(exception);
            logger.LogError(exception, "Send loop crashed for {Player}. {Info}", ShortId, Describe());
        }
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
                logger.LogWarning("Message too big from {Player} (>{Max}B), 연결을 닫음. 앞부분: {Head}",
                    ShortId, MaxMessageBytes, Encoding.UTF8.GetString(buffer, 0, Math.Min(200, length)));
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "message too big", token);
                return null;
            }

            WebSocketReceiveResult result =
                await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), token);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                logger.LogInformation("Client {Player} sent Close {Status} '{Description}'",
                    ShortId, result.CloseStatus, result.CloseStatusDescription);
                return null;
            }

            length += result.Count;

            if (result.EndOfMessage)
            {
                MessagesReceived++;
                BytesReceived += length;
                lastReceivedTicks = age.ElapsedTicks;
                return Encoding.UTF8.GetString(buffer, 0, length);
            }
        }
    }

    /// <summary>예외와 내부 예외를 오류 코드와 함께 한 줄로.</summary>
    public static string DescribeException(Exception exception)
    {
        var builder = new StringBuilder();
        int depth = 0;
        for (Exception? current = exception; current != null && depth < 5; current = current.InnerException, depth++)
        {
            if (depth > 0) builder.Append(" ← ");
            builder.Append(current.GetType().Name).Append(": ").Append(current.Message);
            switch (current)
            {
                case WebSocketException ws:
                    builder.Append($" [WebSocketError={ws.WebSocketErrorCode}, native={ws.NativeErrorCode}]");
                    break;
                case System.Net.Sockets.SocketException se:
                    builder.Append($" [SocketError={se.SocketErrorCode}]");
                    break;
                case IOException io:
                    builder.Append($" [HResult=0x{io.HResult:X8}]");
                    break;
            }
        }
        return builder.ToString();
    }
}
