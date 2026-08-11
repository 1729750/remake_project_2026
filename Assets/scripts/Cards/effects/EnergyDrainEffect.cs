using System;

[Serializable]
public class EnergyDrainEffect : Effect
{
    public EnergyDrainEffect(int magnitude) : base(EffectType.EnergyDrain, magnitude) { }

    public override void OnTurnStarted(CharacterManager subject)
    {
        subject.EnergyHeal(-1);
        _magnitude -= 1;
        if (_magnitude <= 0)
            subject.RemoveEffect<EnergyDrainEffect>();
    }
}
