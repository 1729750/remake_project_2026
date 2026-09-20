using System;

[Serializable]
public class DefenseToCooldown_Multi : Effect_Multi
{
    public DefenseToCooldown_Multi(int magnitude)
        : base(
            EffectType.DefenseToCooldown,
            magnitude)
    {
    }

    public override EffectTargetPolarity
        TargetPolarity =>
        EffectTargetPolarity.Positive;

    public override void OnUse(
        CharacterManager_Multi subject,
        CardInstance_Multi self,
        bool actualUse)
    {
        if (subject == null ||
            self == null)
        {
            return;
        }

        int defense =
            subject.GetDefense();

        self.ChangeCooldownLeft(
            -defense);

        if (actualUse)
        {
            subject.AddDefense(
                -defense);
        }
    }
}
