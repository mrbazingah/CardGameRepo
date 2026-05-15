using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NetworkCardGenerator : NetworkBehaviour
{
    public static NetworkCardGenerator Instance { get; private set; }

    [SerializeField] GameObject deckImage;
    [Space]
    [SerializeField] int cardsPerPlayer;
    [SerializeField] int numberOfCards = 52;
    [Header("Debugging")]
    [SerializeField] bool removeSpecialCards;

    // Remaining draw pile — server only
    List<CardNetData> logicalDeck = new List<CardNetData>();

    // Synced so clients can check deck size (e.g. for CanChance, deck image)
    NetworkVariable<int> remainingDeckCount = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        cardsPerPlayer = RelayManager.Instance != null
            ? RelayManager.Instance.CardsPerPlayer
            : PlayerPrefs.GetInt("CardsPerPlayer", 3);

        GenerateLogicalDeck();
        DealToPlayers();
    }

    // -------------------------------------------------------------------------
    // Deck generation
    // -------------------------------------------------------------------------

    void GenerateLogicalDeck()
    {
        logicalDeck = new List<CardNetData>(numberOfCards);

        for (int i = 0; i < numberOfCards; i++)
        {
            int suit = i / 13;
            int rawValue = (i % 13) + 1;
            int value = rawValue == 1 ? 14 : rawValue;

            if ((value == 2 || value == 10) && removeSpecialCards) continue;

            logicalDeck.Add(new CardNetData { CardId = i, Value = value, Suit = suit });
        }

        // Fisher-Yates shuffle
        for (int i = logicalDeck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (logicalDeck[i], logicalDeck[j]) = (logicalDeck[j], logicalDeck[i]);
        }

        remainingDeckCount.Value = logicalDeck.Count;
    }

    CardNetData[] TakeFromDeck(int count)
    {
        count = Mathf.Min(count, logicalDeck.Count);
        CardNetData[] taken = new CardNetData[count];

        for (int i = 0; i < count; i++)
        {
            taken[i] = logicalDeck[0];
            logicalDeck.RemoveAt(0);
        }

        remainingDeckCount.Value = logicalDeck.Count;
        return taken;
    }

    // -------------------------------------------------------------------------
    // Deal
    // -------------------------------------------------------------------------

    void DealToPlayers()
    {
        var clientIds = NetworkManager.Singleton.ConnectedClientsIds;

        ulong localId = NetworkManager.Singleton.LocalClientId;
        ulong remoteId = 0;
        foreach (ulong id in clientIds)
        {
            if (id != localId) { remoteId = id; break; }
        }

        CardNetData[] localHand  = TakeFromDeck(cardsPerPlayer);
        CardNetData[] localUnder = TakeFromDeck(3);
        CardNetData[] localOver  = TakeFromDeck(3);

        CardNetData[] remoteHand  = TakeFromDeck(cardsPerPlayer);
        CardNetData[] remoteUnder = TakeFromDeck(3);
        CardNetData[] remoteOver  = TakeFromDeck(3);

        var localParams  = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { localId } } };
        var remoteParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { remoteId } } };

        DealHandClientRpc(
            localHand,  localUnder,  localOver,
            remoteHand.Length, remoteUnder.Length, remoteOver.Length,
            localParams);

        DealHandClientRpc(
            remoteHand,  remoteUnder,  remoteOver,
            localHand.Length, localUnder.Length, localOver.Length,
            remoteParams);
    }

    [ClientRpc]
    void DealHandClientRpc(
        CardNetData[] hand, CardNetData[] underSide, CardNetData[] overSide,
        int opponentHandCount, int opponentUnderCount, int opponentOverCount,
        ClientRpcParams rpcParams = default)
    {
        NetworkPlayerHand playerHand   = FindFirstObjectByType<NetworkPlayerHand>();
        NetworkOpponentHand opponentHand = FindFirstObjectByType<NetworkOpponentHand>();

        playerHand.ReceiveDeal(hand, underSide, overSide);
        opponentHand.ReceiveDeal(opponentHandCount, opponentUnderCount, opponentOverCount);
    }

    // -------------------------------------------------------------------------
    // Draw (called via ServerRpc from NetworkPlayerHand when implemented)
    // -------------------------------------------------------------------------

    public CardNetData[] DrawCards(int count)
    {
        if (!IsServer) return null;
        return TakeFromDeck(count);
    }

    // -------------------------------------------------------------------------
    // Misc
    // -------------------------------------------------------------------------

    void Update()
    {
        if (deckImage != null && remainingDeckCount.Value == 0)
            Destroy(deckImage);
    }

    public int GetCardsPerPlayer() => cardsPerPlayer;
    public int GetRemainingDeckCount() => remainingDeckCount.Value;
}
