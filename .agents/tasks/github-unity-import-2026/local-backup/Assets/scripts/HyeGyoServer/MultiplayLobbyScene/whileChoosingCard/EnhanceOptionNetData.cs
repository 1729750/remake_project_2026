using Unity.Netcode;

public struct EnhanceOptionNetData : INetworkSerializable
{
    public EffectType EffectType;
    public int Magnitude;
    public EffectTarget Target;
    public EffectCategory AppliedCategory;
    public int CostDelta;
    public int CooldownDelta;

    public EnhanceOptionNetData(
        EffectType effectType,
        int magnitude,
        EffectTarget target,
        EffectCategory appliedCategory,
        int costDelta,
        int cooldownDelta)
    {
        EffectType = effectType;
        Magnitude = magnitude;
        Target = target;
        AppliedCategory = appliedCategory;
        CostDelta = costDelta;
        CooldownDelta = cooldownDelta;
    }

    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref EffectType);
        serializer.SerializeValue(ref Magnitude);
        serializer.SerializeValue(ref Target);
        serializer.SerializeValue(ref AppliedCategory);
        serializer.SerializeValue(ref CostDelta);
        serializer.SerializeValue(ref CooldownDelta);
    }

    public static EnhanceOptionNetData FromCardUpgrade(
        CardUpgrade upgrade)
    {
        Effect effect =
            upgrade.effect.GetEffect();

        return new EnhanceOptionNetData(
            effect.GetEffectType(),
            effect.GetMagnitude(),
            upgrade.effect.GetEffectTarget(),
            upgrade.effect.GetAppliedCategory(),
            upgrade.costDelta,
            upgrade.cooldownDelta
        );
    }

    public CardUpgrade ToCardUpgrade()
    {
        Effect effect =
            Effect.Create(
                EffectType,
                Magnitude
            );

        CardEffect cardEffect =
            new CardEffect(effect, Target);

        cardEffect.SetAppliedCategory(
            AppliedCategory
        );

        return new CardUpgrade(
            cardEffect,
            CostDelta,
            CooldownDelta
        );
    }
}
