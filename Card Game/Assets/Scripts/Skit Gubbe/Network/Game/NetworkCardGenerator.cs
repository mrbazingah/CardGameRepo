using Fusion;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NetworkCardGenerator : NetworkBehaviour
{
    [SerializeField] NetworkPrefabRef cardPrefab;
    [SerializeField] Sprite[] cardSprites;
    [SerializeField] int cardsPerPlayer = 5;
    [SerializeField] int numberOfCards = 52;
    
    [Networked] public bool CardsDealt { get; set; }
    [Networked] TickTimer dealDelayTimer { get; set; }
    [Networked] public int DeckCount { get; set; }
    [Networked] TickTimer chanceCardDelayTimer { get; set; }
    [Networked] public bool CanDrawChanceCard { get; set; }
    
    List<byte> deckValues;
    GameManagerNetwork gameManager;
    bool hasStartedDealing;
    float chanceCardDelay = 1f;

    public override void Spawned()
    {
        gameManager = FindFirstObjectByType<GameManagerNetwork>();
        
        if (Object.HasStateAuthority)
        {
            CardsDealt = false;
            hasStartedDealing = false;
            CanDrawChanceCard = true;
            InitDeck();
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Wait for game to start and both players to be ready before dealing cards
        if (Object.HasStateAuthority && !CardsDealt && gameManager != null && gameManager.GameStarted)
        {
            if (Runner.ActivePlayers.Count() >= 2)
            {
                if (!hasStartedDealing)
                {
                    dealDelayTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
                    hasStartedDealing = true;
                }
                
                if (dealDelayTimer.Expired(Runner))
                {
                    DealInitialHands();
                    CardsDealt = true;
                }
            }
        }

        // Handle chance card delay
        if (Object.HasStateAuthority && !CanDrawChanceCard && chanceCardDelayTimer.Expired(Runner))
        {
            CanDrawChanceCard = true;
        }
    }

    void InitDeck()
    {
        deckValues = new List<byte>();
        // Create a standard 52-card deck (values 2-14, 4 suits)
        for (int suit = 0; suit < 4; suit++)
        {
            for (byte v = 2; v <= 14; v++)
            {
                deckValues.Add(v);
            }
        }
        Shuffle(deckValues);
        DeckCount = deckValues.Count;
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }

    void DealInitialHands()
    {
        if (!Object.HasStateAuthority || deckValues == null || deckValues.Count == 0)
            return;

        var players = Runner.ActivePlayers.ToList();
        Debug.Log($"Dealing cards to {players.Count} players");

        foreach (var player in players)
        {
            // Deal hand cards
            for (int i = 0; i < cardsPerPlayer; i++)
            {
                SpawnCardTo(player, false);
            }

            // Deal 6 side cards (3 over, 3 under)
            List<NetworkObject> overSideCards = new List<NetworkObject>();
            List<NetworkObject> underSideCards = new List<NetworkObject>();

            for (int i = 0; i < 6; i++)
            {
                var cardObj = SpawnCardTo(player, true); // true = side card
                if (cardObj != null)
                {
                    if (i < 3)
                    {
                        underSideCards.Add(cardObj);
                    }
                    else
                    {
                        overSideCards.Add(cardObj);
                    }
                }
            }

            // Send side cards to player hand
            var hands = FindObjectsOfType<NetworkPlayerHand>();
            var targetHand = hands.FirstOrDefault(h => h.Object != null && h.Object.InputAuthority == player);
            if (targetHand != null)
            {
                targetHand.RPC_SetSideCards(underSideCards.ToArray(), overSideCards.ToArray(), player);
            }
        }

        DeckCount = deckValues.Count;
    }

    public NetworkObject SpawnCardTo(PlayerRef target, bool isSideCard)
    {
        if (!Object.HasStateAuthority || deckValues == null || deckValues.Count == 0)
            return null;

        byte val = deckValues[0];
        deckValues.RemoveAt(0);
        DeckCount = deckValues.Count;

        // Spawn card with the target player as input authority
        var netObj = Runner.Spawn(cardPrefab, Vector3.zero, Quaternion.identity, target);
        var nc = netObj.GetComponent<NetworkedCard>();
        if (nc != null)
        {
            nc.Value = val;
            nc.FaceUp = false;
            nc.IsSideCard = isSideCard;
        }

        // Find the appropriate hand and add the card
        if (!isSideCard)
        {
            var hands = FindObjectsOfType<NetworkPlayerHand>();
            var targetHand = hands.FirstOrDefault(h => h.Object != null && h.Object.InputAuthority == target);
            
            if (targetHand != null)
            {
                targetHand.RPC_AddCardToHand(netObj, target);
            }
            else
            {
                Debug.LogWarning($"Could not find NetworkPlayerHand for player {target}");
            }
        }

        return netObj;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_DrawNewCard(PlayerRef target, int amount, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || deckValues == null || deckValues.Count <= 0)
            return;

        for (int i = 0; i < amount; i++)
        {
            if (deckValues.Count == 0)
                break;

            SpawnCardTo(target, false);
        }

        DeckCount = deckValues.Count;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_GetChanceCard(PlayerRef target, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || deckValues == null || deckValues.Count == 0 || !CanDrawChanceCard)
        {
            RPC_ChanceCardResult(target, 0, info.Source);
            return;
        }

        int randomNumber = Random.Range(0, deckValues.Count);
        byte chanceValue = deckValues[randomNumber];
        deckValues.RemoveAt(randomNumber);
        DeckCount = deckValues.Count;

        // Start delay timer
        CanDrawChanceCard = false;
        chanceCardDelayTimer = TickTimer.CreateFromSeconds(Runner, chanceCardDelay);

        // Spawn chance card
        var netObj = Runner.Spawn(cardPrefab, Vector3.zero, Quaternion.identity, target);
        var nc = netObj.GetComponent<NetworkedCard>();
        if (nc != null)
        {
            nc.Value = chanceValue;
            nc.FaceUp = true; // Chance cards are face up
            nc.IsChanceCard = true;
        }

        RPC_ChanceCardResult(target, chanceValue, info.Source, netObj);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_ChanceCardResult(PlayerRef target, byte value, PlayerRef requester, NetworkObject cardObj = null, RpcInfo info = default)
    {
        if (cardObj != null && requester == Runner.LocalPlayer)
        {
            var hands = FindObjectsOfType<NetworkPlayerHand>();
            var targetHand = hands.FirstOrDefault(h => h.Object != null && h.Object.InputAuthority == target);
            if (targetHand != null)
            {
                targetHand.RPC_AddChanceCard(cardObj, target);
            }
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
