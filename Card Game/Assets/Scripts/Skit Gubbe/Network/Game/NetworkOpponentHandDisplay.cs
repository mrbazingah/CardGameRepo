using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;

/// <summary>
/// Displays the opponent's cards at the top of the screen (face down).
/// Each client locally tracks their opponent's hand count and displays face-down cards.
/// This is NOT a NetworkBehaviour - it runs locally on each client.
/// Place this at the TOP of the screen (y: 4) - opposite of where player hands are (y: -4).
/// </summary>
public class NetworkOpponentHandDisplay : MonoBehaviour
{
    [Header("Display - Position at TOP of screen")]
    [SerializeField] Transform opponentHandTransform; // Position this at top (y: 4)
    [SerializeField] GameObject faceDownCardPrefab; // LOCAL prefab for face-down card display (not networked)
    [SerializeField] float baseCardSpacing = 0.7f;
    [SerializeField] float maxHandWidth = 8f;
    [SerializeField] TextMeshProUGUI opponentCardCountText;
    [SerializeField] Vector2 cardCountTextOffset;
    
    List<GameObject> displayedCards = new List<GameObject>();
    NetworkPlayerHand opponentHand;
    int lastKnownCount = -1;

    void Update()
    {
        // Try to find opponent hand if not found yet
        if (opponentHand == null)
        {
            FindOpponentHand();
            return;
        }

        // Locally read opponent's networked hand count
        int currentCount = opponentHand.NetworkedHandCount;

        // Update display when count changes
        if (currentCount != lastKnownCount)
        {
            lastKnownCount = currentCount;
            UpdateDisplay(currentCount);
        }
    }

    void FindOpponentHand()
    {
        // Find the NetworkRunner to get LocalPlayer
        var runner = Fusion.NetworkRunner.GetRunnerForGameObject(gameObject);
        if (runner == null)
        {
            // Try to find any runner in the scene
            runner = FindObjectOfType<Fusion.NetworkRunner>();
        }
        
        if (runner == null)
            return;

        var allHands = FindObjectsOfType<NetworkPlayerHand>();
        foreach (var hand in allHands)
        {
            // Find the hand that belongs to the OTHER player (not local player)
            if (hand.Object != null && hand.Object.InputAuthority != runner.LocalPlayer)
            {
                opponentHand = hand;
                Debug.Log($"Found opponent hand for player {hand.Object.InputAuthority}");
                break;
            }
        }
    }

    void UpdateDisplay(int cardCount)
    {
        // Update card count text
        if (opponentCardCountText != null)
        {
            opponentCardCountText.text = cardCount.ToString();
            if (opponentHandTransform != null && cardCount > 0)
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

        // Update visual cards locally
        UpdateVisualCards(cardCount);
    }

    void UpdateVisualCards(int cardCount)
    {
        if (opponentHandTransform == null || faceDownCardPrefab == null)
            return;

        // Remove excess cards
        while (displayedCards.Count > cardCount)
        {
            var card = displayedCards[displayedCards.Count - 1];
            if (card != null)
            {
                Destroy(card);
            }
            displayedCards.RemoveAt(displayedCards.Count - 1);
        }

        // Add missing cards (local instantiation, not networked)
        while (displayedCards.Count < cardCount)
        {
            var cardObj = Instantiate(faceDownCardPrefab, opponentHandTransform);
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

    void OnDestroy()
    {
        // Clean up displayed cards
        foreach (var card in displayedCards)
        {
            if (card != null)
            {
                Destroy(card);
            }
        }
        displayedCards.Clear();
    }
}

