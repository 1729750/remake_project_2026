using UnityEngine;
using System.Collections;

public sealed class CardSelectionServerService : MonoBehaviour
{
    [Header("Preparation Data")]
    [SerializeField]
    private PlayerPreparationRegistry preparationRegistry;

    [Header("Match State")]
    [SerializeField]
    private NetworkMatchState matchState;

    private Coroutine beginCardSelectionCoroutine;
private bool cardSelectionStartRequested;

    private bool isStartingCardSelection;


    public int RegisteredPlayerCount =>
        preparationRegistry != null
            ? preparationRegistry.Count
            : 0;


    public bool HasTwoPlayers =>
        RegisteredPlayerCount == 2;


    private void Awake()
    {
        EnsureReferences();
    }


    private void OnEnable()
    {
        EnsureReferences();

        if (preparationRegistry == null)
        {
            Debug.LogError(
                "[CardSelectionServerService] " +
                "PlayerPreparationRegistry가 없습니다."
            );

            return;
        }

        preparationRegistry.PlayerRegistered +=
            HandlePlayerRegistered;

        preparationRegistry.PlayerRemoved +=
            HandlePlayerRemoved;

        Debug.Log(
            "[CardSelectionServerService] " +
            "PlayerPreparationRegistry 연결 완료"
        );

        LogCurrentPlayers();
    }


private void OnDisable()
{
    if (preparationRegistry != null)
    {
        preparationRegistry.PlayerRegistered -=
            HandlePlayerRegistered;

        preparationRegistry.PlayerRemoved -=
            HandlePlayerRemoved;
    }

    if (beginCardSelectionCoroutine != null)
    {
        StopCoroutine(
            beginCardSelectionCoroutine
        );

        beginCardSelectionCoroutine = null;
    }

    cardSelectionStartRequested = false;
}


    private void EnsureReferences()
    {
        if (preparationRegistry == null)
        {
            preparationRegistry =
                FindFirstObjectByType<
                    PlayerPreparationRegistry>();
        }

        if (matchState == null)
        {
            matchState =
                FindFirstObjectByType<
                    NetworkMatchState>();
        }
    }


    // ==================================================
    // PlayerPreparationData 조회
    // ==================================================

    public bool TryGetPlayerData(
        ulong clientId,
        out PlayerPreparationData player)
    {
        player = null;

        if (preparationRegistry == null)
        {
            Debug.LogWarning(
                "[CardSelectionServerService] " +
                "PlayerPreparationRegistry가 없습니다."
            );

            return false;
        }

        if (!preparationRegistry.TryGetPlayer(
                clientId,
                out player))
        {
            Debug.LogWarning(
                "[CardSelectionServerService] " +
                $"플레이어 데이터를 찾지 못했습니다. | " +
                $"ClientId: {clientId}"
            );

            return false;
        }

        return true;
    }


    public bool TryGetOtherPlayerData(
        ulong currentClientId,
        out PlayerPreparationData otherPlayer)
    {
        otherPlayer = null;

        if (preparationRegistry == null)
        {
            return false;
        }

        foreach (
            PlayerPreparationData player
            in preparationRegistry.Players)
        {
            if (player == null)
            {
                continue;
            }

            if (player.ClientId ==
                currentClientId)
            {
                continue;
            }

            otherPlayer = player;

            return true;
        }

        return false;
    }


    // ==================================================
    // Card Selection
    // ==================================================

    /// <summary>
    /// 현재 실제 카드 선택은 Blank 상태.
    ///
    /// 이후:
    /// Server 후보 생성
    /// → CardCandidates 저장
    /// → Client 선택 요청
    /// → Server 검증
    /// 순서로 구현.
    /// </summary>
    public bool TryChooseCard(
        ulong senderClientId,
        int cardId,
        out string rejectReason)
    {
        rejectReason =
            "카드 선택 기능은 아직 연결하지 않은 상태입니다.";

        Debug.Log(
            "[CardSelectionServerService] " +
            "카드 선택 요청 수신 - 현재 Blank 처리 | " +
            $"ClientId: {senderClientId} | " +
            $"CardId: {cardId}"
        );

        return false;
    }


    // ==================================================
    // Registry Events
    // ==================================================

    private void HandlePlayerRegistered(
        ulong clientId)
    {
        if (!preparationRegistry.TryGetPlayer(
                clientId,
                out PlayerPreparationData player))
        {
            return;
        }

        Debug.Log(
            "[CardSelectionServerService] " +
            $"준비 데이터 연결 | " +
            $"ClientId: {clientId} | " +
            $"Candidates: {GetCandidateCount(player)} | " +
            $"SelectedIndex: {player.SelectedCardIndex} | " +
            $"FinalDeck: {player.FinalDeck.Count} | " +
            $"RegistryCount: {preparationRegistry.Count}"
        );


        if (preparationRegistry.Count == 2)
        {
            Debug.Log(
                "[CardSelectionServerService] " +
                "Host / Client 준비 데이터 2개 연결 완료"
            );
        }
    }


    private void HandlePlayerRemoved(
        ulong clientId)
    {
        Debug.Log(
            "[CardSelectionServerService] " +
            $"플레이어 준비 데이터 연결 해제 | " +
            $"ClientId: {clientId} | " +
            $"RegistryCount: {preparationRegistry.Count}"
        );
    }

        public bool TryStartCardSelection(out string rejectReason)
    {
        if (cardSelectionStartRequested)
        {
            rejectReason =
                "카드 선택 시작이 이미 요청되었습니다.";

            return false;
        }


        if (preparationRegistry == null)
        {
            rejectReason =
                "PlayerPreparationRegistry가 없습니다.";

            return false;
        }


        if (preparationRegistry.Count != 2)
        {
            rejectReason =
                "플레이어 2명이 준비되지 않았습니다.";

            return false;
        }


        cardSelectionStartRequested = true;


        beginCardSelectionCoroutine =
            StartCoroutine(
                BeginCardSelectionWhenReady()
            );


        Debug.Log(
            "[CardSelectionServerService] " +
            "카드 선택 시작 요청 수신"
        );


        rejectReason =
            string.Empty;

        return true;
    }


private IEnumerator BeginCardSelectionWhenReady()
{
    // NetworkMatchState의 NetworkObject가
    // Spawn될 때까지 기다린다.
    while (true)
    {
        EnsureReferences();

        if (matchState != null &&
            matchState.IsSpawned)
        {
            break;
        }

        yield return null;
    }


    // 기다리는 도중 플레이어가 빠졌다면 취소
    if (preparationRegistry == null ||
        preparationRegistry.Count != 2)
    {
        cardSelectionStartRequested = false;
        beginCardSelectionCoroutine = null;

        yield break;
    }


    if (!matchState.IsServer)
    {
        cardSelectionStartRequested = false;
        beginCardSelectionCoroutine = null;

        yield break;
    }


    if (matchState.CurrentPhase ==
        MatchPhase.WaitingForPlayers)
    {
        Debug.Log(
            "[CardSelectionServerService] " +
            "2인 준비 완료 | " +
            $"RegistryCount: {preparationRegistry.Count}"
        );

        matchState.ServerBeginCardSelection();
    }


    beginCardSelectionCoroutine = null;
}


    // ==================================================
    // Debug - 데이터 독립성 테스트
    // ==================================================

    [ContextMenu(
        "Debug/Test Independent Player Data")]
    private void DebugTestIndependentPlayerData()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[CardSelectionServerService] " +
                "Play Mode에서 실행해주세요."
            );

            return;
        }

        if (preparationRegistry == null)
        {
            Debug.LogError(
                "[CardSelectionServerService] " +
                "Registry가 없습니다."
            );

            return;
        }

        if (preparationRegistry.Count < 2)
        {
            Debug.LogError(
                "[CardSelectionServerService] " +
                "플레이어가 2명 등록되어 있지 않습니다."
            );

            return;
        }


        ulong hostClientId = 0;

        ulong debugClientId =
            PlayerPreparationRegistry.DebugClientId;


        if (!preparationRegistry.TryGetPlayer(
                hostClientId,
                out PlayerPreparationData hostPlayer))
        {
            Debug.LogError(
                "[CardSelectionServerService] " +
                "Host 데이터를 찾지 못했습니다."
            );

            return;
        }


        if (!preparationRegistry.TryGetPlayer(
                debugClientId,
                out PlayerPreparationData debugPlayer))
        {
            Debug.LogError(
                "[CardSelectionServerService] " +
                "Debug Client 데이터를 찾지 못했습니다."
            );

            return;
        }


        hostPlayer.SelectedCardIndex = 100;

        debugPlayer.SelectedCardIndex = 200;


        bool isSameObject =
            ReferenceEquals(
                hostPlayer,
                debugPlayer
            );


        Debug.Log(
            "[CardSelectionServerService] " +
            "===== 독립 데이터 테스트 ====="
        );

        Debug.Log(
            "[CardSelectionServerService] " +
            $"Host | " +
            $"ClientId: {hostPlayer.ClientId} | " +
            $"SelectedCardIndex: " +
            $"{hostPlayer.SelectedCardIndex}"
        );

        Debug.Log(
            "[CardSelectionServerService] " +
            $"Debug Client | " +
            $"ClientId: {debugPlayer.ClientId} | " +
            $"SelectedCardIndex: " +
            $"{debugPlayer.SelectedCardIndex}"
        );

        Debug.Log(
            "[CardSelectionServerService] " +
            $"같은 PlayerPreparationData 객체인가? " +
            $"{isSameObject}"
        );


        if (hostPlayer.SelectedCardIndex == 100 &&
            debugPlayer.SelectedCardIndex == 200 &&
            !isSameObject)
        {
            Debug.Log(
                "[CardSelectionServerService] " +
                "Host / Client 데이터 독립 저장 확인 성공"
            );
        }
        else
        {
            Debug.LogError(
                "[CardSelectionServerService] " +
                "Host / Client 데이터 독립 저장 확인 실패"
            );
        }
    }


    [ContextMenu(
        "Debug/Reset Test Player Data")]
    private void DebugResetTestPlayerData()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[CardSelectionServerService] " +
                "Play Mode에서 실행해주세요."
            );

            return;
        }

        if (preparationRegistry == null)
        {
            return;
        }


        foreach (
            PlayerPreparationData player
            in preparationRegistry.Players)
        {
            if (player == null)
            {
                continue;
            }

            player.CardCandidates = null;

            player.SelectedCardIndex = -1;

            player.CardSelectionCompleted = false;

            player.ConditionCompleted = false;

            player.FinalDeck.Clear();
        }


        Debug.Log(
            "[CardSelectionServerService] " +
            "테스트 준비 데이터 초기화 완료"
        );

        LogCurrentPlayers();
    }


    // ==================================================
    // Debug - 현재 데이터 출력
    // ==================================================

    [ContextMenu(
        "Debug/Print Player Preparation Data")]
    private void LogCurrentPlayers()
    {
        if (preparationRegistry == null)
        {
            Debug.LogWarning(
                "[CardSelectionServerService] " +
                "Registry가 없습니다."
            );

            return;
        }


        Debug.Log(
            "[CardSelectionServerService] " +
            $"현재 준비 데이터 수: " +
            $"{preparationRegistry.Count}"
        );


        foreach (
            PlayerPreparationData player
            in preparationRegistry.Players)
        {
            if (player == null)
            {
                continue;
            }

            Debug.Log(
                "[CardSelectionServerService] " +
                $"Player | " +
                $"ClientId: {player.ClientId} | " +
                $"Candidates: " +
                $"{GetCandidateCount(player)} | " +
                $"SelectedIndex: " +
                $"{player.SelectedCardIndex} | " +
                $"CardCompleted: " +
                $"{player.CardSelectionCompleted} | " +
                $"FinalDeck: " +
                $"{player.FinalDeck.Count} | " +
                $"ConditionCompleted: " +
                $"{player.ConditionCompleted}"
            );
        }
    }


    private int GetCandidateCount(
        PlayerPreparationData player)
    {
        if (player == null ||
            player.CardCandidates == null)
        {
            return 0;
        }

        return player.CardCandidates.Length;
    }
}