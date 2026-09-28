using System;

[Serializable]
public class EnergyDrainEffect_Multi : Effect_Multi
{
    public EnergyDrainEffect_Multi(int magnitude)
        : base(
            EffectType.EnergyDrain,
            magnitude)
    {
    }

    public override EffectTargetPolarity
        TargetPolarity =>
        EffectTargetPolarity.Negative;

    public override void OnTurnStarted(
        CharacterManager_Multi subject)
    {
        subject.EnergyHeal(-1);

        _magnitude -= 1;

        if (_magnitude <= 0)
        {
            subject
                .RemoveEffect<
                    EnergyDrainEffect_Multi>();
        }
    }
}
