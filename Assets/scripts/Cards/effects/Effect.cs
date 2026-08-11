using System;
using UnityEngine;

[Serializable]
public class Effect
{
   [SerializeField] private EffectType _effectType;
   [SerializeField] protected int _magnitude;
    private int _effectPriority;

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
            if (existing.GetEffectType() == _effectType)
            {
                existing.AddMagnitude(_magnitude);
                return;
            }
        }
        subject.AddEffect(this);
        Debug.Log($"[{subject.gameObject.name}] Gained effect: {_effectType} :{_magnitude}");
    }
    public virtual void OnApplyingOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
    public virtual void OnAppliedOther(CharacterManager subject, CardEffect effect, bool actualUse) { }
}
