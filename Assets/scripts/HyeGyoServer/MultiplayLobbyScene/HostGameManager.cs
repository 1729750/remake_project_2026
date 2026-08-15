using System;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 멀티플레이 로비 네트워크 관리자.
///
/// 담당 역할:
/// 1. NetworkLauncher에 방 생성 / 참가 요청
/// 2. ClientId ↔ Nickname 동기화
/// 3. Host / Client 플레이어 목록 관리
/// 4. 로비 인원 상태 관리
///
/// 담당하지 않는 것:
/// - Host / Client 선택 UI
/// - 닉네임 / 참가 코드 입력 UI
/// - Unity Services 초기화
/// - Authentication
/// - Relay Session 직접 생성 / 참가
/// - NGO Host / Client 직접 시작
///
/// 입력 UI는 LobbyEntryUI가 담당한다.
/// 실제 연결은 NetworkLauncher가 담당한다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class HostGameManager :
    NetworkBehaviour
{
    private const int MaxPlayers = 2;
    private const int MaxNicknameLength = 12;

    // =========================================================
    // Inspector
    // =========================================================

    [Header("Network")]
    [Tooltip(
        "Relay / Session / NGO 연결을 담당하는 NetworkLauncher"
    )]
    [SerializeField]
    private NetworkLauncher networkLauncher;

    [Header("Lobby Status UI")]
    [SerializeField]
    private TMP_Text statusText;

    [SerializeField]
    private TMP_Text joinCodeText;

    [SerializeField]
    private TMP_Text hostNicknameText;

    [SerializeField]
    private TMP_Text clientNicknameText;

    // =========================================================
    // Lobby State
    // =========================================================

    private const string WaitingNicknameText =
        "접속 대기 중...";

    /// <summary>
    /// Server가 관리하고
    /// NGO가 Host / Client에게 동기화하는
    /// 로비 플레이어 목록.
    /// </summary>
    private NetworkList<LobbyPlayerData>
        lobbyPlayers;

    /// <summary>
    /// 현재 PC에서 입력한 닉네임.
    /// NetworkObject Spawn 이후 Server에 제출한다.
    /// </summary>
    private string localNickname =
        string.Empty;

    // =========================================================
    // Events
    // =========================================================

    /// <summary>
    /// 로비 플레이어 정보가 변경되었을 때 호출.
    /// </summary>
    public event Action LobbyPlayersChanged;

    // =========================================================
    // Public Properties
    // =========================================================

    public int ConnectedPlayerCount =>
        lobbyPlayers != null
            ? lobbyPlayers.Count
            : 0;

    public bool IsRoomReady =>
        ConnectedPlayerCount >=
        MaxPlayers;

    public string CurrentJoinCode =>
        networkLauncher != null
            ? networkLauncher.JoinCode
            : string.Empty;

    public bool IsNetworkBusy =>
        networkLauncher != null &&
        networkLauncher.IsBusy;

    public bool IsInSession =>
        networkLauncher != null &&
        networkLauncher.IsInSession;

    // =========================================================
    // Unity Life Cycle
    // =========================================================

    private void Awake()
    {
        lobbyPlayers =
            new NetworkList<LobbyPlayerData>();

        ResetLobbyDisplay();
    }

    private void OnEnable()
    {
        SubscribeLauncherEvents();
    }

    private void OnDisable()
    {
        UnsubscribeLauncherEvents();
    }

    // =========================================================
    // NGO Life Cycle
    // =========================================================

    public override void OnNetworkSpawn()
    {
        lobbyPlayers.OnListChanged +=
            HandleLobbyPlayersChanged;

        /*
         * HostGameManager 오브젝트가
         * 같은 실행 환경에서 다시 네트워크 Spawn되는 경우
         * 이전 로비 데이터가 남지 않도록 Server가 초기화한다.
         */
        if (IsServer)
        {
            lobbyPlayers.Clear();
        }

        /*
         * Client 연결 종료 시
         * Server의 LobbyPlayerData에서도 제거한다.
         */
        if (IsServer &&
            NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton
                .OnClientDisconnectCallback +=
                HandleClientDisconnected;
        }

        RefreshLobbyUI();

        /*
         * Session 생성 / 참가 완료보다
         * NetworkObject Spawn이 늦게 발생할 수도 있다.
         *
         * 따라서 여기서도 닉네임 등록을 시도한다.
         */
        SubmitLocalNicknameIfReady();
    }

    public override void OnNetworkDespawn()
    {
        if (lobbyPlayers != null)
        {
            lobbyPlayers.OnListChanged -=
                HandleLobbyPlayersChanged;
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton
                .OnClientDisconnectCallback -=
                HandleClientDisconnected;
        }
    }

    // =========================================================
    // Room Request
    // =========================================================

    /// <summary>
    /// LobbyEntryUI가 Host 방 생성을 요청할 때 호출.
    ///
    /// 실제 Session / Relay 생성은
    /// NetworkLauncher가 수행한다.
    /// </summary>
    public void RequestCreateRoom(
        string nickname)
    {
        if (!ValidateLauncher())
            return;

        if (networkLauncher.IsBusy)
        {
            SetStatus(
                "현재 네트워크 작업을 처리 중입니다."
            );

            return;
        }

        if (networkLauncher.IsInSession)
        {
            SetStatus(
                "이미 참가 중인 방이 있습니다."
            );

            return;
        }

        string validatedNickname =
            ValidateReceivedNickname(
                nickname
            );

        if (string.IsNullOrWhiteSpace(
                validatedNickname))
        {
            SetStatus(
                "올바른 닉네임을 입력해주세요."
            );

            return;
        }

        localNickname =
            validatedNickname;

        SetStatus(
            "Host 방 생성 중..."
        );

        networkLauncher
            .CreateSession();
    }

    /// <summary>
    /// LobbyEntryUI가 Client 방 참가를 요청할 때 호출.
    ///
    /// 실제 Session / Relay 참가 처리는
    /// NetworkLauncher가 수행한다.
    /// </summary>
    public void RequestJoinRoom(
        string nickname,
        string joinCode)
    {
        if (!ValidateLauncher())
            return;

        if (networkLauncher.IsBusy)
        {
            SetStatus(
                "현재 네트워크 작업을 처리 중입니다."
            );

            return;
        }

        if (networkLauncher.IsInSession)
        {
            SetStatus(
                "이미 참가 중인 방이 있습니다."
            );

            return;
        }

        string validatedNickname =
            ValidateReceivedNickname(
                nickname
            );

        if (string.IsNullOrWhiteSpace(
                validatedNickname))
        {
            SetStatus(
                "올바른 닉네임을 입력해주세요."
            );

            return;
        }

        string normalizedJoinCode =
            joinCode?.Trim()
                .ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(
                normalizedJoinCode))
        {
            SetStatus(
                "참가 코드가 올바르지 않습니다."
            );

            return;
        }

        localNickname =
            validatedNickname;

        SetStatus(
            "Host 방 참가 중..."
        );

        networkLauncher
            .JoinSession(
                normalizedJoinCode
            );
    }

    /// <summary>
    /// 현재 Relay Session에서 나간다.
    /// </summary>
    public void LeaveRoom()
    {
        if (!ValidateLauncher())
            return;

        if (networkLauncher.IsBusy)
        {
            SetStatus(
                "현재 네트워크 작업을 처리 중입니다."
            );

            return;
        }

        if (!networkLauncher.IsInSession)
        {
            SetStatus(
                "참가 중인 방이 없습니다."
            );

            return;
        }

        networkLauncher
            .LeaveSession();
    }

    // =========================================================
    // NetworkLauncher Events
    // =========================================================

    private void SubscribeLauncherEvents()
    {
        if (networkLauncher == null)
            return;

        networkLauncher.StatusChanged +=
            HandleLauncherStatusChanged;

        networkLauncher.SessionCreated +=
            HandleSessionCreated;

        networkLauncher.SessionJoined +=
            HandleSessionJoined;

        networkLauncher.SessionLeft +=
            HandleSessionLeft;
    }

    private void UnsubscribeLauncherEvents()
    {
        if (networkLauncher == null)
            return;

        networkLauncher.StatusChanged -=
            HandleLauncherStatusChanged;

        networkLauncher.SessionCreated -=
            HandleSessionCreated;

        networkLauncher.SessionJoined -=
            HandleSessionJoined;

        networkLauncher.SessionLeft -=
            HandleSessionLeft;
    }

    private void HandleLauncherStatusChanged(
        string message)
    {
        SetStatus(message);
    }

    /// <summary>
    /// Host Session 생성 완료.
    /// </summary>
    private void HandleSessionCreated(
        string joinCode)
    {
        if (joinCodeText != null)
        {
            joinCodeText.text =
                $"참가 코드\n{joinCode}";
        }

        SetStatus(
            "Host 방 생성 완료\n" +
            "Client 접속 대기 중..."
        );

        SubmitLocalNicknameIfReady();
    }

    /// <summary>
    /// Client Session 참가 완료.
    /// </summary>
    private void HandleSessionJoined()
    {
        if (joinCodeText != null &&
            networkLauncher != null)
        {
            joinCodeText.text =
                $"참가 코드\n" +
                $"{networkLauncher.JoinCode}";
        }

        SetStatus(
            "Host 방 참가 완료"
        );

        SubmitLocalNicknameIfReady();
    }

    /// <summary>
    /// Session 종료 완료.
    /// </summary>
    private void HandleSessionLeft()
    {
        localNickname =
            string.Empty;

        ResetLobbyDisplay();

        SetStatus(
            "방에서 나왔습니다."
        );
    }

    // =========================================================
    // Nickname Synchronization
    // =========================================================

    /// <summary>
    /// 현재 PC의 닉네임을
    /// Server에 제출한다.
    /// </summary>
    private void SubmitLocalNicknameIfReady()
    {
        if (!IsSpawned)
            return;

        if (string.IsNullOrWhiteSpace(
                localNickname))
        {
            return;
        }

        var networkNickname =
            new FixedString64Bytes(
                localNickname
            );

        SubmitNicknameRpc(
            networkNickname
        );
    }

    /// <summary>
    /// Client → Server 닉네임 제출.
    ///
    /// ClientId는 Client가 보내지 않는다.
    /// Server가 실제 RPC SenderClientId를 사용한다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void SubmitNicknameRpc(
        FixedString64Bytes nickname,
        RpcParams rpcParams = default)
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        string validatedNickname =
            ValidateReceivedNickname(
                nickname.ToString()
            );

        if (string.IsNullOrWhiteSpace(
                validatedNickname))
        {
            Debug.LogWarning(
                $"Client {senderClientId}가 " +
                "올바르지 않은 닉네임을 제출했습니다."
            );

            return;
        }

        int existingIndex =
            FindPlayerIndex(
                senderClientId
            );

        bool isHostPlayer =
            senderClientId ==
            Unity.Netcode.NetworkManager
                .ServerClientId;

        var playerData =
            new LobbyPlayerData(
                senderClientId,
                new FixedString64Bytes(
                    validatedNickname
                ),
                isHostPlayer
            );

        /*
         * 이미 등록된 Client라면
         * 기존 데이터를 갱신한다.
         */
        if (existingIndex >= 0)
        {
            lobbyPlayers[existingIndex] =
                playerData;

            return;
        }

        /*
         * 현재 게임은 2인 전용.
         */
        if (lobbyPlayers.Count >=
            MaxPlayers)
        {
            Debug.LogWarning(
                $"최대 인원 {MaxPlayers}명을 " +
                "초과하여 플레이어 등록을 거부했습니다."
            );

            return;
        }

        lobbyPlayers.Add(
            playerData
        );

        Debug.Log(
            "[HostGameManager] " +
            "로비 플레이어 등록 | " +
            $"ClientId: {senderClientId} | " +
            $"Nickname: {validatedNickname} | " +
            $"Host: {isHostPlayer}"
        );
    }

    // =========================================================
    // Lobby Player Management
    // =========================================================

    private int FindPlayerIndex(
        ulong clientId)
    {
        if (lobbyPlayers == null)
            return -1;

        for (int i = 0;
             i < lobbyPlayers.Count;
             i++)
        {
            if (lobbyPlayers[i].ClientId ==
                clientId)
            {
                return i;
            }
        }

        return -1;
    }

    private void HandleClientDisconnected(
        ulong clientId)
    {
        if (!IsServer)
            return;

        int index =
            FindPlayerIndex(
                clientId
            );

        if (index >= 0)
        {
            lobbyPlayers.RemoveAt(
                index
            );
        }

        Debug.Log(
            $"[HostGameManager] " +
            $"Client 연결 종료: {clientId}"
        );
    }

    private void HandleLobbyPlayersChanged(
        NetworkListEvent<LobbyPlayerData>
            changeEvent)
    {
        RefreshLobbyUI();
    }

    // =========================================================
    // Public Lobby Data
    // =========================================================

    /// <summary>
    /// 특정 ClientId의 닉네임을 조회한다.
    ///
    /// NetworkMatchBridge 등에서 사용할 수 있다.
    /// </summary>
    public bool TryGetNickname(
        ulong clientId,
        out string nickname)
    {
        int index =
            FindPlayerIndex(
                clientId
            );

        if (index >= 0)
        {
            nickname =
                lobbyPlayers[index]
                    .Nickname
                    .ToString();

            return true;
        }

        nickname =
            string.Empty;

        return false;
    }

    // =========================================================
    // Lobby UI
    // =========================================================

    private void RefreshLobbyUI()
    {
        string hostName =
            WaitingNicknameText;

        string clientName =
            WaitingNicknameText;

        if (lobbyPlayers != null)
        {
            for (int i = 0;
                 i < lobbyPlayers.Count;
                 i++)
            {
                LobbyPlayerData player =
                    lobbyPlayers[i];

                if (player.IsHostPlayer)
                {
                    hostName =
                        player.Nickname
                            .ToString();
                }
                else
                {
                    clientName =
                        player.Nickname
                            .ToString();
                }
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

        if (ConnectedPlayerCount >=
            MaxPlayers)
        {
            SetStatus(
                "Host와 Client 연결 완료\n" +
                "게임 시작 가능"
            );
        }
        else if (IsInSession)
        {
            SetStatus(
                "플레이어 접속 대기\n" +
                $"{ConnectedPlayerCount}/" +
                $"{MaxPlayers}"
            );
        }

        LobbyPlayersChanged?.Invoke();
    }

    private void ResetLobbyDisplay()
    {
        if (joinCodeText != null)
        {
            joinCodeText.text =
                string.Empty;
        }

        if (hostNicknameText != null)
        {
            hostNicknameText.text =
                $"HOST : {WaitingNicknameText}";
        }

        if (clientNicknameText != null)
        {
            clientNicknameText.text =
                $"CLIENT : {WaitingNicknameText}";
        }
    }

    // =========================================================
    // Validation
    // =========================================================

    private bool ValidateLauncher()
    {
        if (networkLauncher != null)
            return true;

        SetStatus(
            "NetworkLauncher가 연결되어 있지 않습니다."
        );

        Debug.LogError(
            "[HostGameManager] " +
            "NetworkLauncher가 Inspector에 연결되어 있지 않습니다."
        );

        return false;
    }

    private static string
        ValidateReceivedNickname(
            string nickname)
    {
        if (string.IsNullOrWhiteSpace(
                nickname))
        {
            return string.Empty;
        }

        string result =
            nickname.Trim();

        if (result.Length >
            MaxNicknameLength)
        {
            result =
                result.Substring(
                    0,
                    MaxNicknameLength
                );
        }

        return result;
    }

    private void SetStatus(
        string message)
    {
        if (statusText != null)
        {
            statusText.text =
                message;
        }

        Debug.Log(
            $"[HostGameManager] {message}"
        );
    }
}

/// <summary>
/// Server가 관리하고
/// NGO가 Host / Client에게 동기화하는
/// 로비 플레이어 정보.
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
        ClientId =
            clientId;

        Nickname =
            nickname;

        IsHostPlayer =
            isHostPlayer;
    }

    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(
            ref ClientId
        );

        serializer.SerializeValue(
            ref Nickname
        );

        serializer.SerializeValue(
            ref IsHostPlayer
        );
    }

    public bool Equals(
        LobbyPlayerData other)
    {
        return
            ClientId ==
                other.ClientId &&
            Nickname.Equals(
                other.Nickname
            ) &&
            IsHostPlayer ==
                other.IsHostPlayer;
    }

    public override bool Equals(
        object obj)
    {
        return
            obj is LobbyPlayerData other &&
            Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash =
                ClientId.GetHashCode();

            hash =
                (hash * 397) ^
                Nickname.GetHashCode();

            hash =
                (hash * 397) ^
                IsHostPlayer.GetHashCode();

            return hash;
        }
    }
}