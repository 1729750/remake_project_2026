using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public sealed class MultiPreparationManager : MonoBehaviour
{
    private const int MaxRounds = 10;
    private const int OptionCount = 3;

    [SerializeField]
    private PlayerPreparationRegistry preparationRegistry;

    [SerializeField]
    private NetworkMatchState matchState;

    [SerializeField]
    private CardOptionGenerator cardOptionGenerator;

    private readonly Dictionary<
        ulong,
        int
    > currentRoundByClient = new();

    private readonly Dictionary<
        ulong,
        PreparationOption[]
    > currentOptionsByClient = new();

    public bool TryBeginForPlayer(
        ulong clientId,
        out PreparationOption[] options,
        out string rejectReason)
    {
        options = null;
        rejectReason = string.Empty;

            Debug.Log(
        "[MultiPreparationManager] " +
        $"TryBeginForPlayer 시작 | " +
        $"ClientId: {clientId} | " +
        $"Phase: {(matchState != null ? matchState.CurrentPhase.ToString() : "NULL")} | " +
        $"Registry: {(preparationRegistry != null ? "있음" : "NULL")} | " +
        $"PlayerCount: {(preparationRegistry != null ? preparationRegistry.Count : -1)}"
    );

        if (!IsServer())
        {
            rejectReason = "Server가 아닙니다.";
            return false;
        }

        // 플레이어 2명이 준비되어 있는데
// 아직 WaitingForPlayers라면
// Server가 Preparation 단계로 진입시킨다.
if (matchState != null &&
    matchState.CurrentPhase ==
        MatchPhase.WaitingForPlayers &&
    preparationRegistry != null &&
    preparationRegistry.Count == 2)
{
    matchState.ServerSetPhase(
        MatchPhase.ChoosingCard
    );

    Debug.Log(
        "[MultiPreparationManager] " +
        "플레이어 2명 확인 → " +
        "Preparation Phase 시작 | " +
        "ChoosingCard"
    );
}

        if (matchState.CurrentPhase !=
            MatchPhase.ChoosingCard &&
            matchState.CurrentPhase !=
            MatchPhase.ChoosingCondition)
        {
    rejectReason =
        $"현재 준비 선택 단계가 아닙니다. " +
        $"CurrentPhase: {matchState.CurrentPhase}";

    Debug.LogWarning(
        "[MultiPreparationManager] " +
        $"준비 요청 거절 | " +
        $"ClientId: {clientId} | " +
        $"Reason: {rejectReason}"
    );


            return false;
        }

if (!preparationRegistry.TryGetPlayer(
        clientId,
        out PlayerPreparationData player))
{
    rejectReason =
        $"플레이어 준비 데이터를 찾을 수 없습니다. " +
        $"ClientId: {clientId}";

    Debug.LogWarning(
        "[MultiPreparationManager] " +
        $"준비 요청 거절 | " +
        $"Reason: {rejectReason}"
    );

    return false;
}

        int round =
            currentRoundByClient.TryGetValue(
                clientId,
                out int value)
                ? value
                : 0;

        if (round >= MaxRounds)
        {
            rejectReason =
                "이미 10회 선택을 완료했습니다.";

            return false;
        }

        options =
            cardOptionGenerator.GenerateOptions(
                player,
                round
            );

        if (options == null ||
            options.Length != OptionCount)
        {
            rejectReason =
                "선택지 생성에 실패했습니다.";

            return false;
        }

        currentOptionsByClient[clientId] =
            options;

        return true;
    }
public bool TryConfirmOption(
    ulong clientId,
    int optionIndex,
    out PreparationOption[] nextOptions,
    out int remaining,
    out string rejectReason)
{
    nextOptions = null;
    remaining = 0;
    rejectReason = string.Empty;

    if (!IsServer())
    {
        rejectReason =
            "Server가 아닙니다.";

        return false;
    }

    if (!preparationRegistry.TryGetPlayer(
            clientId,
            out PlayerPreparationData player))
    {
        rejectReason =
            "플레이어 데이터를 찾을 수 없습니다.";

        return false;
    }

    if (!currentOptionsByClient.TryGetValue(
            clientId,
            out PreparationOption[] options))
    {
        rejectReason =
            "현재 선택지가 없습니다.";

        return false;
    }

    if (optionIndex < 0 ||
        optionIndex >= options.Length)
    {
        rejectReason =
            "잘못된 선택지입니다.";

        return false;
    }

    PreparationOption selected =
        options[optionIndex];

    if (!ApplyOption(
            player,
            selected,
            out rejectReason))
    {
        return false;
    }

    int nextRound =
        currentRoundByClient.TryGetValue(
            clientId,
            out int currentRound)
            ? currentRound + 1
            : 1;

    currentRoundByClient[clientId] =
        nextRound;

    currentOptionsByClient.Remove(
        clientId
    );

    remaining =
        Mathf.Max(
            0,
            MaxRounds - nextRound
        );

    // =========================
    // 10회 완료
    // =========================
    if (nextRound >= MaxRounds)
    {
        player.ConditionCompleted = true;

        CheckAllPlayersCompleted();

        return true;
    }

    // =========================
    // 다음 선택지 3개 생성
    // =========================
    nextOptions =
        cardOptionGenerator
            .GenerateOptions(
                player,
                nextRound
            );

    if (nextOptions == null ||
        nextOptions.Length != OptionCount)
    {
        rejectReason =
            "다음 선택지 생성에 실패했습니다.";

        return false;
    }

    currentOptionsByClient[
        clientId
    ] = nextOptions;

    return true;
}
private bool ApplyOption(
    PlayerPreparationData player,
    PreparationOption option,
    out string rejectReason)
{
    rejectReason = string.Empty;

    switch (option.Type)
    {
        case PreparationOptionType.AddCard:
            return ApplyAddCard(
                player,
                option.Card,
                out rejectReason
            );

        case PreparationOptionType.EnhanceCard:
            return ApplyEnhance(
                player,
                option.TargetCardIndex,
                option.EnhanceId,
                out rejectReason
            );

        case PreparationOptionType.Skip:
            return true;

        default:
            rejectReason =
                "알 수 없는 옵션입니다.";

            return false;
    }
}

private bool ApplyAddCard(
    PlayerPreparationData player,
    CardDefinition card,
    out string rejectReason)
{
    rejectReason = string.Empty;

    if (player == null)
    {
        rejectReason =
            "플레이어 데이터가 없습니다.";

        return false;
    }

    if (card == null)
    {
        rejectReason =
            "추가할 카드가 없습니다.";

        return false;
    }

    CardDefinition runtimeCard =
        card.Clone();

    player.FinalDeck.Add(
        runtimeCard
    );

    Debug.Log(
        "[MultiPreparationManager] " +
        $"카드 추가 | " +
        $"ClientId: {player.ClientId} | " +
        $"Card: {card.name} | " +
        $"DeckCount: {player.FinalDeck.Count}"
    );

    return true;
}
    private bool ApplyEnhance(
        PlayerPreparationData player,
        int targetCardIndex,
        int enhanceId,
        out string rejectReason)
    {
        rejectReason = string.Empty;

        if (targetCardIndex < 0 ||
            targetCardIndex >=
            player.FinalDeck.Count)
        {
            rejectReason =
                "강화 대상 카드가 유효하지 않습니다.";

            return false;
        }

        // 실제 강화 데이터 구조에 맞춰
        // 이후 구현
        return true;
    }

private void CheckAllPlayersCompleted()
{
    if (preparationRegistry.Count != 2)
        return;

    foreach (
        PlayerPreparationData player
        in preparationRegistry.Players)
    {
        if (!player.ConditionCompleted)
            return;
    }

    Debug.Log(
        "[MultiPreparationManager] " +
        "두 플레이어 모두 준비 완료"
    );

    matchState.ServerSetPhase(
        MatchPhase.ShowingResult
    );

    MapManager_Multi.Instance
        ?.NotifyBothPlayersConfirmed();
}

    private bool IsServer()
    {
        return
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsServer;
    }

    public bool TrySkip(
    ulong clientId,
    out PreparationOption[] nextOptions,
    out int remaining,
    out string rejectReason)
{
    nextOptions = null;
    remaining = 0;
    rejectReason = string.Empty;

    if (!IsServer())
    {
        rejectReason =
            "Server가 아닙니다.";

        return false;
    }

    if (!preparationRegistry.TryGetPlayer(
            clientId,
            out PlayerPreparationData player))
    {
        rejectReason =
            "플레이어 데이터를 찾을 수 없습니다.";

        return false;
    }

    int currentRound =
        currentRoundByClient.TryGetValue(
            clientId,
            out int value)
            ? value
            : 0;

    if (currentRound >= MaxRounds)
    {
        rejectReason =
            "이미 준비를 완료했습니다.";

        return false;
    }

    int nextRound =
        currentRound + 1;

    currentRoundByClient[
        clientId
    ] = nextRound;

    // 기존 선택지 폐기
    currentOptionsByClient.Remove(
        clientId
    );

    remaining =
        Mathf.Max(
            0,
            MaxRounds - nextRound
        );

    // 카드 추가/강화 없음

    if (nextRound >= MaxRounds)
    {
        player.ConditionCompleted = true;

        CheckAllPlayersCompleted();

        return true;
    }

    nextOptions =
        cardOptionGenerator
            .GenerateOptions(
                player,
                nextRound
            );

    if (nextOptions == null ||
        nextOptions.Length != OptionCount)
    {
        rejectReason =
            "다음 선택지 생성에 실패했습니다.";

        return false;
    }

    currentOptionsByClient[
        clientId
    ] = nextOptions;

    return true;
}
public int GetRemainingChoices(
    ulong clientId)
{
    int completed =
        currentRoundByClient.TryGetValue(
            clientId,
            out int round)
            ? round
            : 0;

    return Mathf.Clamp(
        MaxRounds - completed,
        0,
        MaxRounds
    );
}

}