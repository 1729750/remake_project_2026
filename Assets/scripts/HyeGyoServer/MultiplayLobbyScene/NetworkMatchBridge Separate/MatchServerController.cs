using Unity.Netcode;
using UnityEngine;

public sealed class MatchServerController
    : MonoBehaviour
{
    [Header("Lobby")]
    [SerializeField]
    private HostGameManager hostGameManager;

    [Header("Shared State")]
    [SerializeField]
    private NetworkMatchState matchState;

    [Header("Preparation")]
    [SerializeField]
    private PlayerPreparationRegistry
        preparationRegistry;

    [Header("Services")]
    [SerializeField]
    private CardSelectionServerService
        cardSelectionService;

    [SerializeField]
    private EnhanceCandidateServerService
        enhanceCandidateService;


    private void Awake()
    {
        if (matchState == null)
        {
            matchState =
                GetComponent<NetworkMatchState>();
        }

        if (preparationRegistry == null)
        {
            preparationRegistry =
                GetComponent<
                    PlayerPreparationRegistry>();
        }

        if (cardSelectionService == null)
        {
            cardSelectionService =
                GetComponent<
                    CardSelectionServerService>();
        }

        if (enhanceCandidateService == null)
        {
            enhanceCandidateService =
                GetComponent<
                    EnhanceCandidateServerService>();
        }
    }


    // =========================================================
    // Match Start
    // =========================================================

    public bool TryStartMatch(
        ulong senderClientId,
        out string rejectReason)
    {
        if (!ValidateServerState(
                out rejectReason))
        {
            return false;
        }


        if (senderClientId !=
            NetworkManager.ServerClientId)
        {
            rejectReason =
                "Host만 게임을 시작할 수 있습니다.";

            return false;
        }


        if (hostGameManager == null)
        {
            rejectReason =
                "HostGameManager가 없습니다.";

            return false;
        }


        if (!hostGameManager.IsRoomReady)
        {
            rejectReason =
                "플레이어 2명이 모두 접속해야 합니다.";

            return false;
        }


        if (matchState.CurrentPhase !=
            MatchPhase.WaitingForPlayers)
        {
            rejectReason =
                "이미 게임이 진행 중입니다.";

            return false;
        }


        matchState.ServerBeginCardSelection(
            NetworkManager.ServerClientId
        );


        Debug.Log(
            "[MatchServerController] " +
            "카드 선택 단계 시작"
        );


        rejectReason =
            string.Empty;

        return true;
    }


    // =========================================================
    // Card
    // =========================================================

    public bool TryChooseCard(
        ulong senderClientId,
        int cardId,
        out string rejectReason)
    {
        if (cardSelectionService == null)
        {
            rejectReason =
                "CardSelectionServerService가 없습니다.";

            return false;
        }

        return cardSelectionService.TryChooseCard(
            senderClientId,
            cardId,
            out rejectReason
        );
    }


    // =========================================================
    // Enhance
    // =========================================================

    public bool TryCreateEnhanceCandidates(
        ulong senderClientId,
        out EnhanceOptionNetData[] networkOptions,
        out string rejectReason)
    {
        networkOptions = null;

        if (enhanceCandidateService == null)
        {
            rejectReason =
                "EnhanceCandidateServerService가 없습니다.";

            return false;
        }

        return enhanceCandidateService
            .TryCreateCandidates(
                senderClientId,
                out networkOptions,
                out rejectReason
            );
    }


    public bool TryConfirmEnhanceCandidate(
        ulong senderClientId,
        int selectedIndex,
        out string rejectReason)
    {
        if (enhanceCandidateService == null)
        {
            rejectReason =
                "EnhanceCandidateServerService가 없습니다.";

            return false;
        }

        return enhanceCandidateService
            .TryConfirmCandidate(
                senderClientId,
                selectedIndex,
                out rejectReason
            );
    }


    // =========================================================
    // Validation
    // =========================================================

    private bool ValidateServerState(
        out string rejectReason)
    {
        if (matchState == null)
        {
            rejectReason =
                "NetworkMatchState가 없습니다.";

            return false;
        }


        if (!matchState.IsSpawned ||
            !matchState.IsServer)
        {
            rejectReason =
                "Server가 아닙니다.";

            return false;
        }


        rejectReason =
            string.Empty;

        return true;
    }
}