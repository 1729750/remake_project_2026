using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;


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


    [Header("Flow")]

    [InspectorName("게임 씬 이름")]
    [SerializeField]
    private string gameplaySceneName =
        "MultiPlayMode";
    [InspectorName("셀렉 & 강화 사용")]
    [SerializeField]
    private bool useSelectAndEnhance = true;

    
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
    // Flow Toggle
    // =========================================================

    /// <summary>
    /// UI Toggle의 OnValueChanged(bool)에 연결.
    ///
    /// true:
    /// 카드 선택 / 강화 준비 과정 사용.
    ///
    /// false:
    /// 준비 과정을 건너뛰고 바로 게임 Scene 이동.
    /// </summary>
    public void SetUseSelectAndEnhance(
        bool enabled)
    {
        useSelectAndEnhance =
            enabled;


        Debug.Log(
            "[MatchServerController] " +
            $"셀렉 & 강화 사용: {enabled}"
        );
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


        // =====================================================
        // 셀렉 & 강화 OFF
        //
        // 현재는 준비 과정 전부 건너뛰고
        // 바로 게임 Scene으로 이동.
        //
        // 임시 개발용 우회이므로
        // MatchPhase 검증보다 먼저 처리한다.
        // =====================================================

        if (!useSelectAndEnhance)
        {
            Debug.Log(
                "[MatchServerController] " +
                "셀렉 & 강화 OFF | " +
                "Preparation 전체 Skip"
            );


            return TryLoadGameplayScene(
                out rejectReason
            );
        }


        // =====================================================
        // 셀렉 & 강화 ON
        // 기존 준비 흐름 사용
        // =====================================================

        if (matchState.CurrentPhase !=
            MatchPhase.WaitingForPlayers)
        {
            rejectReason =
                "이미 게임이 진행 중입니다.";

            return false;
        }


        matchState.ServerBeginCardSelection();


        Debug.Log(
            "[MatchServerController] " +
            "셀렉 & 강화 ON | " +
            "카드 선택 단계 시작"
        );


        rejectReason =
            string.Empty;

        return true;
    }


    // =========================================================
    // Gameplay Scene
    // =========================================================

    private bool TryLoadGameplayScene(
        out string rejectReason)
    {
        NetworkManager networkManager =
            NetworkManager.Singleton;


        if (networkManager == null)
        {
            rejectReason =
                "NetworkManager가 없습니다.";

            return false;
        }


        if (!networkManager.IsServer)
        {
            rejectReason =
                "Server만 게임 Scene을 " +
                "변경할 수 있습니다.";

            return false;
        }


        if (networkManager.SceneManager == null)
        {
            rejectReason =
                "NetworkSceneManager가 없습니다.";

            return false;
        }


        if (string.IsNullOrWhiteSpace(
                gameplaySceneName))
        {
            rejectReason =
                "게임 Scene 이름이 비어 있습니다.";

            return false;
        }


        Debug.Log(
            "[MatchServerController] " +
            "게임 Scene 이동 요청 | " +
            $"Scene: {gameplaySceneName} | " +
            $"ConnectedClients: " +
            $"{networkManager.ConnectedClientsIds.Count}"
        );


        SceneEventProgressStatus status =
            networkManager.SceneManager.LoadScene(
                gameplaySceneName,
                LoadSceneMode.Single
            );


        if (status !=
            SceneEventProgressStatus.Started)
        {
            rejectReason =
                "게임 Scene 이동 실패 | " +
                $"Status: {status}";


            Debug.LogError(
                "[MatchServerController] " +
                rejectReason
            );


            return false;
        }


        Debug.Log(
            "[MatchServerController] " +
            "게임 Scene 이동 시작 성공 | " +
            $"Scene: {gameplaySceneName}"
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