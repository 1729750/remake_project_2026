using System;

[Serializable]
public class VulnerableEffect_Multi : Effect_Multi
{
    private const int Priority = 1;

    public VulnerableEffect_Multi(int magnitude)
        : base(
            EffectType.Vulnerable,
            magnitude,
            Priority)
    {
    }

    public override EffectTargetPolarity
        TargetPolarity =>
        EffectTargetPolarity.Negative;

    public override void OnAppliedOther(
        CharacterManager_Multi subject,
        CardEffect_Multi effect,
        bool actualUse)
    {
        if (effect.GetEffect()
                .GetEffectType() ==
            EffectType.Attack)
        {
            effect.Multiply(1.2f);
        }
    }

    public override void OnTurnEnded(
        CharacterManager_Multi subject)
    {
        _magnitude--;

        if (_magnitude <= 0)
        {
            subject
                .RemoveEffect<
                    VulnerableEffect_Multi>();
        }
    }
}
