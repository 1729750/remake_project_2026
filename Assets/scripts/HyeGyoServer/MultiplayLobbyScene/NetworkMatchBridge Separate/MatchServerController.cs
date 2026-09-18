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
    Debug.Log(
        "[MatchServerController] " +
        "TryStartMatch 진입 | " +
        $"SenderClientId: {senderClientId}"
    );


    // =====================================================
    // Server 상태 확인
    // =====================================================

    if (!ValidateServerState(
            out rejectReason))
    {
        Debug.LogWarning(
            "[MatchServerController] " +
            $"ValidateServerState 실패 | {rejectReason}"
        );

        return false;
    }


    // =====================================================
    // Host만 시작 가능
    // =====================================================

    if (senderClientId !=
        NetworkManager.ServerClientId)
    {
        rejectReason =
            "Host만 게임을 시작할 수 있습니다.";

        Debug.LogWarning(
            "[MatchServerController] " +
            rejectReason
        );

        return false;
    }


    // =====================================================
    // 실제 NGO 접속 인원 검사
    // =====================================================

    if (NetworkManager.Singleton == null)
    {
        rejectReason =
            "NetworkManager가 없습니다.";

        Debug.LogWarning(
            "[MatchServerController] " +
            rejectReason
        );

        return false;
    }


    int connectedPlayerCount =
        NetworkManager.Singleton
            .ConnectedClientsList.Count;


    Debug.Log(
        "[MatchServerController] " +
        $"실제 NGO 접속 인원: " +
        $"{connectedPlayerCount}/2"
    );


    if (connectedPlayerCount < 2)
    {
        rejectReason =
            "실제 NGO 플레이어 2명이 모두 접속해야 합니다.";

        Debug.LogWarning(
            "[MatchServerController] " +
            rejectReason
        );

        return false;
    }


    // =====================================================
    // 셀렉 & 강화 OFF
    // 바로 게임 Scene 이동
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
    // CardSelectionServerService에게 시작 요청
    // =====================================================

    if (cardSelectionService == null)
    {
        rejectReason =
            "CardSelectionServerService가 없습니다.";

        Debug.LogWarning(
            "[MatchServerController] " +
            rejectReason
        );

        return false;
    }


    bool started =
        cardSelectionService.TryStartCardSelection(
            out rejectReason
        );


    if (!started)
    {
        Debug.LogWarning(
            "[MatchServerController] " +
            $"카드 선택 시작 실패 | {rejectReason}"
        );

        return false;
    }


    Debug.Log(
        "[MatchServerController] " +
        "셀렉 & 강화 ON | " +
        "CardSelectionServerService 시작 요청 완료"
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