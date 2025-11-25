using Fusion;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;

/// <summary>
/// Displays the opponent's cards at the top of the screen (face down).
/// This component shows face-down card representations based on the opponent's hand count.
/// Place this at the TOP of the screen (y: 4) - opposite of where player hands are (y: -4).
/// </summary>
public class NetworkOpponentHandDisplay : NetworkBehaviour
{
    [Header("Display - Position at TOP of screen")]
    [SerializeField] Transform opponentHandTransform; // Position this at top (y: 4)
    [SerializeField] NetworkPrefabRef faceDownCardPrefab; // Prefab for face-down card display
    [SerializeField] float baseCardSpacing = 150f;
    [SerializeField] float maxHandWidth = 1000f;
    [SerializeField] TextMeshProUGUI opponentCardCountText;
    [SerializeField] Vector2 cardCountTextOffset;
    
    [Networked] public int OpponentHandCount { get; set; }
    
    NetworkRunner runner;
    GameManagerNetwork gm;
    List<NetworkObject> displayedCards = new List<NetworkObject>();
    NetworkPlayerHand opponentHand;
    int lastKnownCount = -1;

    public override void Spawned()
    {
        runner = Runner;
        gm = FindObjectOfType<GameManagerNetwork>();
        
        if (Object.HasStateAuthority)
        {
            OpponentHandCount = 0;
        }
    }

    void Update()
    {
        if (opponentHand == null)
        {
            FindOpponentHand();
            return;
        }

        // Update opponent hand count on server using networked value
        if (Object.HasStateAuthority)
        {
            int newCount = opponentHand.NetworkedHandCount;
            if (newCount != OpponentHandCount)
            {
                OpponentHandCount = newCount;
            }
        }

        // Update display for all clients
        if (OpponentHandCount != lastKnownCount)
        {
            lastKnownCount = OpponentHandCount;
            UpdateDisplay();
        }
    }

    void FindOpponentHand()
    {
        var allHands = FindObjectsOfType<NetworkPlayerHand>();
        foreach (var hand in allHands)
        {
            if (hand.Object != null && hand.Object.InputAuthority != Runner.LocalPlayer)
            {
                opponentHand = hand;
                break;
            }
        }
    }

    void UpdateDisplay()
    {
        // Update card count text
        if (opponentCardCountText != null)
        {
            opponentCardCountText.text = OpponentHandCount.ToString();
            if (opponentHandTransform != null && OpponentHandCount > 0)
            {
                Vector2 textPos = opponentHandTransform.position;
                textPos = new Vector2(textPos.x + cardCountTextOffset.x, textPos.y + cardCountTextOffset.y);
                opponentCardCountText.transform.position = textPos;
                opponentCardCountText.gameObject.SetActive(true);
            }
            else
            {
                opponentCardCountText.gameObject.SetActive(false);
            }
        }

        // Update visual cards (only on state authority)
        if (Object.HasStateAuthority)
        {
            UpdateVisualCards();
        }
    }

    void UpdateVisualCards()
    {
        if (opponentHandTransform == null || faceDownCardPrefab == null || runner == null)
            return;

        // Remove excess cards
        while (displayedCards.Count > OpponentHandCount)
        {
            var card = displayedCards[displayedCards.Count - 1];
            if (card != null)
            {
                runner.Despawn(card);
            }
            displayedCards.RemoveAt(displayedCards.Count - 1);
        }

        // Add missing cards
        while (displayedCards.Count < OpponentHandCount)
        {
            // Spawn a face-down card representation (visible to all - no input authority)
            var cardObj = runner.Spawn(faceDownCardPrefab, Vector3.zero, Quaternion.identity, null);
            var nc = cardObj.GetComponent<NetworkedCard>();
            if (nc != null)
            {
                nc.FaceUp = false; // Always face down
            }
            
            if (opponentHandTransform != null)
            {
                cardObj.transform.SetParent(opponentHandTransform);
            }
            displayedCards.Add(cardObj);
        }

        // Arrange cards
        ArrangeCards();
    }

    void ArrangeCards()
    {
        if (opponentHandTransform == null || displayedCards.Count == 0)
            return;

        float spacing = Mathf.Min(baseCardSpacing, maxHandWidth / displayedCards.Count);

        for (int i = 0; i < displayedCards.Count; i++)
        {
            if (displayedCards[i] == null)
                continue;

            float x = spacing * (i - (displayedCards.Count - 1) / 2f);
            displayedCards[i].transform.localPosition = new Vector3(x, 0f, 0f);

            var sr = displayedCards[i].GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingOrder = i;
            }
        }
    }
}

