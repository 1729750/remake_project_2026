using System;

[Serializable]
public class TimeSkip_Multi : Effect_Multi
{
    public TimeSkip_Multi(int magnitude)
        : base(
            EffectType.TimeSkip,
            magnitude)
    {
    }

    public override EffectTargetPolarity
        TargetPolarity =>
        EffectTargetPolarity.Positive;

    public override void OnApply(
        CharacterManager_Multi subject)
    {
        CardInstance_Multi target = null;

        foreach (
            CardInstance_Multi queued
            in subject.GetQueue())
        {
            if (queued == null)
                continue;

            if (target == null ||
                queued.GetCooldownLeft() >
                target.GetCooldownLeft())
            {
                target = queued;
            }
        }

        target?.ChangeCooldownLeft(
            -_magnitude);

        PlayApplySound();
    }
}
