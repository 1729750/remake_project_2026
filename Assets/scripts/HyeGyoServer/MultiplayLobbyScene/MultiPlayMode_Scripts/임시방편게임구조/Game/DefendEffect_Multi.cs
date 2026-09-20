using System;

[Serializable]
public class DefendEffect_Multi : Effect_Multi
{
    public DefendEffect_Multi(int magnitude)
        : base(EffectType.Defend, magnitude)
    {
    }

    public override void OnApply(
        CharacterManager_Multi subject)
    {
        subject.AddDefense(_magnitude);
        PlayApplySound();
    }
}
