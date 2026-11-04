namespace CardGameServer.Game;

// Unity Assets/scripts/Cards/effects/*.cs 를 그대로 옮긴 것. 규칙을 바꿀 때는 양쪽을 같이 본다.
// 사운드/로그/UI 갱신만 뺐고, 아래 두 군데만 의도적으로 고쳤다.
//   - VulnerableEffect: 미리보기(actualUse == false)에서도 magnitude가 줄던 버그 → 실제 적용 때만 줄인다.
//   - DivideCooldown: Mathf.RoundToInt(banker's rounding)과 같게 MidpointRounding.ToEven 사용.

public class Effect
{
    protected int magnitude;

    public EffectType Type { get; }
    public int Priority { get; }
    public bool IsInfinite { get; set; }
    public int Magnitude => magnitude;

    public virtual EffectCategory SupportedCategories => EffectCategory.Instant;

    public Effect(EffectType type, int magnitude, int priority = 0)
    {
        Type = type;
        this.magnitude = magnitude;
        Priority = priority;
    }

    public void AddMagnitude(int amount) => magnitude += amount;

    public static Effect Create(EffectType type, int magnitude) => type switch
    {
        EffectType.Attack => new AttackEffect(magnitude),
        EffectType.Defend => new DefendEffect(magnitude),
        EffectType.EnergyHeal => new EnergyHealEffect(magnitude),
        EffectType.Vulnerable => new VulnerableEffect(magnitude),
        EffectType.Weak => new WeakEffect(magnitude),
        EffectType.Strength => new StrengthEffect(magnitude),
        EffectType.Harden => new HardenEffect(magnitude),
        EffectType.Guard => new GuardEffect(magnitude),
        EffectType.PoseBreak => new PoseBreakEffect(magnitude),
        EffectType.DivideCooldown => new DivideCooldown(magnitude),
        EffectType.DefenseToCooldown => new DefenseToCooldown(magnitude),
        EffectType.TimeSkip => new TimeSkip(magnitude),
        EffectType.CostToCooldown => new CostToCooldown(magnitude),
        EffectType.CooldownToCost => new CooldownToCost(magnitude),
        EffectType.Burning => new BurningEffect(magnitude),
        EffectType.AddDump => new AddDumpEffect(magnitude),
        EffectType.Quicker => new QuickerEffect(magnitude),
        EffectType.EnergyDrain => new EnergyDrainEffect(magnitude),
        _ => new Effect(type, magnitude),
    };

    public static EffectCategory SupportedCategoriesOf(EffectType type) => Create(type, 0).SupportedCategories;

    public virtual void OnExpired(Combatant subject) { }
    public virtual void OnTurnStarted(Combatant subject) { }
    public virtual void OnTurnEnded(Combatant subject) { }
    public virtual void OnUse(Combatant subject, CardInstance self, bool actualUse) { }
    public virtual void OnUsingOther(Combatant subject, CardInstance card, bool actualUse) { }
    public virtual void OnApplyingOther(Combatant subject, CardEffect effect, bool actualUse) { }
    public virtual void OnAppliedOther(Combatant subject, CardEffect effect, bool actualUse) { }

    public virtual void OnApply(Combatant subject)
    {
        switch (Type)
        {
            case EffectType.Disposable:
            case EffectType.Preserve:
            case EffectType.CooldownToCost:
            case EffectType.DivideCooldown:
            case EffectType.CostToCooldown:
                return;
        }

        foreach (Effect existing in subject.Effects)
        {
            if (existing.Type == Type && !existing.IsInfinite)
            {
                existing.AddMagnitude(magnitude);
                return;
            }
        }

        subject.AddEffect(this);
    }

    public virtual void OnEnterQueue(Combatant subject, CardInstance self, CardEffect cardEffect)
    {
        Combatant target = cardEffect.GetTarget(subject);

        foreach (Effect existing in target.Effects)
        {
            if (existing.Type != Type || !existing.IsInfinite) continue;
            existing.AddMagnitude(magnitude);
            return;
        }

        IsInfinite = true;
        target.AddEffect(this);
    }

    public virtual void OnExitQueue(Combatant subject, CardInstance self, CardEffect cardEffect, int grantedMagnitude)
    {
        Combatant target = cardEffect.GetTarget(subject);

        foreach (Effect existing in target.Effects)
        {
            if (existing.Type != Type || !existing.IsInfinite) continue;
            existing.AddMagnitude(-grantedMagnitude);
            if (existing.Magnitude <= 0)
                target.RemoveEffectInstance(existing);
            return;
        }
    }
}

public sealed class AttackEffect(int magnitude) : Effect(EffectType.Attack, magnitude)
{
    public override void OnApply(Combatant subject) => subject.Attacked(magnitude);
}

public sealed class EnergyHealEffect(int magnitude) : Effect(EffectType.EnergyHeal, magnitude)
{
    public override void OnApply(Combatant subject) => subject.EnergyHeal(magnitude);
}

public sealed class AddDumpEffect(int magnitude) : Effect(EffectType.AddDump, magnitude)
{
    // 상대 덱에 쿨다운 magnitude, 코스트 1, 효과 없는 쓰레기 카드를 넣는다
    public override void OnApply(Combatant subject)
    {
        var dump = new CardDef("dump", 1, magnitude, CardType.Instant, Array.Empty<CardEffectDef>());
        subject.ReturnToDeck(new CardInstance(dump, subject));
    }
}

public sealed class TimeSkip(int magnitude) : Effect(EffectType.TimeSkip, magnitude)
{
    public override void OnApply(Combatant subject)
    {
        CardInstance? target = null;
        foreach (CardInstance? queued in subject.Queue)
        {
            if (queued == null) continue;
            if (target == null || queued.CooldownLeft > target.CooldownLeft)
                target = queued;
        }
        target?.ChangeCooldownLeft(-magnitude);
    }
}

public sealed class WeakEffect(int magnitude) : Effect(EffectType.Weak, magnitude, 1)
{
    public override void OnApplyingOther(Combatant subject, CardEffect effect, bool actualUse)
    {
        if (effect.Type == EffectType.Attack)
            effect.Multiply(0.8f);
        if (!actualUse || IsInfinite) return;
        magnitude--;
        if (magnitude <= 0)
            subject.RemoveEffect<WeakEffect>();
    }
}

public sealed class DefendEffect(int magnitude) : Effect(EffectType.Defend, magnitude, -1)
{
    public override EffectCategory SupportedCategories => EffectCategory.Continuous;

    // 방어도는 큐에 있는 카드의 남은 magnitude 자체라 효과 목록에 쌓지 않는다
    public override void OnEnterQueue(Combatant subject, CardInstance self, CardEffect cardEffect) { }
    public override void OnExitQueue(Combatant subject, CardInstance self, CardEffect cardEffect, int grantedMagnitude) { }
}

public sealed class HardenEffect(int magnitude) : Effect(EffectType.Harden, magnitude, 2)
{
    public override EffectCategory SupportedCategories => EffectCategory.Instant | EffectCategory.Continuous;

    public override void OnApplyingOther(Combatant subject, CardEffect effect, bool actualUse)
    {
        if (effect.Type == EffectType.Defend)
            effect.Add(magnitude);
    }
}

public sealed class EnergyDrainEffect(int magnitude) : Effect(EffectType.EnergyDrain, magnitude)
{
    public override EffectCategory SupportedCategories => EffectCategory.Instant | EffectCategory.Continuous;

    public override void OnTurnStarted(Combatant subject)
    {
        subject.EnergyHeal(-1);
        if (IsInfinite) return;
        magnitude -= 1;
        if (magnitude <= 0)
            subject.RemoveEffectInstance(this);
    }
}

public sealed class StrengthEffect(int magnitude) : Effect(EffectType.Strength, magnitude, 2)
{
    public override EffectCategory SupportedCategories => EffectCategory.Instant | EffectCategory.Continuous;

    public override void OnApplyingOther(Combatant subject, CardEffect effect, bool actualUse)
    {
        if (effect.Type == EffectType.Attack)
            effect.Add(magnitude);
    }
}

public sealed class DefenseToCooldown(int magnitude) : Effect(EffectType.DefenseToCooldown, magnitude)
{
    public override void OnUse(Combatant subject, CardInstance self, bool actualUse)
    {
        if (self.IsContinuousCard) return;
        self.ChangeCooldownLeft(-subject.GetDefense());
        if (actualUse)
            subject.ConsumeAllDefense();
    }
}

public sealed class CooldownToCost(int magnitude) : Effect(EffectType.CooldownToCost, magnitude)
{
    public override void OnUsingOther(Combatant subject, CardInstance card, bool actualUse)
    {
        if (card.IsContinuousCard) return;
        int cooldown = card.Cooldown;
        card.AddCost(cooldown - 1);
        card.ChangeCooldownLeft(-cooldown + 1);
        if (actualUse && !IsInfinite)
        {
            magnitude -= 1;
            if (magnitude <= 0)
                subject.RemoveEffect<CooldownToCost>();
        }
    }
}

public sealed class VulnerableEffect(int magnitude) : Effect(EffectType.Vulnerable, magnitude, 1)
{
    public override EffectCategory SupportedCategories => EffectCategory.Instant | EffectCategory.Continuous;

    public override void OnAppliedOther(Combatant subject, CardEffect effect, bool actualUse)
    {
        if (effect.Type != EffectType.Attack) return;
        effect.Multiply(1.2f);
        if (!actualUse || IsInfinite) return;
        magnitude--;
        if (magnitude <= 0)
            subject.RemoveEffectInstance(this);
    }
}

public sealed class CostToCooldown(int magnitude) : Effect(EffectType.CostToCooldown, magnitude)
{
    public override void OnUsingOther(Combatant subject, CardInstance card, bool actualUse)
    {
        if (card.IsContinuousCard) return;
        int cost = card.GetCost();
        card.AddCost(-cost);
        card.ChangeCooldownLeft(cost);
        if (actualUse && !IsInfinite)
        {
            magnitude -= 1;
            if (magnitude <= 0)
                subject.RemoveEffect<CostToCooldown>();
        }
    }
}

public sealed class QuickerEffect(int magnitude) : Effect(EffectType.Quicker, magnitude)
{
    public override void OnExpired(Combatant subject) => subject.ChangeTickSpeed(-1);

    public override void OnTurnEnded(Combatant subject)
    {
        if (IsInfinite) return;
        magnitude -= 1;
        if (magnitude <= 0)
            subject.RemoveEffect<QuickerEffect>();
    }

    public override void OnApply(Combatant subject) => subject.ChangeTickSpeed(1);
}

public sealed class PoseBreakEffect(int magnitude) : Effect(EffectType.PoseBreak, magnitude, 4)
{
    public override EffectCategory SupportedCategories => EffectCategory.Instant | EffectCategory.Continuous;

    public override void OnAppliedOther(Combatant subject, CardEffect effect, bool actualUse)
    {
        if (effect.Type == EffectType.Attack)
            effect.Multiply(2f);
    }

    public override void OnTurnStarted(Combatant subject)
    {
        if (IsInfinite) return;
        magnitude--;
        if (magnitude <= 0)
            subject.RemoveEffectInstance(this);
    }
}

public sealed class BurningEffect(int magnitude) : Effect(EffectType.Burning, magnitude)
{
    public override void OnTurnEnded(Combatant subject)
    {
        subject.TakeDamage(magnitude / 10);
        if (IsInfinite) return;
        magnitude -= 1;
        if (magnitude <= 0)
            subject.RemoveEffect<BurningEffect>();
    }

    public override void OnApply(Combatant subject)
    {
        if (subject.IsGuard)
            magnitude /= 3;
        base.OnApply(subject);
    }
}

public sealed class GuardEffect(int magnitude) : Effect(EffectType.Guard, magnitude, 99)
{
    public override EffectCategory SupportedCategories => EffectCategory.Instant | EffectCategory.Continuous;

    public override void OnAppliedOther(Combatant subject, CardEffect effect, bool actualUse)
    {
        if (effect.Type == EffectType.Attack)
            effect.Multiply(0.5f);
    }

    public override void OnTurnStarted(Combatant subject)
    {
        if (IsInfinite) return;
        magnitude--;
        if (magnitude <= 0)
            subject.RemoveEffectInstance(this);
    }
}

public sealed class DivideCooldown(int magnitude) : Effect(EffectType.DivideCooldown, magnitude)
{
    public override void OnUse(Combatant subject, CardInstance self, bool actualUse)
    {
        if (!actualUse) return;
        var cards = new List<CardInstance>();
        if (!self.IsContinuousCard) cards.Add(self);
        foreach (CardInstance? queued in subject.Queue)
        {
            if (queued != null && !queued.IsContinuousCard) cards.Add(queued);
        }
        if (cards.Count == 0) return;
        int sum = cards.Sum(c => c.CooldownLeft);
        int averaged = (int)Math.Round((double)sum / cards.Count, MidpointRounding.ToEven);
        foreach (CardInstance card in cards)
            card.SetCooldownLeft(averaged);
    }
}
