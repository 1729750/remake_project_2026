using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

/// <summary>
/// Relay 연결 전용 로비 관리자.
///
/// 담당 역할:
/// 1. Host 또는 Client 모드 선택
/// 2. 닉네임 입력
/// 3. Relay 세션 생성 및 참가 코드 발급
/// 4. 참가 코드로 Client 접속
/// 5. ClientId와 닉네임 동기화
///
/// 실제 게임 시작, 전투, 이동은 NetworkMatchBridge가 담당합니다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class HostGameManager : NetworkBehaviour
{
    private const int MaxPlayers = 2;
    private const int MaxNicknameLength = 12;

    private enum EntryMode
    {
        None,
        Host,
        Client
    }

    [Header("Network")]
    [Tooltip("NetworkSystem 오브젝트의 NetworkManager를 연결합니다.")]
    [SerializeField] private NetworkManager networkManager;

    [Header("Panels")]
    [SerializeField] private GameObject connectHostClientPanel;
    [SerializeField] private GameObject nicknamePanel;

    [Header("Input Fields")]
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private TMP_InputField joinCodeInput;

    [Header("Texts")]
    [SerializeField] private TMP_Text modeTitleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private TMP_Text hostNicknameText;
    [SerializeField] private TMP_Text clientNicknameText;

    private readonly string waitingNicknameText = "접속 대기 중...";

    private NetworkList<LobbyPlayerData> lobbyPlayers;

    private ISession currentSession;
    private Task initializationTask;

    private EntryMode selectedMode;
    private string localNickname = string.Empty;

    private bool servicesInitialized;
    private bool isBusy;

    /// <summary>
    /// 로컬 화면에서 플레이어 목록이 변경될 때 호출됩니다.
    /// 이후 Player 머리 위 닉네임 UI에서도 사용할 수 있습니다.
    /// </summary>
    public event Action LobbyPlayersChanged;

    public ISession CurrentSession => currentSession;

    public string CurrentJoinCode =>
        currentSession != null
            ? currentSession.Code
            : string.Empty;

    public int ConnectedPlayerCount =>
        lobbyPlayers != null
            ? lobbyPlayers.Count
            : 0;

    public bool IsRoomReady =>
        ConnectedPlayerCount >= MaxPlayers;

    private void Awake()
    {
        /*
         * NetworkList는 각 실행 환경에 하나씩 존재하지만,
         * 실제 목록 변경 권한은 Server에게 있습니다.
         */
        lobbyPlayers = new NetworkList<LobbyPlayerData>();

        ShowModeSelection();
        ResetLobbyUI();
    }

    private async void Start()
    {
        if (networkManager == null)
        {
            networkManager = NetworkManager.Singleton;
        }

        try
        {
            await EnsureServicesReadyAsync();
        }
        catch (Exception exception)
        {
            SetStatus($"서비스 초기화 실패\n{exception.Message}");
            Debug.LogException(exception);
        }
    }

    public override void OnNetworkSpawn()
    {
        lobbyPlayers.OnListChanged += HandleLobbyPlayersChanged;

        if (IsServer && networkManager != null)
        {
            networkManager.OnClientDisconnectCallback +=
                HandleClientDisconnected;
        }

        RefreshLobbyUI();

        /*
         * CreateSessionAsync 또는 JoinSessionByCodeAsync가
         * NetworkObject Spawn보다 먼저 끝났든 나중에 끝났든
         * 닉네임이 등록되도록 처리합니다.
         */
        SubmitLocalNicknameIfReady();
    }

    public override void OnNetworkDespawn()
    {
        lobbyPlayers.OnListChanged -= HandleLobbyPlayersChanged;

        if (networkManager != null)
        {
            networkManager.OnClientDisconnectCallback -=
                HandleClientDisconnected;
        }
    }

    #region UI Mode Selection

    /// <summary>
    /// ConnectHostClientPanel의 Host 버튼에 연결합니다.
    /// </summary>
    public void OpenHostSetup()
    {
        if (isBusy)
            return;

        selectedMode = EntryMode.Host;

        SetPanelState(
            connectionPanelVisible: false,
            nicknamePanelVisible: true
        );

        if (modeTitleText != null)
        {
            modeTitleText.text = "Host 방 만들기";
        }

        if (joinCodeInput != null)
        {
            joinCodeInput.gameObject.SetActive(false);
            joinCodeInput.text = string.Empty;
        }

        if (joinCodeText != null)
        {
            joinCodeText.text =
                "방을 만들면 참가 코드가 표시됩니다.";
        }

        SetStatus("Host 닉네임을 입력하세요.");
    }

    /// <summary>
    /// ConnectHostClientPanel의 Client 버튼에 연결합니다.
    /// </summary>
    public void OpenClientSetup()
    {
        if (isBusy)
            return;

        selectedMode = EntryMode.Client;

        SetPanelState(
            connectionPanelVisible: false,
            nicknamePanelVisible: true
        );

        if (modeTitleText != null)
        {
            modeTitleText.text = "Client 방 참가";
        }

        if (joinCodeInput != null)
        {
            joinCodeInput.gameObject.SetActive(true);
            joinCodeInput.text = string.Empty;
        }

        if (joinCodeText != null)
        {
            joinCodeText.text =
                "Host에게 받은 참가 코드를 입력하세요.";
        }

        SetStatus("닉네임과 참가 코드를 입력하세요.");
    }

    /// <summary>
    /// 뒤로 가기 버튼에 연결합니다.
    /// </summary>
    public void BackToModeSelection()
    {
        if (isBusy || currentSession != null)
            return;

        selectedMode = EntryMode.None;

        ShowModeSelection();
        ResetLobbyUI();
    }

    private void ShowModeSelection()
    {
        SetPanelState(
            connectionPanelVisible: true,
            nicknamePanelVisible: false
        );
    }

    private void SetPanelState(
        bool connectionPanelVisible,
        bool nicknamePanelVisible)
    {
        if (connectHostClientPanel != null)
        {
            connectHostClientPanel.SetActive(
                connectionPanelVisible
            );
        }

        if (nicknamePanel != null)
        {
            nicknamePanel.SetActive(
                nicknamePanelVisible
            );
        }
    }

    #endregion

    #region Host And Client Connection

    /// <summary>
    /// NicknamePanel의 확인 버튼에 연결합니다.
    ///
    /// Host 모드이면 방을 만들고,
    /// Client 모드이면 코드로 참가합니다.
    /// </summary>
    public async void ConfirmConnection()
    {
        if (isBusy)
            return;

        if (selectedMode == EntryMode.None)
        {
            SetStatus("Host 또는 Client를 먼저 선택하세요.");
            return;
        }

        if (!TryReadNickname(out string nickname))
            return;

        string joinCode = string.Empty;

        if (selectedMode == EntryMode.Client)
        {
            if (!TryReadJoinCode(out joinCode))
                return;
        }

        localNickname = nickname;
        isBusy = true;

        try
        {
            await EnsureServicesReadyAsync();
            ValidateNetworkState();

            if (selectedMode == EntryMode.Host)
            {
                await CreateHostSessionAsync();
            }
            else
            {
                await JoinClientSessionAsync(joinCode);
            }
        }
        catch (Exception exception)
        {
            SetStatus(
                $"네트워크 연결 실패\n{exception.Message}"
            );

            Debug.LogException(exception);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task CreateHostSessionAsync()
    {
        SetStatus("Relay Host 방 생성 중...");

        var options = new SessionOptions
        {
            MaxPlayers = MaxPlayers,
            Name = $"{localNickname} Match"
        }.WithRelayNetwork();

        /*
         * RelayRoomController에서 검증했던 핵심 부분입니다.
         *
         * Relay 세션 생성
         * Relay 참가 코드 발급
         * NGO Host 연결 시작
         */
        currentSession =
            await MultiplayerService.Instance
                .CreateSessionAsync(options);

        if (joinCodeText != null)
        {
            joinCodeText.text =
                $"참가 코드\n{currentSession.Code}";
        }

        SetStatus(
            "Host 방 생성 완료\n" +
            "Client 접속 대기 중..."
        );

        Debug.Log(
            $"Host 방 생성 완료 | " +
            $"Session ID: {currentSession.Id} | " +
            $"Join Code: {currentSession.Code}"
        );

        SubmitLocalNicknameIfReady();
    }

    private async Task JoinClientSessionAsync(string joinCode)
    {
        SetStatus("Relay 방 참가 중...");

        /*
         * Host가 생성한 참가 코드로 세션에 들어갑니다.
         * 해당 세션의 Relay 및 NGO Client 연결도 처리됩니다.
         */
        currentSession =
            await MultiplayerService.Instance
                .JoinSessionByCodeAsync(joinCode);

        if (joinCodeText != null)
        {
            joinCodeText.text =
                $"참가 코드\n{currentSession.Code}";
        }

        SetStatus("Host 방 참가 완료");

        Debug.Log(
            $"Client 방 참가 완료 | " +
            $"Session ID: {currentSession.Id} | " +
            $"Join Code: {currentSession.Code}"
        );

        SubmitLocalNicknameIfReady();
    }

    /// <summary>
    /// 방 나가기 버튼에 연결합니다.
    /// </summary>
    public async void LeaveRoom()
    {
        if (isBusy)
            return;

        if (currentSession == null &&
            (networkManager == null ||
             !networkManager.IsListening))
        {
            SetStatus("참가 중인 방이 없습니다.");
            return;
        }

        isBusy = true;

        try
        {
            SetStatus("방에서 나가는 중...");

            if (currentSession != null)
            {
                await currentSession.LeaveAsync();
                currentSession = null;
            }

            /*
             * Session이 NGO 연결을 종료하지 못한 예외 상황에 대한
             * 안전 처리입니다.
             */
            if (networkManager != null &&
                networkManager.IsListening)
            {
                networkManager.Shutdown();
            }

            localNickname = string.Empty;
            selectedMode = EntryMode.None;

            ResetLobbyUI();
            ShowModeSelection();

            SetStatus("방에서 나왔습니다.");
        }
        catch (Exception exception)
        {
            SetStatus(
                $"방 나가기 실패\n{exception.Message}"
            );

            Debug.LogException(exception);
        }
        finally
        {
            isBusy = false;
        }
    }

    private void ValidateNetworkState()
    {
        if (networkManager == null)
        {
            throw new InvalidOperationException(
                "HostGameManager에 NetworkManager가 " +
                "연결되지 않았습니다."
            );
        }

        if (currentSession != null)
        {
            throw new InvalidOperationException(
                "이미 참가 중인 세션이 있습니다."
            );
        }

        if (networkManager.IsListening)
        {
            throw new InvalidOperationException(
                "NetworkManager가 이미 실행 중입니다."
            );
        }
    }

    #endregion

    #region Nickname Synchronization

    private void SubmitLocalNicknameIfReady()
    {
        if (!IsSpawned)
            return;

        if (string.IsNullOrWhiteSpace(localNickname))
            return;

        var networkNickname =
            new FixedString64Bytes(localNickname);

        SubmitNicknameRpc(networkNickname);
    }

    /// <summary>
    /// 각 Client가 자신의 닉네임을 Server에 제출합니다.
    ///
    /// Client는 자신의 ClientId를 직접 보내지 않습니다.
    /// Server가 RPC를 실제로 보낸 ClientId를 확인합니다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void SubmitNicknameRpc(
        FixedString64Bytes nickname,
        RpcParams rpcParams = default)
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        string validatedNickname =
            ValidateReceivedNickname(nickname.ToString());

        if (string.IsNullOrWhiteSpace(validatedNickname))
        {
            Debug.LogWarning(
                $"Client {senderClientId}가 " +
                "올바르지 않은 닉네임을 제출했습니다."
            );

            return;
        }

        int existingIndex =
            FindPlayerIndex(senderClientId);

        bool isHostPlayer =
            senderClientId ==
            Unity.Netcode.NetworkManager.ServerClientId;

        var playerData = new LobbyPlayerData(
            senderClientId,
            new FixedString64Bytes(validatedNickname),
            isHostPlayer
        );

        if (existingIndex >= 0)
        {
            lobbyPlayers[existingIndex] = playerData;
            return;
        }

        if (lobbyPlayers.Count >= MaxPlayers)
        {
            Debug.LogWarning(
                $"최대 인원 {MaxPlayers}명을 초과하여 " +
                $"닉네임 등록을 거부했습니다."
            );

            return;
        }

        lobbyPlayers.Add(playerData);

        Debug.Log(
            $"로비 플레이어 등록 | " +
            $"Client ID: {senderClientId} | " +
            $"Nickname: {validatedNickname} | " +
            $"Host: {isHostPlayer}"
        );
    }

    private int FindPlayerIndex(ulong clientId)
    {
        for (int i = 0; i < lobbyPlayers.Count; i++)
        {
            if (lobbyPlayers[i].ClientId == clientId)
            {
                return i;
            }
        }

        return -1;
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        int index = FindPlayerIndex(clientId);

        if (index >= 0)
        {
            lobbyPlayers.RemoveAt(index);
        }

        Debug.Log($"Client 연결 종료: {clientId}");
    }

    private void HandleLobbyPlayersChanged(
        NetworkListEvent<LobbyPlayerData> changeEvent)
    {
        RefreshLobbyUI();
    }

    private void RefreshLobbyUI()
    {
        string hostName = waitingNicknameText;
        string clientName = waitingNicknameText;

        for (int i = 0; i < lobbyPlayers.Count; i++)
        {
            LobbyPlayerData player = lobbyPlayers[i];

            if (player.IsHostPlayer)
            {
                hostName = player.Nickname.ToString();
            }
            else
            {
                clientName = player.Nickname.ToString();
            }
        }

        if (hostNicknameText != null)
        {
            hostNicknameText.text =
                $"HOST : {hostName}";
        }

        if (clientNicknameText != null)
        {
            clientNicknameText.text =
                $"CLIENT : {clientName}";
        }

        if (lobbyPlayers.Count >= MaxPlayers)
        {
            SetStatus(
                "Host와 Client 연결 완료\n" +
                "게임 시작 가능"
            );
        }
        else if (currentSession != null)
        {
            SetStatus(
                $"플레이어 접속 대기\n" +
                $"{lobbyPlayers.Count}/{MaxPlayers}"
            );
        }

        LobbyPlayersChanged?.Invoke();
    }

    /// <summary>
    /// 다른 네트워크 스크립트가 ClientId로 닉네임을 찾을 때 사용합니다.
    ///
    /// 예:
    /// NetworkPlayer의 OwnerClientId를 전달하여
    /// 캐릭터 머리 위 TMP_Text에 표시할 수 있습니다.
    /// </summary>
    public bool TryGetNickname(
        ulong clientId,
        out string nickname)
    {
        int index = FindPlayerIndex(clientId);

        if (index >= 0)
        {
            nickname =
                lobbyPlayers[index].Nickname.ToString();

            return true;
        }

        nickname = string.Empty;
        return false;
    }

    private static string ValidateReceivedNickname(
        string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return string.Empty;

        string result = nickname.Trim();

        if (result.Length > MaxNicknameLength)
        {
            result = result.Substring(
                0,
                MaxNicknameLength
            );
        }

        return result;
    }

    #endregion

    #region Services

    private async Task EnsureServicesReadyAsync()
    {
        if (servicesInitialized)
            return;

        if (initializationTask == null)
        {
            initializationTask =
                InitializeServicesAsync();
        }

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

        servicesInitialized = true;

        SetStatus(
            $"서비스 로그인 완료\n" +
            $"Player ID: " +
            $"{AuthenticationService.Instance.PlayerId}"
        );

        Debug.Log(
            $"UGS 로그인 완료 | " +
            $"Player ID: " +
            $"{AuthenticationService.Instance.PlayerId}"
        );
    }

    #endregion

    #region Input Validation

    private bool TryReadNickname(out string nickname)
    {
        nickname =
            nicknameInput != null
                ? nicknameInput.text.Trim()
                : string.Empty;

        if (string.IsNullOrWhiteSpace(nickname))
        {
            SetStatus("닉네임을 입력하세요.");
            return false;
        }

        if (nickname.Length > MaxNicknameLength)
        {
            SetStatus(
                $"닉네임은 최대 " +
                $"{MaxNicknameLength}자까지 가능합니다."
            );

            return false;
        }

        return true;
    }

    private bool TryReadJoinCode(out string joinCode)
    {
        joinCode =
            joinCodeInput != null
                ? joinCodeInput.text
                    .Trim()
                    .ToUpperInvariant()
                : string.Empty;

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            SetStatus("참가 코드를 입력하세요.");
            return false;
        }

        return true;
    }

    #endregion

    #region UI Helpers

    private void ResetLobbyUI()
    {
        if (joinCodeText != null)
        {
            joinCodeText.text = string.Empty;
        }

        if (hostNicknameText != null)
        {
            hostNicknameText.text =
                $"HOST : {waitingNicknameText}";
        }

        if (clientNicknameText != null)
        {
            clientNicknameText.text =
                $"CLIENT : {waitingNicknameText}";
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.Log($"[HostGameManager] {message}");
    }

    #endregion
}

/// <summary>
/// Server가 관리하고 모든 Client에게 동기화하는 로비 플레이어 정보입니다.
/// </summary>
[Serializable]
public struct LobbyPlayerData :
    INetworkSerializable,
    IEquatable<LobbyPlayerData>
{
    public ulong ClientId;
    public FixedString64Bytes Nickname;
    public bool IsHostPlayer;

    public LobbyPlayerData(
        ulong clientId,
        FixedString64Bytes nickname,
        bool isHostPlayer)
    {
        ClientId = clientId;
        Nickname = nickname;
        IsHostPlayer = isHostPlayer;
    }

    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Nickname);
        serializer.SerializeValue(ref IsHostPlayer);
    }

    public bool Equals(LobbyPlayerData other)
    {
        return
            ClientId == other.ClientId &&
            Nickname.Equals(other.Nickname) &&
            IsHostPlayer == other.IsHostPlayer;
    }

    public override bool Equals(object obj)
    {
        return
            obj is LobbyPlayerData other &&
            Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = ClientId.GetHashCode();
            hash = (hash * 397) ^ Nickname.GetHashCode();
            hash = (hash * 397) ^
                   IsHostPlayer.GetHashCode();

            return hash;
        }
    }
}