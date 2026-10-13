using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 초기 카드 선택 이후 플레이어별 10회 준비 선택을 서버에서 관리한다.
/// 현재 발급된 선택지를 서버에 보관하고 Client는 index만 전송한다.
/// </summary>
public sealed class MultiPreparationManager : MonoBehaviour
{
    public const int MaxRounds = 10;
    private const int OptionCount = 3;

    [SerializeField]
    private PlayerPreparationRegistry preparationRegistry;

    [SerializeField]
    private NetworkMatchState matchState;

    [SerializeField]
    private CardOptionGenerator cardOptionGenerator;

    private readonly Dictionary<ulong, int>
        currentRoundByClient = new();

    private readonly Dictionary<ulong, PreparationOption[]>
        currentOptionsByClient = new();

    private bool allPlayersCompletedRaised;

    public event Action AllPlayersCompleted;

    private void Awake()
    {
        preparationRegistry ??=
            FindFirstObjectByType<PlayerPreparationRegistry>();

        matchState ??=
            GetComponent<NetworkMatchState>();

        cardOptionGenerator ??=
            GetComponent<CardOptionGenerator>();
    }

    public bool TryBeginForPlayer(
        ulong clientId,
        out PreparationOption[] options,
        out string rejectReason)
    {
        options = null;
        rejectReason = string.Empty;

        if (!ValidateCommon(
                clientId,
                out PlayerPreparationData player,
                out rejectReason))
        {
            return false;
        }

        if (matchState.CurrentPhase !=
            MatchPhase.ChoosingCondition)
        {
            rejectReason =
                $"현재 준비 단계가 아닙니다: {matchState.CurrentPhase}";

            return false;
        }

        if (!player.CardSelectionCompleted)
        {
            rejectReason =
                "초기 카드 선택을 먼저 완료해야 합니다.";

            return false;
        }

        if (player.ConditionCompleted ||
            GetCompletedRounds(clientId) >= MaxRounds)
        {
            rejectReason = "이미 10회 선택을 완료했습니다.";
            return false;
        }

        // Begin 요청을 다시 보내도 무료 reroll하지 않고
        // 이미 발급한 동일 선택지를 재전송한다.
        if (currentOptionsByClient.TryGetValue(
                clientId,
                out PreparationOption[] existing))
        {
            options = existing;
            return true;
        }

        options =
            cardOptionGenerator.GenerateOptions(
                player,
                GetCompletedRounds(clientId)
            );

        if (options == null ||
            options.Length != OptionCount)
        {
            rejectReason = "선택지 생성에 실패했습니다.";
            return false;
        }

        currentOptionsByClient[clientId] = options;

        return true;
    }

    public bool TryConfirmOption(
        ulong clientId,
        int optionIndex,
        out PreparationOption selectedOption,
        out PreparationOption[] nextOptions,
        out int remaining,
        out string rejectReason)
    {
        selectedOption = default;
        nextOptions = null;
        remaining = 0;
        rejectReason = string.Empty;

        if (!ValidateCommon(
                clientId,
                out PlayerPreparationData player,
                out rejectReason))
        {
            return false;
        }

        if (matchState.CurrentPhase !=
            MatchPhase.ChoosingCondition)
        {
            rejectReason = "현재 준비 선택 단계가 아닙니다.";
            return false;
        }

        if (!currentOptionsByClient.TryGetValue(
                clientId,
                out PreparationOption[] options))
        {
            rejectReason = "현재 발급된 선택지가 없습니다.";
            return false;
        }

        if (optionIndex < 0 ||
            optionIndex >= options.Length)
        {
            rejectReason = "잘못된 선택지입니다.";
            return false;
        }

        selectedOption = options[optionIndex];

        if (!ApplyOption(
                player,
                selectedOption,
                out rejectReason))
        {
            return false;
        }

        return CompleteRound(
            clientId,
            player,
            out nextOptions,
            out remaining,
            out rejectReason
        );
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

        if (!ValidateCommon(
                clientId,
                out PlayerPreparationData player,
                out rejectReason))
        {
            return false;
        }

        if (matchState.CurrentPhase !=
            MatchPhase.ChoosingCondition)
        {
            rejectReason = "현재 준비 선택 단계가 아닙니다.";
            return false;
        }

        // 실제 outstanding offer가 있을 때만 Skip을 허용해
        // 반복 RPC로 10회를 즉시 소모하지 못하게 한다.
        if (!currentOptionsByClient.ContainsKey(clientId))
        {
            rejectReason = "건너뛸 현재 선택지가 없습니다.";
            return false;
        }

        return CompleteRound(
            clientId,
            player,
            out nextOptions,
            out remaining,
            out rejectReason
        );
    }

    public void ResetState()
    {
        currentRoundByClient.Clear();
        currentOptionsByClient.Clear();
        allPlayersCompletedRaised = false;
    }

    public int GetRemainingChoices(
        ulong clientId)
    {
        return Mathf.Clamp(
            MaxRounds - GetCompletedRounds(clientId),
            0,
            MaxRounds
        );
    }

    private bool CompleteRound(
        ulong clientId,
        PlayerPreparationData player,
        out PreparationOption[] nextOptions,
        out int remaining,
        out string rejectReason)
    {
        nextOptions = null;
        rejectReason = string.Empty;

        int completed =
            GetCompletedRounds(clientId) + 1;

        currentRoundByClient[clientId] = completed;
        currentOptionsByClient.Remove(clientId);

        remaining =
            Mathf.Max(0, MaxRounds - completed);

        if (completed >= MaxRounds)
        {
            player.ConditionCompleted = true;
            CheckAllPlayersCompleted();
            return true;
        }

        nextOptions =
            cardOptionGenerator.GenerateOptions(
                player,
                completed
            );

        if (nextOptions == null ||
            nextOptions.Length != OptionCount)
        {
            rejectReason =
                "다음 선택지 생성에 실패했습니다.";

            return false;
        }

        currentOptionsByClient[clientId] = nextOptions;

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
                if (option.Card == null)
                {
                    rejectReason = "추가할 카드가 없습니다.";
                    return false;
                }

                player.FinalDeck.Add(
                    option.Card.Clone()
                );

                return true;

            case PreparationOptionType.EnhanceCard:
                if (option.Upgrade == null ||
                    option.TargetCardIndex < 0 ||
                    option.TargetCardIndex >=
                        player.FinalDeck.Count)
                {
                    rejectReason =
                        "강화 대상 또는 강화 데이터가 유효하지 않습니다.";

                    return false;
                }

                CardDefinition target =
                    player.FinalDeck[
                        option.TargetCardIndex
                    ];

                if (target == null ||
                    !RewardManager.CanEnhance(
                        target,
                        option.Upgrade))
                {
                    rejectReason =
                        "현재 덱에 적용할 수 없는 강화입니다.";

                    return false;
                }

                target.ApplyUpgrade(option.Upgrade);
                return true;

            case PreparationOptionType.Skip:
                return true;

            default:
                rejectReason = "알 수 없는 옵션입니다.";
                return false;
        }
    }

    private bool ValidateCommon(
        ulong clientId,
        out PlayerPreparationData player,
        out string rejectReason)
    {
        player = null;
        rejectReason = string.Empty;

        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
        {
            rejectReason = "Server가 아닙니다.";
            return false;
        }

        if (preparationRegistry == null ||
            matchState == null ||
            cardOptionGenerator == null)
        {
            rejectReason = "준비 서비스 참조가 없습니다.";
            return false;
        }

        if (!preparationRegistry.TryGetPlayer(
                clientId,
                out player))
        {
            rejectReason = "플레이어 데이터를 찾을 수 없습니다.";
            return false;
        }

        return true;
    }

    private int GetCompletedRounds(
        ulong clientId)
    {
        return currentRoundByClient.TryGetValue(
                clientId,
                out int value)
            ? value
            : 0;
    }

    private void CheckAllPlayersCompleted()
    {
        if (allPlayersCompletedRaised ||
            preparationRegistry == null ||
            preparationRegistry.Count != 2)
        {
            return;
        }

        foreach (PlayerPreparationData player
                 in preparationRegistry.Players)
        {
            if (player == null ||
                !player.ConditionCompleted)
            {
                return;
            }
        }

        allPlayersCompletedRaised = true;

        matchState.ServerSetPhase(
            MatchPhase.ShowingResult
        );

        Debug.Log(
            "[MultiPreparationManager] " +
            "Host / Client 준비 10회 모두 완료"
        );

        AllPlayersCompleted?.Invoke();
    }
}
