using System;

[Serializable]
public class CooldownToCost_Multi : Effect_Multi
{
    public CooldownToCost_Multi(int magnitude)
        : base(
            EffectType.CooldownToCost,
            magnitude)
    {
    }

    public override EffectTargetPolarity
        TargetPolarity =>
        EffectTargetPolarity.Positive;

    public override void OnUsingOther(
        CharacterManager_Multi subject,
        CardInstance_Multi cardInstance,
        bool actualUse)
    {
        int cooldown =
            cardInstance.GetCooldown();

        cardInstance.AddCost(
            cooldown - 1);

        cardInstance
            .ChangeCooldownLeft(
                -cooldown + 1);

        if (!actualUse)
            return;

        _magnitude -= 1;

        if (_magnitude <= 0)
        {
            subject
                .RemoveEffect<
                    CooldownToCost_Multi>();
        }
    }
}
