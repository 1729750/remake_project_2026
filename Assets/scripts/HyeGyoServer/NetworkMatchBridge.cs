using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkMatchBridge : NetworkBehaviour
{
    [Header("Lobby")]
    [SerializeField] private HostGameManager hostGameManager;

    [Header("Game Managers")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private BattleManager battleManager;

    [Header("Data")]
    [SerializeField] private CardDatabase cardDatabase;

    // 네트워크 동기화 상태

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

    private NetworkList<CardSelectionData> selectedCards;

    // 로컬 UI용 이벤트
    public event Action MatchStateChanged;
    public event Action<string> LocalMessage;

    public MatchPhase CurrentPhase => matchPhase.Value;
    public ulong CurrentTurnClientId => currentTurnClientId.Value;

    public int SelectedCardCount =>
        selectedCards != null
            ? selectedCards.Count
            : 0;

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

    // 게임 시작
    /// Host UI의 게임 시작 버튼에서 호출.
    public void RequestStartMatch()
    {
        if (!IsSpawned)
        {
            LocalMessage?.Invoke("NetworkMatchBridge가 아직 Spawn되지 않았습니다.");
            return;
        }

        RequestStartMatchRpc();
    }

    [Rpc(SendTo.Server)]
    private void RequestStartMatchRpc(
        RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        /*
         * 게임 시작은 Host만 요청 가능.
         */
        if (senderClientId !=NetworkManager.ServerClientId)
        {
            SendRejectMessage(senderClientId,"Host만 게임을 시작할 수 있습니다.");
            return;
        }

        if (hostGameManager == null ||!hostGameManager.IsRoomReady)
        {
            SendRejectMessage(senderClientId, "플레이어 2명이 모두 접속해야 합니다.");
            return;
        }

        selectedCards.Clear();

        if (playerManager != null)
        {
            playerManager.ServerResetMatch();
        }

        if (gameManager != null)
        {
            gameManager.ServerStartMatch();
        }

        //첫 번째 턴은 Host.
        currentTurnClientId.Value = NetworkManager.ServerClientId;
        matchPhase.Value = MatchPhase.ChoosingCard;

        Debug.Log("[Server] 게임 시작 | " + $"첫 턴: {currentTurnClientId.Value}");
    }

    // 카드 선택 요청
    /// Host/Client의 카드 버튼에서 호출.
    public void RequestChooseCard(int cardId)
    {
        if (!IsSpawned)
        {
            LocalMessage?.Invoke("네트워크 게임이 시작되지 않았습니다.");
            return;
        }

        RequestChooseCardRpc(cardId);
    }

    /// 이 함수는 Server에서 실행된다.
    /// Host도 Client도 동일하게 이 RPC를 사용한다.
    [Rpc(SendTo.Server)]
    private void RequestChooseCardRpc(int cardId,RpcParams rpcParams = default)
    {
        ulong senderClientId =rpcParams.Receive.SenderClientId;
        Debug.Log($"[Server] 카드 선택 요청 | " + $"Client: {senderClientId} | " + $"Card: {cardId}");

        // 검증 1 : 등록된 플레이어인가?
        if (!IsRegisteredPlayer(senderClientId))
        {
            SendRejectMessage(senderClientId,"등록되지 않은 플레이어입니다.");
            return;
        }

        // 검증 2 : 현재 카드 선택 단계인가?
        if (matchPhase.Value != MatchPhase.ChoosingCard)
        {
            SendRejectMessage(senderClientId,"현재는 카드를 선택할 수 없습니다.");
            return;
        }

        // 검증 3 : 본인 차례인가?
        if (currentTurnClientId.Value != senderClientId)
        {
            SendRejectMessage(senderClientId,"현재 당신의 차례가 아닙니다.");
            return;
        }

        // 검증 4 : 실제 존재하는 카드인가?
        if (cardDatabase == null || !cardDatabase.TryGetCard(cardId, out CardDefinition card))
        {
            SendRejectMessage(senderClientId,"존재하지 않는 카드입니다.");
            return;
        }


        // 검증 5 : 이미 선택된 카드인가?
        if (IsCardAlreadySelected(cardId))
        {
            SendRejectMessage(senderClientId,"이미 선택된 카드입니다.");
            return;
        }


        // 검증 6 : 플레이어가 선택 가능한가?
        if (playerManager != null && !playerManager.ServerCanChooseCard(senderClientId, cardId))
        {
            SendRejectMessage(senderClientId,"현재 이 카드를 선택할 수 없습니다.");
            return;
        }

        // 여기까지 왔으면 Server 승인
    ServerApplyCardSelection(senderClientId,card);
    }

    // Server의 실제 카드 처리
    private void ServerApplyCardSelection(ulong clientId, CardDefinition card)
    {
        if (!IsServer)
            return;

        /*
         * 서버 내부 게임 데이터 변경
         */
        if (playerManager != null)
        {
            playerManager.ServerSetSelectedCard(
                clientId,
                card.Id
            );
        }

        if (gameManager != null)
        {
            gameManager.ServerApplyCardSelection(
                clientId,
                card
            );
        }

        /*
         * NetworkList 변경.
         *
         * 여기의 데이터가 NGO를 통해
         * Host와 Client 모두에게 동기화된다.
         */
        selectedCards.Add(
            new CardSelectionData(
                clientId,
                card.Id
            )
        );

        Debug.Log(
            $"[Server] 카드 선택 승인 | " +
            $"Client: {clientId} | " +
            $"Card: {card.Id}"
        );

        // 2명이 모두 선택했다.
        if (selectedCards.Count >= 2)
        {
            matchPhase.Value =
                MatchPhase.ChoosingCondition;

            Debug.Log(
                "[Server] 카드 선택 완료 → 조건 선택 단계"
            );

            return;
        }

        //아직 한 명만 골랐다면 다음 플레이어에게 턴 넘김.
        currentTurnClientId.Value =
            GetOtherClientId(clientId);
    }

    // 검증 Helper
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

    private bool IsCardAlreadySelected(
        int cardId)
    {
        for (int i = 0;
             i < selectedCards.Count;
             i++)
        {
            if (selectedCards[i].CardId ==
                cardId)
            {
                return true;
            }
        }

        return false;
    }

    private ulong GetOtherClientId(
        ulong currentClientId)
    {
        if (NetworkManager.Singleton == null)
            return currentClientId;

        foreach (
            NetworkClient client in
            NetworkManager.Singleton
                .ConnectedClientsList)
        {
            if (client.ClientId !=
                currentClientId)
            {
                return client.ClientId;
            }
        }

        return currentClientId;
    }

    // 동기화된 데이터 조회
    public bool TryGetSelectedCard(
        ulong clientId,
        out int cardId)
    {
        for (int i = 0;
             i < selectedCards.Count;
             i++)
        {
            CardSelectionData data =
                selectedCards[i];

            if (data.ClientId ==
                clientId)
            {
                cardId = data.CardId;
                return true;
            }
        }

        cardId = -1;
        return false;
    }

    // Client UI 갱신
    private void HandleMatchPhaseChanged(
        MatchPhase previous,
        MatchPhase current)
    {
        Debug.Log(
            $"MatchPhase: {previous} → {current}"
        );

        MatchStateChanged?.Invoke();
    }

    private void HandleTurnChanged(
        ulong previous,
        ulong current)
    {
        Debug.Log(
            $"Turn: {previous} → {current}"
        );

        MatchStateChanged?.Invoke();
    }

    private void HandleSelectedCardsChanged(
        NetworkListEvent<CardSelectionData>
            changeEvent)
    {
        MatchStateChanged?.Invoke();
    }

    // 요청 거절 메시지
    private void SendRejectMessage(
        ulong targetClientId,
        string message)
    {
        RejectRequestRpc(
            targetClientId,
            new FixedString128Bytes(message)
        );
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void RejectRequestRpc(
        ulong targetClientId,
        FixedString128Bytes message)
    {
        if (NetworkManager.Singleton == null)
            return;

        //RPC 자체는 전체에게 전달하지만,해당 Client만 메시지를 사용한다.
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