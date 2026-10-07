using Unity.Netcode;

public struct PreparationOptionNetData
    : INetworkSerializable
{
    public byte Type;
    public int CardPoolIndex;
    public int TargetCardIndex;
    public int EnhanceId;
    public bool HasUpgrade;
    public EnhanceOptionNetData Upgrade;

    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref Type);
        serializer.SerializeValue(ref CardPoolIndex);
        serializer.SerializeValue(ref TargetCardIndex);
        serializer.SerializeValue(ref EnhanceId);
        serializer.SerializeValue(ref HasUpgrade);
        serializer.SerializeValue(ref Upgrade);
    }
}
