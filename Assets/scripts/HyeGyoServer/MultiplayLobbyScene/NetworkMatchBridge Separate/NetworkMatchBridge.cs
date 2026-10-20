using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Client/UI 요청을 Server로 전달하는 중계 전용 클래스.
/// 상태 저장과 게임 판정은 다른 클래스가 담당한다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkMatchState))]
public sealed class NetworkMatchBridge : NetworkBehaviour
{
    [Header("Network Mode")]
    [SerializeField]
    private NetworkModeGate networkModeGate;

    [Header("Server Logic")]
    [SerializeField]
    private MatchServerController serverController;

    [Header("Shared State")]
    [SerializeField]
    private NetworkMatchState matchState;

    [Header("Preparation")]
    [SerializeField]
    private MultiPreparationManager preparationManager;

    [SerializeField]
    private CardOptionGenerator cardOptionGenerator;


    // =========================================================
    // Events
    // =========================================================

    public event Action<string> LocalMessage;

    /// <summary>
    /// Server에서 생성한 강화 후보가
    /// 이 Local Client에게 도착했을 때 발생한다.
    /// Reward_Multi가 구독한다.
    /// </summary>
    public event Action<EnhanceOptionNetData[]>
        EnhanceCandidatesReceived;


    // =========================================================
    // State Read
    // =========================================================

    public bool IsMultiGameMode =>
        networkModeGate != null &&
        networkModeGate.NetworkEnabled;

    public MatchPhase CurrentPhase =>
        matchState != null
            ? matchState.CurrentPhase
            : MatchPhase.WaitingForPlayers;

    public ulong CurrentTurnClientId =>
        matchState != null
            ? matchState.CurrentTurnClientId
            : 0;

    public int SelectedCardCount =>
        matchState != null
            ? matchState.SelectedCardCount
            : 0;


    public event Action MatchStateChanged
    {
        add
        {
            if (matchState != null)
                matchState.MatchStateChanged += value;
        }

        remove
        {
            if (matchState != null)
                matchState.MatchStateChanged -= value;
        }
    }


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        if (matchState == null)
        {
            matchState =
                GetComponent<NetworkMatchState>();
        }

        if (serverController == null)
        {
            serverController =
                GetComponent<MatchServerController>();
        }

if (networkModeGate == null ||
    !networkModeGate.NetworkEnabled)
{
    NetworkModeGate[] gates =
        FindObjectsByType<NetworkModeGate>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

    foreach (NetworkModeGate gate in gates)
    {
        if (gate == null)
            continue;

        if (!gate.NetworkEnabled)
            continue;

        networkModeGate = gate;

        Debug.Log(
            "[NetworkMatchBridge] " +
            $"활성 NetworkModeGate 자동 연결 | " +
            $"Name: {gate.gameObject.name} | " +
            $"InstanceID: {gate.GetInstanceID()}"
        );

        break;
    }
}
    }


    // =========================================================
    // Match Start
    // =========================================================

    public void RequestStartMatch()
    {
        if (!CanSendNetworkRequest())
            return;

        RequestStartMatchRpc();
    }


    [Rpc(SendTo.Server)]
    private void RequestStartMatchRpc(
        RpcParams rpcParams = default)
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        if (serverController == null)
        {
            SendRejectMessage(
                senderClientId,
                "MatchServerController가 연결되어 있지 않습니다."
            );

            return;
        }

        if (!serverController.TryStartMatch(
                senderClientId,
                out string rejectReason))
        {
            SendRejectMessage(
                senderClientId,
                rejectReason
            );
        }
    }


    // =========================================================
    // Card Selection
    // =========================================================

    public void RequestChooseCard(
        int cardId)
    {
        if (!CanSendNetworkRequest())
            return;

        RequestChooseCardRpc(
            cardId
        );
    }


    [Rpc(SendTo.Server)]
    private void RequestChooseCardRpc(
        int cardId,
        RpcParams rpcParams = default)
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        if (serverController == null)
        {
            SendRejectMessage(
                senderClientId,
                "MatchServerController가 연결되어 있지 않습니다."
            );

            return;
        }

        if (!serverController.TryChooseCard(
                senderClientId,
                cardId,
                out string rejectReason))
        {
            SendRejectMessage(
                senderClientId,
                rejectReason
            );
        }
    }


    // =========================================================
    // Enhance Candidates
    // =========================================================

    /// <summary>
    /// Host / Client UI에서 강화 후보 3개를 요청한다.
    /// 실제 랜덤 생성은 Server가 수행한다.
    /// </summary>
    public void RequestEnhanceCandidates()
    {
        if (!CanSendNetworkRequest())
            return;

        Debug.Log(
            "[NetworkMatchBridge] " +
            "강화 후보 요청 전송"
        );

        RequestEnhanceCandidatesRpc();
    }


    /// <summary>
    /// Client → Server
    ///
    /// 실제 요청을 보낸 ClientId는
    /// 사용자가 직접 보내지 않고 NGO에서 가져온다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void RequestEnhanceCandidatesRpc(
        RpcParams rpcParams = default)
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        Debug.Log(
            "[NetworkMatchBridge][Server] " +
            $"강화 후보 요청 수신 | " +
            $"ClientId: {senderClientId}"
        );

        if (serverController == null)
        {
            SendRejectMessage(
                senderClientId,
                "MatchServerController가 연결되어 있지 않습니다."
            );

            return;
        }

        if (!serverController
                .TryCreateEnhanceCandidates(
                    senderClientId,
                    out EnhanceOptionNetData[] options,
                    out string rejectReason))
        {
            SendRejectMessage(
                senderClientId,
                rejectReason
            );

            return;
        }

        // 현재 1차 구현에서는 후보가 정확히 3개여야 한다.
        if (options == null ||
            options.Length != 3)
        {
            SendRejectMessage(
                senderClientId,
                "강화 후보 생성 개수가 올바르지 않습니다."
            );

            return;
        }

        SendEnhanceCandidatesClientRpc(
            options[0], options[1], options[2],
            new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { senderClientId } } });
    }


    /// <summary>
    /// Server → 해당 Host/Client
    ///
    /// MatchServerController가 생성한 후보 3개를
    /// Reward_Multi 쪽으로 전달한다.
    /// </summary>
    [ClientRpc]
    private void SendEnhanceCandidatesClientRpc(
        EnhanceOptionNetData option0,
        EnhanceOptionNetData option1,
        EnhanceOptionNetData option2,
        ClientRpcParams clientRpcParams = default)
    {

        EnhanceOptionNetData[] options =
        {
            option0,
            option1,
            option2
        };

        Debug.Log(
            "[NetworkMatchBridge] " +
            $"강화 후보 수신 완료 | " +
            $"LocalClientId: " +
            $"{NetworkManager.Singleton.LocalClientId}"
        );

        EnhanceCandidatesReceived?.Invoke(
            options
        );
    }


    // =========================================================
    // Enhance Confirm
    // =========================================================

    /// <summary>
    /// Reward_Multi에서 선택한 후보 index만 Server에 전달한다.
    ///
    /// CardUpgrade 자체를 보내지 않는다.
    /// </summary>
    public void RequestConfirmEnhanceCandidate(
        int selectedIndex)
    {
        if (!CanSendNetworkRequest())
            return;

        Debug.Log(
            "[NetworkMatchBridge] " +
            $"강화 후보 선택 전송 | " +
            $"Index: {selectedIndex}"
        );

        RequestConfirmEnhanceCandidateRpc(
            selectedIndex
        );
    }


    /// <summary>
    /// Client → Server
    ///
    /// Server가 이전에 해당 ClientId에게 발급해둔
    /// CardUpgrade[]에서 selectedIndex를 다시 찾아 검증한다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void RequestConfirmEnhanceCandidateRpc(
        int selectedIndex,
        RpcParams rpcParams = default)
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        Debug.Log(
            "[NetworkMatchBridge][Server] " +
            $"강화 후보 선택 수신 | " +
            $"ClientId: {senderClientId} | " +
            $"Index: {selectedIndex}"
        );

        if (serverController == null)
        {
            SendRejectMessage(
                senderClientId,
                "MatchServerController가 연결되어 있지 않습니다."
            );

            return;
        }

        if (!serverController
                .TryConfirmEnhanceCandidate(
                    senderClientId,
                    selectedIndex,
                    out string rejectReason))
        {
            SendRejectMessage(
                senderClientId,
                rejectReason
            );

            return;
        }

        Debug.Log(
            "[NetworkMatchBridge][Server] " +
            $"강화 후보 선택 검증 완료 | " +
            $"ClientId: {senderClientId} | " +
            $"Index: {selectedIndex}"
        );
    }


    // =========================================================
    // Backward-compatible State Read
    // =========================================================

    public bool TryGetSelectedCard(
        ulong clientId,
        out int cardId)
    {
        if (matchState == null)
        {
            cardId = -1;
            return false;
        }

        return matchState.TryGetSelectedCard(
            clientId,
            out cardId
        );
    }


    // =========================================================
    // Validation
    // =========================================================

private bool CanSendNetworkRequest()
{
    if (networkModeGate == null)
    {
        Debug.LogError(
            "[NetworkMatchBridge] " +
            "요청 실패: NetworkModeGate == null"
        );

        return false;
    }

    if (!networkModeGate.NetworkEnabled)
    {
        Debug.LogError(
            "[NetworkMatchBridge] " +
            "요청 실패: NetworkEnabled == false"
        );

        return false;
    }

    if (!IsSpawned)
    {
        Debug.LogError(
            "[NetworkMatchBridge] " +
            "요청 실패: IsSpawned == false"
        );

        return false;
    }

    Debug.Log(
        "[NetworkMatchBridge] " +
        "CanSendNetworkRequest 성공"
    );

    return true;
}

    // =========================================================
    // Reject Message
    // =========================================================

    private void SendRejectMessage(
        ulong targetClientId,
        string message)
    {
        if (!IsServer)
            return;

        Debug.LogWarning(
            "[NetworkMatchBridge][Server] " +
            $"요청 거절 | " +
            $"ClientId: {targetClientId} | " +
            $"Reason: {message}"
        );

        RejectRequestClientRpc(
            new FixedString128Bytes(message),
            new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { targetClientId } } });
    }


    [ClientRpc]
    private void RejectRequestClientRpc(
        FixedString128Bytes message,
        ClientRpcParams clientRpcParams = default)
    {
        Debug.LogWarning(
            "[NetworkMatchBridge] " +
            $"Server 요청 거절 | " +
            $"{message}"
        );

        LocalMessage?.Invoke(
            message.ToString()
        );
    }

public void RequestBeginPreparation()
{
    Debug.Log(
        "[NetworkMatchBridge] " +
        $"Gate Name: {(networkModeGate != null ? networkModeGate.gameObject.name : "NULL")} | " +
        $"Gate InstanceID: {(networkModeGate != null ? networkModeGate.GetInstanceID() : -1)} | " +
        $"NetworkEnabled: {(networkModeGate != null && networkModeGate.NetworkEnabled)}"
    );

    NetworkModeGate[] allGates =
        FindObjectsByType<NetworkModeGate>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

    Debug.Log(
        $"[NetworkMatchBridge] 현재 씬/런타임 NetworkModeGate 개수: {allGates.Length}"
    );

    for (int i = 0; i < allGates.Length; i++)
    {
        Debug.Log(
            "[NetworkMatchBridge] " +
            $"Gate[{i}] | " +
            $"Name: {allGates[i].gameObject.name} | " +
            $"InstanceID: {allGates[i].GetInstanceID()} | " +
            $"Enabled: {allGates[i].NetworkEnabled} | " +
            $"Active: {allGates[i].gameObject.activeInHierarchy}"
        );
    }

    if (!CanSendNetworkRequest())
    {
        Debug.LogWarning(
            "[NetworkMatchBridge] CanSendNetworkRequest 실패"
        );

        return;
    }

    Debug.Log(
        "[NetworkMatchBridge] 준비 선택지 요청 전송"
    );

    RequestBeginPreparationRpc();
}

    private void ResolvePreparationReferences()
    {
        if (preparationManager == null) preparationManager = FindFirstObjectByType<MultiPreparationManager>();
        if (cardOptionGenerator == null) cardOptionGenerator = FindFirstObjectByType<CardOptionGenerator>();
    }

[Rpc(SendTo.Server)]
private void RequestBeginPreparationRpc(
    RpcParams rpcParams = default)
{
    ResolvePreparationReferences();
    ulong senderClientId =
        rpcParams.Receive.SenderClientId;

        Debug.Log(
        "[NetworkMatchBridge][Server] " +
        $"준비 선택지 요청 수신 | " +
        $"ClientId: {senderClientId}"
    );
        Debug.Log(
        "[NetworkMatchBridge][Server] " +
        $"현재 Preparation Phase: " +
        $"{(preparationManager != null ? "Manager 있음" : "Manager 없음")}"
    );

    if (preparationManager == null)
    {
        SendRejectMessage(
            senderClientId,
            "MultiPreparationManager가 없습니다."
        );

        return;
    }

    if (cardOptionGenerator == null)
    {
        SendRejectMessage(
            senderClientId,
            "CardOptionGenerator가 없습니다."
        );

        return;
    }

    if (!preparationManager.TryBeginForPlayer(
            senderClientId,
            out PreparationOption[] options,
            out string rejectReason))
    {
        SendRejectMessage(
            senderClientId,
            rejectReason
        );

        return;
    }

    if (options != null && options.Length != 3)
    {
        SendRejectMessage(
            senderClientId,
            "준비 선택지가 3개가 아닙니다."
        );

        return;
    }

    SendPreparationState(senderClientId, options);
}

private int localPreparationRound;
private PreparationOption[] localPreparationOptions;

    public bool PreparationRequestPending { get; private set; }

public void RequestConfirmPreparationOption(int optionIndex)
{
    if (!PreparationRequestPending && CanSendNetworkRequest())
    {
        PreparationRequestPending = true;
        RequestConfirmPreparationOptionRpc(optionIndex, localPreparationRound);
    }
}

public void RequestSkipPreparation()
{
    RequestConfirmPreparationOption(-1);
}

[Rpc(SendTo.Server)]
private void RequestConfirmPreparationOptionRpc(int optionIndex, int expectedRound, RpcParams rpcParams = default)
{
    ResolvePreparationReferences();
    ulong sender = rpcParams.Receive.SenderClientId;
    if (preparationManager == null) return;
    bool accepted = preparationManager.TryConfirmOption(sender, optionIndex, expectedRound,
        out _, out PreparationOption[] next, out _, out string reason);
    if (!accepted)
    {
        SendRejectMessage(sender, reason);
        preparationManager.TryBeginForPlayer(sender, out next, out _);
    }
    SendPreparationState(sender, next);
    if (accepted) preparationManager.CheckAllPlayersCompleted();
}

[Serializable]
private class PreparationViewState
{
    public string[] cards;
    public PreparationOptionNetData[] metadata;
    public string[] candidateCards;
    public int remaining;
}

private void SendPreparationState(ulong owner, PreparationOption[] options)
{
    if (!IsServer || !NetworkManager.Singleton.ConnectedClients.ContainsKey(owner)) return;
    CardDefinition[] deck = preparationManager.GetConfirmedDeck(owner);
    int[] sources = preparationManager.GetConfirmedSources(owner);
    var cards = new string[deck == null ? 0 : deck.Length];
    for (int i = 0; i < cards.Length; i++) cards[i] = cardOptionGenerator.SerializeCard(deck[i], sources[i]);
    var metadata = new PreparationOptionNetData[options == null ? 0 : options.Length];
    var candidateCards = new string[metadata.Length];
    for (int i = 0; i < metadata.Length; i++)
    {
        metadata[i] = cardOptionGenerator.ToNetData(options[i]);
        int source = options[i].Type == PreparationOptionType.AddCard ? options[i].CardPoolIndex
            : options[i].Type == PreparationOptionType.EnhanceCard ? sources[options[i].TargetCardIndex] : -1;
        candidateCards[i] = options[i].Card == null ? string.Empty : cardOptionGenerator.SerializeCard(options[i].Card, source);
    }
    string payload = JsonUtility.ToJson(new PreparationViewState
    {
        cards = cards, metadata = metadata, candidateCards = candidateCards,
        remaining = preparationManager.GetRemainingChoices(owner)
    });
    SendPreparationStateClientRpc(payload,
        new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { owner } } });
}

[ClientRpc]
private void SendPreparationStateClientRpc(string payload, ClientRpcParams clientRpcParams = default)
{
    ResolvePreparationReferences();
    if (payload == null || payload.Length > 262144) return;
    PreparationViewState state = JsonUtility.FromJson<PreparationViewState>(payload);
    if (state == null || state.cards == null || state.cards.Length > 10 || state.metadata == null ||
        (state.metadata.Length != 0 && state.metadata.Length != 3) || state.candidateCards == null ||
        state.candidateCards.Length != state.metadata.Length || state.remaining < 0 || state.remaining > 10 ||
        10 - state.remaining < localPreparationRound) return;
    string[] cards = state.cards;
    PreparationOptionNetData[] metadata = state.metadata;
    string[] candidateCards = state.candidateCards;
    int remaining = state.remaining;
    var deck = new CardDefinition[cards.Length];
    var options = new PreparationOption[metadata.Length];
    try
    {
        for (int i = 0; i < cards.Length; i++) deck[i] = cardOptionGenerator.DeserializeCard(cards[i]);
        for (int i = 0; i < options.Length; i++)
        {
            options[i] = new PreparationOption
            {
                Type = (PreparationOptionType)metadata[i].Type, CardPoolIndex = metadata[i].CardPoolIndex,
                TargetCardIndex = metadata[i].TargetCardIndex, EnhanceId = metadata[i].EnhanceId,
                Card = string.IsNullOrEmpty(candidateCards[i]) ? null : cardOptionGenerator.DeserializeCard(candidateCards[i])
            };
        }
    }
    catch (Exception exception)
    {
        foreach (CardDefinition card in deck) if (card != null) Destroy(card);
        foreach (PreparationOption option in options) if (option.Card != null) Destroy(option.Card);
        Debug.LogException(exception);
        return;
    }
    if (10 - remaining < localPreparationRound) return;
    localPreparationRound = 10 - remaining;
    PreparationRequestPending = false;
    MapManager_Multi map = MapManager_Multi.Instance;
    map?.ApplyConfirmedDeckSnapshot(deck);
    foreach (CardDefinition card in deck) if (card != null) Destroy(card);
    map?.SetRemainingChoices(remaining);
    if (options.Length == 3) map?.ShowOptions(options);
    else map?.ShowWaitingForOpponent();
    if (localPreparationOptions != null)
        foreach (PreparationOption option in localPreparationOptions) if (option.Card != null) Destroy(option.Card);
    localPreparationOptions = options;
}

}