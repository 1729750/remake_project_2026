using System;

[Serializable]
public class PoseBreakEffect_Multi : Effect_Multi
{
    private const int Priority = 4;

    public PoseBreakEffect_Multi(int magnitude)
        : base(
            EffectType.PoseBreak,
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
            effect.Multiply(2f);
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
                    PoseBreakEffect_Multi>();
        }
    }
}
