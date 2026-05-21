using Unity.Netcode;

public struct CardNetData : INetworkSerializable, System.IEquatable<CardNetData>
{
    public int CardId;  // index into shared ordered card sprite list (0–51)
    public int Value;   // 2–14 (14 = Ace)
    public int Suit;    // 0 = Hearts, 1 = Diamond, 2 = Spades, 3 = Clubs

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref CardId);
        serializer.SerializeValue(ref Value);
        serializer.SerializeValue(ref Suit);
    }

    public bool Equals(CardNetData other) => CardId == other.CardId && Value == other.Value && Suit == other.Suit;
}
