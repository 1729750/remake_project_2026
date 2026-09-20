using System;

[Serializable]
public class AttackEffect_Multi : Effect_Multi
{
    public AttackEffect_Multi(int magnitude)
        : base(EffectType.Attack, magnitude)
    {
    }

    public override void OnApply(
        CharacterManager_Multi subject)
    {
        subject.Attacked(_magnitude);
    }
}
