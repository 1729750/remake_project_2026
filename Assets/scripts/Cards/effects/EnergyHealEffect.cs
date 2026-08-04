using System;

[Serializable]
public class EnergyHealEffect : Effect
{
    public EnergyHealEffect(int magnitude) : base(EffectType.EnergyHeal, magnitude) { }

    public override void OnApply(CharacterManager subject, CardEffect cardEffect)
    {
        subject.EnergyHeal(cardEffect.GetMagnitude());
    }
}
