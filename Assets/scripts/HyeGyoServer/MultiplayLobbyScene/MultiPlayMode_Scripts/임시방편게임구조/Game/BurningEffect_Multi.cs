using System;

[Serializable]
public class BurningEffect_Multi : Effect_Multi
{
    public BurningEffect_Multi(int magnitude)
        : base(
            EffectType.Burning,
            magnitude)
    {
    }

    public override EffectTargetPolarity
        TargetPolarity =>
        EffectTargetPolarity.Negative;

    public override void OnTurnEnded(
        CharacterManager_Multi subject)
    {
        subject.TakeDamage(
            _magnitude / 10);

        _magnitude -= 1;

        if (_magnitude <= 0)
        {
            subject
                .RemoveEffect<
                    BurningEffect_Multi>();
        }
    }

    public override void OnApply(
        CharacterManager_Multi subject)
    {
        if (subject.GetIsGuard())
            _magnitude /= 3;

        base.OnApply(subject);
    }
}
