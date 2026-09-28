using System;

[Serializable]
public class CostToCooldown_Multi : Effect_Multi
{
    public CostToCooldown_Multi(int magnitude)
        : base(
            EffectType.CostToCooldown,
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
        int cost =
            cardInstance.GetCost();

        cardInstance.AddCost(
            -cost);

        cardInstance
            .ChangeCooldownLeft(cost);

        if (!actualUse)
            return;

        _magnitude -= 1;

        if (_magnitude <= 0)
        {
            subject
                .RemoveEffect<
                    CostToCooldown_Multi>();
        }
    }
}
