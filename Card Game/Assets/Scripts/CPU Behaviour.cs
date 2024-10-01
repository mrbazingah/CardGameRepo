using System.Collections.Generic;
using UnityEngine;

public class AIHand : MonoBehaviour
{
    [Header("Hand Settings")]
    [SerializeField] List<GameObject> handCards;
    [Header("Side Settings")]
    [SerializeField] List<GameObject> underSideCards, overSideCards;

    [SerializeField] bool isTurn;

    bool usingOverSideCards, usingUnderSideCards;

    Pile pile;
    CardGenrator cardGenerator;
    GameManager gameManager;

    void Start()
    {
        pile = FindFirstObjectByType<Pile>();
        cardGenerator = FindFirstObjectByType<CardGenrator>();
        gameManager = FindFirstObjectByType<GameManager>();
    }

    public void AddHandCards(GameObject newCard)
    {
        handCards.Add(newCard);
        DisplayCardCount();
    }

    public void SetUnderSideCards(List<GameObject> newCards) => underSideCards = newCards;
    public void SetOverSideCards(List<GameObject> newCards) => overSideCards = newCards;

    void Update()
    {
        UpdateSideUsage();

        if (isTurn)
        {
            PlayAITurn();
        }
    }

    void UpdateSideUsage()
    {
        usingOverSideCards = handCards.Count == 0 && overSideCards.Count > 0;
        usingUnderSideCards = handCards.Count == 0 && overSideCards.Count == 0 && underSideCards.Count > 0;
    }

    void PlayAITurn()
    {
        List<GameObject> currentCards = GetCards();
        GameObject selectedCard = null;

        foreach (GameObject card in currentCards)
        {
            Card cardComponent = card.GetComponent<Card>();
            if (CanPlayCard(cardComponent.GetValue()))
            {
                selectedCard = card;
                break;
            }
        }

        if (selectedCard != null)
        {
            PlayCard(selectedCard);
        }
        else
        {
            PickUpPile();
        }

        EndTurn(selectedCard);
    }

    void PlayCard(GameObject cardInHand)
    {
        if (!isTurn) return;

        if (CanPlayCard(cardInHand.GetComponent<Card>().GetValue()))
        {
            RemoveCardFromList(cardInHand);
            pile.AddCardsToPile(cardInHand);

            if (cardGenerator.GetDeck().Count > 0 && handCards.Count < 3)
            {
                cardGenerator.DrawNewCard(1);
            }

            if (ShouldDiscard(cardInHand.GetComponent<Card>().GetValue()))
            {
                pile.DiscardCardsInPile();
            }
        }
        else if (usingUnderSideCards || usingOverSideCards)
        {
            pile.AddCardsToPile(cardInHand);
            RemoveCardFromList(cardInHand);
            PickUpPile();
        }
    }

    void PickUpPile()
    {
        List<GameObject> pileCards = pile.GetCardsInPile();
        handCards.AddRange(pileCards);
        pile.ClearPile();
        DisplayCardCount();
    }

    void RemoveCardFromList(GameObject cardInHand)
    {
        if (usingOverSideCards)
        {
            overSideCards.Remove(cardInHand);
        }
        else if (usingUnderSideCards)
        {
            underSideCards.Remove(cardInHand);
        }
        else
        {
            handCards.Remove(cardInHand);
        }
    }

    bool ShouldDiscard(int cardValue) => cardValue == 10 && (handCards.Count != 0 || overSideCards.Count != 0 || underSideCards.Count != 0);

    bool CanPlayCard(float cardValue) => cardValue >= pile.GetCurrentCard() || cardValue == 10 || cardValue == 2;

    void EndTurn(GameObject lastPlayed)
    {
        isTurn = gameManager.NextTurn(handCards, lastPlayed);
    }

    public void SetTurn(bool b) => isTurn = b;

    public List<GameObject> GetCards()
    {
        if (usingOverSideCards) return overSideCards;
        if (usingUnderSideCards) return underSideCards;
        return handCards;
    }

    void DisplayCardCount()
    {
        Debug.Log("AI has " + handCards.Count + " cards in hand.");
    }
}
