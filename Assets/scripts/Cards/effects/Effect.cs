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

    public static Effect Create(EffectType type, int magnitude)
    {
        switch (type)
        {
            case EffectType.Vulnerable: return new VulnerableEffect(magnitude);
            case EffectType.Weak:       return new WeakEffect(magnitude);
            case EffectType.Strength:   return new StrengthEffect(magnitude);
            case EffectType.Harden:     return new HardenEffect(magnitude);
            case EffectType.Guard:      return new GuardEffect(magnitude);
            default:                    return new Effect(type, magnitude);
        }
    }

    public virtual void OnExpired(CharacterManager subject) { }
    public virtual void OnTurnStarted(CharacterManager subject) { }
    public virtual void OnTurnEnded(CharacterManager subject) { }
    public virtual void OnApplying(CharacterManager subject, CardEffect effect, bool actualUse) { }
    public virtual void OnApplied(CharacterManager subject, CardEffect effect, bool actualUse) { }
}
