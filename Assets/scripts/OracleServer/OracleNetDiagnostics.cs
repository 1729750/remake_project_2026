using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 네트워크 오류 하나. 화면(OracleNetDebugOverlay)과 로그 파일, 서버 보고에 같이 쓴다.
/// </summary>
public sealed class NetError
{
    /// <summary>어느 단계에서 났는지: 연결 / 보내기 / 받기 / 응답 없음 / 메시지 해석 / 서버 오류</summary>
    public string Stage;

    /// <summary>한 줄 요약 (사람이 읽는 말).</summary>
    public string Summary;

    /// <summary>원인 추정과 해볼 것.</summary>
    public string Hint;

    /// <summary>예외 종류, 오류 코드, 내부 예외까지 전부.</summary>
    public string Detail;

    public DateTime Time = DateTime.Now;

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"[{Time:HH:mm:ss}] {Stage}: {Summary}");
        if (!string.IsNullOrEmpty(Hint)) builder.AppendLine($"원인 추정: {Hint}");
        if (!string.IsNullOrEmpty(Detail)) builder.AppendLine(Detail);
        return builder.ToString().TrimEnd();
    }
}

/// <summary>
/// 네트워크 디버깅 도우미.
/// - 예외를 "무슨 일이 났고 왜 그럴 수 있는지"로 풀어 쓴다 (Describe)
/// - 최근 로그를 메모리와 파일(persistentDataPath/oracle-net.log)에 남긴다
/// - 연결이 안 될 때 HTTP /health 로 서버가 살아 있는지 따로 확인한다 (ProbeHealthAsync)
/// </summary>
public static class OracleNetDiagnostics
{
    private const int MaxLines = 300;
    private const long MaxLogFileBytes = 1024 * 1024;

    private static readonly object gate = new object();
    private static readonly LinkedList<string> lines = new LinkedList<string>();
    private static string logFilePath;

    /// <summary>마지막 오류. 없으면 null.</summary>
    public static NetError LastError { get; private set; }

    /// <summary>오류가 날 때마다 (어느 스레드에서든). 화면 표시는 메인 스레드에서 따로 꺼내 쓴다.</summary>
    public static event Action<NetError> ErrorRaised;

    public static string LogFilePath
    {
        get
        {
            if (logFilePath == null)
            {
                try { logFilePath = Path.Combine(Application.persistentDataPath, "oracle-net.log"); }
                catch { logFilePath = string.Empty; }
            }
            return logFilePath;
        }
    }

    // =========================================================
    // Log
    // =========================================================

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(NetError error)
    {
        LastError = error;
        Write("ERROR", error.ToString());
        try { ErrorRaised?.Invoke(error); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    public static string[] RecentLines()
    {
        lock (gate)
        {
            var copy = new string[lines.Count];
            lines.CopyTo(copy, 0);
            return copy;
        }
    }

    private static void Write(string level, string message)
    {
        string line = $"{DateTime.Now:HH:mm:ss.fff} {level} {message}";

        switch (level)
        {
            case "ERROR": Debug.LogError($"[OracleNet] {message}"); break;
            case "WARN": Debug.LogWarning($"[OracleNet] {message}"); break;
            default: Debug.Log($"[OracleNet] {message}"); break;
        }

        lock (gate)
        {
            lines.AddLast(line);
            while (lines.Count > MaxLines) lines.RemoveFirst();

            // 빌드한 게임에서도 원인을 볼 수 있게 파일에도 남긴다 (실패해도 게임은 계속)
            string path = LogFilePath;
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > MaxLogFileBytes)
                    File.Delete(path);
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch { }
        }
    }

    // =========================================================
    // Describe
    // =========================================================

    /// <summary>
    /// 예외를 풀어 쓴다. timedOut은 우리가 건 시간 제한 때문에 취소된 경우 true.
    /// </summary>
    public static NetError Describe(string stage, Exception exception, bool timedOut = false)
    {
        var error = new NetError { Stage = stage, Detail = ExceptionChain(exception) };

        SocketException socketError = Find<SocketException>(exception);
        WebSocketException webSocketError = Find<WebSocketException>(exception);

        if (timedOut)
        {
            error.Summary = "시간 초과 (서버가 제때 응답하지 않음)";
            error.Hint = "패킷이 서버까지 가지 못하거나 돌아오지 못합니다. 와이파이/공유기/학교망이 7777 포트를 막는지, " +
                         "서버가 꺼졌는지, OCI 보안 목록에 7777이 열려 있는지 확인하세요. 아래 /health 확인 결과를 같이 보세요.";
        }
        else if (socketError != null)
        {
            error.Summary = $"소켓 오류: {socketError.SocketErrorCode} ({(int)socketError.SocketErrorCode})";
            error.Hint = SocketHint(socketError.SocketErrorCode);
        }
        else if (webSocketError != null)
        {
            error.Summary = $"WebSocket 오류: {webSocketError.WebSocketErrorCode} (native {webSocketError.NativeErrorCode})";
            error.Hint = WebSocketHint(webSocketError.WebSocketErrorCode, exception);
        }
        else if (exception is ObjectDisposedException)
        {
            error.Summary = "이미 닫힌 연결을 사용함";
            error.Hint = "연결이 끊긴 직후에 보내기/받기를 시도했습니다. 보통 앞에 다른 오류가 먼저 찍혀 있습니다.";
        }
        else if (exception is OperationCanceledException)
        {
            error.Summary = "작업 취소됨";
            error.Hint = "씬 전환이나 Disconnect()로 연결을 닫는 중이었을 수 있습니다.";
        }
        else if (exception is HttpRequestException)
        {
            error.Summary = "HTTP 요청 실패";
            error.Hint = "서버의 HTTP 응답 자체를 받지 못했습니다.";
        }
        else
        {
            error.Summary = $"{exception.GetType().Name}: {exception.Message}";
        }

        return error;
    }

    private static string SocketHint(SocketError code)
    {
        switch (code)
        {
            case SocketError.ConnectionRefused:
                return "서버 컴퓨터까지는 닿았지만 7777 포트에서 받는 프로그램이 없습니다. 서버의 cardgame 서비스가 꺼졌거나 재시작 중입니다. " +
                       "(sudo systemctl status cardgame)";
            case SocketError.TimedOut:
                return "서버가 응답하지 않습니다. 중간 네트워크(와이파이/방화벽)가 패킷을 버리는 중이거나 서버가 꺼졌습니다.";
            case SocketError.HostUnreachable:
            case SocketError.NetworkUnreachable:
            case SocketError.NetworkDown:
                return "이 PC에서 서버로 가는 길이 없습니다. 인터넷 연결(와이파이) 자체를 확인하세요.";
            case SocketError.HostNotFound:
            case SocketError.TryAgain:
            case SocketError.NoData:
                return "주소(DNS)를 찾지 못했습니다. 서버 주소 오타나 인터넷 연결을 확인하세요.";
            case SocketError.ConnectionReset:
                return "연결이 갑자기 끊겼습니다(RST). 서버 재시작/배포, 공유기 NAT 만료, 백신·방화벽이 연결을 끊었을 수 있습니다.";
            case SocketError.ConnectionAborted:
                return "이 PC 쪽에서 연결이 중단되었습니다. 백신·방화벽 또는 네트워크 전환(와이파이 바뀜)을 의심하세요.";
            case SocketError.AccessDenied:
                return "이 PC의 방화벽/백신이 Unity(또는 게임)의 네트워크 사용을 막았습니다.";
            case SocketError.Shutdown:
            case SocketError.NotConnected:
                return "이미 끊긴 연결로 보내려고 했습니다.";
            default:
                return null;
        }
    }

    private static string WebSocketHint(WebSocketError code, Exception exception)
    {
        switch (code)
        {
            case WebSocketError.ConnectionClosedPrematurely:
                return "서버가 닫는다는 인사(Close) 없이 연결이 끊겼습니다. 서버 재시작/배포, 와이파이 끊김, 공유기가 오래된 연결을 정리한 경우입니다.";
            case WebSocketError.NotAWebSocket:
            case WebSocketError.HeaderError:
                return "서버가 WebSocket이 아닌 응답을 보냈습니다. 주소(/ws)가 틀렸거나 중간 프록시가 WebSocket 업그레이드를 막습니다. " +
                       $"({exception.Message})";
            case WebSocketError.UnsupportedProtocol:
            case WebSocketError.UnsupportedVersion:
                return "WebSocket 버전/프로토콜이 맞지 않습니다.";
            case WebSocketError.InvalidState:
                return "연결이 이미 닫힌 상태에서 보내기/받기를 했습니다.";
            case WebSocketError.Faulted:
            case WebSocketError.NativeError:
                return "아래 내부 예외(소켓 오류)를 보세요.";
            default:
                return null;
        }
    }

    private static T Find<T>(Exception exception) where T : Exception
    {
        for (Exception current = exception; current != null; current = current.InnerException)
        {
            if (current is T match) return match;
            if (current is AggregateException aggregate && aggregate.InnerExceptions.Count > 0)
            {
                T inner = Find<T>(aggregate.InnerExceptions[0]);
                if (inner != null) return inner;
            }
        }
        return null;
    }

    /// <summary>예외와 내부 예외들을 한 줄씩. (스택은 Unity 콘솔에 따로 찍힌다)</summary>
    public static string ExceptionChain(Exception exception)
    {
        var builder = new StringBuilder();
        int depth = 0;
        for (Exception current = exception; current != null && depth < 6; current = current.InnerException, depth++)
        {
            builder.Append(depth == 0 ? "예외: " : "  ↳ 내부: ");
            builder.Append(current.GetType().FullName);
            builder.Append(": ");
            builder.Append(current.Message);

            if (current is SocketException socket)
                builder.Append($" [SocketError={socket.SocketErrorCode}, code={socket.ErrorCode}]");
            else if (current is WebSocketException webSocket)
                builder.Append($" [WebSocketError={webSocket.WebSocketErrorCode}, native={webSocket.NativeErrorCode}, HResult=0x{webSocket.HResult:X8}]");
            else
                builder.Append($" [HResult=0x{current.HResult:X8}]");

            builder.AppendLine();
        }
        return builder.ToString().TrimEnd();
    }

    // =========================================================
    // Health Probe
    // =========================================================

    /// <summary>
    /// ws://host:port/ws → http://host:port/health 를 따로 찔러 본다.
    /// WebSocket만 안 되는지, 서버 자체에 못 가는지 구분하는 데 쓴다.
    /// </summary>
    public static async Task<string> ProbeHealthAsync(string webSocketUrl, float timeoutSeconds = 3f)
    {
        string healthUrl;
        try
        {
            var uri = new Uri(webSocketUrl);
            string scheme = uri.Scheme == "wss" ? "https" : "http";
            healthUrl = $"{scheme}://{uri.Host}:{uri.Port}/health";
        }
        catch (Exception exception)
        {
            return $"/health 확인 불가 (주소 해석 실패: {exception.Message})";
        }

        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) })
            using (HttpResponseMessage response = await http.GetAsync(healthUrl))
            {
                string body = await response.Content.ReadAsStringAsync();
                if (body.Length > 120) body = body.Substring(0, 120);
                return $"/health → HTTP {(int)response.StatusCode} ({started.ElapsedMilliseconds}ms) {body}\n" +
                       (response.IsSuccessStatusCode
                           ? "  ⇒ 서버는 살아 있고 HTTP는 통합니다. WebSocket 연결만 실패했다면 프록시/백신이 WebSocket을 막는지 확인하세요."
                           : "  ⇒ 서버에는 닿지만 정상 응답이 아닙니다. 서버 로그를 확인하세요 (journalctl -u cardgame).");
            }
        }
        catch (TaskCanceledException)
        {
            return $"/health → {timeoutSeconds:0}초 동안 응답 없음 ({healthUrl})\n" +
                   "  ⇒ HTTP로도 서버에 못 갑니다. 네트워크가 7777 포트를 막거나(학교/공용 와이파이), 서버/서비스가 꺼졌습니다. " +
                   "휴대폰 핫스팟으로 바꿔서 되면 네트워크 문제입니다.";
        }
        catch (Exception exception)
        {
            NetError described = Describe("health", exception);
            return $"/health → 실패 ({started.ElapsedMilliseconds}ms): {described.Summary}\n" +
                   (string.IsNullOrEmpty(described.Hint) ? string.Empty : $"  ⇒ {described.Hint}");
        }
    }
}

/// <summary>
/// 디버그용 화면 표시 (IMGUI, 게임 UI와 별개).
/// - 네트워크 오류가 나면 자동으로 왼쪽 위에 내용을 띄운다 (복사 / 닫기)
/// - F3: 연결 상태와 최근 로그를 켜고 끈다
/// OracleServerClient가 같은 오브젝트에 붙여서 씬이 바뀌어도 남는다.
/// </summary>
public sealed class OracleNetDebugOverlay : MonoBehaviour
{
    private NetError shownError;
    private volatile NetError pendingError;
    private bool showStats;
    private Vector2 logScroll;
    private GUIStyle boxStyle;
    private GUIStyle textStyle;
    private int styleForHeight;

    private void OnEnable() => OracleNetDiagnostics.ErrorRaised += OnErrorRaised;

    private void OnDisable() => OracleNetDiagnostics.ErrorRaised -= OnErrorRaised;

    // 어느 스레드에서든 올 수 있어서 다음 프레임에 띄운다
    private void OnErrorRaised(NetError error) => pendingError = error;

    private void Update()
    {
        if (pendingError != null)
        {
            shownError = pendingError;
            pendingError = null;
        }

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
            showStats = !showStats;
    }

    private void EnsureStyles()
    {
        if (boxStyle != null && styleForHeight == Screen.height) return;
        styleForHeight = Screen.height;
        int fontSize = Mathf.Clamp(Screen.height / 50, 12, 22);

        var background = new Texture2D(1, 1);
        background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.82f));
        background.Apply();

        boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
        boxStyle.normal.background = background;

        textStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize, wordWrap = true, richText = true };
        textStyle.normal.textColor = Color.white;
    }

    private void OnGUI()
    {
        if (shownError == null && !showStats) return;
        EnsureStyles();

        float width = Mathf.Min(Screen.width - 20f, 760f);
        GUILayout.BeginArea(new Rect(10f, 10f, width, Screen.height - 20f));

        if (shownError != null)
            DrawError();

        if (showStats)
            DrawStats();

        GUILayout.EndArea();
    }

    private void DrawError()
    {
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label($"<color=#ff7070><b>네트워크 오류 — {shownError.Stage}</b></color>  <size={textStyle.fontSize - 2}>{shownError.Time:HH:mm:ss}</size>", textStyle);
        GUILayout.Label($"<b>{shownError.Summary}</b>", textStyle);
        if (!string.IsNullOrEmpty(shownError.Hint))
            GUILayout.Label($"<color=#ffd070>원인 추정:</color> {shownError.Hint}", textStyle);
        if (!string.IsNullOrEmpty(shownError.Detail))
            GUILayout.Label($"<size={textStyle.fontSize - 3}><color=#c0c0c0>{shownError.Detail}</color></size>", textStyle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("복사", GUILayout.Width(80f)))
            GUIUtility.systemCopyBuffer = BuildReport();
        if (GUILayout.Button(showStats ? "상태 숨기기 (F3)" : "상태/로그 보기 (F3)", GUILayout.Width(160f)))
            showStats = !showStats;
        if (GUILayout.Button("닫기", GUILayout.Width(80f)))
            shownError = null;
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawStats()
    {
        GUILayout.BeginVertical(boxStyle);
        OracleServerClient client = OracleServerClient.Instance;
        GUILayout.Label(client != null ? client.DescribeStatus() : "OracleServerClient 없음", textStyle);
        GUILayout.Label($"<size={textStyle.fontSize - 3}><color=#c0c0c0>로그 파일: {OracleNetDiagnostics.LogFilePath}</color></size>", textStyle);

        string[] recent = OracleNetDiagnostics.RecentLines();
        logScroll = GUILayout.BeginScrollView(logScroll, GUILayout.Height(Screen.height * 0.35f));
        var builder = new StringBuilder();
        for (int i = Mathf.Max(0, recent.Length - 40); i < recent.Length; i++)
            builder.AppendLine(recent[i]);
        GUILayout.Label($"<size={textStyle.fontSize - 3}>{Escape(builder.ToString())}</size>", textStyle);
        GUILayout.EndScrollView();

        if (GUILayout.Button("상태 + 최근 로그 복사", GUILayout.Width(200f)))
            GUIUtility.systemCopyBuffer = BuildReport();
        GUILayout.EndVertical();
    }

    // 로그 안의 < > 가 리치텍스트 태그로 먹히지 않게
    private static string Escape(string text) => text.Replace("<", "‹").Replace(">", "›");

    private string BuildReport()
    {
        var builder = new StringBuilder();
        if (shownError != null) builder.AppendLine(shownError.ToString()).AppendLine();
        OracleServerClient client = OracleServerClient.Instance;
        if (client != null) builder.AppendLine(client.DescribeStatus()).AppendLine();
        builder.AppendLine("--- 최근 로그 ---");
        foreach (string line in OracleNetDiagnostics.RecentLines()) builder.AppendLine(line);
        return builder.ToString();
    }
}
