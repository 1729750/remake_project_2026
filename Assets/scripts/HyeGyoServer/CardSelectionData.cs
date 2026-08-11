using System;
using Unity.Netcode;

[Serializable]
public struct CardSelectionData :
    INetworkSerializable,
    IEquatable<CardSelectionData>
{
    public ulong ClientId;
    public int CardId;

    public CardSelectionData(
        ulong clientId,
        int cardId)
    {
        ClientId = clientId;
        CardId = cardId;
    }

    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(
            ref ClientId
        );

        serializer.SerializeValue(
            ref CardId
        );
    }

    public bool Equals(
        CardSelectionData other)
    {
        return
            ClientId == other.ClientId &&
            CardId == other.CardId;
    }

    public override bool Equals(
        object obj)
    {
        return
            obj is CardSelectionData other &&
            Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return
                (ClientId.GetHashCode() * 397) ^
                CardId;
        }
    }
}