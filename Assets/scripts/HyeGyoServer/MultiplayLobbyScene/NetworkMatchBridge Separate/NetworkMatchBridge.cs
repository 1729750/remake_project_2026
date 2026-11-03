using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Client/UI 요청을 Server로 전달하는 중계 전용 클래스.
/// 상태 저장과 게임 판정은 다른 클래스가 담당한다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
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
        if (matchState == null) matchState = GetComponent<NetworkMatchState>();
        if (serverController == null) serverController = GetComponent<MatchServerController>();
    }

    public event Action PreparationAvailabilityChanged;
    public event Action PreparationViewChanged;
    public bool PreparationReady => IsSpawned && IsMultiGameMode && matchState != null && matchState.PreparationReady;
    private void HandlePreparationAvailability() => PreparationAvailabilityChanged?.Invoke();
    private void HandleNetworkEnabled(bool enabled) => HandlePreparationAvailability();

    public override void OnNetworkSpawn()
    {
        if (networkModeGate != null) networkModeGate.NetworkEnabledChanged -= HandleNetworkEnabled;
        networkModeGate = NetworkManager.GetComponent<NetworkModeGate>();
        if (networkModeGate != null) networkModeGate.NetworkEnabledChanged += HandleNetworkEnabled;
        if (matchState != null)
        {
            matchState.MatchStateChanged += HandlePreparationAvailability;
            matchState.LifecycleChanged += HandlePreparationAvailability;
        }
        HandlePreparationAvailability();
    }

    public override void OnNetworkDespawn()
    {
        if (networkModeGate != null) networkModeGate.NetworkEnabledChanged -= HandleNetworkEnabled;
        if (matchState != null)
        {
            matchState.MatchStateChanged -= HandlePreparationAvailability;
            matchState.LifecycleChanged -= HandlePreparationAvailability;
        }
        MapManager_Multi.Instance?.ClearPreparationVisuals();
        CardOptionGenerator.DisposeOptions(localPreparationOptions);
        localPreparationOptions = null;
        foreach (CardDefinition card in localConfirmedDeck) if (card != null) Destroy(card);
        localConfirmedDeck = Array.Empty<CardDefinition>();
        localPreparationRound = localRevision = localEpoch = 0;
        nextRequestId = pendingRequestId = 0;
        automaticBeginSent = false;
        PreparationRequestPending = false;
        hasLocalView = false;
        HandlePreparationAvailability();
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

    private int localPreparationRound, localRevision, localEpoch;
    private long nextRequestId, pendingRequestId;
    private bool automaticBeginSent, hasLocalView, localOptionsRetry, recoveryAttempted;
    private PreparationOption[] localPreparationOptions;
    private CardDefinition[] localConfirmedDeck = Array.Empty<CardDefinition>();
    public bool PreparationRequestPending { get; private set; }

    public void RequestBeginPreparation() => TryBeginLocalPreparation();

    public bool TryBeginLocalPreparation()
    {
        if (automaticBeginSent || PreparationRequestPending || !PreparationReady || !IsClient) return false;
        automaticBeginSent = true;
        return DispatchStateRequest();
    }

    public bool RequestPreparationResync()
    {
        if (!PreparationReady || PreparationRequestPending || !IsClient) return false;
        return DispatchStateRequest();
    }

    private bool DispatchStateRequest()
    {
        PreparationRequestPending = true;
        pendingRequestId = ++nextRequestId;
        RequestBeginPreparationRpc(pendingRequestId);
        return true;
    }

    [Rpc(SendTo.Server)]
    private void RequestBeginPreparationRpc(long requestId, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        bool valid = preparationManager.TryBeginForPlayer(sender, out PreparationOption[] options, out string reason);
        SendPreparationState(sender, options, requestId, false, valid ? string.Empty : reason);
    }

    public bool RequestConfirmPreparationOption(int optionIndex)
    {
        if (PreparationRequestPending || !PreparationReady || !hasLocalView || localOptionsRetry ||
            localPreparationRound >= 10 || !IsClient) return false;
        recoveryAttempted = false;
        PreparationRequestPending = true;
        pendingRequestId = ++nextRequestId;
        RequestConfirmPreparationOptionRpc(optionIndex, localPreparationRound, localRevision, localEpoch, pendingRequestId);
        return true;
    }

    public void RequestSkipPreparation() => RequestConfirmPreparationOption(-1);

    [Rpc(SendTo.Server)]
    private void RequestConfirmPreparationOptionRpc(int optionIndex, int round, int revision, int epoch,
        long requestId, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        bool accepted = preparationManager.TryConfirmOption(sender, optionIndex, round, revision, epoch, requestId,
            out PreparationOption[] options, out string reason);
        if (!accepted) preparationManager.TryBeginForPlayer(sender, out options, out _);
        SendPreparationState(sender, options, requestId, accepted, reason);
        if (accepted) preparationManager.CheckAllPlayersCompleted();
    }

    [Serializable]
    private class PreparationViewState
    {
        public long requestId;
        public int epoch, revision, round, remaining;
        public bool accepted, optionsRetry;
        public string error;
        public string[] cards;
        public PreparationOptionNetData[] metadata;
        public string[] candidateCards, afterCards;
    }

    private void SendPreparationState(ulong owner, PreparationOption[] options, long requestId, bool accepted, string error)
    {
        if (!IsServer || !NetworkManager.ConnectedClients.ContainsKey(owner)) return;
        var state = new PreparationViewState
        {
            requestId = requestId, accepted = accepted, error = error,
            epoch = preparationManager.Epoch, revision = preparationManager.GetRevision(owner),
            remaining = preparationManager.GetRemainingChoices(owner),
            cards = Array.Empty<string>(), metadata = Array.Empty<PreparationOptionNetData>(),
            candidateCards = Array.Empty<string>(), afterCards = Array.Empty<string>()
        };
        state.round = 10 - state.remaining;
        try
        {
            CardDefinition[] deck = preparationManager.GetConfirmedDeck(owner);
            int[] sources = preparationManager.GetConfirmedSources(owner);
            if (deck.Length != sources.Length) throw new InvalidOperationException("Invalid owner source metadata.");
            state.cards = new string[deck.Length];
            for (int i = 0; i < deck.Length; i++) state.cards[i] = cardOptionGenerator.SerializeCard(deck[i], sources[i]);
            int count = options == null ? 0 : options.Length;
            state.metadata = new PreparationOptionNetData[count];
            state.candidateCards = new string[count];
            state.afterCards = new string[count];
            for (int i = 0; i < count; i++)
            {
                state.metadata[i] = cardOptionGenerator.ToNetData(options[i]);
                state.candidateCards[i] = cardOptionGenerator.SerializeCard(options[i].Card, options[i].CardPoolIndex);
                state.afterCards[i] = options[i].AfterPreview == null ? string.Empty :
                    cardOptionGenerator.SerializeCard(options[i].AfterPreview, options[i].CardPoolIndex);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            state.error = "Owner snapshot is unavailable; request state again.";
            state.cards = null; // Typed failure: never replace a confirmed view with an empty deck.
            state.metadata = Array.Empty<PreparationOptionNetData>();
            state.candidateCards = state.afterCards = Array.Empty<string>();
        }
        state.optionsRetry = state.remaining > 0 && state.metadata.Length == 0;
        SendPreparationStateClientRpc(JsonUtility.ToJson(state),
            new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { owner } } });
    }

    private static CardUpgrade DecodeUpgrade(PreparationOptionNetData data)
    {
        if (!Enum.IsDefined(typeof(EffectType), (EffectType)data.EffectType) ||
            !Enum.IsDefined(typeof(EffectTarget), (EffectTarget)data.Target))
            throw new InvalidOperationException("Invalid offered effect.");
        var effect = new CardEffect(Effect.Create((EffectType)data.EffectType, data.Magnitude), (EffectTarget)data.Target);
        EffectCategory category = (EffectCategory)data.Category;
        if ((category != EffectCategory.Instant && category != EffectCategory.Continuous) ||
            (category & ~effect.GetSupportedCategories()) != 0) throw new InvalidOperationException("Invalid offered category.");
        effect.SetAppliedCategory(category);
        return new CardUpgrade(effect, data.CostDelta, data.CooldownDelta);
    }

    [ClientRpc]
    private void SendPreparationStateClientRpc(string payload, ClientRpcParams clientRpcParams = default)
    {
        PreparationViewState state = null;
        CardDefinition[] deck = null;
        PreparationOption[] options = null;
        try
        {
            if (payload == null || payload.Length > 262144) throw new InvalidOperationException("Invalid snapshot size.");
            state = JsonUtility.FromJson<PreparationViewState>(payload);
            if (state == null || state.requestId <= 0 || state.requestId > nextRequestId ||
                state.epoch < localEpoch || (state.epoch == localEpoch && state.revision < localRevision)) return;
            if (state.cards == null) throw new InvalidOperationException(state.error);
            if (state.cards.Length > 10 || state.metadata == null || state.candidateCards == null || state.afterCards == null ||
                (state.metadata.Length != 0 && state.metadata.Length != 3) ||
                state.candidateCards.Length != state.metadata.Length || state.afterCards.Length != state.metadata.Length ||
                state.round < 0 || state.round > 10 || state.remaining != 10 - state.round || state.cards.Length > state.round ||
                (state.remaining == 0 && state.metadata.Length != 0) ||
                state.optionsRetry != (state.remaining > 0 && state.metadata.Length == 0))
                throw new InvalidOperationException("Invalid preparation snapshot.");
            deck = new CardDefinition[state.cards.Length];
            options = new PreparationOption[state.metadata.Length];
            for (int i = 0; i < deck.Length; i++) deck[i] = cardOptionGenerator.DeserializeCard(state.cards[i]);
            for (int i = 0; i < options.Length; i++)
            {
                PreparationOptionNetData data = state.metadata[i];
                bool enhance = data.Type == (byte)PreparationOptionType.EnhanceCard;
                if (data.OfferedSlot != i || (data.Type != (byte)PreparationOptionType.AddCard && !enhance) ||
                    cardOptionGenerator.GetCardByPoolIndex(data.CardPoolIndex) == null ||
                    (i == 0 && (enhance || data.NoEnhanceTarget || data.RandomEnhance)) ||
                    (i == 1 && (data.RandomEnhance || (!enhance && !data.NoEnhanceTarget))) ||
                    (i == 2 && enhance != (data.RandomEnhance && !data.NoEnhanceTarget)) ||
                    (data.NoEnhanceTarget && (enhance || i == 0 || (i == 2 && !data.RandomEnhance))) ||
                    (enhance && (data.TargetCardIndex < 0 || data.TargetCardIndex >= deck.Length || data.EnhanceId < 0)) ||
                    (!enhance && (data.TargetCardIndex != -1 || data.EnhanceId != -1)))
                    throw new InvalidOperationException("Invalid candidate metadata.");
                options[i] = new PreparationOption
                {
                    Type = (PreparationOptionType)data.Type, OfferedSlot = data.OfferedSlot,
                    RandomEnhance = data.RandomEnhance, NoEnhanceTarget = data.NoEnhanceTarget,
                    CardPoolIndex = data.CardPoolIndex, TargetCardIndex = data.TargetCardIndex, EnhanceId = data.EnhanceId,
                    Card = cardOptionGenerator.DeserializeCard(state.candidateCards[i])
                };
                if (enhance)
                {
                    options[i].Upgrade = DecodeUpgrade(data);
                    options[i].AfterPreview = cardOptionGenerator.DeserializeCard(state.afterCards[i]);
                    if (!RewardManager.CanEnhance(options[i].Card, options[i].Upgrade) ||
                        state.candidateCards[i] != cardOptionGenerator.SerializeCard(deck[data.TargetCardIndex], data.CardPoolIndex))
                        throw new InvalidOperationException("Invalid enhancement target preview.");
                    CardDefinition expected = CardOptionGenerator.CopyCard(options[i].Card);
                    try
                    {
                        expected.ApplyUpgrade(CardOptionGenerator.CopyUpgrade(options[i].Upgrade));
                        if (cardOptionGenerator.SerializeCard(expected, data.CardPoolIndex) != state.afterCards[i])
                            throw new InvalidOperationException("Invalid enhancement after preview.");
                    }
                    finally { Destroy(expected); }
                }
                else if (!string.IsNullOrEmpty(state.afterCards[i]) || data.EffectType != -1)
                    throw new InvalidOperationException("Unexpected Add upgrade preview.");
            }
            MapManager_Multi.Instance?.ClearPreparationVisuals();
            CardOptionGenerator.DisposeOptions(localPreparationOptions);
            foreach (CardDefinition card in localConfirmedDeck) if (card != null) Destroy(card);
            localConfirmedDeck = deck;
            localPreparationOptions = options;
            deck = null;
            options = null;
            localPreparationRound = state.round;
            localRevision = state.revision;
            localEpoch = state.epoch;
            localOptionsRetry = state.optionsRetry;
            hasLocalView = true;
            if (state.requestId == pendingRequestId) PreparationRequestPending = false;
            if (!string.IsNullOrEmpty(state.error)) LocalMessage?.Invoke(state.error);
            PreparationViewChanged?.Invoke();
            if (state.optionsRetry && !recoveryAttempted && PreparationReady)
            {
                recoveryAttempted = true;
                RequestPreparationResync();
            }
        }
        catch (Exception exception)
        {
            if (deck != null) foreach (CardDefinition card in deck) if (card != null) Destroy(card);
            CardOptionGenerator.DisposeOptions(options);
            // Only a correlated failure may release the matching transaction.
            if (state != null && state.requestId == pendingRequestId)
            {
                PreparationRequestPending = false;
                LocalMessage?.Invoke(exception.Message);
                PreparationViewChanged?.Invoke();
                if (!recoveryAttempted && PreparationReady)
                {
                    recoveryAttempted = true;
                    RequestPreparationResync();
                }
            }
            Debug.LogException(exception, this);
        }
    }

    public bool RenderCachedPreparation(MapManager_Multi map)
    {
        if (!hasLocalView) return false;
        map.ApplyConfirmedDeckSnapshot(localConfirmedDeck);
        map.SetRemainingChoices(10 - localPreparationRound);
        if (localPreparationOptions != null && localPreparationOptions.Length == 3 && PreparationReady)
            map.ShowOptions(localPreparationOptions);
        else map.ShowWaitingForOpponent();
        return true;
    }

}