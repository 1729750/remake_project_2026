using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
public sealed class RelayRoomController : MonoBehaviour
{
    private const int MaxPlayers = 2;

    private ISession currentSession;
    private Task initializationTask;

    private string joinCodeInput = string.Empty;
    private string statusMessage = "서비스 초기화 중...";
    private bool isInitialized;
    private bool isBusy;

    private async void Start()
    {
        try
        {
            await EnsureServicesReadyAsync();
        }
        catch (Exception exception)
        {
            statusMessage = "초기화 실패";
            Debug.LogException(exception);
        }
    }

    /// <summary>
    /// Unity Services 초기화와 익명 로그인을 한 번만 수행합니다.
    /// </summary>
    private async Task EnsureServicesReadyAsync()
    {
        if (isInitialized)
            return;

        if (initializationTask == null)
            initializationTask = InitializeServicesAsync();

        try
        {
            await initializationTask;
        }
        catch
        {
            // 초기화에 실패한 경우 다시 시도할 수 있게 합니다.
            initializationTask = null;
            throw;
        }
    }

    private async Task InitializeServicesAsync()
    {
        statusMessage = "Unity Services 초기화 중...";

        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            statusMessage = "익명 로그인 중...";
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        isInitialized = true;
        statusMessage =
            $"로그인 완료\nPlayer ID: {AuthenticationService.Instance.PlayerId}";

        Debug.Log(
            $"UGS 로그인 완료. Player ID: " +
            $"{AuthenticationService.Instance.PlayerId}"
        );
    }

    /// <summary>
    /// UI의 '방 만들기' 버튼에서 호출합니다.
    /// </summary>
    public async void CreateRoom()
    {
        if (isBusy || currentSession != null)
            return;

        isBusy = true;

        try
        {
            await EnsureServicesReadyAsync();

            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening)
            {
                throw new InvalidOperationException(
                    "이미 Host 또는 Client가 실행 중입니다. " +
                    "Play 모드를 다시 시작한 후 시도하세요."
                );
            }

            statusMessage = "Relay 방 생성 중...";

            var options = new SessionOptions
            {
                MaxPlayers = MaxPlayers,
                Name = "HyeGyo Match"
            }.WithRelayNetwork();

            currentSession =
                await MultiplayerService.Instance.CreateSessionAsync(options);

            statusMessage =
                $"방 생성 완료\n참가 코드: {currentSession.Code}";

            Debug.Log(
                $"Relay 방 생성 완료. " +
                $"Session ID: {currentSession.Id}, " +
                $"Join Code: {currentSession.Code}"
            );
        }
        catch (Exception exception)
        {
            statusMessage = $"방 생성 실패\n{exception.Message}";
            Debug.LogException(exception);
        }
        finally
        {
            isBusy = false;
        }
    }

    /// <summary>
    /// 입력한 참가 코드로 방에 접속합니다.
    /// </summary>
    public async void JoinRoom()
    {
        if (isBusy || currentSession != null)
            return;

        string code = joinCodeInput.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(code))
        {
            statusMessage = "참가 코드를 입력하세요.";
            return;
        }

        isBusy = true;

        try
        {
            await EnsureServicesReadyAsync();

            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening)
            {
                throw new InvalidOperationException(
                    "이미 Host 또는 Client가 실행 중입니다. " +
                    "Play 모드를 다시 시작한 후 시도하세요."
                );
            }

            statusMessage = "방 참가 중...";

            currentSession =
                await MultiplayerService.Instance
                    .JoinSessionByCodeAsync(code);

            statusMessage =
                $"방 참가 완료\n참가 코드: {currentSession.Code}";

            Debug.Log(
                $"Relay 방 참가 완료. " +
                $"Session ID: {currentSession.Id}, " +
                $"Join Code: {currentSession.Code}"
            );
        }
        catch (Exception exception)
        {
            statusMessage = $"방 참가 실패\n{exception.Message}";
            Debug.LogException(exception);
        }
        finally
        {
            isBusy = false;
        }
    }

    /// <summary>
    /// 현재 세션에서 나갑니다.
    /// </summary>
    public async void LeaveRoom()
    {
        if (isBusy || currentSession == null)
            return;

        isBusy = true;

        try
        {
            statusMessage = "방에서 나가는 중...";

            await currentSession.LeaveAsync();
            currentSession = null;

            statusMessage = "방에서 나왔습니다.";
        }
        catch (Exception exception)
        {
            statusMessage = $"나가기 실패\n{exception.Message}";
            Debug.LogException(exception);
        }
        finally
        {
            isBusy = false;
        }
    }

    /*
     * 임시 테스트용 UI입니다.
     * 실제 완성 단계에서는 Canvas + Button + TMP_InputField로 교체하면 됩니다.
     */
    private void OnGUI()
{
    // 720p를 기준으로 해상도가 높아질수록 UI도 확대합니다.
    float uiScale = Mathf.Clamp(Screen.height / 720f, 1f, 2.2f);

    Matrix4x4 previousMatrix = GUI.matrix;

    GUI.matrix = Matrix4x4.TRS(
        Vector3.zero,
        Quaternion.identity,
        new Vector3(uiScale, uiScale, 1f)
    );

    float scaledScreenWidth = Screen.width / uiScale;
    float scaledScreenHeight = Screen.height / uiScale;

    const float panelWidth = 620f;
    const float panelHeight = 500f;

    float panelX = (scaledScreenWidth - panelWidth) * 0.5f;
    float panelY = (scaledScreenHeight - panelHeight) * 0.5f;

    GUIStyle panelStyle = new GUIStyle(GUI.skin.box)
    {
        padding = new RectOffset(30, 30, 25, 25)
    };

    GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
    {
        fontSize = 30,
        fontStyle = FontStyle.Bold,
        alignment = TextAnchor.MiddleCenter
    };

    GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
    {
        fontSize = 20,
        alignment = TextAnchor.MiddleCenter,
        wordWrap = true
    };

    GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
    {
        fontSize = 20,
        fontStyle = FontStyle.Bold
    };

    GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textField)
    {
        fontSize = 24,
        alignment = TextAnchor.MiddleCenter,
        fixedHeight = 48f
    };

    GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
    {
        fontSize = 23,
        fontStyle = FontStyle.Bold,
        fixedHeight = 58f
    };

    GUILayout.BeginArea(
        new Rect(panelX, panelY, panelWidth, panelHeight),
        panelStyle
    );

    GUILayout.Label("Relay 방 테스트", titleStyle);
    GUILayout.Space(15f);

    GUILayout.Label(statusMessage, statusStyle);
    GUILayout.Space(20f);

    bool canConnect =
        isInitialized &&
        !isBusy &&
        currentSession == null;

    GUI.enabled = canConnect;

    if (GUILayout.Button("방 만들기", buttonStyle))
    {
        CreateRoom();
    }

    GUILayout.Space(18f);

    GUILayout.Label("참가 코드", labelStyle);

    joinCodeInput = GUILayout.TextField(
        joinCodeInput,
        12,
        textFieldStyle
    );

    GUILayout.Space(12f);

    if (GUILayout.Button("코드로 참가", buttonStyle))
    {
        JoinRoom();
    }

    GUI.enabled = true;

    if (currentSession != null)
    {
        GUILayout.Space(18f);

        GUILayout.Label(
            $"참가 코드: {currentSession.Code}",
            statusStyle
        );

        GUILayout.Label(
            $"접속 인원: {currentSession.PlayerCount}/" +
            $"{currentSession.MaxPlayers}",
            statusStyle
        );

        GUILayout.Label(
            $"네트워크 상태: {GetNetworkStatus()}",
            statusStyle
        );

        GUILayout.Space(12f);

        GUI.enabled = !isBusy;

        if (GUILayout.Button("방 나가기", buttonStyle))
        {
            LeaveRoom();
        }

        GUI.enabled = true;
    }

    if (isBusy)
    {
        GUILayout.Space(12f);
        GUILayout.Label("처리 중...", statusStyle);
    }

    GUILayout.EndArea();

    // 다른 Unity GUI에 확대 설정이 영향을 주지 않도록 복원합니다.
    GUI.matrix = previousMatrix;
}

    private static string GetNetworkStatus()
    {
        NetworkManager manager = NetworkManager.Singleton;

        if (manager == null)
            return "NetworkManager 없음";

        if (manager.IsHost)
            return "Host";

        if (manager.IsServer)
            return "Server";

        if (manager.IsClient)
            return "Client";

        return "연결 대기";
    }
}