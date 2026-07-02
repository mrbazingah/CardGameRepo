using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
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

    // Authoritative overSide state per player — replicated automatically to all clients.
    // player0 = host, player1 = client. NetworkOpponentHand reads the opponent's list.
    public NetworkList<CardNetData> player0OverSide = new NetworkList<CardNetData>();
    public NetworkList<CardNetData> player1OverSide = new NetworkList<CardNetData>();

    bool hasDealt;

    // Singleton is bound to the network lifecycle so Instance always points at the
    // spawned, RPC-bound replica. No Awake guard, no Destroy of duplicates.
    public override void OnNetworkSpawn()
    {
        Instance = this;
        Debug.Log($"[NCG] OnNetworkSpawn — netObjId={NetworkObjectId} IsServer={IsServer} IsClient={IsClient} IsHost={IsHost}");

        if (!IsServer) { return; }

        // Both players are already connected before the game scene loads (Option 1 flow),
        // so the deal trigger is NGO's scene-load completion: it fires only when ALL
        // clients have finished loading the scene, guaranteeing NetworkPlayerHand and
        // NetworkOpponentHand exist on both sides before the deal RPCs are sent.
        NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager != null && NetworkManager.SceneManager != null)
        {
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
        }

        if (Instance == this) { Instance = null; }
    }

    void OnSceneLoadCompleted(string sceneName, LoadSceneMode loadMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        Debug.Log($"[NCG] OnLoadEventCompleted — scene={sceneName} completed={clientsCompleted.Count} timedOut={clientsTimedOut.Count}");

        if (hasDealt) { return; }
        hasDealt = true;

        NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;

        cardsPerPlayer = RelayManager.Instance != null ? RelayManager.Instance.CardsPerPlayer : PlayerPrefs.GetInt("CardsPerPlayer", 3);
        Debug.Log($"[NCG] Dealing — cardsPerPlayer={cardsPerPlayer} connected={NetworkManager.Singleton.ConnectedClientsIds.Count}");

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

        Debug.Log($"[NCG] DealToPlayers — localId={localId} remoteId={remoteId} cardsPerPlayer={cardsPerPlayer}");

        CardNetData[] localHand = TakeFromDeck(cardsPerPlayer);
        CardNetData[] localUnder = TakeFromDeck(3);
        CardNetData[] localOver = TakeFromDeck(3);

        CardNetData[] remoteHand = TakeFromDeck(cardsPerPlayer);
        CardNetData[] remoteUnder = TakeFromDeck(3);
        CardNetData[] remoteOver = TakeFromDeck(3);

        // Seed the authoritative lists. local = host = player0, remote = client = player1.
        // These lists are the single source of truth for the opponent overSide display,
        // covering both the initial deal and every later swap.
        SeedOverSideLists(localOver, remoteOver);

        var localParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { localId } } };
        var remoteParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { remoteId } } };

        // Own-hand deal by targeted RPC (hand + both side stacks for the player themselves).
        DealPlayerCardsClientRpc(localHand, localUnder, localOver, localParams);
        DealPlayerCardsClientRpc(remoteHand, remoteUnder, remoteOver, remoteParams);

        // Opponent info sends hand + underSide only. OverSide comes from the NetworkList,
        // so it is not sent here (doing both would double up the display).
        DealOpponentInfoClientRpc(remoteHand, remoteUnder, localParams);
        DealOpponentInfoClientRpc(localHand, localUnder, remoteParams);
    }

    void SeedOverSideLists(CardNetData[] player0Over, CardNetData[] player1Over)
    {
        player0OverSide.Clear();
        foreach (CardNetData d in player0Over) { player0OverSide.Add(d); }

        player1OverSide.Clear();
        foreach (CardNetData d in player1Over) { player1OverSide.Add(d); }
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
    void DealOpponentInfoClientRpc(CardNetData[] opponentHand, CardNetData[] opponentUnderSide, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"[NCG] DealOpponentInfoClientRpc received — hand={opponentHand.Length} under={opponentUnderSide.Length} (overSide comes from NetworkList)");
        NetworkOpponentHand opponentHand2 = FindFirstObjectByType<NetworkOpponentHand>();
        if (opponentHand2 == null) { Debug.LogError("[NCG] NetworkOpponentHand NOT FOUND"); return; }
        opponentHand2.ReceiveDeal(opponentHand, opponentUnderSide);
    }

    // -------------------------------------------------------------------------
    // Card swap — server writes the authoritative list; NetworkList replication
    // and OnListChanged on the opponent's side handle the rest.
    // -------------------------------------------------------------------------

    [ServerRpc(RequireOwnership = false)]
    public void SwapCardsServerRpc(CardNetData[] newOverSide, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        NetworkList<CardNetData> list = senderId == NetworkManager.ServerClientId ? player0OverSide : player1OverSide;

        list.Clear();
        foreach (CardNetData data in newOverSide) { list.Add(data); }

        Debug.Log($"[NCG] SwapCardsServerRpc — senderId={senderId} wroteList={(senderId == NetworkManager.ServerClientId ? "player0" : "player1")} count={newOverSide.Length}");
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