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

    List<CardNetData> logicalDeck = new List<CardNetData>();

    NetworkVariable<int> remainingDeckCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[NCG] OnNetworkSpawn — IsServer={IsServer} IsClient={IsClient} IsHost={IsHost}");
        if (!IsServer) { return; }
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NCG] OnClientConnected — clientId={clientId} LocalClientId={NetworkManager.Singleton.LocalClientId}");
        if (clientId == NetworkManager.Singleton.LocalClientId) { return; }
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        cardsPerPlayer = RelayManager.Instance != null ? RelayManager.Instance.CardsPerPlayer : PlayerPrefs.GetInt("CardsPerPlayer", 3);
        Debug.Log($"[NCG] Dealing — cardsPerPlayer={cardsPerPlayer} ConnectedCount={NetworkManager.Singleton.ConnectedClientsIds.Count}");
        GenerateLogicalDeck();
        DealToPlayers();
    }

    public override void OnDestroy()
    {
        if (NetworkManager.Singleton != null) { NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected; }
        base.OnDestroy();
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

            if ((value == 2 || value == 10) && removeSpecialCards) { continue; }

            logicalDeck.Add(new CardNetData { CardId = i, Value = value, Suit = suit });
        }

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

        Debug.Log($"[NCG] DealToPlayers — localId={localId} remoteId={remoteId}");

        CardNetData[] localHand = TakeFromDeck(cardsPerPlayer);
        CardNetData[] localUnder = TakeFromDeck(3);
        CardNetData[] localOver = TakeFromDeck(3);

        CardNetData[] remoteHand = TakeFromDeck(cardsPerPlayer);
        CardNetData[] remoteUnder = TakeFromDeck(3);
        CardNetData[] remoteOver = TakeFromDeck(3);

        var localParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { localId } } };
        var remoteParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { remoteId } } };

        DealPlayerCardsClientRpc(localHand, localUnder, localOver, localParams);
        DealPlayerCardsClientRpc(remoteHand, remoteUnder, remoteOver, remoteParams);

        DealOpponentInfoClientRpc(remoteHand.Length, remoteUnder.Length, remoteOver, localParams);
        DealOpponentInfoClientRpc(localHand.Length, localUnder.Length, localOver, remoteParams);
    }

    [ClientRpc]
    void DealPlayerCardsClientRpc(CardNetData[] hand, CardNetData[] underSide, CardNetData[] overSide, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"[NCG] DealPlayerCardsClientRpc received — hand={hand.Length} under={underSide.Length} over={overSide.Length}");
        NetworkPlayerHand playerHand = FindFirstObjectByType<NetworkPlayerHand>();
        if (playerHand == null) { Debug.LogError("[NCG] NetworkPlayerHand NOT FOUND"); return; }
        playerHand.ReceiveDeal(hand, underSide, overSide);
    }

    [ClientRpc]
    void DealOpponentInfoClientRpc(int opponentHandCount, int opponentUnderCount, CardNetData[] opponentOverSide, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"[NCG] DealOpponentInfoClientRpc received — handCount={opponentHandCount} underCount={opponentUnderCount} over={opponentOverSide.Length}");
        NetworkOpponentHand opponentHand = FindFirstObjectByType<NetworkOpponentHand>();
        if (opponentHand == null) { Debug.LogError("[NCG] NetworkOpponentHand NOT FOUND"); return; }
        opponentHand.ReceiveDeal(opponentHandCount, opponentUnderCount, opponentOverSide);
    }

    // -------------------------------------------------------------------------
    // Draw (called via ServerRpc from NetworkPlayerHand when implemented)
    // -------------------------------------------------------------------------

    public CardNetData[] DrawCards(int count)
    {
        if (!IsServer) { return null; }
        return TakeFromDeck(count);
    }

    // -------------------------------------------------------------------------
    // Misc
    // -------------------------------------------------------------------------

    void Update()
    {
        if (deckImage != null && remainingDeckCount.Value == 0) { Destroy(deckImage); }
    }

    public int GetCardsPerPlayer() => cardsPerPlayer;
    public int GetRemainingDeckCount() => remainingDeckCount.Value;
}
