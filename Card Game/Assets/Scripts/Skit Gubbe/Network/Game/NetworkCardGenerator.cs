using Fusion;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Host-authoritative card generator using deterministic deck seeding.
/// Only syncs: deck seed, deck count, and actions (deal, draw).
/// </summary>
public class NetworkCardGenerator : NetworkBehaviour
{
    [Header("Card Prefabs - Public for NetworkPlayerHand access")]
    public GameObject cardPrefab; // Local prefab, not NetworkPrefabRef
    public GameObject backCardPrefab;
    public Sprite[] cardSprites;
    [SerializeField] int cardsPerPlayer = 5;
    [SerializeField] bool removeSpecialCards = false;
    [SerializeField] float chanceCardDelay = 1f;
    
    // Synced state (minimal)
    [Networked] public int DeckSeed { get; set; }
    [Networked] public int DeckCount { get; set; }
    [Networked] public bool CardsDealt { get; set; }
    [Networked] public bool CanDrawChanceCard { get; set; }
    [Networked] TickTimer chanceCardDelayTimer { get; set; }
    [Networked] TickTimer dealDelayTimer { get; set; }
    
    // Local state (generated from seed)
    List<byte> localDeck;
    GameManagerNetwork gameManager;
    bool hasStartedDealing;
    bool hasGeneratedLocalDeck;

    public override void Spawned()
    {
        gameManager = FindFirstObjectByType<GameManagerNetwork>();
        
        Debug.Log($"NetworkCardGenerator: Spawned. IsServer: {Runner?.IsServer}");
        
        if (Runner != null && Runner.IsServer)
        {
            // Host generates seed and initializes deck
            CardsDealt = false;
            hasStartedDealing = false;
            CanDrawChanceCard = true;
            DeckSeed = Random.Range(int.MinValue, int.MaxValue);
            InitDeck();
            Debug.Log($"NetworkCardGenerator: Host initialized deck with seed {DeckSeed}, {localDeck?.Count ?? 0} cards");
        }
    }
    
    public override void FixedUpdateNetwork()
    {
        // Generate local deck from synced seed (once per client)
        if (!hasGeneratedLocalDeck && DeckSeed != 0)
        {
            GenerateLocalDeckFromSeed();
        }
        
        // Only server handles card dealing
        if (Runner == null || !Runner.IsServer)
            return;

        // Wait for game to start and both players to be ready before dealing cards
        if (!CardsDealt)
        {
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManagerNetwork>();
                if (gameManager == null)
                {
                    return; // Will retry next tick
                }
            }

            if (gameManager.GameStarted && localDeck != null)
            {
                int playerCount = Runner.ActivePlayers.Count();
                if (playerCount >= 2)
                {
                    if (!hasStartedDealing)
                    {
                        Debug.Log($"NetworkCardGenerator: Starting deal timer. Players: {playerCount}, GameStarted: {gameManager.GameStarted}");
                        dealDelayTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
                        hasStartedDealing = true;
                    }
                    
                    if (hasStartedDealing && dealDelayTimer.Expired(Runner))
                    {
                        Debug.Log("NetworkCardGenerator: Dealing cards now!");
                        DealInitialHands();
                        CardsDealt = true;
                    }
                }
            }
        }

        // Handle chance card delay
        if (!CanDrawChanceCard && chanceCardDelayTimer.Expired(Runner))
        {
            CanDrawChanceCard = true;
        }
    }
    
    void GenerateLocalDeckFromSeed()
    {
        localDeck = DeterministicDeck.GenerateDeck(DeckSeed, removeSpecialCards);
        DeckCount = localDeck.Count;
        hasGeneratedLocalDeck = true;
        Debug.Log($"NetworkCardGenerator: Client generated local deck from seed {DeckSeed}, {localDeck.Count} cards");
    }

    void InitDeck()
    {
        // Host generates deck from seed
        localDeck = DeterministicDeck.GenerateDeck(DeckSeed, removeSpecialCards);
        DeckCount = localDeck.Count;
        hasGeneratedLocalDeck = true;
    }

    void DealInitialHands()
    {
        if (Runner == null || !Runner.IsServer || localDeck == null || localDeck.Count == 0)
        {
            Debug.LogWarning($"DealInitialHands: Cannot deal - IsServer: {Runner?.IsServer}, DeckCount: {localDeck?.Count ?? 0}");
            return;
        }

        var players = Runner.ActivePlayers.ToList();
        Debug.Log($"DealInitialHands: Dealing cards to {players.Count} players");

        foreach (var player in players)
        {
            // Deal hand cards (send action, not cards)
            List<byte> handCards = DeterministicDeck.DrawCards(localDeck, cardsPerPlayer);
            RPC_DealHandCards(player, handCards.ToArray());
            
            // Deal 6 side cards (3 under, 3 over)
            List<byte> underSideCards = DeterministicDeck.DrawCards(localDeck, 3);
            List<byte> overSideCards = DeterministicDeck.DrawCards(localDeck, 3);
            RPC_DealSideCards(player, underSideCards.ToArray(), overSideCards.ToArray());
        }

        DeckCount = localDeck.Count;
        Debug.Log($"DealInitialHands: Complete. Remaining deck: {DeckCount}");
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_DealHandCards(PlayerRef target, byte[] cardValues, RpcInfo info = default)
    {
        // All clients receive this action and generate cards locally
        var hands = FindObjectsOfType<NetworkPlayerHand>();
        var targetHand = hands.FirstOrDefault(h => h.Object != null && h.Object.InputAuthority == target);
        
        if (targetHand != null)
        {
            targetHand.AddHandCardsLocally(cardValues);
        }
        else
        {
            Debug.LogWarning($"RPC_DealHandCards: Could not find hand for player {target}");
        }
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_DealSideCards(PlayerRef target, byte[] underSideValues, byte[] overSideValues, RpcInfo info = default)
    {
        // All clients receive this action and generate side cards locally
        var hands = FindObjectsOfType<NetworkPlayerHand>();
        var targetHand = hands.FirstOrDefault(h => h.Object != null && h.Object.InputAuthority == target);
        
        if (targetHand != null)
        {
            targetHand.SetSideCardsLocally(underSideValues, overSideValues);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_DrawNewCard(PlayerRef target, int amount, RpcInfo info = default)
    {
        if (Runner == null || !Runner.IsServer || localDeck == null || localDeck.Count <= 0)
            return;

        // Draw cards from deterministic deck
        List<byte> drawnCards = DeterministicDeck.DrawCards(localDeck, amount);
        DeckCount = localDeck.Count;
        
        // Send action to all clients
        RPC_ReceiveDrawnCards(target, drawnCards.ToArray());
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_ReceiveDrawnCards(PlayerRef target, byte[] cardValues, RpcInfo info = default)
    {
        // All clients generate cards locally from the action
        var hands = FindObjectsOfType<NetworkPlayerHand>();
        var targetHand = hands.FirstOrDefault(h => h.Object != null && h.Object.InputAuthority == target);
        
        if (targetHand != null)
        {
            targetHand.AddHandCardsLocally(cardValues);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_GetChanceCard(PlayerRef target, RpcInfo info = default)
    {
        if (Runner == null || !Runner.IsServer || localDeck == null || localDeck.Count == 0 || !CanDrawChanceCard)
        {
            RPC_ChanceCardResult(target, 0);
            return;
        }

        // Draw from deterministic deck (using current deck state)
        byte chanceValue = DeterministicDeck.DrawCard(localDeck);
        DeckCount = localDeck.Count;

        // Start delay timer
        CanDrawChanceCard = false;
        chanceCardDelayTimer = TickTimer.CreateFromSeconds(Runner, chanceCardDelay);

        // Send action to all clients
        RPC_ChanceCardResult(target, chanceValue);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_ChanceCardResult(PlayerRef target, byte value, RpcInfo info = default)
    {
        // All clients generate chance card locally
        if (value == 0)
            return;
            
        var hands = FindObjectsOfType<NetworkPlayerHand>();
        var targetHand = hands.FirstOrDefault(h => h.Object != null && h.Object.InputAuthority == target);
        if (targetHand != null)
        {
            targetHand.AddChanceCardLocally(value);
        }
    }

    public int GetCardsPerPlayer()
    {
        return cardsPerPlayer;
    }

    public int GetDeckCount()
    {
        return DeckCount;
    }
}

