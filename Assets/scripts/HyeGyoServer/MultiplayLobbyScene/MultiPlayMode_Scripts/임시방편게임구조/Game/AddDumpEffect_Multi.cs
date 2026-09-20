using System;

[Serializable]
public class AddDumpEffect_Multi : Effect_Multi
{
    private CardInstance_Multi dump;

    public AddDumpEffect_Multi(int magnitude)
        : base(
            EffectType.AddDump,
            magnitude)
    {
    }

    public override EffectTargetPolarity
        TargetPolarity =>
        EffectTargetPolarity.Negative;

    public override void OnApply(
        CharacterManager_Multi subject)
    {
        CardDefinition dumpDefinition =
            CardDefinition.Create(
                _magnitude,
                1,
                Array.Empty<CardEffect>(),
                null,
                null);

        dump =
            new CardInstance_Multi(
                dumpDefinition,
                subject);

        subject.ReturnToDeck(dump);

        PlayApplySound();
    }
}
