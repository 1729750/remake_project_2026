using System;

[Serializable]
public class QuickerEffect_Multi : Effect_Multi
{
    public QuickerEffect_Multi(int magnitude)
        : base(
            EffectType.Quicker,
            magnitude)
    {
    }

    public override EffectTargetPolarity
        TargetPolarity =>
        EffectTargetPolarity.Positive;

    public override void OnExpired(
        CharacterManager_Multi subject)
    {
        subject.ChangeTickSpeed(-1);
    }

    public override void OnTurnEnded(
        CharacterManager_Multi subject)
    {
        _magnitude -= 1;

        if (_magnitude <= 0)
        {
            subject
                .RemoveEffect<
                    QuickerEffect_Multi>();
        }
    }

    public override void OnApply(
        CharacterManager_Multi subject)
    {
        subject.ChangeTickSpeed(1);
        PlayApplySound();
    }
}
