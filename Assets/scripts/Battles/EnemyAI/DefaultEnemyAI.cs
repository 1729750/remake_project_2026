using System;
using System.Collections.Generic;
using UnityEngine;

// 코어 아이디어("상대 카드를 보고 반응한다")에 맞춘 기본 AI. 큐의 남은 쿨다운으로 앞으로 몇 번째 턴
// 종료에 무엇이 떨어지는지 타임라인을 만들고, 그 위에서 각 행동의 가치를 시뮬레이션해 고른다.
//
// - 방어: 상대 큐의 공격이 떨어지는 시점에 아직 살아 있을 방어 카드만 가치가 있다(쿨다운 ≥ 남은 턴).
//   막아낼 수 있는 "추가로 막는 피해"만 가치로 치므로, 위협이 없으면 방어를 미리 깔지 않는다.
// - 공격: 상대 방어가 사라진 뒤에 떨어지도록 시점을 고르고, 시전 시간이 길수록 상대가 반응할 여지를
//   할인한다(반응하면 상대도 그만큼 코스트를 쓰므로 일부만 깎는다).
// - 버프/디버프: 효과가 유지되는 동안 실제로 떨어지는 공격들이 얼마나 더/덜 들어가는지로 평가한다 —
//   혼자서는 의미 없는 "짧고 강한" 버프는 곧 떨어질 공격이 있을 때만 쓴다.
// - 막을 카드가 없는데 이번 턴 종료에 큰 피해가 떨어지면, 턴이 끝나갈 때 방어 자세를 취한다.
// - 곧 큰 공격이 오는데 막을 카드를 아직 못 내면, 그 카드를 낼 코스트를 남겨 둔다.
[CreateAssetMenu(fileName = "DefaultEnemyAI", menuName = "Scriptable Objects/Enemy AI/Default")]
public class DefaultEnemyAI : EnemyAIBehavior
{
    [SerializeField, Tooltip("코스트 1의 가치를 피해량으로 환산한 값. 카드 점수에서 코스트 × 이 값을 뺀다.")]
    private float energyValue = 3f;
    [SerializeField, Tooltip("카드를 내기 위한 최소 점수.")]
    private float playThreshold = 0.5f;
    [SerializeField, Tooltip("턴 남은 시간이 이 비율 이하일 때만 방어 자세를 새로 취한다(카드를 내면 자세가 풀리므로).")]
    private float guardLateTurnRatio = 0.4f;
    [SerializeField, Tooltip("상대가 새로 낸 카드를 알아차리기까지 걸리는 시간(초).")]
    private float reactionDelay = 0.4f;

    private const float HandAttackWeight = 0.4f;
    private const float LethalBonus = 40f;
    private const float PoisonStackValue = 6f;

    protected struct Hit
    {
        public int Turn;
        public float Damage;
        public bool Blockable;
        public float Weight;
    }

    protected class Shield
    {
        public int Expiry;
        public float Amount;
    }

    // 매 Decide마다 새로 만드는 판단 근거. ScoreCard/ChooseBestCardIndex(하위 클래스 포함)가 공유한다.
    protected class Context
    {
        public readonly List<Hit> MyHits = new List<Hit>();        // 내 큐의 공격 → 상대
        public readonly List<Hit> MyHandHits = new List<Hit>();    // 손패의 공격(지금 낸다고 가정)
        public readonly List<Hit> Threats = new List<Hit>();       // 상대 큐의 공격 → 나
        public readonly List<Shield> MyShields = new List<Shield>();
        public readonly List<Shield> OppShields = new List<Shield>();
        public int HandDefendCount;
        public float Urgency = 1f;
        public int Reserve;
    }

    [NonSerialized] private readonly Dictionary<CardInstance, float> _firstSeen = new Dictionary<CardInstance, float>();
    [NonSerialized] protected Context Ctx;

    public override int Decide(in BattleSnapshot snapshot)
    {
        Ctx = BuildContext(snapshot);

        if (snapshot.SelfQueueHasFreeSlot)
        {
            int bestIndex = ChooseBestCardIndex(snapshot);
            if (bestIndex >= 0) return bestIndex;
        }

        return DecideGuard(snapshot);
    }

    // 지금 코스트로 낼 수 있는 손패 카드들 중 ScoreCard가 가장 높게 평가한 카드의 인덱스를 고른다.
    // 곧 떨어질 큰 공격을 막을 카드를 위해 남겨 둘 코스트(Reserve)는 방어가 아닌 카드에 쓰지 않는다.
    protected virtual int ChooseBestCardIndex(in BattleSnapshot snapshot)
    {
        CardInstance[] hand = snapshot.SelfHand;
        int bestIndex = -1;
        float bestScore = playThreshold;

        for (int i = 0; i < hand.Length; i++)
        {
            CardInstance card = hand[i];
            if (card == null) continue;
            int cost = PreviewCost(snapshot, card);
            if (cost > snapshot.SelfCost) continue;
            if (!ProvidesDefense(card) && snapshot.SelfCost - cost < Ctx.Reserve) continue;

            float score = ScoreCard(snapshot, card);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    protected static int PreviewCost(in BattleSnapshot snapshot, CardInstance card) => card.GetCost();

    // 이 카드를 지금 냈을 때의 가치(피해량 환산) − 코스트 × energyValue.
    protected float ScoreCard(in BattleSnapshot snapshot, CardInstance card)
    {
        if (snapshot.Self == null || snapshot.Opponent == null) return 0f;
        Ctx ??= BuildContext(snapshot);

        int duration = Mathf.Max(card.GetCooldown(), 1);
        float value = 0f;
        List<CardEffect> effects = card.GetEffects();

        // 효과 없는 카드(쓰레기)는 손패를 막고 있으므로, 큐에 여유가 있으면 치워버리는 것도 가치가 있다.
        if (effects.Count == 0)
            value = CountFreeSlots(snapshot.SelfQueue) >= 2 ? 4f : 0f;

        foreach (CardEffect cardEffect in effects)
            value += ScoreEffect(snapshot, card, cardEffect, duration) * GetEffectWeight(cardEffect.GetEffect().GetEffectType());

        return value - card.GetCost() * EnergyPenalty(snapshot);
    }

    // 특수 AI가 성향(방어/상태이상/공격)에 따라 EffectType별 점수 배율을 바꾸기 위한 훅. 기본 AI는 전부 1배.
    protected virtual float GetEffectWeight(EffectType type) => 1f;

    // 코스트 10 이상이면 회복량이 줄고 15에서 넘치므로, 많이 쥐고 있을수록 쓰는 부담이 적다.
    private float EnergyPenalty(in BattleSnapshot snapshot) =>
        snapshot.SelfCost >= 10 ? energyValue * 0.5f : energyValue;

    // ───────────────────────── 상황 파악 ─────────────────────────

    private Context BuildContext(in BattleSnapshot snapshot)
    {
        var ctx = new Context();
        CharacterManager self = snapshot.Self;
        CharacterManager opponent = snapshot.Opponent;
        if (self == null || opponent == null) return ctx;

        ctx.Urgency = 1f + (1f - Mathf.Clamp01((float)snapshot.SelfHealth / Mathf.Max(snapshot.SelfMaxHealth, 1)));
        UpdateNoticed(snapshot.OpponentQueue);

        CollectHits(self, opponent, snapshot.SelfQueue, ctx.MyHits, 1f, useCooldownLeft: true, noticedOnly: false);
        CollectHits(opponent, self, snapshot.OpponentQueue, ctx.Threats, 1f, useCooldownLeft: true, noticedOnly: true);
        CollectHits(self, opponent, snapshot.SelfHand, ctx.MyHandHits, HandAttackWeight, useCooldownLeft: false, noticedOnly: false);

        CollectShields(snapshot.SelfQueue, snapshot.SelfDefense, ctx.MyShields, noticedOnly: false);
        CollectShields(snapshot.OpponentQueue, snapshot.OpponentDefense, ctx.OppShields, noticedOnly: true);

        foreach (CardInstance card in snapshot.SelfHand)
            if (card != null && ProvidesDefense(card)) ctx.HandDefendCount++;

        ctx.Reserve = ComputeReserve(snapshot, ctx);
        return ctx;
    }

    // 상대 큐에 새로 들어온 카드는 reactionDelay초가 지나야 판단에 반영한다(사람처럼 반응할 시간을 둔다).
    private void UpdateNoticed(CardInstance[] opponentQueue)
    {
        var stale = new List<CardInstance>();
        foreach (CardInstance card in _firstSeen.Keys)
            if (Array.IndexOf(opponentQueue, card) < 0) stale.Add(card);
        foreach (CardInstance card in stale) _firstSeen.Remove(card);

        foreach (CardInstance card in opponentQueue)
            if (card != null && !_firstSeen.ContainsKey(card)) _firstSeen[card] = Time.time;
    }

    private bool IsNoticed(CardInstance card) =>
        _firstSeen.TryGetValue(card, out float seenAt) && Time.time - seenAt >= reactionDelay;

    private void CollectHits(CharacterManager attacker, CharacterManager defender, CardInstance[] cards,
        List<Hit> into, float weight, bool useCooldownLeft, bool noticedOnly)
    {
        foreach (CardInstance card in cards)
        {
            if (card == null || (noticedOnly && !IsNoticed(card))) continue;
            int turn = Mathf.Max(useCooldownLeft ? card.GetCooldownLeft() : card.GetCooldown(), 1);
            foreach (CardEffect cardEffect in card.GetEffects())
            {
                if (!cardEffect.IsAttackEffect() || cardEffect.GetTarget(attacker) != defender) continue;
                AttackEffect probe = (AttackEffect)Effect.Create(cardEffect.GetEffect().GetEffectType(), 0);
                float damage = PreviewMagnitude(attacker, defender, cardEffect);
                into.Add(new Hit { Turn = turn, Damage = damage, Blockable = probe.ConsumesDefense, Weight = weight });
            }
        }
    }

    private static void CollectShields(CardInstance[] queue, int totalDefense, List<Shield> into, bool noticedOnly)
    {
        int fromCards = 0;
        foreach (CardInstance card in queue)
        {
            if (card == null) continue;
            foreach (CardEffect cardEffect in card.GetEffects())
            {
                if (cardEffect.GetEffect().GetEffectType() != EffectType.Defend) continue;
                int remaining = card.GetQueuedMagnitude(cardEffect);
                fromCards += remaining;
                if (remaining > 0)
                    into.Add(new Shield { Expiry = Mathf.Max(card.GetCooldownLeft(), 1), Amount = remaining });
            }
        }
        // LastStand 등이 큐 카드와 무관하게 쌓아준 보너스 방어도는 만료 시점을 알 수 없어 계속 남는다고 본다.
        int bonus = totalDefense - fromCards;
        if (bonus > 0)
            into.Add(new Shield { Expiry = int.MaxValue, Amount = bonus });
    }

    // 곧 떨어질 큰 공격을 막을 수 있는 방어 카드가 손패에 있는데 지금은 코스트가 모자라면,
    // 그 공격이 떨어지는 턴까지의 회복량을 감안해 남겨 둬야 할 코스트.
    private static int ComputeReserve(in BattleSnapshot snapshot, Context ctx)
    {
        int reserve = 0;
        foreach (Hit threat in ctx.Threats)
        {
            if (!threat.Blockable) continue;
            float uncovered = UncoveredOf(threat, ctx.Threats, ctx.MyShields);
            if (uncovered < 10f) continue;

            int cheapest = int.MaxValue;
            foreach (CardInstance card in snapshot.SelfHand)
                if (card != null && ProvidesDefense(card)) cheapest = Mathf.Min(cheapest, card.GetCost());
            if (cheapest == int.MaxValue) continue;

            reserve = Mathf.Max(reserve, cheapest - 2 * (threat.Turn - 1));
        }
        return reserve;
    }

    // ───────────────────────── 방어 자세 ─────────────────────────

    private int DecideGuard(in BattleSnapshot snapshot)
    {
        float uncoveredNow = 0f;
        foreach (Hit threat in Ctx.Threats)
            if (threat.Turn <= 1 && threat.Blockable)
                uncoveredNow += UncoveredOf(threat, Ctx.Threats, Ctx.MyShields);

        // 방어 자세는 피해를 절반으로 줄이는 대신 다음 턴 코스트 회복이 1 줄어든다.
        bool wantGuard = uncoveredNow * 0.5f * Ctx.Urgency > energyValue;

        if (wantGuard && !snapshot.SelfIsGuarding && snapshot.RemainingTurnTime <= snapshot.TurnDuration * guardLateTurnRatio)
            return ActionDefend;
        if (!wantGuard && snapshot.SelfIsGuarding)
            return ActionStopDefend;
        return ActionNone;
    }

    // ───────────────────────── 효과별 가치 ─────────────────────────

    private float ScoreEffect(in BattleSnapshot snapshot, CardInstance card, CardEffect cardEffect, int duration)
    {
        CharacterManager self = snapshot.Self;
        CharacterManager target = cardEffect.GetTarget(self);
        bool onSelf = target == self;
        EffectType type = cardEffect.GetEffect().GetEffectType();
        int magnitude = cardEffect.GetEffect().GetMagnitude();
        // 스킬은 큐에 들어가는 지금부터 duration번째 턴 종료까지 유지된다. 턴 시작마다 발동하는 효과는
        // 그 사이에 오는 턴 시작(duration - 1번)에만 발동한다.
        int turnStartsInQueue = duration - 1;

        if (cardEffect.IsAttackEffect())
            return onSelf ? -PreviewMagnitude(self, self, cardEffect) : ScoreAttack(snapshot, cardEffect, duration);

        switch (type)
        {
            case EffectType.Burning:
                // 방어를 무시하고 큐에 있는 동안 매 턴 종료마다 들어간다 — 막을 수도, 반응할 수도 없다.
                return (onSelf ? -1f : 1f) * BurningEffect.DamagePerTurn(magnitude) * duration;

            case EffectType.Defend:
                return onSelf ? ScoreDefend(snapshot, cardEffect, duration) : -magnitude;

            case EffectType.LastStand:
            {
                int hitsInWindow = 0;
                foreach (Hit threat in Ctx.Threats)
                    if (threat.Turn <= duration) hitsInWindow++;
                float value = magnitude * Mathf.Max(0, hitsInWindow - 1) * 0.8f * Ctx.Urgency;
                return onSelf ? value : -value;
            }

            case EffectType.Strength:
            case EffectType.Overcharge:
            case EffectType.Weak:
                return ScoreOutgoingModifier(type, magnitude, onSelf, duration);

            case EffectType.Vulnerable:
            case EffectType.PoseBreak:
            case EffectType.Guard:
                return ScoreIncomingModifier(type, onSelf, duration);

            case EffectType.Harden:
            {
                float threatFactor = Ctx.Threats.Count > 0 ? 1f : 0.3f;
                float value = magnitude * Ctx.HandDefendCount * 0.4f * threatFactor;
                return onSelf ? value : -value * 0.5f;
            }

            case EffectType.EnergyHeal:
            {
                // 코스트 상한(15)을 넘는 회복은 버려진다.
                int headroom = Mathf.Max(0, CharacterManager.MaxCost - (snapshot.SelfCost - card.GetCost()));
                float effective = Mathf.Min(magnitude * turnStartsInQueue, headroom);
                float value = effective * energyValue * 0.8f;
                return onSelf ? value : -value;
            }

            case EffectType.EnergyDrain:
                // 상대 기본 회복이 턴당 2라 그 이상은 깎을 게 거의 없다.
                return (onSelf ? -1f : 1f) * Mathf.Min(magnitude, 2) * turnStartsInQueue * energyValue * 0.6f;

            case EffectType.AddDump:
                return (onSelf ? -1f : 1f) * (2f + 0.4f * magnitude);

            case EffectType.Quicker:
            {
                int instantCardsQueued = 0;
                foreach (CardInstance queued in snapshot.SelfQueue)
                    if (queued != null && !queued.IsContinuousCard()) instantCardsQueued++;
                return (onSelf ? 1f : -1f) * 2f * Mathf.Min(duration, 3) * Mathf.Max(instantCardsQueued, 1);
            }

            default:
                return 0f;
        }
    }

    private float ScoreAttack(in BattleSnapshot snapshot, CardEffect cardEffect, int duration)
    {
        CharacterManager self = snapshot.Self;
        CharacterManager opponent = snapshot.Opponent;
        AttackEffect probe = (AttackEffect)Effect.Create(cardEffect.GetEffect().GetEffectType(), 0);
        float damage = PreviewMagnitude(self, opponent, cardEffect);

        var withThis = new List<Hit>(Ctx.MyHits)
        {
            new Hit { Turn = duration, Damage = damage, Blockable = probe.ConsumesDefense, Weight = 1f },
        };
        float before = Uncovered(Ctx.MyHits, Ctx.OppShields);
        float after = Uncovered(withThis, Ctx.OppShields);
        float gain = after - before;

        // 상대가 반응해 막으면 이 피해는 없어지지만 상대도 그만큼 코스트를 쓰므로, 반응 확률의 일부만 깎는다.
        float value = gain * (1f - 0.4f * ReactChance(snapshot, duration, damage));
        if (probe is VenomousAttackEffect && gain > 0f)
            value += PoisonStackValue;
        if (after >= snapshot.OpponentHealth && before < snapshot.OpponentHealth)
            value += LethalBonus;
        return value;
    }

    // 시전 시간이 길수록, 그리고 상대가 그 사이에 막을 코스트를 마련할 수 있을수록 반응당하기 쉽다.
    private static float ReactChance(in BattleSnapshot snapshot, int castTurns, float damage)
    {
        if (castTurns <= 1) return 0f;
        bool affordable = snapshot.OpponentCost + 2 * (castTurns - 1) >= damage / 4.5f;
        return affordable ? Mathf.Min(0.5f, 0.1f * (castTurns - 1)) : 0.05f * (castTurns - 1);
    }

    private float ScoreDefend(in BattleSnapshot snapshot, CardEffect cardEffect, int duration)
    {
        float amount = PreviewMagnitude(snapshot.Self, snapshot.Self, cardEffect);
        var withThis = new List<Shield>(Ctx.MyShields) { new Shield { Expiry = duration, Amount = amount } };

        float before = Uncovered(Ctx.Threats, Ctx.MyShields);
        float after = Uncovered(Ctx.Threats, withThis);
        float value = (before - after) * Ctx.Urgency;
        if (before >= snapshot.SelfHealth && after < snapshot.SelfHealth)
            value += LethalBonus;
        return value;
    }

    // 공격하는 쪽에 붙는 보정(Strength +N, Overcharge ×2, Weak ×0.8). 내게 붙으면 내 공격이, 상대에게
    // 붙으면 상대 공격이 바뀐다 — 카드가 큐에 머무는 동안(duration번째 턴 종료까지) 떨어지는 공격만 센다.
    private float ScoreOutgoingModifier(EffectType type, int magnitude, bool onSelf, int duration)
    {
        Func<float, float> modify = type switch
        {
            EffectType.Strength => d => d + magnitude,
            EffectType.Overcharge => d => d * 2f,
            _ => d => d * 0.8f, // Weak
        };
        return onSelf ? MyDamageGain(modify, duration) : ThreatReduction(modify, duration);
    }

    // 맞는 쪽에 붙는 보정(Vulnerable ×1.2, PoseBreak ×2, Guard ×0.5). 상대에게 붙으면 내 공격이,
    // 내게 붙으면 상대 공격이 바뀐다.
    private float ScoreIncomingModifier(EffectType type, bool onSelf, int duration)
    {
        Func<float, float> modify = type switch
        {
            EffectType.Vulnerable => d => d * 1.2f,
            EffectType.PoseBreak => d => d * 2f,
            _ => d => d * 0.5f, // Guard
        };
        return onSelf ? ThreatReduction(modify, duration) : MyDamageGain(modify, duration);
    }

    private float MyDamageGain(Func<float, float> modify, int duration)
    {
        float gain = Uncovered(ApplyInWindow(Ctx.MyHits, modify, duration), Ctx.OppShields)
                     - Uncovered(Ctx.MyHits, Ctx.OppShields);

        // 손패의 공격은 지금 바로 낸다면 window 안에 떨어지는지만 보고, 실제로 낼지 모르니 가중치를 낮춘다.
        foreach (Hit hit in Ctx.MyHandHits)
            if (hit.Turn <= duration)
                gain += (Mathf.Max(0f, modify(hit.Damage)) - hit.Damage) * hit.Weight;
        return gain;
    }

    private float ThreatReduction(Func<float, float> modify, int duration)
    {
        float before = Uncovered(Ctx.Threats, Ctx.MyShields);
        float after = Uncovered(ApplyInWindow(Ctx.Threats, modify, duration), Ctx.MyShields);
        return (before - after) * Ctx.Urgency;
    }

    // 큐에 들어가는 지금부터 duration번째 턴 종료까지 떨어지는 공격에만 보정을 적용한 사본.
    private static List<Hit> ApplyInWindow(List<Hit> hits, Func<float, float> modify, int duration)
    {
        var result = new List<Hit>(hits.Count);
        foreach (Hit hit in hits)
        {
            Hit modified = hit;
            if (hit.Turn <= duration) modified.Damage = Mathf.Max(0f, modify(hit.Damage));
            result.Add(modified);
        }
        return result;
    }

    // ───────────────────────── 시뮬레이션 ─────────────────────────

    // 공격들을 떨어지는 순서대로 방어에 부딪혀, 막히지 않고 들어가는 피해(가중치 반영)의 합.
    // 방어는 그 공격이 떨어지는 턴 종료까지 살아 있어야 하고(Expiry ≥ Turn), 먼저 끝나는 것부터 쓴다.
    // 같은 턴 종료에 끝나는 방어도 그 턴의 공격은 막는다(BattleManager.ResolveTurnEnd 참고).
    private static float Uncovered(List<Hit> hits, List<Shield> shields)
    {
        var pool = new List<Shield>(shields.Count);
        foreach (Shield s in shields) pool.Add(new Shield { Expiry = s.Expiry, Amount = s.Amount });
        pool.Sort((a, b) => a.Expiry.CompareTo(b.Expiry));

        var ordered = new List<Hit>(hits);
        ordered.Sort((a, b) => a.Turn.CompareTo(b.Turn));

        float total = 0f;
        foreach (Hit hit in ordered)
            total += Absorb(hit, pool) * hit.Weight;
        return total;
    }

    // hits 전체를 순서대로 처리했을 때 그중 target 공격 하나가 막히지 않고 들어가는 피해.
    private static float UncoveredOf(Hit target, List<Hit> hits, List<Shield> shields)
    {
        var pool = new List<Shield>(shields.Count);
        foreach (Shield s in shields) pool.Add(new Shield { Expiry = s.Expiry, Amount = s.Amount });
        pool.Sort((a, b) => a.Expiry.CompareTo(b.Expiry));

        var ordered = new List<Hit>(hits);
        ordered.Sort((a, b) => a.Turn.CompareTo(b.Turn));

        foreach (Hit hit in ordered)
        {
            float through = Absorb(hit, pool);
            if (hit.Turn == target.Turn && Mathf.Approximately(hit.Damage, target.Damage) && hit.Blockable == target.Blockable)
                return through;
        }
        return target.Damage;
    }

    private static float Absorb(Hit hit, List<Shield> pool)
    {
        if (!hit.Blockable) return hit.Damage;
        float remaining = hit.Damage;
        foreach (Shield shield in pool)
        {
            if (remaining <= 0f) break;
            if (shield.Expiry < hit.Turn || shield.Amount <= 0f) continue;
            float used = Mathf.Min(shield.Amount, remaining);
            shield.Amount -= used;
            remaining -= used;
        }
        return remaining;
    }

    // ───────────────────────── 보조 ─────────────────────────

    // CardInstance.RefreshDisplay와 같은 경로(공격자 쪽 OnApplyingOther → 맞는 쪽 OnAppliedOther)로
    // 버프/디버프를 반영한 최종 magnitude. 원본 CardEffect를 오염시키지 않도록 복제본에서 계산한다.
    private static float PreviewMagnitude(CharacterManager actor, CharacterManager target, CardEffect cardEffect)
    {
        var copy = new CardEffect(cardEffect.GetEffect(), cardEffect.GetEffectTarget());
        copy.SetAppliedCategory(cardEffect.GetAppliedCategory());

        Effect[] actorEffects = actor.GetEffectPrioritize();
        for (int i = actorEffects.Length - 1; i >= 0; i--)
            actorEffects[i].OnApplyingOther(actor, copy, false);
        foreach (Effect targetEffect in target.GetEffectPrioritize())
            targetEffect.OnAppliedOther(target, copy, false);

        return copy.GetMagnitude();
    }

    private static bool ProvidesDefense(CardInstance card)
    {
        foreach (CardEffect cardEffect in card.GetEffects())
        {
            EffectType type = cardEffect.GetEffect().GetEffectType();
            if (type == EffectType.Defend || type == EffectType.LastStand || type == EffectType.Guard) return true;
        }
        return false;
    }

    private static int CountFreeSlots(CardInstance[] queue)
    {
        int free = 0;
        foreach (CardInstance card in queue)
            if (card == null) free++;
        return free;
    }
}
