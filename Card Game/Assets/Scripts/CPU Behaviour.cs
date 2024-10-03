using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIHand : MonoBehaviour
{
    [Header("Hand Settings")]
    [SerializeField] List<GameObject> handCards;
    [Header("Side Settings")]
    [SerializeField] List<GameObject> underSideCards, overSideCards;
    [SerializeField] bool isTurn;
    [SerializeField] int turnNumber;
    [SerializeField] float playDelay;

    bool usingOverSideCards, usingUnderSideCards;
    bool isPlaying;

    Pile pile;
    CardGenrator cardGenerator;
    GameManager gameManager;

    void Start()
    {
        pile = FindFirstObjectByType<Pile>();
        cardGenerator = FindFirstObjectByType<CardGenrator>();
        gameManager = FindFirstObjectByType<GameManager>();
        isPlaying = false;
    }

    public void SetTurnNumber(int i)
    {
        turnNumber = i;
    }

    public void AddHandCards(GameObject newCard)
    {
        handCards.Add(newCard);
    }

    public void SetUnderSideCards(List<GameObject> newCards) => underSideCards = newCards;
    public void SetOverSideCards(List<GameObject> newCards) => overSideCards = newCards;

    void Update()
    {
        UpdateSideUsage();
        CheckTurn();

        if (isTurn && !isPlaying)
        {
            StartCoroutine(PlayAITurnWithDelay());
        }
    }

    void CheckTurn()
    {
        isTurn = turnNumber == gameManager.GetTurn();
    }

    void UpdateSideUsage()
    {
        usingOverSideCards = handCards.Count == 0 && overSideCards.Count > 0;
        usingUnderSideCards = handCards.Count == 0 && overSideCards.Count == 0 && underSideCards.Count > 0;
    }

    IEnumerator PlayAITurnWithDelay()
    {
        isPlaying = true;
        yield return new WaitForSeconds(playDelay);
        bool playAgain;
        do
        {
            playAgain = false;
            GameObject selectedCard = null;

            List<GameObject> currentCards = GetCards();

            List<GameObject> playableCards = new List<GameObject>();
            List<GameObject> specialCards = new List<GameObject>();

            foreach (GameObject card in currentCards)
            {
                Card cardComponent = card.GetComponent<Card>();
                int cardValue = cardComponent.GetValue();

                if (CanPlayCard(cardValue))
                {
                    if (cardValue == 2 || cardValue == 10)
                    {
                        specialCards.Add(card);  
                    }
                    else
                    {
                        playableCards.Add(card); 
                    }
                }
            }

            if (playableCards.Count > 0)
            {
                playableCards.Sort((a, b) => a.GetComponent<Card>().GetValue().CompareTo(b.GetComponent<Card>().GetValue()));
                selectedCard = playableCards[0];
            }
            else if (specialCards.Count > 0)
            {
                selectedCard = specialCards[0]; 
            }

            if (selectedCard != null)
            {
                PlayCard(selectedCard);

                int cardValue = selectedCard.GetComponent<Card>().GetValue();
                if (cardValue == 2 || cardValue == 10)
                {
                    playAgain = true;
                    Debug.Log("AI played a special card (2 or 10), playing again.");
                    yield return new WaitForSeconds(playDelay);
                }
                else
                {
                    gameManager.NextTurn(selectedCard);
                }
            }
            else
            {
                if (pile.GetCardsInPile().Count > 0)
                {
                    PickUpPile();
                    Debug.Log("No playable cards, AI picking up the pile.");
                    gameManager.NextTurn(null);
                }
                else
                {
                    Debug.Log("Pile is empty, AI can't pick up.");
                }
                playAgain = false;
            }

        } while (playAgain);

        CheckTurn();

        if (handCards.Count < 3 && cardGenerator.GetDeck().Count != 0)
        {
            cardGenerator.DrawNewCard(3 - handCards.Count, false);
        }

        isPlaying = false;
    }

    void PlayCard(GameObject cardInHand)
    {
        if (!isTurn || gameManager.GetWinner()) return;

        if (CanPlayCard(cardInHand.GetComponent<Card>().GetValue()))
        {
            RemoveCardFromList(cardInHand);
            pile.AddCardsToPile(cardInHand);

            if (handCards.Count < 3 && cardGenerator.GetDeck().Count != 0)
            {
                cardGenerator.DrawNewCard(3 - handCards.Count, false);
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

    public void SetTurn(bool b) => isTurn = b;

    public List<GameObject> GetCards()
    {
        if (usingOverSideCards) return overSideCards;
        if (usingUnderSideCards) return underSideCards;
        return handCards;
    }
}
