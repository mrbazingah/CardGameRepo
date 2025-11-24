using Fusion;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NetworkPile : NetworkBehaviour
{
    [SerializeField] Transform pileTransform;
    [SerializeField] NetworkPrefabRef cardPrefab;
    [SerializeField] float cardSpacing = 0.1f;
    [SerializeField] float lerpSpeed = 5f;
    [SerializeField] float maxRotation = 15f;
    [SerializeField] float discardDelay = 1f;
    
    [Networked] public int PileCount { get; private set; }
    [Networked] TickTimer discardTimer { get; set; }
    [Networked] public bool IsDiscarding { get; set; }
    
    List<NetworkObject> pileCards = new List<NetworkObject>();
    List<byte> pileValues = new List<byte>(); // Track card values for game logic

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            PileCount = 0;
            IsDiscarding = false;
        }
    }

    void Update()
    {
        if (HasStateAuthority)
        {
            LerpCardsToPile();
            RotateCards();
        }
    }

    void LerpCardsToPile()
    {
        foreach (var cardObj in pileCards)
        {
            if (cardObj != null && pileTransform != null)
            {
                cardObj.transform.position = Vector3.Lerp(
                    cardObj.transform.position,
                    pileTransform.position,
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
                var nc = cardObj.GetComponent<NetworkedCard>();
                if (nc != null && nc.Rotation == 0)
                {
                    float rot = Random.Range(-maxRotation, maxRotation);
                    nc.SetRotation(rot);
                }
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority && IsDiscarding && discardTimer.Expired(Runner))
        {
            CompleteDiscard();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_AddCardToPile(byte value, PlayerRef playedBy, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
            return;

        PileCount++;
        pileValues.Add(value);
        
        // Spawn a card visible to ALL players
        Vector3 spawnPos = pileTransform != null ? pileTransform.position : Vector3.zero;
        spawnPos.z = -PileCount * cardSpacing;
        
        var cardObj = Runner.Spawn(cardPrefab, spawnPos, Quaternion.identity, null);
        
        var nc = cardObj.GetComponent<NetworkedCard>();
        if (nc != null)
        {
            nc.Value = value;
            nc.FaceUp = true;
        }

        if (pileTransform != null)
        {
            cardObj.transform.SetParent(pileTransform);
        }
        
        var sr = cardObj.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = PileCount;
        }

        pileCards.Add(cardObj);
        
        RPC_UpdatePileVisual(value, PileCount);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_UpdatePileVisual(byte value, int count, RpcInfo info = default)
    {
        // Confirms pile state to all clients
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
        // Despawn all cards in pile
        foreach (var cardObj in pileCards)
        {
            if (cardObj != null && Runner != null)
            {
                Runner.Despawn(cardObj);
            }
        }

        pileCards.Clear();
        pileValues.Clear();
        PileCount = 0;
        IsDiscarding = false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PickUpPile(PlayerRef target, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
            return;

        // Transfer all pile card values to the player's hand
        var hands = FindObjectsOfType<NetworkPlayerHand>();
        var targetHand = hands.FirstOrDefault(h => h.Object != null && h.Object.InputAuthority == target);
        
        if (targetHand != null && pileValues.Count > 0)
        {
            // Spawn new cards for each value in the pile
            foreach (byte value in pileValues)
            {
                var newCard = Runner.Spawn(cardPrefab, Vector3.zero, Quaternion.identity, target);
                var newNc = newCard.GetComponent<NetworkedCard>();
                if (newNc != null)
                {
                    newNc.Value = value;
                    newNc.FaceUp = false; // Cards go to hand face down
                }
                targetHand.RPC_AddCardToHand(newCard, target);
            }
        }

        // Clear pile
        CompleteDiscard();
        
        // Broadcast to all clients
        RPC_BroadcastPilePickedUp(target);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_BroadcastPilePickedUp(PlayerRef target, RpcInfo info = default)
    {
        // Notify all clients that pile was picked up
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
