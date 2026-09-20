using System;

[Serializable]
public class StrengthEffect_Multi : Effect_Multi
{
    private const int Priority = 2;

    public StrengthEffect_Multi(int magnitude)
        : base(
            EffectType.Strength,
            magnitude,
            Priority)
    {
    }

    public override EffectTargetPolarity
        TargetPolarity =>
        EffectTargetPolarity.Positive;

    public override void OnApplyingOther(
        CharacterManager_Multi subject,
        CardEffect_Multi effect,
        bool actualUse)
    {
        if (effect.GetEffect()
                .GetEffectType() ==
            EffectType.Attack)
        {
            effect.Add(_magnitude);
        }
    }
}
