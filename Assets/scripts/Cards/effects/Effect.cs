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
            default:                    return new Effect(type, magnitude);
        }
    }

    public virtual void OnExpired(CharacterManager subject) { }
    public virtual void OnTurnStarted(CharacterManager subject) { }
    public virtual void OnTurnEnded(CharacterManager subject) { }

    // 즉시 발동하지 않는(스택형) 효과의 기본 동작: 같은 타입의 기존 효과가 있으면 magnitude만 합산하고,
    // 없으면 자신을 subject의 효과 목록에 새로 등록한다. Attack/Defend/EnergyHeal처럼 즉시 처리되는
    // 타입은 이 기본 동작 대신 override에서 바로 결과를 적용한다.
    public virtual void OnApply(CharacterManager subject, CardEffect cardEffect)
    {
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
