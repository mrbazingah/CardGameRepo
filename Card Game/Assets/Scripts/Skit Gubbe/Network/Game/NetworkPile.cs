using Fusion;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Host-authoritative pile manager.
/// Only syncs: card values, pile count, discard state.
/// All clients generate pile cards locally from synced values.
/// </summary>
public class NetworkPile : NetworkBehaviour
{
    [SerializeField] Transform pileTransform;
    [SerializeField] float cardSpacing = 0.1f;
    [SerializeField] float lerpSpeed = 5f;
    [SerializeField] float maxRotation = 15f;
    [SerializeField] float discardDelay = 1f;
    
    // Synced state (minimal)
    [Networked] public int PileCount { get; private set; }
    [Networked] TickTimer discardTimer { get; set; }
    [Networked] public bool IsDiscarding { get; set; }
    
    // Local state
    List<GameObject> pileCards = new List<GameObject>(); // Local card GameObjects
    List<byte> pileValues = new List<byte>(); // Synced via RPCs
    NetworkCardGenerator cardGenerator;
    int lastKnownPileCount = -1;

    public override void Spawned()
    {
        cardGenerator = FindObjectOfType<NetworkCardGenerator>();
        
        if (Object.HasStateAuthority)
        {
            PileCount = 0;
            IsDiscarding = false;
        }
    }
    
    public override void FixedUpdateNetwork()
    {
        // Check if pile count changed (triggers regeneration on clients)
        if (PileCount != lastKnownPileCount)
        {
            lastKnownPileCount = PileCount;
            // Pile will be updated via RPC_UpdatePileValues
        }
        
        // Handle discard timer (host only)
        if (Object.HasStateAuthority && IsDiscarding && discardTimer.Expired(Runner))
        {
            CompleteDiscard();
        }
    }

    void Update()
    {
        LerpCardsToPile();
        RotateCards();
    }

    void LerpCardsToPile()
    {
        if (pileTransform == null) return;
        
        for (int i = 0; i < pileCards.Count; i++)
        {
            if (pileCards[i] != null)
            {
                Vector3 targetPos = pileTransform.position;
                targetPos.z = -i * cardSpacing;
                pileCards[i].transform.position = Vector3.Lerp(
                    pileCards[i].transform.position,
                    targetPos,
                    lerpSpeed * Time.deltaTime
                );
            }
        }
    }

    void RotateCards()
    {
        foreach (var cardObj in pileCards)
        {
            if (cardObj != null)
            {
                var card = cardObj.GetComponent<Card>();
                if (card != null && !card.GetHasBeenTurned())
                {
                    float rot = Random.Range(-maxRotation, maxRotation);
                    card.Rotate(rot, true);
                }
            }
        }
    }
    
    GameObject CreateLocalCard(byte value)
    {
        if (cardGenerator == null)
        {
            cardGenerator = FindObjectOfType<NetworkCardGenerator>();
            if (cardGenerator == null || cardGenerator.cardPrefab == null)
            {
                Debug.LogError("NetworkPile: Cannot create card - NetworkCardGenerator or prefab not found!");
                return null;
            }
        }
        
        GameObject card = Instantiate(cardGenerator.cardPrefab);
        Card cardComponent = card.GetComponent<Card>();
        if (cardComponent != null)
        {
            cardComponent.SetValue(value);
        }
        
        // Set sprite
        SpriteRenderer sr = card.GetComponent<SpriteRenderer>();
        if (sr != null && cardGenerator.cardSprites != null && cardGenerator.cardSprites.Length > 0)
        {
            int spriteIndex = value - 2;
            if (spriteIndex >= 0 && spriteIndex < cardGenerator.cardSprites.Length)
            {
                sr.sprite = cardGenerator.cardSprites[spriteIndex];
            }
            sr.gameObject.SetActive(true); // Face up
        }
        
        if (pileTransform != null)
        {
            card.transform.SetParent(pileTransform);
        }
        
        return card;
    }
    
    void RegeneratePileCards()
    {
        // Clear existing cards
        foreach (var card in pileCards)
        {
            if (card != null) Destroy(card);
        }
        pileCards.Clear();
        
        // Create new cards from synced values
        for (int i = 0; i < pileValues.Count; i++)
        {
            GameObject card = CreateLocalCard(pileValues[i]);
            if (card != null)
            {
                var sr = card.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sortingOrder = i;
                }
                pileCards.Add(card);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_AddCardToPile(byte value, PlayerRef playedBy, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
            return;

        PileCount++;
        pileValues.Add(value);
        
        // Broadcast updated pile values to all clients
        RPC_UpdatePileValues(pileValues.ToArray());
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_UpdatePileValues(byte[] values, RpcInfo info = default)
    {
        // All clients update their local pile
        pileValues = new List<byte>(values);
        RegeneratePileCards();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_DiscardPile(RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
            return;

        if (!IsDiscarding)
        {
            IsDiscarding = true;
            discardTimer = TickTimer.CreateFromSeconds(Runner, discardDelay);
        }
    }

    void CompleteDiscard()
    {
        // Clear local cards
        foreach (var cardObj in pileCards)
        {
            if (cardObj != null)
            {
                Destroy(cardObj);
            }
        }

        pileCards.Clear();
        pileValues.Clear();
        PileCount = 0;
        IsDiscarding = false;
        lastKnownPileCount = 0;
        
        // Broadcast clear to all clients
        RPC_UpdatePileValues(new byte[0]);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PickUpPile(PlayerRef target, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
            return;

        // Transfer all pile card values to the player's hand (send action)
        var hands = FindObjectsOfType<NetworkPlayerHand>();
        var targetHand = hands.FirstOrDefault(h => h.Object != null && h.Object.InputAuthority == target);
        
        if (targetHand != null && pileValues.Count > 0)
        {
            // Send action to add cards to hand
            RPC_AddPileCardsToHand(target, pileValues.ToArray());
        }

        // Clear pile
        CompleteDiscard();
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_AddPileCardsToHand(PlayerRef target, byte[] cardValues, RpcInfo info = default)
    {
        // All clients add cards to hand locally
        var hands = FindObjectsOfType<NetworkPlayerHand>();
        var targetHand = hands.FirstOrDefault(h => h.Object != null && h.Object.InputAuthority == target);
        
        if (targetHand != null)
        {
            targetHand.AddHandCardsLocally(cardValues);
        }
    }

    public byte GetCurrentCard(bool isChance)
    {
        if (pileValues.Count == 0)
            return 0;

        if (!isChance)
        {
            return pileValues[pileValues.Count - 1];
        }
        else
        {
            // For chance cards, look at second-to-last card
            if (pileValues.Count >= 2)
            {
                return pileValues[pileValues.Count - 2];
            }
            return 0;
        }
    }

    public byte GetTopValue()
    {
        if (pileValues.Count == 0)
            return 0;
        return pileValues[pileValues.Count - 1];
    }

    public int GetPileCount()
    {
        return PileCount;
    }

    public List<byte> GetPileValues()
    {
        return new List<byte>(pileValues);
    }
}
