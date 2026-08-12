using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkMatchBridge : NetworkBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("Game Mode")]
    [Tooltip("False = 기존 SingleMode / True = 네트워크 MultiMode")]
    [SerializeField]
    private bool multiGameMode = true;


    [Header("Lobby")]
    [SerializeField]
    private HostGameManager hostGameManager;


    [Header("Data")]
    [SerializeField]
    private CardDatabase cardDatabase;


    /*
     * 기존 GameManager / PlayerManager / BattleManager는
     * 다른 팀원이 만든 SingleMode 로직이므로
     * 현재 NetworkMatchBridge에서 직접 수정하지 않는다.
     *
     * 추후 MultiMode와 실제 전투 로직을 연결할 때
     * 필요한 API를 협의해서 연결하는 것이 좋다.
     */


    // =========================================================
    // Network State
    // =========================================================

    private readonly NetworkVariable<MatchPhase> matchPhase =
        new NetworkVariable<MatchPhase>(
            MatchPhase.WaitingForPlayers,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );


    private readonly NetworkVariable<ulong> currentTurnClientId =
        new NetworkVariable<ulong>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );


    /*
     * 어떤 Client가 어떤 CardId를 선택했는지 저장.
     *
     * Server만 변경하고 NGO가 Host / Client에게
     * 자동으로 동기화한다.
     */
    private NetworkList<CardSelectionData> selectedCards;


    // =========================================================
    // Local Events
    // =========================================================

    /// <summary>
    /// MatchPhase, Turn, SelectedCards 등에 변화가 생겼을 때
    /// 로컬 UI가 갱신할 수 있도록 호출한다.
    /// </summary>
    public event Action MatchStateChanged;


    /// <summary>
    /// 현재 로컬 플레이어에게 메시지를 표시할 때 사용한다.
    /// </summary>
    public event Action<string> LocalMessage;


    // =========================================================
    // Public Properties
    // =========================================================

    /// <summary>
    /// true이면 MultiMode.
    /// false이면 기존 SingleMode.
    /// </summary>
    public bool IsMultiGameMode =>
        multiGameMode;


    public MatchPhase CurrentPhase =>
        matchPhase.Value;


    public ulong CurrentTurnClientId =>
        currentTurnClientId.Value;


    public int SelectedCardCount =>
        selectedCards != null
            ? selectedCards.Count
            : 0;


    // =========================================================
    // Unity / NGO Life Cycle
    // =========================================================

    private void Awake()
    {
        selectedCards =
            new NetworkList<CardSelectionData>();
    }


    public override void OnNetworkSpawn()
    {
        matchPhase.OnValueChanged +=
            HandleMatchPhaseChanged;

        currentTurnClientId.OnValueChanged +=
            HandleTurnChanged;

        selectedCards.OnListChanged +=
            HandleSelectedCardsChanged;


        /*
         * SingleMode일 경우 NetworkMatchBridge는
         * 게임 진행에 개입하지 않는다.
         *
         * NetworkObject 자체가 존재하는 것은 문제없다.
         */
        if (!multiGameMode)
        {
            Debug.Log(
                "[NetworkMatchBridge] SingleMode로 설정되어 있습니다."
            );

            return;
        }


        Debug.Log(
            "[NetworkMatchBridge] MultiMode로 시작합니다."
        );

        MatchStateChanged?.Invoke();
    }


    public override void OnNetworkDespawn()
    {
        matchPhase.OnValueChanged -=
            HandleMatchPhaseChanged;

        currentTurnClientId.OnValueChanged -=
            HandleTurnChanged;

        selectedCards.OnListChanged -=
            HandleSelectedCardsChanged;
    }


    // =========================================================
    // Match Start
    // =========================================================

    /// <summary>
    /// Host의 게임 시작 버튼에서 호출한다.
    ///
    /// multiGameMode == false:
    /// 기존 SingleMode를 사용하므로 아무것도 하지 않는다.
    ///
    /// multiGameMode == true:
    /// Server에게 멀티플레이 게임 시작을 요청한다.
    /// </summary>
    public void RequestStartMatch()
    {
        if (!multiGameMode)
        {
            Debug.Log(
                "[NetworkMatchBridge] " +
                "SingleMode에서는 네트워크 게임 시작을 사용하지 않습니다."
            );

            return;
        }


        if (!IsSpawned)
        {
            LocalMessage?.Invoke(
                "NetworkMatchBridge가 아직 Spawn되지 않았습니다."
            );

            return;
        }


        RequestStartMatchRpc();
    }


    /// <summary>
    /// Server에서 실제 게임 시작 요청을 검증한다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void RequestStartMatchRpc(
        RpcParams rpcParams = default)
    {
        if (!multiGameMode)
            return;


        ulong senderClientId =
            rpcParams.Receive.SenderClientId;


        // -----------------------------------------------------
        // 검증 1 : Host가 요청했는가?
        // -----------------------------------------------------

        if (senderClientId !=
            NetworkManager.ServerClientId)
        {
            SendRejectMessage(
                senderClientId,
                "Host만 게임을 시작할 수 있습니다."
            );

            return;
        }


        // -----------------------------------------------------
        // 검증 2 : Host + Client 두 명이 모두 들어왔는가?
        // -----------------------------------------------------

        if (hostGameManager == null)
        {
            SendRejectMessage(
                senderClientId,
                "HostGameManager가 연결되어 있지 않습니다."
            );

            return;
        }


        if (!hostGameManager.IsRoomReady)
        {
            SendRejectMessage(
                senderClientId,
                "플레이어 2명이 모두 접속해야 합니다."
            );

            return;
        }


        // -----------------------------------------------------
        // 이전 멀티플레이 상태 초기화
        // -----------------------------------------------------

        selectedCards.Clear();


        /*
         * 기존 PlayerManager.ServerResetMatch()
         * 기존 GameManager.ServerStartMatch()
         *
         * 위 함수들은 실제 프로젝트에 존재하지 않으므로
         * 호출하지 않는다.
         *
         * MultiMode의 상태는 현재 NetworkMatchBridge가
         * 직접 관리한다.
         */


        // 첫 번째 카드 선택 턴은 Host
        currentTurnClientId.Value =
            NetworkManager.ServerClientId;


        // 카드 선택 단계로 전환
        matchPhase.Value =
            MatchPhase.ChoosingCard;


        Debug.Log(
            "[Server] 멀티 게임 시작 | " +
            $"첫 번째 턴 ClientId: {currentTurnClientId.Value}"
        );
    }


    // =========================================================
    // Card Selection Request
    // =========================================================

    /// <summary>
    /// Host / Client의 카드 UI Button에서 호출한다.
    /// </summary>
    public void RequestChooseCard(int cardId)
    {
        if (!multiGameMode)
        {
            Debug.Log(
                "[NetworkMatchBridge] " +
                "SingleMode에서는 네트워크 카드 선택을 사용하지 않습니다."
            );

            return;
        }


        if (!IsSpawned)
        {
            LocalMessage?.Invoke(
                "네트워크 게임이 시작되지 않았습니다."
            );

            return;
        }


        RequestChooseCardRpc(cardId);
    }


    /// <summary>
    /// Host / Client가 선택한 CardId를
    /// Server에게 전송한다.
    ///
    /// 모든 검증은 Server에서 수행한다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void RequestChooseCardRpc(
        int cardId,
        RpcParams rpcParams = default)
    {
        if (!multiGameMode)
            return;


        ulong senderClientId =
            rpcParams.Receive.SenderClientId;


        Debug.Log(
            "[Server] 카드 선택 요청 | " +
            $"Client: {senderClientId} | " +
            $"CardId: {cardId}"
        );


        // -----------------------------------------------------
        // 검증 1 : 등록된 플레이어인가?
        // -----------------------------------------------------

        if (!IsRegisteredPlayer(senderClientId))
        {
            SendRejectMessage(
                senderClientId,
                "등록되지 않은 플레이어입니다."
            );

            return;
        }


        // -----------------------------------------------------
        // 검증 2 : 현재 카드 선택 단계인가?
        // -----------------------------------------------------

        if (matchPhase.Value !=
            MatchPhase.ChoosingCard)
        {
            SendRejectMessage(
                senderClientId,
                "현재는 카드를 선택할 수 없습니다."
            );

            return;
        }


        // -----------------------------------------------------
        // 검증 3 : 현재 본인의 차례인가?
        // -----------------------------------------------------

        if (currentTurnClientId.Value !=
            senderClientId)
        {
            SendRejectMessage(
                senderClientId,
                "현재 당신의 차례가 아닙니다."
            );

            return;
        }


        // -----------------------------------------------------
        // 검증 4 : CardDatabase가 연결되어 있는가?
        // -----------------------------------------------------

        if (cardDatabase == null)
        {
            Debug.LogError(
                "[NetworkMatchBridge] " +
                "CardDatabase가 Inspector에 연결되어 있지 않습니다."
            );


            SendRejectMessage(
                senderClientId,
                "카드 데이터베이스 오류입니다."
            );

            return;
        }


        // -----------------------------------------------------
        // 검증 5 : 실제 존재하는 CardId인가?
        // -----------------------------------------------------

        /*
         * 기존 CardDefinition에는 Id 필드가 없다.
         *
         * 따라서 CardDefinition.Id를 사용하지 않고,
         * CardDatabase가 CardId → CardDefinition 관계를 관리한다.
         */
        if (!cardDatabase.TryGetCard(
                cardId,
                out _))
        {
            SendRejectMessage(
                senderClientId,
                "존재하지 않는 카드입니다."
            );

            return;
        }


        // -----------------------------------------------------
        // 검증 6 : 이미 다른 플레이어가 선택한 카드인가?
        // -----------------------------------------------------

        if (IsCardAlreadySelected(cardId))
        {
            SendRejectMessage(
                senderClientId,
                "이미 선택된 카드입니다."
            );

            return;
        }


        /*
         * 기존에 사용하던:
         *
         * playerManager.ServerCanChooseCard(...)
         *
         * 메서드는 현재 PlayerManager에 존재하지 않는다.
         *
         * 따라서 현재 MultiMode에서는
         * NetworkMatchBridge 자체 검증만 수행한다.
         */


        // -----------------------------------------------------
        // 모든 검증 완료
        // -----------------------------------------------------

        ServerApplyCardSelection(
            senderClientId,
            cardId
        );
    }


    // =========================================================
    // Server Card Selection Apply
    // =========================================================

    /// <summary>
    /// 검증이 끝난 카드 선택을
    /// Server의 NetworkList에 저장한다.
    /// </summary>
    private void ServerApplyCardSelection(
        ulong clientId,
        int cardId)
    {
        if (!IsServer)
            return;


        if (!multiGameMode)
            return;


        /*
         * 기존 PlayerManager에는
         * ServerSetSelectedCard()가 존재하지 않는다.
         *
         * 기존 GameManager에도
         * ServerApplyCardSelection()가 존재하지 않는다.
         *
         * 따라서 현재 MultiMode의 카드 선택 상태는
         * NetworkMatchBridge가 직접 관리한다.
         */


        // -----------------------------------------------------
        // ClientId + CardId를 NetworkList에 저장
        // -----------------------------------------------------

        selectedCards.Add(
            new CardSelectionData(
                clientId,
                cardId
            )
        );


        /*
         * NetworkList의 변경 사항은 NGO가
         * Host / Client 양쪽에 자동 동기화한다.
         */


        Debug.Log(
            "[Server] 카드 선택 승인 | " +
            $"Client: {clientId} | " +
            $"CardId: {cardId}"
        );


        // -----------------------------------------------------
        // Host + Client 두 명 모두 선택 완료
        // -----------------------------------------------------

        if (selectedCards.Count >= 2)
        {
            matchPhase.Value =
                MatchPhase.ChoosingCondition;


            Debug.Log(
                "[Server] 카드 선택 완료 → 조건 선택 단계"
            );


            return;
        }


        // -----------------------------------------------------
        // 아직 한 명만 골랐다면
        // 다른 플레이어에게 턴을 넘긴다.
        // -----------------------------------------------------

        ulong nextClientId =
            GetOtherClientId(clientId);


        currentTurnClientId.Value =
            nextClientId;


        Debug.Log(
            "[Server] 카드 선택 턴 변경 | " +
            $"Next Client: {nextClientId}"
        );
    }


    // =========================================================
    // Validation Helpers
    // =========================================================

    /// <summary>
    /// HostGameManager의 LobbyPlayerData에
    /// 등록된 Client인지 확인한다.
    /// </summary>
    private bool IsRegisteredPlayer(
        ulong clientId)
    {
        if (hostGameManager == null)
            return false;


        return hostGameManager.TryGetNickname(
            clientId,
            out _
        );
    }


    /// <summary>
    /// 해당 CardId가 이미 선택되었는지 검사한다.
    /// </summary>
    private bool IsCardAlreadySelected(
        int cardId)
    {
        if (selectedCards == null)
            return false;


        for (int i = 0;
             i < selectedCards.Count;
             i++)
        {
            CardSelectionData data =
                selectedCards[i];


            if (data.CardId == cardId)
            {
                return true;
            }
        }


        return false;
    }


    /// <summary>
    /// 현재 Client가 아닌 다른 Client의 ID를 찾는다.
    ///
    /// 1:1 게임이므로 Host라면 Client,
    /// Client라면 Host를 반환한다.
    /// </summary>
    private ulong GetOtherClientId(
        ulong currentClientId)
    {
        if (NetworkManager.Singleton == null)
            return currentClientId;


        foreach (
            NetworkClient client
            in NetworkManager.Singleton
                .ConnectedClientsList)
        {
            if (client.ClientId !=
                currentClientId)
            {
                return client.ClientId;
            }
        }


        /*
         * 다른 플레이어가 없다면
         * 현재 ClientId를 그대로 반환한다.
         */
        return currentClientId;
    }


    // =========================================================
    // Synchronized Data Read
    // =========================================================

    /// <summary>
    /// 특정 Client가 선택한 CardId를 조회한다.
    ///
    /// Host / Client 양쪽에서 호출 가능하다.
    /// </summary>
    public bool TryGetSelectedCard(
        ulong clientId,
        out int cardId)
    {
        if (selectedCards == null)
        {
            cardId = -1;
            return false;
        }


        for (int i = 0;
             i < selectedCards.Count;
             i++)
        {
            CardSelectionData data =
                selectedCards[i];


            if (data.ClientId ==
                clientId)
            {
                cardId =
                    data.CardId;


                return true;
            }
        }


        cardId = -1;
        return false;
    }


    // =========================================================
    // Network State Events
    // =========================================================

    private void HandleMatchPhaseChanged(
        MatchPhase previous,
        MatchPhase current)
    {
        if (!multiGameMode)
            return;


        Debug.Log(
            $"[NetworkMatchBridge] " +
            $"MatchPhase: {previous} → {current}"
        );


        MatchStateChanged?.Invoke();
    }


    private void HandleTurnChanged(
        ulong previous,
        ulong current)
    {
        if (!multiGameMode)
            return;


        Debug.Log(
            $"[NetworkMatchBridge] " +
            $"Turn: {previous} → {current}"
        );


        MatchStateChanged?.Invoke();
    }


    private void HandleSelectedCardsChanged(
        NetworkListEvent<CardSelectionData>
            changeEvent)
    {
        if (!multiGameMode)
            return;


        Debug.Log(
            "[NetworkMatchBridge] " +
            $"SelectedCards 변경 | Count: {selectedCards.Count}"
        );


        MatchStateChanged?.Invoke();
    }


    // =========================================================
    // Reject Message
    // =========================================================

    /// <summary>
    /// Server가 특정 Client에게
    /// 요청 거절 메시지를 보낸다.
    /// </summary>
    private void SendRejectMessage(
        ulong targetClientId,
        string message)
    {
        if (!IsServer)
            return;


        RejectRequestRpc(
            targetClientId,
            new FixedString128Bytes(message)
        );
    }


    /// <summary>
    /// RPC는 모든 Client에게 전송되지만
    /// targetClientId와 일치하는 Client만
    /// 메시지를 처리한다.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void RejectRequestRpc(
        ulong targetClientId,
        FixedString128Bytes message)
    {
        if (!multiGameMode)
            return;


        if (NetworkManager.Singleton == null)
            return;


        if (NetworkManager.Singleton.LocalClientId !=
            targetClientId)
        {
            return;
        }


        LocalMessage?.Invoke(
            message.ToString()
        );
    }
}