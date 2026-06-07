using System;
using UnityEngine;

[Serializable]
public class Effect
{
   [SerializeField] private EffectType _effectType;
   [SerializeField] protected int _magnitude;

    public Effect(EffectType effectType, int magnitude)
    {
        _effectType = effectType;
        _magnitude = magnitude;
    }

    public EffectType GetEffectType() => _effectType;
    public int GetMagnitude() => _magnitude;
    
    public void AddMagnitude(int magnitude) => _magnitude += magnitude;

    public static Effect Create(EffectType type, int magnitude)
    {
        switch (type)
        {
            case EffectType.Vulnerable: return new VulnerableEffect(magnitude);
            case EffectType.Weak:       return new WeakEffect(magnitude);
            case EffectType.Strength:   return new StrengthEffect(magnitude);
            case EffectType.Harden:     return new HardenEffect(magnitude);
            default:                    return new Effect(type, magnitude);
        }
    }

    public virtual void OnApplied(BattleManager subject) {}
    public virtual void OnExpired(BattleManager subject) { }
    public virtual void OnTurnStarted(BattleManager subject) { }
    public virtual void OnTurnEnded(BattleManager subject) { }
    public virtual void OnApplying(BattleManager subject, CardEffect effect, bool actualUse) { }
    public virtual void OnApplyed(BattleManager subject, CardEffect effect, bool actualUse) { }
}
