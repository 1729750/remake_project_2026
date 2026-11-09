namespace CardGameServer.Game;

/// <summary>강화 1회: 붙일 효과와 그 대가로 늘어나는 코스트/쿨다운. (Unity CardUpgrade)</summary>
public sealed record CardUpgrade(CardEffectDef Effect, int CostDelta, int CooldownDelta);

/// <summary>EffectPrices.json 한 줄: 강화 예산 계수와 magnitude 범위.</summary>
public sealed record EffectPrice(float Price, int MagnitudeMin, int MagnitudeMax, int MagnitudeUnit);

/// <summary>
/// 카드 강화 규칙. Unity 쪽
///   RewardManager.CanEnhance / EnhanceableEffectTypes,
///   CardOptionGenerator.CreateEnhanceOption / RollUpgrade (멀티 기획),
///   CardDefinition.ApplyUpgrade / UpgradeEffect
/// 를 옮겼다. 강화된 카드는 이름(그림)은 그대로고 효과/코스트/쿨다운만 바뀐다.
/// </summary>
public static class Enhance
{
    private const int MaxEffectsWithoutMerge = 3;

    public static readonly EffectType[] EnhanceableTypes = Enum.GetValues<EffectType>()
        .Where(t => t != EffectType.Disposable && t != EffectType.Preserve && t != EffectType.Guard && t != EffectType.Weak)
        .ToArray();

    // =========================================================
    // 판정
    // =========================================================

    public static bool CanEnhance(CardDef card, CardUpgrade upgrade)
    {
        EffectCategory applied = AppliedCategory(upgrade.Effect);
        CardType required = applied == EffectCategory.Continuous ? CardType.Continuous : CardType.Instant;
        if (card.CardType != CardType.Mix && card.CardType != required) return false;

        EffectType type = upgrade.Effect.Type;

        if (applied == EffectCategory.Continuous &&
            card.Effects.Any(e => e.Type == type && AppliedCategory(e) == EffectCategory.Continuous))
            return false;

        bool hasMergeTarget = card.Effects.Any(e => e.Type == type && AppliedCategory(e) == applied);
        if (DoesntUseMagnitude(type) && hasMergeTarget) return false;
        if (card.Effects.Count < MaxEffectsWithoutMerge) return true;
        return hasMergeTarget;
    }

    // =========================================================
    // 적용 (CardDefinition.ApplyUpgrade)
    // =========================================================

    public static CardDef Apply(CardDef card, CardUpgrade upgrade)
    {
        var effects = card.Effects.ToList();
        CardType cardType = card.CardType;
        EffectCategory applied = AppliedCategory(upgrade.Effect);

        int merge = effects.FindIndex(e => e.Type == upgrade.Effect.Type && AppliedCategory(e) == applied);
        if (merge >= 0)
        {
            effects[merge] = effects[merge] with { Magnitude = effects[merge].Magnitude + upgrade.Effect.Magnitude };
        }
        else
        {
            if (applied == (EffectCategory.Instant | EffectCategory.Continuous)) cardType = CardType.Mix;
            effects.Add(upgrade.Effect with { AppliedCategory = applied });
        }

        return card with
        {
            CardType = cardType,
            Effects = effects,
            Cost = Math.Max(0, card.Cost + upgrade.CostDelta),
            Cooldown = Math.Max(1, card.Cooldown + upgrade.CooldownDelta),
            Level = card.Level + 1,
        };
    }

    // =========================================================
    // 굴리기 (CardOptionGenerator.CreateEnhanceOption + RollUpgrade)
    // =========================================================

    /// <summary>덱에서 강화 가능한 카드 하나와 강화 내용을 고른다. 없으면 null.</summary>
    public static (int target, CardUpgrade upgrade)? Roll(IReadOnlyList<CardDef> deck, CardLibrary library, Random random)
    {
        var targets = new List<(int index, List<(EffectType type, EffectCategory category)> choices)>();

        for (int i = 0; i < deck.Count; i++)
        {
            var choices = new List<(EffectType, EffectCategory)>();
            foreach (EffectType type in EnhanceableTypes)
            {
                EffectCategory supported = Effect.SupportedCategoriesOf(type);
                foreach (EffectCategory category in new[] { EffectCategory.Instant, EffectCategory.Continuous })
                {
                    if ((supported & category) == 0) continue;
                    var probe = new CardUpgrade(new CardEffectDef(type, 0, EffectTarget.User, category), 0, 0);
                    if (CanEnhance(deck[i], probe)) choices.Add((type, category));
                }
            }
            if (choices.Count > 0) targets.Add((i, choices));
        }

        if (targets.Count == 0) return null;

        var (index, options) = targets[random.Next(targets.Count)];
        var (pickedType, pickedCategory) = options[random.Next(options.Count)];
        CardUpgrade upgrade = RollUpgrade(pickedType, pickedCategory, library, random);

        return CanEnhance(deck[index], upgrade) ? (index, upgrade) : null;
    }

    private static CardUpgrade RollUpgrade(EffectType type, EffectCategory category, CardLibrary library, Random random)
    {
        EffectPrice price = library.PriceOf(type);
        int unit = Math.Max(1, price.MagnitudeUnit);
        int steps = Math.Max(0, (price.MagnitudeMax - price.MagnitudeMin) / unit) + 1;
        int magnitude = price.MagnitudeMin + unit * random.Next(steps);

        EffectTarget target = Polarity(type) switch
        {
            TargetSide.Positive => magnitude > 0 ? EffectTarget.User : EffectTarget.Opponent,
            TargetSide.Negative => magnitude > 0 ? EffectTarget.Opponent : EffectTarget.User,
            _ => random.Next(2) == 0 ? EffectTarget.User : EffectTarget.Opponent,
        };

        int budget = (int)Math.Floor(price.Price * (DoesntUseMagnitude(type) ? 1 : magnitude));
        if (category == EffectCategory.Continuous) budget *= 5;

        int sign = budget < 0 ? -1 : 1;
        int cost = 0, cooldown = 0;
        for (int i = 0; i < Math.Abs(budget); i++)
        {
            if (random.NextDouble() < 2.0 / 3.0) cooldown += sign;
            else cost += sign;
        }

        return new CardUpgrade(new CardEffectDef(type, magnitude, target, category), cost, cooldown);
    }

    // =========================================================
    // Effect 성질 (Unity Effect.TargetPolarity / DoesntUseMagnitude)
    // =========================================================

    private enum TargetSide { Positive, Negative, Neutral }

    private static TargetSide Polarity(EffectType type) => type switch
    {
        EffectType.Attack or EffectType.Weak or EffectType.Vulnerable or EffectType.PoseBreak or EffectType.AddDump
            or EffectType.Burning or EffectType.EnergyDrain => TargetSide.Negative,
        EffectType.Defend or EffectType.EnergyHeal or EffectType.Preserve or EffectType.Disposable or EffectType.Strength
            or EffectType.Harden or EffectType.Guard or EffectType.TimeSkip or EffectType.Quicker or EffectType.CostToCooldown
            or EffectType.CooldownToCost or EffectType.DivideCooldown or EffectType.DefenseToCooldown => TargetSide.Positive,
        _ => TargetSide.Neutral,
    };

    private static bool DoesntUseMagnitude(EffectType type) =>
        type is EffectType.Disposable or EffectType.Preserve or EffectType.DivideCooldown or EffectType.DefenseToCooldown;

    private static EffectCategory AppliedCategory(CardEffectDef effect) => new CardEffect(effect).GetAppliedCategory();
}
