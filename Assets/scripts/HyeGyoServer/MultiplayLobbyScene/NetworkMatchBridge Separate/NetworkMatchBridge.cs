using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Lobby 카드 준비 UI 요청을 서버 서비스로 전달하고,
/// 각 플레이어에게 필요한 후보/결과만 targeted RPC로 돌려준다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkMatchState))]
public sealed class NetworkMatchBridge : NetworkBehaviour
{
    [SerializeField]
    private NetworkModeGate networkModeGate;

    [SerializeField]
    private MatchServerController serverController;

    [SerializeField]
    private NetworkMatchState matchState;

    [SerializeField]
    private MultiPreparationManager preparationManager;

    [SerializeField]
    private CardOptionGenerator cardOptionGenerator;

    [SerializeField]
    private CardSelectionServerService cardSelectionService;

    [SerializeField]
    private EnhanceCandidateServerService enhanceCandidateService;

    public event Action<string> LocalMessage;
    public event Action<EnhanceOptionNetData[]>
        EnhanceCandidatesReceived;

    public event Action<CardDefinition[]>
        InitialCardCandidatesReceived;

    public event Action<CardDefinition>
        InitialCardConfirmedReceived;

    public event Action<PreparationOption[]>
        PreparationOptionsReceived;

    public event Action<PreparationOption, int, PreparationOption[]>
        PreparationResultReceived;

    public event Action<int, PreparationOption[]>
        PreparationSkippedReceived;

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
            : NetworkMatchState.NoClientId;

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

    private void Awake()
    {
        matchState ??= GetComponent<NetworkMatchState>();
        serverController ??= GetComponent<MatchServerController>();
        preparationManager ??= GetComponent<MultiPreparationManager>();
        cardOptionGenerator ??= GetComponent<CardOptionGenerator>();
        cardSelectionService ??= GetComponent<CardSelectionServerService>();
        enhanceCandidateService ??=
            GetComponent<EnhanceCandidateServerService>();

        if (networkModeGate == null ||
            !networkModeGate.NetworkEnabled)
        {
            foreach (NetworkModeGate gate in
                     FindObjectsByType<NetworkModeGate>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (gate != null && gate.NetworkEnabled)
                {
                    networkModeGate = gate;
                    break;
                }
            }
        }

        MultiplayerPreparationUI ui =
            GetComponent<MultiplayerPreparationUI>();

        if (ui == null)
        {
            ui = gameObject.AddComponent<MultiplayerPreparationUI>();
        }

        ui.Initialize(this, cardOptionGenerator);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer || cardSelectionService == null)
            return;

        cardSelectionService.InitialCandidatesReady +=
            HandleInitialCandidatesReady;

        cardSelectionService.InitialCardConfirmed +=
            HandleInitialCardConfirmed;

        cardSelectionService.AllInitialCardsSelected +=
            HandleAllInitialCardsSelected;
    }

    public override void OnNetworkDespawn()
    {
        if (cardSelectionService == null)
            return;

        cardSelectionService.InitialCandidatesReady -=
            HandleInitialCandidatesReady;

        cardSelectionService.InitialCardConfirmed -=
            HandleInitialCardConfirmed;

        cardSelectionService.AllInitialCardsSelected -=
            HandleAllInitialCardsSelected;
    }

    public void RequestStartMatch()
    {
        if (CanSendNetworkRequest())
            RequestStartMatchRpc();
    }

    [Rpc(SendTo.Server)]
    private void RequestStartMatchRpc(
        RpcParams rpcParams = default)
    {
        ulong sender =
            rpcParams.Receive.SenderClientId;

        if (serverController == null)
        {
            SendRejectMessage(sender, "게임 시작 서비스가 없습니다.");
            return;
        }

        if (!serverController.TryStartMatch(
                sender,
                out string rejectReason))
        {
            SendRejectMessage(sender, rejectReason);
        }
    }

    // =========================================================
    // Initial card
    // =========================================================

    public void RequestChooseCard(
        int candidateIndex)
    {
        if (CanSendNetworkRequest())
            RequestChooseCardRpc(candidateIndex);
    }

    [Rpc(SendTo.Server)]
    private void RequestChooseCardRpc(
        int candidateIndex,
        RpcParams rpcParams = default)
    {
        ulong sender =
            rpcParams.Receive.SenderClientId;

        if (cardSelectionService == null)
        {
            SendRejectMessage(sender, "초기 카드 선택 서비스가 없습니다.");
            return;
        }

        if (!cardSelectionService.TryChooseCard(
                sender,
                candidateIndex,
                out string rejectReason))
        {
            SendRejectMessage(sender, rejectReason);
        }
    }

    private void HandleInitialCandidatesReady(
        ulong targetClientId,
        int card0,
        int card1,
        int card2)
    {
        SendInitialCardCandidatesRpc(
            card0,
            card1,
            card2,
            RpcTarget.Single(
                targetClientId,
                RpcTargetUse.Temp
            )
        );
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SendInitialCardCandidatesRpc(
        int card0,
        int card1,
        int card2,
        RpcParams rpcParams = default)
    {
        CardDefinition[] cards =
        {
            cardOptionGenerator?.GetCardByPoolIndex(card0),
            cardOptionGenerator?.GetCardByPoolIndex(card1),
            cardOptionGenerator?.GetCardByPoolIndex(card2)
        };

        InitialCardCandidatesReceived?.Invoke(cards);
    }

    private void HandleInitialCardConfirmed(
        ulong targetClientId,
        int cardPoolIndex)
    {
        SendInitialCardConfirmedRpc(
            cardPoolIndex,
            RpcTarget.Single(
                targetClientId,
                RpcTargetUse.Temp
            )
        );
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SendInitialCardConfirmedRpc(
        int cardPoolIndex,
        RpcParams rpcParams = default)
    {
        InitialCardConfirmedReceived?.Invoke(
            cardOptionGenerator?.GetCardByPoolIndex(
                cardPoolIndex
            )
        );
    }

    private void HandleAllInitialCardsSelected()
    {
        if (!IsServer ||
            preparationManager == null ||
            cardSelectionService == null)
        {
            return;
        }

        foreach (PlayerPreparationData player
                 in cardSelectionService.Players)
        {
            if (!preparationManager.TryBeginForPlayer(
                    player.ClientId,
                    out PreparationOption[] options,
                    out string rejectReason))
            {
                SendRejectMessage(
                    player.ClientId,
                    rejectReason
                );

                continue;
            }

            SendPreparationOptions(
                player.ClientId,
                options
            );
        }
    }

    // =========================================================
    // Ten preparation rounds
    // =========================================================

    public void RequestBeginPreparation()
    {
        if (CanSendNetworkRequest())
            RequestBeginPreparationRpc();
    }

    [Rpc(SendTo.Server)]
    private void RequestBeginPreparationRpc(
        RpcParams rpcParams = default)
    {
        ulong sender =
            rpcParams.Receive.SenderClientId;

        if (preparationManager == null)
        {
            SendRejectMessage(sender, "준비 서비스가 없습니다.");
            return;
        }

        if (!preparationManager.TryBeginForPlayer(
                sender,
                out PreparationOption[] options,
                out string rejectReason))
        {
            SendRejectMessage(sender, rejectReason);
            return;
        }

        SendPreparationOptions(sender, options);
    }

    public void RequestConfirmPreparationOption(
        int optionIndex)
    {
        if (CanSendNetworkRequest())
            RequestConfirmPreparationOptionRpc(optionIndex);
    }

    [Rpc(SendTo.Server)]
    private void RequestConfirmPreparationOptionRpc(
        int optionIndex,
        RpcParams rpcParams = default)
    {
        ulong sender =
            rpcParams.Receive.SenderClientId;

        if (preparationManager == null)
        {
            SendRejectMessage(sender, "준비 선택 서비스가 없습니다.");
            return;
        }

        if (!preparationManager.TryConfirmOption(
                sender,
                optionIndex,
                out PreparationOption selected,
                out PreparationOption[] next,
                out int remaining,
                out string rejectReason))
        {
            SendRejectMessage(sender, rejectReason);
            return;
        }

        SendPreparationResult(
            sender,
            selected,
            remaining,
            next
        );
    }

    public void RequestSkipPreparation()
    {
        if (CanSendNetworkRequest())
            RequestSkipPreparationRpc();
    }

    [Rpc(SendTo.Server)]
    private void RequestSkipPreparationRpc(
        RpcParams rpcParams = default)
    {
        ulong sender =
            rpcParams.Receive.SenderClientId;

        if (preparationManager == null)
        {
            SendRejectMessage(sender, "준비 Skip 서비스가 없습니다.");
            return;
        }

        if (!preparationManager.TrySkip(
                sender,
                out PreparationOption[] next,
                out int remaining,
                out string rejectReason))
        {
            SendRejectMessage(sender, rejectReason);
            return;
        }

        SendPreparationSkippedRpc(
            remaining,
            next != null,
            ToNetData(next, 0),
            ToNetData(next, 1),
            ToNetData(next, 2),
            RpcTarget.Single(sender, RpcTargetUse.Temp)
        );
    }

    private void SendPreparationOptions(
        ulong targetClientId,
        PreparationOption[] options)
    {
        if (options == null || options.Length != 3)
        {
            SendRejectMessage(
                targetClientId,
                "준비 선택지가 3개가 아닙니다."
            );

            return;
        }

        SendPreparationOptionsRpc(
            cardOptionGenerator.ToNetData(options[0]),
            cardOptionGenerator.ToNetData(options[1]),
            cardOptionGenerator.ToNetData(options[2]),
            RpcTarget.Single(
                targetClientId,
                RpcTargetUse.Temp
            )
        );
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SendPreparationOptionsRpc(
        PreparationOptionNetData option0,
        PreparationOptionNetData option1,
        PreparationOptionNetData option2,
        RpcParams rpcParams = default)
    {
        PreparationOptionsReceived?.Invoke(
            new[]
            {
                cardOptionGenerator.FromNetData(option0),
                cardOptionGenerator.FromNetData(option1),
                cardOptionGenerator.FromNetData(option2)
            }
        );
    }

    private void SendPreparationResult(
        ulong targetClientId,
        PreparationOption selected,
        int remaining,
        PreparationOption[] next)
    {
        SendPreparationResultRpc(
            cardOptionGenerator.ToNetData(selected),
            remaining,
            next != null,
            ToNetData(next, 0),
            ToNetData(next, 1),
            ToNetData(next, 2),
            RpcTarget.Single(
                targetClientId,
                RpcTargetUse.Temp
            )
        );
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SendPreparationResultRpc(
        PreparationOptionNetData selected,
        int remaining,
        bool hasNext,
        PreparationOptionNetData next0,
        PreparationOptionNetData next1,
        PreparationOptionNetData next2,
        RpcParams rpcParams = default)
    {
        PreparationResultReceived?.Invoke(
            cardOptionGenerator.FromNetData(selected),
            remaining,
            hasNext
                ? new[]
                {
                    cardOptionGenerator.FromNetData(next0),
                    cardOptionGenerator.FromNetData(next1),
                    cardOptionGenerator.FromNetData(next2)
                }
                : null
        );
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SendPreparationSkippedRpc(
        int remaining,
        bool hasNext,
        PreparationOptionNetData next0,
        PreparationOptionNetData next1,
        PreparationOptionNetData next2,
        RpcParams rpcParams = default)
    {
        PreparationSkippedReceived?.Invoke(
            remaining,
            hasNext
                ? new[]
                {
                    cardOptionGenerator.FromNetData(next0),
                    cardOptionGenerator.FromNetData(next1),
                    cardOptionGenerator.FromNetData(next2)
                }
                : null
        );
    }

    private PreparationOptionNetData ToNetData(
        PreparationOption[] options,
        int index)
    {
        return options != null &&
               index >= 0 &&
               index < options.Length
            ? cardOptionGenerator.ToNetData(options[index])
            : default;
    }

    // =========================================================
    // Legacy enhance API (kept for existing scene buttons)
    // =========================================================

    public void RequestEnhanceCandidates()
    {
        if (CanSendNetworkRequest())
            RequestEnhanceCandidatesRpc();
    }

    [Rpc(SendTo.Server)]
    private void RequestEnhanceCandidatesRpc(
        RpcParams rpcParams = default)
    {
        ulong sender =
            rpcParams.Receive.SenderClientId;

        if (serverController == null)
        {
            SendRejectMessage(sender, "강화 후보 서비스가 없습니다.");
            return;
        }

        if (!serverController.TryCreateEnhanceCandidates(
                sender,
                out EnhanceOptionNetData[] options,
                out string rejectReason) ||
            options == null || options.Length != 3)
        {
            SendRejectMessage(sender, rejectReason);
            return;
        }

        SendEnhanceCandidatesRpc(
            options[0], options[1], options[2],
            RpcTarget.Single(sender, RpcTargetUse.Temp)
        );
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SendEnhanceCandidatesRpc(
        EnhanceOptionNetData option0,
        EnhanceOptionNetData option1,
        EnhanceOptionNetData option2,
        RpcParams rpcParams = default)
    {
        EnhanceCandidatesReceived?.Invoke(
            new[] { option0, option1, option2 }
        );
    }

    public void RequestConfirmEnhanceCandidate(
        int selectedIndex)
    {
        if (CanSendNetworkRequest())
            RequestConfirmEnhanceCandidateRpc(selectedIndex);
    }

    [Rpc(SendTo.Server)]
    private void RequestConfirmEnhanceCandidateRpc(
        int selectedIndex,
        RpcParams rpcParams = default)
    {
        ulong sender =
            rpcParams.Receive.SenderClientId;

        if (serverController == null)
        {
            SendRejectMessage(sender, "강화 확정 서비스가 없습니다.");
            return;
        }

        if (!serverController.TryConfirmEnhanceCandidate(
                sender,
                selectedIndex,
                out string rejectReason))
        {
            SendRejectMessage(sender, rejectReason);
        }
    }

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

    private bool CanSendNetworkRequest()
    {
        if (networkModeGate == null ||
            !networkModeGate.NetworkEnabled ||
            !IsSpawned)
        {
            Debug.LogWarning(
                "[NetworkMatchBridge] " +
                "네트워크 요청을 보낼 준비가 되지 않았습니다."
            );

            return false;
        }

        return true;
    }

    private void SendRejectMessage(
        ulong targetClientId,
        string message)
    {
        if (!IsServer)
            return;

        RejectRequestRpc(
            new FixedString128Bytes(
                string.IsNullOrWhiteSpace(message)
                    ? "요청이 거절되었습니다."
                    : message
            ),
            RpcTarget.Single(
                targetClientId,
                RpcTargetUse.Temp
            )
        );
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void RejectRequestRpc(
        FixedString128Bytes message,
        RpcParams rpcParams = default)
    {
        Debug.LogWarning(
            $"[NetworkMatchBridge] Server 요청 거절 | {message}"
        );

        LocalMessage?.Invoke(message.ToString());
    }
}
