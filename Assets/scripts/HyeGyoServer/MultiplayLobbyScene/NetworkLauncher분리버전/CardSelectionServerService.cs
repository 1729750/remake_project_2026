using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 2명이 연결된 뒤 각 플레이어의 초기 카드 후보를 서버에서 생성하고,
/// 선택 index를 검증해 기본 약공격/약방어가 포함된 FinalDeck을 만든다.
/// </summary>
public sealed class CardSelectionServerService : MonoBehaviour
{
    private const int CandidateCount = 3;

    [SerializeField]
    private PlayerPreparationRegistry preparationRegistry;

    [SerializeField]
    private NetworkMatchState matchState;

    [SerializeField]
    private CardOptionGenerator cardOptionGenerator;

    [SerializeField]
    private MultiPreparationManager preparationManager;

    private Coroutine beginCardSelectionCoroutine;
    private bool cardSelectionStartRequested;
    private bool allSelectionsRaised;

    public int RegisteredPlayerCount =>
        preparationRegistry != null
            ? preparationRegistry.Count
            : 0;

    public bool HasTwoPlayers =>
        RegisteredPlayerCount == 2;

    public IEnumerable<PlayerPreparationData> Players =>
        preparationRegistry != null
            ? preparationRegistry.Players
            : Array.Empty<PlayerPreparationData>();

    public event Action<ulong, int, int, int>
        InitialCandidatesReady;

    public event Action<ulong, int>
        InitialCardConfirmed;

    public event Action AllInitialCardsSelected;

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        EnsureReferences();

        if (preparationRegistry != null)
        {
            preparationRegistry.PlayerRemoved +=
                HandlePlayerRemoved;
        }
    }

    private void OnDisable()
    {
        if (preparationRegistry != null)
        {
            preparationRegistry.PlayerRemoved -=
                HandlePlayerRemoved;
        }

        if (beginCardSelectionCoroutine != null)
        {
            StopCoroutine(beginCardSelectionCoroutine);
            beginCardSelectionCoroutine = null;
        }
    }

    private void HandlePlayerRemoved(
        ulong clientId)
    {
        ResetForNewMatch();
    }

    public void ResetForNewMatch()
    {
        if (beginCardSelectionCoroutine != null)
        {
            StopCoroutine(beginCardSelectionCoroutine);
            beginCardSelectionCoroutine = null;
        }

        cardSelectionStartRequested = false;
        allSelectionsRaised = false;
        preparationManager?.ResetState();

        if (preparationRegistry != null)
        {
            foreach (PlayerPreparationData player
                     in preparationRegistry.Players)
            {
                if (player == null)
                    continue;

                foreach (CardDefinition card in player.FinalDeck)
                {
                    if (card != null)
                        Destroy(card);
                }

                player.FinalDeck.Clear();
                player.CardCandidates = null;
                player.SelectedCardIndex = -1;
                player.CardSelectionCompleted = false;
                player.ConditionCompleted = false;
            }
        }

        if (matchState != null && matchState.IsServer)
        {
            matchState.ServerResetToWaiting();
        }
    }

    private void EnsureReferences()
    {
        preparationRegistry ??=
            FindFirstObjectByType<PlayerPreparationRegistry>();

        matchState ??=
            GetComponent<NetworkMatchState>();

        cardOptionGenerator ??=
            GetComponent<CardOptionGenerator>();

        preparationManager ??=
            GetComponent<MultiPreparationManager>();
    }

    public bool TryStartCardSelection(
        out string rejectReason)
    {
        EnsureReferences();

        if (cardSelectionStartRequested)
        {
            rejectReason =
                "카드 선택 시작이 이미 요청되었습니다.";

            return false;
        }

        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
        {
            rejectReason = "Server가 아닙니다.";
            return false;
        }

        if (preparationRegistry == null ||
            preparationRegistry.Count != 2)
        {
            rejectReason =
                "플레이어 2명이 준비되지 않았습니다.";

            return false;
        }

        if (matchState == null ||
            cardOptionGenerator == null)
        {
            rejectReason =
                "카드 선택 서비스 참조가 없습니다.";

            return false;
        }

        cardSelectionStartRequested = true;
        beginCardSelectionCoroutine =
            StartCoroutine(BeginCardSelectionWhenReady());

        rejectReason = string.Empty;
        return true;
    }

    public bool TryChooseCard(
        ulong senderClientId,
        int candidateIndex,
        out string rejectReason)
    {
        EnsureReferences();
        rejectReason = string.Empty;

        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
        {
            rejectReason = "Server가 아닙니다.";
            return false;
        }

        if (matchState.CurrentPhase !=
            MatchPhase.ChoosingCard)
        {
            rejectReason = "현재 초기 카드 선택 단계가 아닙니다.";
            return false;
        }

        if (!preparationRegistry.TryGetPlayer(
                senderClientId,
                out PlayerPreparationData player))
        {
            rejectReason = "플레이어 데이터를 찾을 수 없습니다.";
            return false;
        }

        if (player.CardSelectionCompleted)
        {
            rejectReason = "이미 초기 카드를 선택했습니다.";
            return false;
        }

        CardDefinition[] candidates =
            player.CardCandidates;

        if (candidates == null ||
            candidateIndex < 0 ||
            candidateIndex >= candidates.Length ||
            candidates[candidateIndex] == null)
        {
            rejectReason = "유효하지 않은 초기 카드 선택입니다.";
            return false;
        }

        CardDefinition selected =
            candidates[candidateIndex];

        if (!cardOptionGenerator.InitializeFinalDeck(
                player,
                selected,
                out rejectReason))
        {
            return false;
        }

        player.SelectedCardIndex = candidateIndex;
        player.CardSelectionCompleted = true;

        int poolIndex =
            cardOptionGenerator.GetCardPoolIndex(selected);

        InitialCardConfirmed?.Invoke(
            senderClientId,
            poolIndex
        );

        Debug.Log(
            "[CardSelectionServerService] " +
            $"초기 카드 확정 | ClientId: {senderClientId} | " +
            $"Card: {selected.name} | Deck: {player.FinalDeck.Count}"
        );

        CheckAllInitialSelections();
        return true;
    }

    private IEnumerator BeginCardSelectionWhenReady()
    {
        while (matchState == null ||
               !matchState.IsSpawned)
        {
            EnsureReferences();
            yield return null;
        }

        if (preparationRegistry == null ||
            preparationRegistry.Count != 2 ||
            !matchState.IsServer)
        {
            cardSelectionStartRequested = false;
            beginCardSelectionCoroutine = null;
            yield break;
        }

        matchState.ServerBeginCardSelection();

        foreach (PlayerPreparationData player
                 in preparationRegistry.Players)
        {
            CardDefinition[] candidates =
                cardOptionGenerator.GenerateInitialCandidates(
                    CandidateCount
                );

            if (candidates.Length != CandidateCount)
            {
                Debug.LogError(
                    "[CardSelectionServerService] " +
                    "초기 카드 후보를 3개 만들 수 없습니다."
                );

                cardSelectionStartRequested = false;
                beginCardSelectionCoroutine = null;
                yield break;
            }

            player.CardCandidates = candidates;
            player.SelectedCardIndex = -1;
            player.CardSelectionCompleted = false;
            player.ConditionCompleted = false;
            player.FinalDeck.Clear();

            InitialCandidatesReady?.Invoke(
                player.ClientId,
                cardOptionGenerator.GetCardPoolIndex(candidates[0]),
                cardOptionGenerator.GetCardPoolIndex(candidates[1]),
                cardOptionGenerator.GetCardPoolIndex(candidates[2])
            );
        }

        Debug.Log(
            "[CardSelectionServerService] " +
            "양쪽 초기 카드 후보 생성/전송 완료"
        );

        beginCardSelectionCoroutine = null;
    }

    private void CheckAllInitialSelections()
    {
        if (allSelectionsRaised ||
            preparationRegistry == null ||
            preparationRegistry.Count != 2)
        {
            return;
        }

        foreach (PlayerPreparationData player
                 in preparationRegistry.Players)
        {
            if (player == null ||
                !player.CardSelectionCompleted)
            {
                return;
            }
        }

        allSelectionsRaised = true;

        matchState.ServerSetPhase(
            MatchPhase.ChoosingCondition
        );

        AllInitialCardsSelected?.Invoke();
    }
}
