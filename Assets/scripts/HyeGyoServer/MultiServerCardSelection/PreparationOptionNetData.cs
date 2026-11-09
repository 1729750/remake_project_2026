using Unity.Netcode;

[System.Serializable]
public struct PreparationOptionNetData
    : INetworkSerializable
{
    public byte Type;
    public int OfferedSlot;
    public bool RandomEnhance, NoEnhanceTarget;
    public int EffectType, Magnitude, Target, Category, CostDelta, CooldownDelta;

    public int CardPoolIndex;

    public int TargetCardIndex;

    public int EnhanceId;


    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref OfferedSlot);
        serializer.SerializeValue(ref RandomEnhance);
        serializer.SerializeValue(ref NoEnhanceTarget);
        serializer.SerializeValue(ref EffectType);
        serializer.SerializeValue(ref Magnitude);
        serializer.SerializeValue(ref Target);
        serializer.SerializeValue(ref Category);
        serializer.SerializeValue(ref CostDelta);
        serializer.SerializeValue(ref CooldownDelta);
        serializer.SerializeValue(
            ref Type
        );

        serializer.SerializeValue(
            ref CardPoolIndex
        );

        serializer.SerializeValue(
            ref TargetCardIndex
        );

        serializer.SerializeValue(
            ref EnhanceId
        );
    }
}