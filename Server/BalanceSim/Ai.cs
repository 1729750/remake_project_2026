namespace BalanceSim;

/// <summary>전투 AI: 턴 시작마다 반복해서 불린다. 손패 번호(0~3), -1 가드 토글, -2 리드로우, null = 이번 턴 끝.</summary>
public interface IBattleAi
{
    int? NextAction(Combatant self, Random random);
}

/// <summary>카드 선택 단계 AI: 선택지 번호, 또는 null = 건너뛰기.</summary>
public interface IDraftAi
{
    int? Choose(IReadOnlyList<CardDef> deck, PickOption[] options, Random random);
}

/// <summary>AI 한 명 = 카드 선택 방식 + 전투 방식.</summary>
public sealed record AiProfile(string Name, IDraftAi Draft, IBattleAi Battle)
{
    public static readonly IReadOnlyDictionary<string, AiProfile> All = new Dictionary<string, AiProfile>
    {
        ["random"] = new("random", new RandomDraft(), new RandomBattle()),
        ["default"] = new("default", new ValueDraft(explore: 0.25), new DefaultBattle(costEfficient: false)),
        ["greedy"] = new("greedy", new ValueDraft(explore: 0.25), new DefaultBattle(costEfficient: true)),
    };
}

// =========================================================
// 전투
// =========================================================

/// <summary>낼 수 있는 카드 중 아무거나. 낼 카드가 없으면 가끔 가드. (기준선)</summary>
public sealed class RandomBattle : IBattleAi
{
    public int? NextAction(Combatant self, Random random)
    {
        var playable = Enumerable.Range(0, Combatant.HandSize).Where(self.CanPlay).ToList();
        if (playable.Count > 0) return playable[random.Next(playable.Count)];
        if (!self.IsGuard && random.NextDouble() < 0.2) return Combatant.GuardAction;
        return null;
    }
}

/// <summary>
/// Unity 싱글의 DefaultEnemyAI를 옮긴 것: 큐에 빈칸이 있으면, 코스트가 되는 카드 중 효과 점수가 가장 높은 카드를 낸다.
/// costEfficient면 점수를 코스트로 나눈 효율로 고른다 ("greedy").
/// </summary>
public sealed class DefaultBattle(bool costEfficient) : IBattleAi
{
    public int? NextAction(Combatant self, Random random)
    {
        int best = -1;
        double bestScore = 0;

        for (int i = 0; i < Combatant.HandSize; i++)
        {
            if (!self.CanPlay(i)) continue;
            CardInstance card = self.Hand[i]!;
            double score = CardValue.InBattle(self, card);
            if (costEfficient) score /= Math.Max(1, self.Preview(card).cost);
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best >= 0 ? best : null;
    }
}

// =========================================================
// 카드 선택
// =========================================================

/// <summary>선택지 중 아무거나, 10%는 건너뛰기.</summary>
public sealed class RandomDraft : IDraftAi
{
    public int? Choose(IReadOnlyList<CardDef> deck, PickOption[] options, Random random) =>
        random.NextDouble() < 0.1 ? null : random.Next(options.Length);
}

/// <summary>
/// 덱 가치가 가장 많이 오르는 선택지. 추가 = 카드 가치 − 덱 평균(평균 이하 카드는 덱을 묽게 함),
/// 강화 = 강화 후 가치 − 강화 전 가치. 모두 손해면 건너뛴다. 코스트가 최대 코스트를 넘는 카드는 가치 0.
/// explore 확률로는 무작위 선택지를 고른다: AI가 싫어하는 카드도 덱에 들어가야 그 카드의 승률을 잴 수 있다.
/// </summary>
public sealed class ValueDraft(double explore) : IDraftAi
{
    public int? Choose(IReadOnlyList<CardDef> deck, PickOption[] options, Random random)
    {
        if (random.NextDouble() < explore) return random.Next(options.Length);

        double average = deck.Count > 0 ? deck.Average(CardValue.OfCard) : 0;
        int? best = null;
        double bestGain = 0;

        for (int i = 0; i < options.Length; i++)
        {
            PickOption option = options[i];
            double gain = option.Kind == "enhance"
                ? CardValue.OfCard(option.Card) - CardValue.OfCard(deck[option.Target])
                : CardValue.OfCard(option.Card) - average;
            if (gain > bestGain)
            {
                bestGain = gain;
                best = i;
            }
        }

        return best;
    }
}

// =========================================================
// 카드 가치 (DefaultEnemyAI.ScoreCard / MagnitudeImportance)
// =========================================================

public static class CardValue
{
    /// <summary>전투 중: 지금 버프/디버프와 상황(상대 공격 임박, 내 공격 준비)을 반영한 점수.</summary>
    public static double InBattle(Combatant self, CardInstance card)
    {
        var (_, magnitudes) = self.Preview(card);
        bool incoming = HasAttackSoon(self.Opponent);
        bool pressure = HasAttackPressure(self);
        int duration = Math.Max(card.Cooldown, 1);
        double total = 0;

        for (int i = 0; i < card.Effects.Count; i++)
        {
            CardEffect effect = card.Effects[i];
            Combatant target = effect.GetTarget(self);
            bool targetsSelf = target == self;
            int magnitude = magnitudes[i];

            if (effect.Type == EffectType.Attack)
            {
                double value = magnitude;
                if (magnitude >= target.GetDefense() + target.Health) value += 40; // 지금 내면 처치 가능하면 최우선
                total += targetsSelf ? -value : value;
                continue;
            }

            total += EffectScore(effect.Type, effect.GetAppliedCategory(), magnitude, targetsSelf, duration, incoming, pressure);
        }

        return total;
    }

    /// <summary>전투 밖(카드 선택): 상황 없이 효과 점수를 쿨다운 1턴당으로 나눈 값. 쓸 수 없는 카드는 0.</summary>
    public static double OfCard(CardDef card)
    {
        if (card.Cost > Combatant.MaxCost) return 0;

        double total = 0;
        int duration = Math.Max(card.Cooldown, 1);
        foreach (CardEffectDef effect in card.Effects)
        {
            bool targetsSelf = effect.Target == EffectTarget.User;
            EffectCategory category = new CardEffect(effect).GetAppliedCategory();
            total += effect.Type == EffectType.Attack
                ? (targetsSelf ? -effect.Magnitude : effect.Magnitude)
                : EffectScore(effect.Type, category, effect.Magnitude, targetsSelf, duration, incoming: false, pressure: true);
        }

        // 쿨다운이 길수록 자주 못 쓰고, 코스트가 클수록 다른 카드를 못 쓴다
        return total / (duration + card.Cost * 0.5);
    }

    private static double EffectScore(EffectType type, EffectCategory category, int magnitude, bool targetsSelf,
        int duration, bool incoming, bool pressure)
    {
        double importance = type switch
        {
            EffectType.Defend => magnitude * (incoming ? 1.5 : 0.7),
            EffectType.Guard => magnitude * (incoming ? 6 : 1.5),
            EffectType.Vulnerable => magnitude * (pressure ? 8 : 2.5),
            EffectType.Weak => magnitude * (incoming ? 8 : 2.5),
            EffectType.PoseBreak => magnitude * 6,
            EffectType.Strength => magnitude * (pressure ? 6 : 2),
            EffectType.Harden => magnitude * 3,
            EffectType.EnergyHeal => magnitude * 3,
            EffectType.EnergyDrain => magnitude * 4,
            EffectType.Burning => magnitude * 3,
            EffectType.AddDump => magnitude * 2,
            EffectType.Quicker => magnitude * 4,
            EffectType.TimeSkip => magnitude * 3,
            _ => Math.Max(magnitude, 1) * 2,
        };

        double sign = Enhance.PolaritySign(type) switch
        {
            1 => targetsSelf ? 1 : -1,
            -1 => targetsSelf ? -1 : 1,
            _ => 1,
        };

        double score = 0;
        if (category.HasFlag(EffectCategory.Instant)) score += importance * sign;
        if (category.HasFlag(EffectCategory.Continuous)) score += importance * Math.Sqrt(duration / 3.0) * sign;
        return score;
    }

    private static bool HasAttackSoon(Combatant side) =>
        side.Queue.Any(q => q != null && q.CooldownLeft <= 2 && q.Effects.Any(e => e.Type == EffectType.Attack));

    private static bool HasAttackPressure(Combatant self) =>
        self.Queue.Concat(self.Hand).Any(c => c != null && c.Effects.Any(e => e.Type == EffectType.Attack));
}
