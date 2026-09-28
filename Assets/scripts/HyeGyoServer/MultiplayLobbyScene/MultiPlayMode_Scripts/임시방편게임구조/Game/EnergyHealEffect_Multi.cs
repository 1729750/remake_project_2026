using System;

[Serializable]
public class EnergyHealEffect_Multi : Effect_Multi
{
    public EnergyHealEffect_Multi(int magnitude)
        : base(EffectType.EnergyHeal, magnitude)
    {
    }

    public override void OnApply(
        CharacterManager_Multi subject)
    {
        subject.EnergyHeal(_magnitude);
        PlayApplySound();
    }
}
