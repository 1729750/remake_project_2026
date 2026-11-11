using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 오라클 서버와의 WebSocket 연결 하나를 씬 전환 후에도 유지한다.
///
/// 받은 메시지는 백그라운드 스레드에서 큐에 쌓고, Update(메인 스레드)에서 MessageReceived로 꺼내 준다.
/// 그래서 이벤트 핸들러에서는 바로 Unity 오브젝트를 만져도 된다.
///
/// 네트워크 오류는 OracleNetDiagnostics로 원인을 풀어 쓰고, 화면 왼쪽 위(OracleNetDebugOverlay)에 띄운다.
/// F3 = 연결 상태/최근 로그. 로그 파일은 persistentDataPath/oracle-net.log.
///
/// 주의: ClientWebSocket은 WebGL 빌드에서 동작하지 않는다. (PC 빌드 / 에디터 전용)
/// </summary>
public sealed class OracleServerClient : MonoBehaviour
{
    public static OracleServerClient Instance { get; private set; }

    [SerializeField]
    private string serverUrl = "ws://161.33.142.112:7777/ws";

    [SerializeField]
    private float connectTimeoutSeconds = 5f;

    [Header("Debug")]
    [Tooltip("보내고 받는 메시지를 전부 콘솔에 찍는다 (state 제외)")]
    [SerializeField]
    private bool logMessages = true;

    [Tooltip("ping 응답이 이 시간(초) 넘게 없으면 경고")]
    [SerializeField]
    private float stallWarnSeconds = 3f;

    [Tooltip("ping 응답이 이 시간(초) 넘게 없으면 오류로 띄운다")]
    [SerializeField]
    private float stallErrorSeconds = 10f;

    private ClientWebSocket socket;
    private CancellationTokenSource lifetime;
    private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
    private readonly ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();

    public bool IsConnected => socket != null && socket.State == WebSocketState.Open;

    /// <summary>서버가 welcome으로 알려준 내 ID.</summary>
    public string PlayerId { get; private set; } = string.Empty;

    // 씬이 바뀌는 동안 온 메시지를 새 씬이 이어받을 수 있게 방 상태와 마지막 게임 상태를 기억해 둔다.
    // (로비 씬에서 room_ready를 받고 게임 씬을 로드하는 사이에 첫 state가 도착한다)

    /// <summary>들어가 있는 방 코드. 방 밖이면 빈 문자열.</summary>
    public string RoomCode { get; private set; } = string.Empty;

    public bool InRoom => !string.IsNullOrEmpty(RoomCode);

    /// <summary>방에 있는 플레이어 (먼저 들어온 순서 = 방장이 0번).</summary>
    public List<PlayerInfo> RoomPlayers { get; } = new List<PlayerInfo>();

    /// <summary>마지막으로 받은 state 메시지. 방을 나가면 null.</summary>
    public ServerMessage LastState { get; private set; }

    private static readonly Stopwatch clock = Stopwatch.StartNew();

    /// <summary>
    /// 스레드에서도 읽을 수 있는 단조 시계(초). 메시지 도착 시각(ServerMessage.receivedAt)과 같은 기준이라
    /// 타이머는 Time.* 대신 이 값으로 계산한다.
    /// </summary>
    public static double Now => clock.Elapsed.TotalSeconds;

    private const float PingInterval = 2f;
    private double pingSentAt = -1;
    private double nextPingAt;

    /// <summary>서버까지 왕복 시간(초). 받은 state는 이 값의 절반만큼 전에 만들어진 것으로 본다.</summary>
    public float RoundTripTime { get; private set; }

    public event Action Connected;
    public event Action Disconnected;
    public event Action<ServerMessage> MessageReceived;

    // ---- 진단용 기록 ----
    private int connectAttempts;
    private int connectFailures;
    private int unexpectedDisconnects;
    private double connectedAt = -1;
    private double lastReceivedAt = -1;
    private double lastSentAt = -1;
    private long messagesSent;
    private long messagesReceived;
    private long bytesSent;
    private long bytesReceived;
    private string lastReceivedType = "-";
    private string lastSentType = "-";
    private float lastRttSample;
    private float maxRttSample;
    private bool stallWarned;
    private bool stallReported;
    private string lastCloseInfo = "-";
    private string lastHealthProbe = "-";

    /// <summary>다음에 연결되면 서버 로그에도 남기도록 보낼 오류 보고 (한 번 보내면 비운다).</summary>
    private string pendingServerReport;

    /// <summary>마지막 오류 한 줄 요약 (로비 상태 글자에 붙여 쓴다). 없으면 빈 문자열.</summary>
    public string LastErrorSummary =>
        OracleNetDiagnostics.LastError == null ? string.Empty : OracleNetDiagnostics.LastError.Summary;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (GetComponent<OracleNetDebugOverlay>() == null)
            gameObject.AddComponent<OracleNetDebugOverlay>();

        OracleNetDiagnostics.Info($"클라이언트 시작: {Application.productName} {Application.version}, Unity {Application.unityVersion}, " +
                                  $"{SystemInfo.operatingSystem}, 서버 {serverUrl}, 인터넷 상태 {Application.internetReachability}");
    }

    private void Update()
    {
        while (mainThreadQueue.TryDequeue(out Action action))
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                // 메시지 처리(화면 갱신) 중 오류. 네트워크 문제는 아니지만 같이 띄워 둔다
                Debug.LogException(exception);
                OracleNetDiagnostics.Error(new NetError
                {
                    Stage = "메시지 처리",
                    Summary = $"받은 메시지를 처리하다 예외 ({lastReceivedType})",
                    Hint = "네트워크가 아니라 클라이언트 코드 문제입니다. Unity 콘솔의 스택을 보세요.",
                    Detail = OracleNetDiagnostics.ExceptionChain(exception),
                });
            }
        }

        // 왕복 시간 측정: 응답을 기다리는 ping이 없을 때만 주기적으로 보낸다
        if (IsConnected && pingSentAt < 0 && Now >= nextPingAt)
        {
            pingSentAt = Now;
            Send(ClientMessage.Ping());
        }

        CheckStall();
    }

    /// <summary>ping을 보냈는데 응답이 안 오면 "연결은 살아 있는 척하지만 실제로는 막힌" 상태다.</summary>
    private void CheckStall()
    {
        if (!IsConnected || pingSentAt < 0) return;

        double waiting = Now - pingSentAt;
        double silence = lastReceivedAt < 0 ? waiting : Now - lastReceivedAt;

        if (!stallWarned && waiting > stallWarnSeconds)
        {
            stallWarned = true;
            OracleNetDiagnostics.Warn($"서버 응답 지연: ping 보낸 지 {waiting:0.0}초, 마지막 수신 {silence:0.0}초 전 ({lastReceivedType})");
        }

        if (!stallReported && waiting > stallErrorSeconds)
        {
            stallReported = true;
            OracleNetDiagnostics.Error(new NetError
            {
                Stage = "응답 없음",
                Summary = $"{waiting:0}초 동안 서버 응답이 없습니다 (소켓은 아직 열린 상태)",
                Hint = "와이파이가 잠깐 끊겼거나, 공유기/통신사가 패킷을 버리고 있습니다. 서버가 멈췄을 수도 있습니다. " +
                       "곧 회복되면 이 창은 닫아도 됩니다. 30초 안에 회복되지 않으면 연결이 끊긴 것으로 처리됩니다.",
                Detail = DescribeStatus(),
            });
        }
    }

    private void MeasurePong(double receivedAt)
    {
        if (pingSentAt < 0) return;
        float sample = (float)(receivedAt - pingSentAt);
        pingSentAt = -1;
        nextPingAt = Now + PingInterval;

        if (stallWarned)
            OracleNetDiagnostics.Info($"서버 응답 회복 (ping 왕복 {sample * 1000f:0}ms)");
        stallWarned = false;
        stallReported = false;

        // 갑자기 튀면 기록 (와이파이 품질 확인용)
        if (RoundTripTime > 0f && sample > 0.3f && sample > RoundTripTime * 3f)
            OracleNetDiagnostics.Warn($"왕복 시간 급증: {sample * 1000f:0}ms (평균 {RoundTripTime * 1000f:0}ms)");

        lastRttSample = sample;
        maxRttSample = Mathf.Max(maxRttSample, sample);

        // 튀는 값에 흔들리지 않게 부드럽게
        RoundTripTime = RoundTripTime <= 0f ? sample : Mathf.Lerp(RoundTripTime, sample, 0.3f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            CloseSocket();
        }
    }

    /// <summary>이미 연결돼 있으면 바로 true. 실패하면 false. (실패 원인은 OracleNetDiagnostics.LastError)</summary>
    public async Task<bool> ConnectAsync()
    {
        if (IsConnected)
        {
            return true;
        }

        CloseSocket();

        connectAttempts++;
        lifetime = new CancellationTokenSource();
        socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        ClientWebSocket ws = socket;
        CancellationTokenSource myLifetime = lifetime;

        OracleNetDiagnostics.Info($"연결 시도 #{connectAttempts}: {serverUrl} (제한 {connectTimeoutSeconds:0}초, 인터넷 상태 {Application.internetReachability})");
        var started = Stopwatch.StartNew();
        bool timedOut = false;

        try
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(myLifetime.Token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(connectTimeoutSeconds));
                try
                {
                    await ws.ConnectAsync(new Uri(serverUrl), timeout.Token);
                }
                catch (OperationCanceledException) when (!myLifetime.IsCancellationRequested)
                {
                    timedOut = true;
                    throw;
                }
            }
        }
        catch (Exception exception)
        {
            long elapsed = started.ElapsedMilliseconds;
            bool cancelledByUs = myLifetime.IsCancellationRequested && !timedOut;
            connectFailures++;

            if (cancelledByUs)
            {
                OracleNetDiagnostics.Info($"연결 시도 #{connectAttempts} 취소됨 ({elapsed}ms)");
                return false;
            }

            NetError error = OracleNetDiagnostics.Describe("연결", exception, timedOut);
            error.Summary = $"서버 연결 실패 ({elapsed}ms): {error.Summary}";

            if (socket == ws) CloseSocket();

            // 원인을 좁히려고 HTTP로 한 번 더 찔러 본다
            OracleNetDiagnostics.Info("서버 상태 확인 중 (/health)...");
            lastHealthProbe = await OracleNetDiagnostics.ProbeHealthAsync(serverUrl);
            error.Detail = $"{error.Detail}\n{lastHealthProbe}\n시도 {connectAttempts}회 중 실패 {connectFailures}회, 소켓 상태 {ws.State}";

            OracleNetDiagnostics.Error(error);
            pendingServerReport = error.ToString();
            return false;
        }

        if (socket != ws)
        {
            // 연결하는 사이 Disconnect()가 불렸다
            ws.Dispose();
            return false;
        }

        connectedAt = Now;
        lastReceivedAt = -1;
        stallWarned = false;
        stallReported = false;
        OracleNetDiagnostics.Info($"연결됨: {serverUrl} ({started.ElapsedMilliseconds}ms, 시도 #{connectAttempts})");
        _ = ReceiveLoopAsync(ws, myLifetime.Token);
        Connected?.Invoke();

        // 지난번 오류를 서버 로그에도 남긴다 (서버 쪽 기록과 맞춰 보기 위해)
        if (!string.IsNullOrEmpty(pendingServerReport))
        {
            string report = pendingServerReport;
            pendingServerReport = null;
            Send(ClientMessage.ClientError(Truncate(report, 1500)));
        }

        return true;
    }

    public async void Send(ClientMessage message)
    {
        if (!IsConnected)
        {
            OracleNetDiagnostics.Warn($"연결 안 됨, 보내지 못함: {message.type} (소켓 {(socket == null ? "없음" : socket.State.ToString())}, 마지막 종료: {lastCloseInfo})");
            return;
        }

        string json = JsonUtility.ToJson(message);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        // 기다리는 동안 Disconnect될 수 있으니 지금의 소켓/토큰을 잡아 둔다
        ClientWebSocket ws = socket;
        CancellationToken token = lifetime.Token;

        if (logMessages && message.type != "ping")
            OracleNetDiagnostics.Info($"→ {json}");

        var waited = Stopwatch.StartNew();
        await sendLock.WaitAsync();
        try
        {
            if (waited.ElapsedMilliseconds > 500)
                OracleNetDiagnostics.Warn($"보내기 대기 {waited.ElapsedMilliseconds}ms ({message.type}) — 앞의 전송이 막혀 있었습니다 (업로드 정체).");

            var sending = Stopwatch.StartNew();
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
            if (sending.ElapsedMilliseconds > 1000)
                OracleNetDiagnostics.Warn($"보내기에 {sending.ElapsedMilliseconds}ms 걸림 ({message.type}) — 네트워크 업로드가 막히고 있습니다.");

            messagesSent++;
            bytesSent += bytes.Length;
            lastSentAt = Now;
            lastSentType = message.type;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // 직접 닫는 중
        }
        catch (Exception exception)
        {
            NetError error = OracleNetDiagnostics.Describe("보내기", exception);
            error.Summary = $"'{message.type}' 보내기 실패: {error.Summary}";
            error.Detail = $"{error.Detail}\n소켓 상태 {ws.State}, 연결 후 {ConnectedSeconds():0.0}초";
            OracleNetDiagnostics.Error(error);
            pendingServerReport = error.ToString();
        }
        finally
        {
            sendLock.Release();
        }
    }

    public void Disconnect()
    {
        bool wasConnected = socket != null;
        if (wasConnected)
            OracleNetDiagnostics.Info($"직접 연결 종료 (연결 후 {ConnectedSeconds():0.0}초)");

        CloseSocket();

        if (wasConnected)
        {
            Disconnected?.Invoke();
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken token)
    {
        var buffer = new byte[8192];
        var builder = new StringBuilder();
        string endReason = "알 수 없음";
        NetError error = null;

        try
        {
            while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    endReason = $"서버가 연결을 닫음 (Close {(int?)result.CloseStatus} {result.CloseStatus}: {result.CloseStatusDescription})";
                    // 서버가 비정상 이유로 닫았으면 오류로 띄운다
                    if (result.CloseStatus != WebSocketCloseStatus.NormalClosure && result.CloseStatus != WebSocketCloseStatus.EndpointUnavailable)
                    {
                        error = new NetError
                        {
                            Stage = "받기",
                            Summary = endReason,
                            Hint = result.CloseStatus == WebSocketCloseStatus.MessageTooBig
                                ? "보낸 메시지가 서버 제한(4KB)보다 큽니다."
                                : "서버가 이 연결을 끊었습니다. 서버 로그를 확인하세요 (journalctl -u cardgame).",
                        };
                    }
                    else if (result.CloseStatus == WebSocketCloseStatus.EndpointUnavailable)
                    {
                        error = new NetError
                        {
                            Stage = "받기",
                            Summary = endReason,
                            Hint = "서버가 종료/재시작되는 중입니다 (배포 중일 수 있음). 잠시 후 다시 시도하세요.",
                        };
                    }
                    break;
                }

                bytesReceived += result.Count;
                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (!result.EndOfMessage)
                {
                    continue;
                }

                string json = builder.ToString();
                builder.Clear();
                double receivedAt = Now;
                lastReceivedAt = receivedAt;
                messagesReceived++;
                mainThreadQueue.Enqueue(() => Dispatch(json, receivedAt));
            }

            if (token.IsCancellationRequested) endReason = "직접 닫음";
            else if (ws.State != WebSocketState.Open && endReason == "알 수 없음") endReason = $"소켓 상태가 {ws.State}로 바뀜";
        }
        catch (OperationCanceledException)
        {
            endReason = "직접 닫음";
        }
        catch (Exception exception)
        {
            error = OracleNetDiagnostics.Describe("받기", exception);
            endReason = error.Summary;
            error.Summary = $"연결 끊김: {error.Summary}";
        }

        double connectedFor = connectedAt < 0 ? 0 : Now - connectedAt;
        double silence = lastReceivedAt < 0 ? connectedFor : Now - lastReceivedAt;
        lastCloseInfo = $"{endReason} (연결 {connectedFor:0.0}초 유지, 마지막 수신 {silence:0.0}초 전)";

        // 직접 Disconnect()한 경우가 아니면 끊김을 알린다
        if (!token.IsCancellationRequested)
        {
            unexpectedDisconnects++;

            if (error == null)
            {
                error = new NetError
                {
                    Stage = "받기",
                    Summary = $"연결 끊김: {endReason}",
                    Hint = "서버 재시작(배포), 와이파이 끊김, 공유기의 연결 정리 등으로 끊겼습니다.",
                };
            }

            error.Detail = (string.IsNullOrEmpty(error.Detail) ? string.Empty : error.Detail + "\n") +
                           $"연결 {connectedFor:0.0}초 유지, 마지막 수신 {silence:0.0}초 전 ({lastReceivedType}), " +
                           $"방 {(InRoom ? RoomCode : "없음")}, 받은 메시지 {messagesReceived}개, 보낸 메시지 {messagesSent}개, " +
                           $"평균 왕복 {RoundTripTime * 1000f:0}ms, 예기치 않은 끊김 {unexpectedDisconnects}회째";

            NetError report = error;
            pendingServerReport = report.ToString();
            OracleNetDiagnostics.Error(report);

            mainThreadQueue.Enqueue(() =>
            {
                if (socket == ws)
                {
                    CloseSocket();
                    Disconnected?.Invoke();
                }
            });
        }
        else
        {
            OracleNetDiagnostics.Info($"수신 종료: {lastCloseInfo}");
        }
    }

    private void Dispatch(string json, double receivedAt)
    {
        ServerMessage message;
        try
        {
            message = JsonUtility.FromJson<ServerMessage>(json);
        }
        catch (Exception exception)
        {
            OracleNetDiagnostics.Error(new NetError
            {
                Stage = "메시지 해석",
                Summary = "서버 메시지를 JSON으로 읽지 못했습니다",
                Hint = "서버와 클라이언트의 메시지 형식(ServerProtocol.cs ↔ Protocol.cs/Views.cs)이 맞지 않을 수 있습니다.",
                Detail = $"{OracleNetDiagnostics.ExceptionChain(exception)}\n받은 내용: {Truncate(json, 400)}",
            });
            return;
        }

        if (message != null) message.receivedAt = receivedAt;

        if (message == null || string.IsNullOrEmpty(message.type))
        {
            OracleNetDiagnostics.Warn($"알 수 없는 메시지: {Truncate(json, 400)}");
            return;
        }

        lastReceivedType = message.type;
        if (logMessages && message.type != "pong" && message.type != "state")
            OracleNetDiagnostics.Info($"← {Truncate(json, 300)}");

        switch (message.type)
        {
            case "welcome":
                PlayerId = message.playerId;
                break;

            case "pong":
                MeasurePong(receivedAt);
                break;

            case "room_joined":
                RoomCode = message.code;
                RoomPlayers.Clear();
                if (message.players != null) RoomPlayers.AddRange(message.players);
                LastState = null;
                break;

            case "player_joined":
                RoomPlayers.Add(message.player);
                break;

            case "player_left":
                RoomPlayers.RemoveAll(p => p.playerId == message.playerId);
                break;

            case "room_left":
                ClearRoom();
                break;

            case "state":
                LastState = message;
                break;

            case "error":
                // 서버가 요청을 거절한 경우 (방 없음 등). 연결 문제는 아니라 화면 경고만
                OracleNetDiagnostics.Warn($"서버가 요청을 거절함: {message.message} (마지막으로 보낸 것: {lastSentType})");
                break;
        }

        MessageReceived?.Invoke(message);
    }

    private void CloseSocket()
    {
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = null;

        socket?.Dispose();
        socket = null;
        PlayerId = string.Empty;
        pingSentAt = -1;
        connectedAt = -1;
        ClearRoom();
    }

    private void ClearRoom()
    {
        RoomCode = string.Empty;
        RoomPlayers.Clear();
        LastState = null;
    }

    private double ConnectedSeconds() => connectedAt < 0 ? 0 : Now - connectedAt;

    private static string Truncate(string text, int max) =>
        string.IsNullOrEmpty(text) || text.Length <= max ? text : text.Substring(0, max) + "…";

    /// <summary>F3 화면과 오류 보고에 쓰는 현재 연결 상태.</summary>
    public string DescribeStatus()
    {
        var builder = new StringBuilder();
        string state = socket == null ? "없음" : socket.State.ToString();
        builder.AppendLine($"서버 {serverUrl} · 소켓 {state} · 인터넷 {Application.internetReachability}");
        builder.AppendLine($"내 ID {(string.IsNullOrEmpty(PlayerId) ? "-" : PlayerId.Substring(0, Math.Min(8, PlayerId.Length)))} · 방 {(InRoom ? RoomCode : "-")} ({RoomPlayers.Count}명)");
        builder.AppendLine($"연결 유지 {ConnectedSeconds():0.0}초 · 시도 {connectAttempts}회 / 실패 {connectFailures}회 / 예기치 않은 끊김 {unexpectedDisconnects}회");
        builder.AppendLine($"왕복 평균 {RoundTripTime * 1000f:0}ms · 최근 {lastRttSample * 1000f:0}ms · 최대 {maxRttSample * 1000f:0}ms" +
                           (pingSentAt >= 0 ? $" · ping 응답 대기 {Now - pingSentAt:0.0}초" : string.Empty));
        builder.AppendLine($"받음 {messagesReceived}개 ({bytesReceived / 1024f:0.0}KB, 마지막 {lastReceivedType} {(lastReceivedAt < 0 ? "-" : (Now - lastReceivedAt).ToString("0.0") + "초 전")})");
        builder.AppendLine($"보냄 {messagesSent}개 ({bytesSent / 1024f:0.0}KB, 마지막 {lastSentType} {(lastSentAt < 0 ? "-" : (Now - lastSentAt).ToString("0.0") + "초 전")})");
        builder.AppendLine($"마지막 종료: {lastCloseInfo}");
        builder.Append($"마지막 /health 확인: {lastHealthProbe}");
        return builder.ToString();
    }

    /// <summary>씬에 없으면 만들어서 돌려준다.</summary>
    public static OracleServerClient GetOrCreate()
    {
        if (Instance != null) return Instance;
        return new GameObject(nameof(OracleServerClient)).AddComponent<OracleServerClient>();
    }
}
