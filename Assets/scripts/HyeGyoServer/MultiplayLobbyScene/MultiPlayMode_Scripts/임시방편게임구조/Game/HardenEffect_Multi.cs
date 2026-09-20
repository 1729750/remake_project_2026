public class HardenEffect_Multi : Effect_Multi
{
    private const int Priority = 2;

    public HardenEffect_Multi(int magnitude)
        : base(
            EffectType.Harden,
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
            EffectType.Defend)
        {
            effect.Add(_magnitude);
        }
    }
}
