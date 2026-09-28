using System;

[Serializable]
public class GuardEffect_Multi : Effect_Multi
{
    private const int Priority = 1;

    public GuardEffect_Multi(int magnitude)
        : base(
            EffectType.Guard,
            magnitude,
            Priority)
    {
    }

    public override EffectTargetPolarity
        TargetPolarity =>
        EffectTargetPolarity.Positive;

    public override void OnAppliedOther(
        CharacterManager_Multi subject,
        CardEffect_Multi effect,
        bool actualUse)
    {
        if (effect.GetEffect()
                .GetEffectType() ==
            EffectType.Attack)
        {
            effect.Multiply(0.5f);
        }
    }

    public override void OnTurnStarted(
        CharacterManager_Multi subject)
    {
        _magnitude--;

        if (_magnitude <= 0)
        {
            subject
                .RemoveEffect<
                    GuardEffect_Multi>();
        }
    }
}
