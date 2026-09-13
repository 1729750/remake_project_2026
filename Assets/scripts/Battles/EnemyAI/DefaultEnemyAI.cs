using UnityEngine;

// 기존에 CharacterManager.UpdateAi에 하드코딩되어 있던 판단 로직을 그대로 옮긴 기본 AI.
// enemyData(CharacterData)에 별도 EnemyAIBehavior가 지정되지 않은 모든 적이 이걸 기본값으로 쓴다.
[CreateAssetMenu(fileName = "DefaultEnemyAI", menuName = "Scriptable Objects/Enemy AI/Default")]
public class DefaultEnemyAI : EnemyAIBehavior
{
    public override int Decide(in BattleSnapshot snapshot)
    {
        if (!snapshot.SelfQueueHasFreeSlot) return ActionNone;

        int bestIndex = ChooseBestCardIndex(snapshot);
        return bestIndex >= 0 ? bestIndex : ActionNone;
    }

    // 지금 코스트로 낼 수 있는 손패 카드들 중 ScoreCard가 가장 높게 평가한 카드의 인덱스를 고른다.
    // 점수가 0 이하(도움이 안 되거나 자충수인 카드밖에 없음)면 아무것도 내지 않고 다음 기회로 미룬다.
    protected virtual int ChooseBestCardIndex(in BattleSnapshot snapshot)
    {
        CardInstance[] hand = snapshot.SelfHand;
        int bestIndex = -1;
        float bestScore = 0f;

        for (int i = 0; i < hand.Length; i++)
        {
            CardInstance card = hand[i];
            if (card == null) continue;
            if (PreviewCost(snapshot, card) > snapshot.SelfCost) continue;

            float score = ScoreCard(snapshot, card);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    // QueueCard가 실제로 쓰는 것과 같은 preview 경로(버프로 바뀐 실제 코스트)로 계산한다 —
    // 원본 cost만 보면 EnergyDrain/CostToCooldown류로 실제 코스트가 달라졌을 때 판단이 어긋난다.
    protected static int PreviewCost(in BattleSnapshot snapshot, CardInstance card)
    {
        CardInstance preview = card.Clone();
        preview.ResolveUse(snapshot.Self, snapshot.SelfQueue, false);
        return preview.GetCost();
    }

    // 카드의 각 CardEffect를 CardInstance.RefreshDisplay와 동일한 경로로 미리 계산해(현재 버프
    // 반영한 최종 magnitude) 점수를 합산한다.
    protected static float ScoreCard(in BattleSnapshot snapshot, CardInstance card)
    {
        CharacterManager self = snapshot.Self;
        CharacterManager opponent = snapshot.Opponent;
        if (self == null || opponent == null) return 0f;

        CardInstance preview = card.Clone();
        preview.ResolveUse(self, snapshot.SelfQueue, false);

        float total = 0f;
        foreach (CardEffect cardEffect in preview.GetEffects())
        {
            cardEffect.Reset();
            Effect[] ownerEffects = self.GetEffectPrioritize();
            for (int i = ownerEffects.Length - 1; i >= 0; i--)
                ownerEffects[i].OnApplyingOther(self, cardEffect, false);

            CharacterManager target = cardEffect.GetTarget(self);
            Effect[] targetEffects = target.GetEffectPrioritize();
            foreach (Effect targetEffect in targetEffects)
                targetEffect.OnAppliedOther(target, cardEffect, false);

            total += ScoreCardEffect(snapshot, cardEffect, target, opponent);
        }
        return total;
    }

    // CardEffect 하나가 실제로 적용됐을 때 이 캐릭터(AI) 입장에서 얼마나 가치 있는지 점수를 매긴다.
    // 방향(자신에게 좋은지/나쁜지)은 Effect.TargetPolarity + 실제 target이 자신인지로 정하고,
    // 크기는 타입별 가중치(및 상황: 상대의 임박한 공격, 자신의 공격 수단 보유 여부)로 정한다.
    private static float ScoreCardEffect(in BattleSnapshot snapshot, CardEffect cardEffect, CharacterManager target, CharacterManager opponent)
    {
        EffectType type = cardEffect.GetEffect().GetEffectType();
        int magnitude = cardEffect.GetMagnitude();
        bool targetsSelf = target == snapshot.Self;

        // Attack은 즉발 데미지라 처치 가능 여부까지 바로 계산할 수 있어 별도로 다룬다.
        if (type == EffectType.Attack)
        {
            float value = magnitude;
            if (magnitude >= target.GetDefense() + target.GetHealth())
                value += 40f; // 지금 내면 상대를 처치할 수 있으면 최우선으로 취급한다.
            return targetsSelf ? -value : value;
        }

        bool opponentAttackIncoming = OpponentHasImminentAttack(snapshot);
        bool haveAttackPressure = HasAttackPressure(snapshot);
        float importance = MagnitudeImportance(type, magnitude, opponentAttackIncoming, haveAttackPressure);

        Effect runtimeEffect = Effect.Create(type, magnitude);
        float sign = runtimeEffect.TargetPolarity switch
        {
            EffectTargetPolarity.Positive => targetsSelf ? 1f : -1f,
            EffectTargetPolarity.Negative => targetsSelf ? -1f : 1f,
            _ => 1f,
        };

        return importance * sign;
    }

    // 타입별 magnitude 1당 가중치. 상황(상대의 공격이 임박했는지/내가 공격 수단을 갖고 있는지)에
    // 따라 방어형·시너지형 효과의 가치를 올린다.
    private static float MagnitudeImportance(EffectType type, int magnitude, bool opponentAttackIncoming, bool haveAttackPressure)
    {
        switch (type)
        {
            case EffectType.Defend:        return magnitude * (opponentAttackIncoming ? 1.5f : 0.7f);
            case EffectType.Guard:         return magnitude * (opponentAttackIncoming ? 6f : 1.5f);
            case EffectType.Vulnerable:    return magnitude * (haveAttackPressure ? 8f : 2.5f);
            case EffectType.Weak:          return magnitude * (opponentAttackIncoming ? 8f : 2.5f);
            case EffectType.PoseBreak:     return magnitude * 6f;
            case EffectType.Strength:      return magnitude * (haveAttackPressure ? 6f : 2f);
            case EffectType.Harden:        return magnitude * 3f;
            case EffectType.EnergyHeal:    return magnitude * 3f;
            case EffectType.EnergyDrain:   return magnitude * 4f;
            case EffectType.Burning:       return magnitude * 3f;
            case EffectType.AddDump:       return magnitude * 2f;
            case EffectType.Quicker:       return magnitude * 4f;
            case EffectType.TimeSkip:      return magnitude * 3f;
            // Preserve/Disposable/CostToCooldown/CooldownToCost/DivideCooldown/DefenseToCooldown처럼
            // 세부 평가를 만들지 않은 나머지 타입 — 완전히 무시하지는 않도록 소폭의 기본 점수만 준다.
            default:                       return Mathf.Max(magnitude, 1) * 2f;
        }
    }

    // 상대 큐에 곧(2턴 이내) 발동될 Attack 카드가 있는지. 방어 카드 가치 판단에 쓰기 위해 여유를 좀 더 둔다(cooldownLeft <= 2).
    private static bool OpponentHasImminentAttack(in BattleSnapshot snapshot)
    {
        foreach (CardInstance queued in snapshot.OpponentQueue)
        {
            if (queued == null || queued.GetCooldownLeft() > 2) continue;
            foreach (CardEffect cardEffect in queued.GetEffects())
                if (cardEffect.GetEffect().GetEffectType() == EffectType.Attack)
                    return true;
        }
        return false;
    }

    // 자신의 큐/손패에 Attack 카드가 있는지 — Strength/Vulnerable처럼 공격력에 얹는 효과가
    // 지금 당장 써먹을 데가 있는지 판단하는 데 쓴다.
    private static bool HasAttackPressure(in BattleSnapshot snapshot)
    {
        foreach (CardInstance queued in snapshot.SelfQueue)
        {
            if (queued == null) continue;
            foreach (CardEffect cardEffect in queued.GetEffects())
                if (cardEffect.GetEffect().GetEffectType() == EffectType.Attack)
                    return true;
        }
        foreach (CardInstance card in snapshot.SelfHand)
        {
            if (card == null) continue;
            foreach (CardEffect cardEffect in card.GetEffects())
                if (cardEffect.GetEffect().GetEffectType() == EffectType.Attack)
                    return true;
        }
        return false;
    }
}
