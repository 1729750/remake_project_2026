using System;
using UnityEngine;

[Serializable]
public class Effect_Multi : Effect
{
    public Effect_Multi(
        EffectType effectType,
        int magnitude,
        int effectPriority = 0)
        : base(effectType, magnitude, effectPriority)
    {
    }

    public static Effect_Multi Create(
        EffectType type,
        int magnitude)
    {
        switch (type)
        {
            case EffectType.Attack: return new AttackEffect_Multi(magnitude);
            case EffectType.Defend: return new DefendEffect_Multi(magnitude);
            case EffectType.EnergyHeal: return new EnergyHealEffect_Multi(magnitude);
            case EffectType.Vulnerable: return new VulnerableEffect_Multi(magnitude);
            case EffectType.Weak: return new WeakEffect_Multi(magnitude);
            case EffectType.Strength: return new StrengthEffect_Multi(magnitude);
            case EffectType.Harden: return new HardenEffect_Multi(magnitude);
            case EffectType.Guard: return new GuardEffect_Multi(magnitude);
            case EffectType.PoseBreak: return new PoseBreakEffect_Multi(magnitude);
            case EffectType.DivideCooldown: return new DivideCooldown_Multi(magnitude);
            case EffectType.TimeSkip: return new TimeSkip_Multi(magnitude);
            case EffectType.CostToCooldown: return new CostToCooldown_Multi(magnitude);
            case EffectType.CooldownToCost: return new CooldownToCost_Multi(magnitude);
            case EffectType.Burning: return new BurningEffect_Multi(magnitude);
            case EffectType.AddDump: return new AddDumpEffect_Multi(magnitude);
            case EffectType.Quicker: return new QuickerEffect_Multi(magnitude);
            case EffectType.EnergyDrain: return new EnergyDrainEffect_Multi(magnitude);

            // DefenseToCooldown의 Single 소스는 아직 전달되지 않았으므로
            // 동작을 추측하지 않고 base Multi Effect로 유지한다.
            case EffectType.DefenseToCooldown:
                return new Effect_Multi(type, magnitude);

            default:
                return new Effect_Multi(type, magnitude);
        }
    }

    public virtual void OnExpired(CharacterManager_Multi subject) { }
    public virtual void OnTurnStarted(CharacterManager_Multi subject) { }
    public virtual void OnTurnEnded(CharacterManager_Multi subject) { }

    public virtual void OnUse(
        CharacterManager_Multi subject,
        CardInstance_Multi self,
        bool actualUse)
    {
    }

    public virtual void OnUsingOther(
        CharacterManager_Multi subject,
        CardInstance_Multi cardInstance,
        bool actualUse)
    {
    }

    public virtual void OnTick(CharacterManager_Multi subject) { }

    public virtual void OnApply(CharacterManager_Multi subject)
    {
        switch (GetEffectType())
        {
            case EffectType.Disposable:
            case EffectType.Preserve:
            case EffectType.CooldownToCost:
            case EffectType.DivideCooldown:
            case EffectType.CostToCooldown:
                return;
        }

        foreach (Effect_Multi existing in subject.GetEffects())
        {
            if (existing.GetEffectType() == GetEffectType())
            {
                existing.AddMagnitude(_magnitude);
                PlayApplySound();
                subject.SyncPublicStateServer();
                return;
            }
        }

        subject.AddEffect(this);
        PlayApplySound();

        Debug.Log(
            $"[{subject.gameObject.name}] Gained effect: {GetEffectType()} :{_magnitude}");
    }

    public virtual void OnApplyingOther(
        CharacterManager_Multi subject,
        CardEffect_Multi effect,
        bool actualUse)
    {
    }

    public virtual void OnAppliedOther(
        CharacterManager_Multi subject,
        CardEffect_Multi effect,
        bool actualUse)
    {
    }
}
