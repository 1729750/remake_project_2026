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
/// 주의: ClientWebSocket은 WebGL 빌드에서 동작하지 않는다. (PC 빌드 / 에디터 전용)
/// </summary>
public sealed class OracleServerClient : MonoBehaviour
{
    public static OracleServerClient Instance { get; private set; }

    [SerializeField]
    private string serverUrl = "ws://161.33.142.112:7777/ws";

    [SerializeField]
    private float connectTimeoutSeconds = 5f;

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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
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
                Debug.LogException(exception);
            }
        }

        // 왕복 시간 측정: 응답을 기다리는 ping이 없을 때만 주기적으로 보낸다
        if (IsConnected && pingSentAt < 0 && Now >= nextPingAt)
        {
            pingSentAt = Now;
            Send(ClientMessage.Ping());
        }
    }

    private void MeasurePong(double receivedAt)
    {
        if (pingSentAt < 0) return;
        float sample = (float)(receivedAt - pingSentAt);
        pingSentAt = -1;
        nextPingAt = Now + PingInterval;
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

    /// <summary>이미 연결돼 있으면 바로 true. 실패하면 false.</summary>
    public async Task<bool> ConnectAsync()
    {
        if (IsConnected)
        {
            return true;
        }

        CloseSocket();

        lifetime = new CancellationTokenSource();
        socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

        try
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(connectTimeoutSeconds));
                await socket.ConnectAsync(new Uri(serverUrl), timeout.Token);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[OracleServerClient] 연결 실패 ({serverUrl}): {exception.Message}");
            CloseSocket();
            return false;
        }

        Debug.Log($"[OracleServerClient] 연결됨: {serverUrl}");
        _ = ReceiveLoopAsync(socket, lifetime.Token);
        Connected?.Invoke();
        return true;
    }

    public async void Send(ClientMessage message)
    {
        if (!IsConnected)
        {
            Debug.LogWarning($"[OracleServerClient] 연결 안 됨, 보내지 못함: {message.type}");
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));

        // 기다리는 동안 Disconnect될 수 있으니 지금의 소켓/토큰을 잡아 둔다
        ClientWebSocket ws = socket;
        CancellationToken token = lifetime.Token;

        await sendLock.WaitAsync();
        try
        {
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[OracleServerClient] 보내기 실패: {exception.Message}");
        }
        finally
        {
            sendLock.Release();
        }
    }

    public void Disconnect()
    {
        bool wasConnected = socket != null;
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

        try
        {
            while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (!result.EndOfMessage)
                {
                    continue;
                }

                string json = builder.ToString();
                builder.Clear();
                double receivedAt = Now;
                mainThreadQueue.Enqueue(() => Dispatch(json, receivedAt));
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException exception)
        {
            Debug.LogWarning($"[OracleServerClient] 연결 끊김: {exception.Message}");
        }

        // 직접 Disconnect()한 경우가 아니면 끊김을 알린다
        if (!token.IsCancellationRequested)
        {
            mainThreadQueue.Enqueue(() =>
            {
                if (socket == ws)
                {
                    CloseSocket();
                    Disconnected?.Invoke();
                }
            });
        }
    }

    private void Dispatch(string json, double receivedAt)
    {
        ServerMessage message = JsonUtility.FromJson<ServerMessage>(json);
        if (message != null) message.receivedAt = receivedAt;

        if (message == null || string.IsNullOrEmpty(message.type))
        {
            Debug.LogWarning($"[OracleServerClient] 알 수 없는 메시지: {json}");
            return;
        }

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
        ClearRoom();
    }

    private void ClearRoom()
    {
        RoomCode = string.Empty;
        RoomPlayers.Clear();
        LastState = null;
    }

    /// <summary>씬에 없으면 만들어서 돌려준다.</summary>
    public static OracleServerClient GetOrCreate()
    {
        if (Instance != null) return Instance;
        return new GameObject(nameof(OracleServerClient)).AddComponent<OracleServerClient>();
    }
}
