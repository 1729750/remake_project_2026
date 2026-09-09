using System;
using UnityEngine;

[Serializable]
public class Effect
{
   [SerializeField] private EffectType _effectType;
   [SerializeField] protected int _magnitude;
    private int _effectPriority;

    // true인 동안 이 인스턴스의 magnitude는 턴 경과(OnTurnStarted/OnTurnEnded)나 조건 충족
    // (OnApplyingOther/OnUsingOther 등에서의 소모형 감소)으로 줄어들 수 없다. 큐에 카드가 머무는
    // 동안 OnEnterQueue가 기본 제공하는 continuous 효과에 붙는 표시로, 카드 자신의 큐 잔류가
    // 수명을 관리하므로 독립적인 턴 감소 로직과 충돌하지 않게 막는 용도다.
    public bool IsInfinite { get; set; }

    // 이 EffectType이 원리상 어떤 방식으로 발동할 수 있는지(능력) — 실제로 어느 쪽으로 쓸지는
    // 카드마다 CardEffect.GetAppliedCategory()가 고른다. Instant(카드 쿨타임이 다 됐을 때 한 번,
    // 기존 동작), Continuous(카드가 큐에 머무는 동안만, OnEnterQueue/OnExitQueue) 비트를 조합한다.
    // 기본은 Instant만 — Attack/EnergyHeal/AddDump처럼 일회성 부수효과가 있는 타입은 Continuous로
    // 재해석하면 안 되므로(무엇을 "되돌릴지" 정의할 수 없다) 명시적으로 override한 타입만 확장한다.
    public virtual EffectCategory SupportedCategories => EffectCategory.Instant;

    public Effect(EffectType effectType, int magnitude, int effectPriority = 0)
    {
        _effectType = effectType;
        _magnitude = magnitude;
        _effectPriority = effectPriority;
    }
    public EffectType GetEffectType() => _effectType;
    public int GetMagnitude() => _magnitude;
    public int GetEffectPriority() => _effectPriority;

    public void AddMagnitude(int magnitude) => _magnitude += magnitude;

    // 이 Effect가 강화 후보로 뽑혔을 때(RewardManager.RollEnhanceOption) magnitude 부호로부터
    // EffectTarget(User/Opponent)을 정하는 데 쓰는 극성. Attack/Defend처럼 별도 서브클래스가 없는
    // 타입은 _effectType으로 구분하고, 서브클래스는 자신의 성격(버프/디버프)에 맞게 override한다.
    public virtual EffectTargetPolarity TargetPolarity
    {
        get
        {
            switch (_effectType)
            {
                case EffectType.Attack: return EffectTargetPolarity.Negative;
                case EffectType.Defend: return EffectTargetPolarity.Positive;
                case EffectType.EnergyHeal: return EffectTargetPolarity.Positive;
                // Preserve/Disposable은 서브클래스 없이 이 베이스 클래스 그대로 쓰이는 카드 자신의
                // 성질(마커) 플래그라 User를 향한다.
                case EffectType.Preserve: return EffectTargetPolarity.Positive;
                case EffectType.Disposable: return EffectTargetPolarity.Positive;
                default:                return EffectTargetPolarity.Neutral;
            }
        }
    }

    // true면 EffectDisplay가 수치를 표시하지 않고(SetEffect), 강화 후보 가격 계산에서 magnitude를
    // 무조건 1로 취급하고(RewardManager.RollEnhanceOption), 강화 대상 덱 표시에서 이미 이 효과를
    // 가진 카드를 후보에서 제외한다(RewardManager.ShowEnhanceDeckSelection) — magnitude가 의미를
    // 갖지 않는(스택형이 아닌) 타입들의 공통 성격이다.
    public virtual bool DoesntUseMagnitude
    {
        get
        {
            switch (_effectType)
            {
                case EffectType.Disposable:
                case EffectType.Preserve:
                case EffectType.DivideCooldown:
                case EffectType.DefenseToCooldown:
                    return true;
                default:
                    return false;
            }
        }
    }

    public static Effect Create(EffectType type, int magnitude)
    {
        switch (type)
        {
            case EffectType.Attack:     return new AttackEffect(magnitude);
            case EffectType.Defend:     return new DefendEffect(magnitude);
            case EffectType.EnergyHeal: return new EnergyHealEffect(magnitude);
            case EffectType.Vulnerable: return new VulnerableEffect(magnitude);
            case EffectType.Weak:       return new WeakEffect(magnitude);
            case EffectType.Strength:   return new StrengthEffect(magnitude);
            case EffectType.Harden:     return new HardenEffect(magnitude);
            case EffectType.Guard:      return new GuardEffect(magnitude);
            case EffectType.PoseBreak : return new PoseBreakEffect(magnitude);
            case EffectType.DivideCooldown: return new DivideCooldown(magnitude);
            case EffectType.DefenseToCooldown: return new DefenseToCooldown(magnitude);
            case EffectType.TimeSkip:   return new TimeSkip(magnitude);
            case EffectType.CostToCooldown: return new CostToCooldown(magnitude);
            case EffectType.CooldownToCost: return new CooldownToCost(magnitude);
            case EffectType.Burning:    return new BurningEffect(magnitude);
            case EffectType.AddDump:    return new AddDumpEffect(magnitude);
            case EffectType.Quicker: return new QuickerEffect(magnitude);
            case EffectType.EnergyDrain: return new EnergyDrainEffect(magnitude);
            default:                    return new Effect(type, magnitude);
        }
    }

    public virtual void OnExpired(CharacterManager subject) { }
    public virtual void OnTurnStarted(CharacterManager subject) { }
    public virtual void OnTurnEnded(CharacterManager subject) { }
    // self: 이 Effect가 속한 CardInstance(지금 사용되는 카드 자신). actualUse가 true면 실제로 카드가
    // 사용되는 시점의 호출이고, false면 시각화 갱신을 위한 미리보기(dry-run) 호출이다 — 실제 상태를
    // 바꾸는 부수효과(스택 소모 등)는 actualUse일 때만 해야 한다.
    public virtual void OnUse(CharacterManager subject, CardInstance self, bool actualUse) { }
    // subject: 이 CardInstance(자신이 속한 카드)를 사용하는 주체. cardInstance: 지금 막 사용되는 카드
    // (큐에 있던 자신이 아니라 새로 사용되는 카드 쪽). 큐에 있던 카드가 새 카드 사용에 반응할 때 쓴다.
    public virtual void OnUsingOther(CharacterManager subject, CardInstance cardInstance, bool actualUse) { }
    public virtual void OnTick(CharacterManager subject) { }
    // 즉시 발동하지 않는(스택형) 효과의 기본 동작: 같은 타입의 기존 효과가 있으면 magnitude만 합산하고,
    // 없으면 자신을 subject의 효과 목록에 새로 등록한다. Attack/Defend/EnergyHeal처럼 즉시 처리되는
    // 타입은 이 기본 동작 대신 override에서 바로 결과를 적용한다.
    public virtual void OnApply(CharacterManager subject)
    {
        switch (_effectType)
        {
            case EffectType.Disposable:
            case EffectType.Preserve: 
            case EffectType.CooldownToCost:
            case EffectType.DivideCooldown:
            case EffectType.CostToCooldown:
                return;
            default:
                break;
        }
        foreach (var existing in subject.GetEffects())
        {
            // IsInfinite인 인스턴스는 continuous 몫(카드가 큐에 머무는 동안만 사는 별도 풀)이므로
            // 절대 합치지 않는다 — 합치면 instant 몫이 그 풀의 IsInfinite에 묻어가 감소가 막히고,
            // 나중에 OnExitQueue가 되돌릴 때 instant 몫까지 깎아가게 된다.
            if (existing.GetEffectType() == _effectType && !existing.IsInfinite)
            {
                existing.AddMagnitude(_magnitude);
                PlayApplySound();
                return;
            }
        }
        subject.AddEffect(this);
        PlayApplySound();
        Debug.Log($"[{subject.gameObject.name}] Gained effect: {_effectType} :{_magnitude}");
    }
    public virtual void OnApplyingOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
    public virtual void OnAppliedOther(CharacterManager subject, CardEffect effect, bool actualUse) { }

    // Continuous/Mix 효과의 기본 동작: 카드가 큐에 들어가는 순간(CardInstance.EnterQueue) 대상에게
    // 이 효과를 부여한다. OnApply를 재사용하지 않는다 — instant 몫(IsInfinite==false)과 절대 합치면
    // 안 되기 때문에, 여기서는 처음부터 IsInfinite인 기존 인스턴스만 찾아 합산하고, 없으면 새
    // continuous 풀을 만든다. 그래서 같은 타입이라도 instant 스택과 continuous 스택은 항상 별개
    // 인스턴스로 공존한다: instant 쪽은 자기 자신의 턴 감소 로직으로만 줄고, continuous 쪽은 이
    // 카드(들)가 큐에 머무는 동안의 magnitude 합만큼만 존재하다가 OnExitQueue로 정확히 되돌아간다.
    // 방어(DefendEffect)처럼 큐 카드 자신의 잔여 magnitude로 방어도를 관리하는 특수 케이스는 이
    // 기본 동작을 오버라이드해서 쓰지 않는다.
    public virtual void OnEnterQueue(CharacterManager subject, CardInstance self, CardEffect cardEffect)
    {
        CharacterManager target = cardEffect.GetTarget(subject);

        foreach (Effect existing in target.GetEffects())
        {
            if (existing.GetEffectType() != GetEffectType() || !existing.IsInfinite) continue;
            existing.AddMagnitude(_magnitude);
            PlayApplySound();
            return;
        }

        IsInfinite = true;
        target.AddEffect(this);
        PlayApplySound();
    }

    // OnEnterQueue가 부여한 magnitude만큼(grantedMagnitude) continuous 풀(IsInfinite인 인스턴스)에서만
    // 되돌린다 — instant 풀은 IsInfinite가 아니므로 여기서 절대 건드리지 않는다. 카드가 큐를 떠날 때
    // (재생되어 CardInstance.ExitQueue가 호출될 때) 실행된다. 남은 magnitude가 0 이하가 되면
    // 효과 자체를 제거한다.
    public virtual void OnExitQueue(CharacterManager subject, CardInstance self, CardEffect cardEffect, int grantedMagnitude)
    {
        CharacterManager target = cardEffect.GetTarget(subject);

        foreach (Effect existing in target.GetEffects())
        {
            if (existing.GetEffectType() != GetEffectType() || !existing.IsInfinite) continue;
            existing.AddMagnitude(-grantedMagnitude);
            if (existing.GetMagnitude() <= 0)
                target.RemoveEffectInstance(existing);
            return;
        }
    }

    // 이 Effect가 실제로 획득/발동될 때 SoundManager로 재생할 EffectSound.
    // 기본은 TargetPolarity(Positive→Buff, 그 외→Debuff)를 따르되, 고유한 사운드가 있는 타입은
    // 예외로 지정한다(Burning→Burn, Defend→ShieldGet, TimeSkip/Quicker→TimeSkip).
    // Attack은 카드가 재생될 때(CardInstance.Play)와 피격 시(CharacterManager.Attacked)의 전용
    // 사운드로 따로 처리하므로 여기서는 재생하지 않는다 — OnApply를 완전히 오버라이드하는
    // AttackEffect는 이 메서드를 호출하지 않는다.
    protected void PlayApplySound()
    {
        EffectSound sound;
        switch (_effectType)
        {
            case EffectType.Burning:
                sound = EffectSound.Burn;
                break;
            case EffectType.Defend:
                sound = EffectSound.ShieldGet;
                break;
            case EffectType.TimeSkip:
            case EffectType.Quicker:
                sound = EffectSound.TimeSkip;
                break;
            default:
                sound = TargetPolarity == EffectTargetPolarity.Positive ? EffectSound.Buff : EffectSound.Debuff;
                break;
        }
        SoundManager.Instance?.Play(sound);
    }
}
