using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
public sealed class NetworkLauncher : MonoBehaviour
{
    private const int MaxPlayers = 2;

    private NetworkManager networkManager;
    private ISession currentSession;
    private Task initializationTask;

    public bool IsInitialized { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsInSession => currentSession != null;

    public string JoinCode =>
        currentSession != null
            ? currentSession.Code
            : string.Empty;

    public string StatusMessage { get; private set; } =
        "서비스 초기화 중...";

    public event Action<string> StatusChanged;
    public event Action<string> SessionCreated;
    public event Action SessionJoined;
    public event Action SessionLeft;

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();
    }

    private async void Start()
    {
        SubscribeNetworkEvents();

        try
        {
            await EnsureServicesReadyAsync();
        }
        catch (Exception exception)
        {
            SetStatus($"초기화 실패\n{exception.Message}");
            Debug.LogException(exception);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeNetworkEvents();
    }

    private void SubscribeNetworkEvents()
    {
        if (networkManager == null)
            return;

        networkManager.OnClientConnectedCallback +=
            HandleClientConnected;

        networkManager.OnClientDisconnectCallback +=
            HandleClientDisconnected;
    }

    private void UnsubscribeNetworkEvents()
    {
        if (networkManager == null)
            return;

        networkManager.OnClientConnectedCallback -=
            HandleClientConnected;

        networkManager.OnClientDisconnectCallback -=
            HandleClientDisconnected;
    }

    private async Task EnsureServicesReadyAsync()
    {
        if (IsInitialized)
            return;

        initializationTask ??= InitializeServicesAsync();

        try
        {
            await initializationTask;
        }
        catch
        {
            initializationTask = null;
            throw;
        }
    }

    private async Task InitializeServicesAsync()
    {
        SetStatus("Unity Services 초기화 중...");

        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            SetStatus("익명 로그인 중...");

            await AuthenticationService.Instance
                .SignInAnonymouslyAsync();
        }

        IsInitialized = true;

        SetStatus(
            $"로그인 완료\n" +
            $"Player ID: {AuthenticationService.Instance.PlayerId}"
        );
    }

    /// <summary>
    /// Host가 Relay 세션을 생성합니다.
    /// Canvas Button에서 호출할 수 있습니다.
    /// </summary>
    public async void CreateSession()
    {
        if (!CanBeginSessionOperation())
            return;

        IsBusy = true;

        try
        {
            await EnsureServicesReadyAsync();

            SetStatus("Relay 세션 생성 중...");

            var options = new SessionOptions
            {
                MaxPlayers = MaxPlayers,
                Name = "HyeGyo Match"
            }.WithRelayNetwork();

            /*
             * WithRelayNetwork를 사용하므로
             * 세션 생성 과정에서 NGO Host 네트워크도 시작됩니다.
             */
            currentSession =
                await MultiplayerService.Instance
                    .CreateSessionAsync(options);

            SetStatus(
                $"방 생성 완료\n" +
                $"참가 코드: {currentSession.Code}"
            );

            SessionCreated?.Invoke(currentSession.Code);

            Debug.Log(
                $"Relay 세션 생성 완료 | " +
                $"Session ID: {currentSession.Id} | " +
                $"Join Code: {currentSession.Code}"
            );
        }
        catch (Exception exception)
        {
            currentSession = null;

            SetStatus(
                $"방 생성 실패\n{exception.Message}"
            );

            Debug.LogException(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 참가 코드로 Relay 세션에 참가합니다.
    /// </summary>
    public async void JoinSession(string joinCode)
    {
        if (!CanBeginSessionOperation())
            return;

        string normalizedCode =
            joinCode?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            SetStatus("참가 코드를 입력하세요.");
            return;
        }

        IsBusy = true;

        try
        {
            await EnsureServicesReadyAsync();

            SetStatus("Relay 세션 참가 중...");

            /*
             * 참가 과정에서 NGO Client 네트워크도
             * 자동으로 연결됩니다.
             */
            currentSession =
                await MultiplayerService.Instance
                    .JoinSessionByCodeAsync(normalizedCode);

            SetStatus(
                $"방 참가 완료\n" +
                $"참가 코드: {currentSession.Code}"
            );

            SessionJoined?.Invoke();

            Debug.Log(
                $"Relay 세션 참가 완료 | " +
                $"Session ID: {currentSession.Id} | " +
                $"Join Code: {currentSession.Code}"
            );
        }
        catch (Exception exception)
        {
            currentSession = null;

            SetStatus(
                $"방 참가 실패\n{exception.Message}"
            );

            Debug.LogException(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 현재 세션에서 나갑니다.
    /// </summary>
    public async void LeaveSession()
    {
        if (IsBusy || currentSession == null)
            return;

        IsBusy = true;

        try
        {
            SetStatus("세션에서 나가는 중...");

            await currentSession.LeaveAsync();

            currentSession = null;

            SetStatus("세션에서 나왔습니다.");
            SessionLeft?.Invoke();
        }
        catch (Exception exception)
        {
            SetStatus(
                $"세션 나가기 실패\n{exception.Message}"
            );

            Debug.LogException(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanBeginSessionOperation()
    {
        if (IsBusy)
        {
            SetStatus("현재 다른 작업을 처리 중입니다.");
            return false;
        }

        if (currentSession != null)
        {
            SetStatus("이미 참가 중인 세션이 있습니다.");
            return false;
        }

        return true;
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"NGO Client 연결 완료: {clientId}");

        if (networkManager.IsServer)
        {
            int playerCount =
                networkManager.ConnectedClientsList.Count;

            SetStatus(
                $"플레이어 접속\n" +
                $"접속 인원: {playerCount}/{MaxPlayers}"
            );
        }
        else if (clientId == networkManager.LocalClientId)
        {
            SetStatus(
                $"Relay 네트워크 접속 완료\n" +
                $"Client ID: {clientId}"
            );
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        Debug.Log($"NGO Client 연결 종료: {clientId}");

        if (networkManager != null &&
            networkManager.IsServer)
        {
            int playerCount =
                networkManager.ConnectedClientsList.Count;

            SetStatus(
                $"플레이어 연결 종료\n" +
                $"현재 인원: {playerCount}/{MaxPlayers}"
            );
        }
        else
        {
            SetStatus("Host와의 연결이 종료되었습니다.");
        }
    }

    private void SetStatus(string message)
    {
        StatusMessage = message;
        StatusChanged?.Invoke(message);
    }
}