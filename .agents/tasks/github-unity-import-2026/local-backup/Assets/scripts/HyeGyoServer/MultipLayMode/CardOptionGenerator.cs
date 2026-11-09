using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 멀티 준비 단계의 모든 랜덤 후보를 서버에서 생성한다.
/// 카드 표시는 Single 모드와 같은 CardDefinition/프리팹을 사용하고,
/// Client는 이 컴포넌트의 동일한 cardPool index로 표시 데이터만 복원한다.
/// </summary>
public sealed class CardOptionGenerator : MonoBehaviour
{
    private const string EffectPricesResourcePath =
        "Data/EffectPrices";

    private const int MaxEnhanceRollAttempts = 100;
    private const float CooldownAllocationChance = 2f / 3f;
    private const float ContinuousChance = 0.2f;
    private const int ContinuousBudgetMultiplier = 5;

    [Header("Card Pool (Single Reward Cards)")]
    [SerializeField]
    private List<CardDefinition> cardPool =
        new List<CardDefinition>();

    [Header("Base Cards")]
    [Tooltip("모든 플레이어에게 기본으로 지급되는 약한 공격 카드")]
    [SerializeField]
    private CardDefinition basicAttackCard;

    [Tooltip("모든 플레이어에게 기본으로 지급되는 약한 방어 카드")]
    [SerializeField]
    private CardDefinition basicDefenseCard;

    private readonly Dictionary<EffectType, EffectPriceInfo>
        effectPrices = new();

    private static readonly EffectType[] EnhanceableEffectTypes =
        ((EffectType[])Enum.GetValues(typeof(EffectType)))
        .Where(type =>
            type != EffectType.Disposable &&
            type != EffectType.Preserve &&
            type != EffectType.Guard &&
            type != EffectType.Weak)
        .ToArray();

    private void Awake()
    {
        BuildEffectPriceCache();
    }

    public CardDefinition[] GenerateInitialCandidates(
        int count = 3)
    {
        List<CardDefinition> pool =
            cardPool
                .Where(card => card != null)
                .Distinct()
                .ToList();

        int pickCount =
            Mathf.Min(Mathf.Max(0, count), pool.Count);

        CardDefinition[] result =
            new CardDefinition[pickCount];

        for (int i = 0; i < pickCount; i++)
        {
            int index =
                UnityEngine.Random.Range(0, pool.Count);

            result[i] = pool[index];
            pool.RemoveAt(index);
        }

        return result;
    }

    public bool InitializeFinalDeck(
        PlayerPreparationData player,
        CardDefinition selectedCard,
        out string rejectReason)
    {
        rejectReason = string.Empty;

        if (player == null || selectedCard == null)
        {
            rejectReason = "초기 카드 데이터가 올바르지 않습니다.";
            return false;
        }

        if (basicAttackCard == null ||
            basicDefenseCard == null)
        {
            rejectReason =
                "기본 약공격/약방어 카드가 연결되어 있지 않습니다.";

            return false;
        }

        player.FinalDeck.Clear();
        player.FinalDeck.Add(basicAttackCard.Clone());
        player.FinalDeck.Add(basicDefenseCard.Clone());
        player.FinalDeck.Add(selectedCard.Clone());

        return true;
    }

    /// <summary>
    /// 매 라운드 정확히 3개를 만든다.
    /// 0: 강화(가능할 때), 1: 카드 추가, 2: 추가/강화 랜덤.
    /// 강화가 불가능한 경우 해당 슬롯은 카드 추가로 안전하게 대체한다.
    /// </summary>
    public PreparationOption[] GenerateOptions(
        PlayerPreparationData player,
        int round)
    {
        PreparationOption[] result =
            new PreparationOption[3];

        result[0] =
            TryCreateEnhanceOption(player, out PreparationOption enhance)
                ? enhance
                : CreateAddCardOption();

        result[1] =
            CreateAddCardOption();

        bool chooseEnhance =
            UnityEngine.Random.value < 0.5f;

        result[2] =
            chooseEnhance &&
            TryCreateEnhanceOption(player, out PreparationOption randomEnhance)
                ? randomEnhance
                : CreateAddCardOption();

        return result;
    }

    private PreparationOption CreateAddCardOption()
    {
        List<CardDefinition> validCards =
            cardPool
                .Where(card => card != null)
                .ToList();

        if (validCards.Count == 0)
        {
            Debug.LogError(
                "[CardOptionGenerator] Card Pool이 비어 있습니다."
            );

            return PreparationOption.CreateSkip();
        }

        CardDefinition card =
            validCards[
                UnityEngine.Random.Range(0, validCards.Count)
            ];

        return new PreparationOption
        {
            Type = PreparationOptionType.AddCard,
            Card = card,
            TargetCardIndex = -1,
            EnhanceId = -1,
            Upgrade = null
        };
    }

    private bool TryCreateEnhanceOption(
        PlayerPreparationData player,
        out PreparationOption option)
    {
        option = default;

        if (player?.FinalDeck == null ||
            player.FinalDeck.Count == 0)
        {
            return false;
        }

        for (int attempt = 0;
             attempt < MaxEnhanceRollAttempts;
             attempt++)
        {
            CardUpgrade upgrade =
                RollEnhanceOption();

            if (upgrade == null)
                continue;

            List<int> targets =
                new List<int>();

            for (int i = 0;
                 i < player.FinalDeck.Count;
                 i++)
            {
                CardDefinition card =
                    player.FinalDeck[i];

                if (card != null &&
                    RewardManager.CanEnhance(card, upgrade))
                {
                    targets.Add(i);
                }
            }

            if (targets.Count == 0)
                continue;

            option = new PreparationOption
            {
                Type = PreparationOptionType.EnhanceCard,
                Card = null,
                TargetCardIndex =
                    targets[
                        UnityEngine.Random.Range(0, targets.Count)
                    ],
                EnhanceId = 0,
                Upgrade = upgrade
            };

            return true;
        }

        return false;
    }

    private CardUpgrade RollEnhanceOption()
    {
        if (EnhanceableEffectTypes.Length == 0)
            return null;

        EffectType effectType =
            EnhanceableEffectTypes[
                UnityEngine.Random.Range(
                    0,
                    EnhanceableEffectTypes.Length
                )
            ];

        if (!effectPrices.TryGetValue(
                effectType,
                out EffectPriceInfo priceInfo))
        {
            priceInfo = new EffectPriceInfo
            {
                price = 1f,
                magnitudeMin = 1,
                magnitudeMax = 3,
                magnitudeUnit = 1
            };
        }

        int magnitude =
            RollMagnitude(priceInfo);

        Effect effect =
            Effect.Create(effectType, magnitude);

        if (effect == null)
            return null;

        EffectTarget target =
            ResolveEnhanceTarget(
                effect.TargetPolarity,
                magnitude
            );

        CardEffect cardEffect =
            new CardEffect(effect, target);

        EffectCategory appliedCategory =
            RollAppliedCategory(
                effect.SupportedCategories
            );

        cardEffect.SetAppliedCategory(
            appliedCategory
        );

        int priceMagnitude =
            effect.DoesntUseMagnitude
                ? 1
                : magnitude;

        int budget =
            Mathf.FloorToInt(
                priceInfo.price * priceMagnitude
            );

        if (appliedCategory ==
            EffectCategory.Continuous)
        {
            budget *= ContinuousBudgetMultiplier;
        }

        (int cost, int cooldown) =
            DistributeBudget(budget);

        return new CardUpgrade(
            cardEffect,
            cost,
            cooldown
        );
    }

    public int GetCardPoolIndex(
        CardDefinition card)
    {
        return card == null || cardPool == null
            ? -1
            : cardPool.IndexOf(card);
    }

    public CardDefinition GetCardByPoolIndex(
        int index)
    {
        if (cardPool == null ||
            index < 0 ||
            index >= cardPool.Count)
        {
            return null;
        }

        return cardPool[index];
    }

    public CardDefinition GetBasicAttackCard() =>
        basicAttackCard;

    public CardDefinition GetBasicDefenseCard() =>
        basicDefenseCard;

    public PreparationOptionNetData ToNetData(
        PreparationOption option)
    {
        bool hasUpgrade =
            option.Type == PreparationOptionType.EnhanceCard &&
            option.Upgrade != null;

        return new PreparationOptionNetData
        {
            Type = (byte)option.Type,
            CardPoolIndex =
                option.Type == PreparationOptionType.AddCard
                    ? GetCardPoolIndex(option.Card)
                    : -1,
            TargetCardIndex = option.TargetCardIndex,
            EnhanceId = option.EnhanceId,
            HasUpgrade = hasUpgrade,
            Upgrade = hasUpgrade
                ? EnhanceOptionNetData.FromCardUpgrade(
                    option.Upgrade
                )
                : default
        };
    }

    public PreparationOption FromNetData(
        PreparationOptionNetData data)
    {
        PreparationOptionType type =
            (PreparationOptionType)data.Type;

        return new PreparationOption
        {
            Type = type,
            Card = type == PreparationOptionType.AddCard
                ? GetCardByPoolIndex(data.CardPoolIndex)
                : null,
            TargetCardIndex = data.TargetCardIndex,
            EnhanceId = data.EnhanceId,
            Upgrade = data.HasUpgrade
                ? data.Upgrade.ToCardUpgrade()
                : null
        };
    }

    private void BuildEffectPriceCache()
    {
        effectPrices.Clear();

        TextAsset json =
            Resources.Load<TextAsset>(
                EffectPricesResourcePath
            );

        if (json == null)
        {
            Debug.LogWarning(
                "[CardOptionGenerator] " +
                $"Resources/{EffectPricesResourcePath}.json을 찾지 못했습니다."
            );

            return;
        }

        EffectPriceTable table =
            JsonUtility.FromJson<EffectPriceTable>(
                json.text
            );

        if (table?.prices == null)
            return;

        foreach (EffectPriceJsonEntry entry
                 in table.prices)
        {
            if (Enum.TryParse(
                    entry.effectType,
                    true,
                    out EffectType type))
            {
                effectPrices[type] =
                    new EffectPriceInfo
                    {
                        price = entry.price,
                        magnitudeMin = entry.magnitudeMin,
                        magnitudeMax = entry.magnitudeMax,
                        magnitudeUnit = entry.magnitudeUnit
                    };
            }
        }
    }

    private static int RollMagnitude(
        EffectPriceInfo info)
    {
        int unit = Mathf.Max(1, info.magnitudeUnit);
        int min = info.magnitudeMin;
        int max = Mathf.Max(min, info.magnitudeMax);
        int steps = (max - min) / unit + 1;

        return min +
            unit * UnityEngine.Random.Range(0, steps);
    }

    private static EffectCategory RollAppliedCategory(
        EffectCategory supported)
    {
        if (supported ==
            (EffectCategory.Instant |
             EffectCategory.Continuous))
        {
            return UnityEngine.Random.value < ContinuousChance
                ? EffectCategory.Continuous
                : EffectCategory.Instant;
        }

        return supported;
    }

    private static EffectTarget ResolveEnhanceTarget(
        EffectTargetPolarity polarity,
        int magnitude)
    {
        bool positive = magnitude > 0;

        return polarity switch
        {
            EffectTargetPolarity.Positive =>
                positive
                    ? EffectTarget.User
                    : EffectTarget.Opponent,
            EffectTargetPolarity.Negative =>
                positive
                    ? EffectTarget.Opponent
                    : EffectTarget.User,
            _ => UnityEngine.Random.value < 0.5f
                ? EffectTarget.User
                : EffectTarget.Opponent
        };
    }

    private static (int cost, int cooldown)
        DistributeBudget(int budget)
    {
        bool negative = budget < 0;
        budget = Mathf.Abs(budget);

        int cost = 0;
        int cooldown = 0;

        for (int i = 0; i < budget; i++)
        {
            if (UnityEngine.Random.value <
                CooldownAllocationChance)
            {
                cooldown++;
            }
            else
            {
                cost++;
            }
        }

        if (negative)
        {
            cost = -cost;
            cooldown = -cooldown;
        }

        return (cost, cooldown);
    }

    [Serializable]
    private sealed class EffectPriceJsonEntry
    {
        public string effectType;
        public float price;
        public int magnitudeMin;
        public int magnitudeMax;
        public int magnitudeUnit;
    }

    [Serializable]
    private sealed class EffectPriceTable
    {
        public List<EffectPriceJsonEntry> prices;
    }
}
