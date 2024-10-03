using System.Collections.Generic;
using UnityEngine;

public class PlayerHand : MonoBehaviour
{
    [Header("Hand Settings")]
    [SerializeField] List<GameObject> handCards;
    [SerializeField] Transform handTransform;
    [SerializeField] float baseCardSpacing = 150f, verticalSpacing = 50f, maxHandWidth = 1000f, popUpHeight = 50f;
    [Header("Side Settings")]
    [SerializeField] List<GameObject> underSideCards, overSideCards;
    [SerializeField] Transform underSideTransform, overSideTransform;
    [SerializeField] float sideBaseCardSpacing = 150f, sideVerticalSpacing = 50f, sideMaxHandWidth = 1000f, overSideOffset;
    [Space]
    [SerializeField] bool isTurn;
    [SerializeField] int turnNumber;
    [SerializeField] GameObject pickupButton;

    GameObject hoveredCard;
    bool usingOverSideCards, usingUnderSideCards;

    int savedCardValue;

    Pile pile;
    CardGenrator cardGenerator;
    GameManager gameManager;

    void Start()
    {
        pile = FindFirstObjectByType<Pile>();
        cardGenerator = FindFirstObjectByType<CardGenrator>();
        gameManager = FindFirstObjectByType<GameManager>();
    }

    public void SetTurnNumber(int i)
    {
        turnNumber = i;
    }

    public void AddHandCards(GameObject newCard)
    {
        handCards.Add(newCard);
        UpdateCardSortingOrder(handCards);
    }

    void UpdateCardSortingOrder(List<GameObject> cards)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].GetComponent<SpriteRenderer>().sortingOrder = i;
        }
    }

    public void SetUnderSideCards(List<GameObject> newCards) => underSideCards = newCards;
    public void SetOverSideCards(List<GameObject> newCards) => overSideCards = newCards;

    void Update()
    {
        SortCards(false);
        DetectHover();
        UpdateSideUsage();
        CheckTurn();
    }

    void CheckTurn()
    {
        isTurn = turnNumber == gameManager.GetTurn();
    }

    #region Sorting
    void UpdateSideUsage()
    {
        usingOverSideCards = handCards.Count == 0 && overSideCards.Count > 0;
        usingUnderSideCards = handCards.Count == 0 && overSideCards.Count == 0 && underSideCards.Count > 0;
    }

    public void SortCards(bool sortSideCards)
    {
        if (handCards.Count > 0)
        {
            ArrangeCards(handCards, handTransform, baseCardSpacing, verticalSpacing, maxHandWidth);
        }

        if (sortSideCards)
        {
            ArrangeCards(overSideCards, overSideTransform, sideBaseCardSpacing, sideVerticalSpacing, sideMaxHandWidth, overSideOffset);
            ArrangeCards(underSideCards, underSideTransform, sideBaseCardSpacing, sideVerticalSpacing, sideMaxHandWidth);
        }

        ApplyHoverEffect(overSideCards, overSideTransform, sideBaseCardSpacing, sideVerticalSpacing, sideMaxHandWidth, overSideOffset);
        ApplyHoverEffect(underSideCards, underSideTransform, sideBaseCardSpacing, sideVerticalSpacing, sideMaxHandWidth);
    }

    void ArrangeCards(List<GameObject> cards, Transform parent, float spacing, float verticalSpace, float maxWidth, float offset = 0)
    {
        if (cards.Count == 0) return;

        float cardSpacing = Mathf.Min(spacing, maxWidth / cards.Count);

        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].transform.SetParent(parent);

            if (cards.Count > 1)
            {
                float horizontalOffset = cardSpacing * (i - (cards.Count - 1) / 2f);
                float normalizedPosition = 2f * i / (cards.Count - 1) - 1f;
                float verticalOffset = verticalSpace * (1 - normalizedPosition * normalizedPosition);
                Vector3 cardPosition = new Vector3(horizontalOffset + offset, verticalOffset + offset, 0);

                cards[i].transform.localPosition = cards[i] == hoveredCard ? cardPosition + new Vector3(0, popUpHeight, 0) : cardPosition;
            }
            else
            {
                cards[i].transform.localPosition = Vector3.zero;
            }
        }
    }

    void ApplyHoverEffect(List<GameObject> cards, Transform parent, float spacing, float verticalSpace, float maxWidth, float offset = 0)
    {
        if (cards.Count == 0) return;

        float cardSpacing = Mathf.Min(spacing, maxWidth / cards.Count);

        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].transform.SetParent(parent);

            if (cards.Count > 1)
            {
                float horizontalOffset = cardSpacing * (i - (cards.Count - 1) / 2f);
                float normalizedPosition = 2f * i / (cards.Count - 1) - 1f;
                float verticalOffset = verticalSpace * (1 - normalizedPosition * normalizedPosition);
                Vector3 cardPosition = new Vector3(horizontalOffset + offset, verticalOffset + offset, 0);

                cards[i].transform.localPosition = cards[i] == hoveredCard ? cardPosition + new Vector3(0, popUpHeight, 0) : cardPosition;
            }
            else
            {
                cards[i].transform.localPosition = Vector3.zero;
            }
        }
    }

    void DetectHover()
    {
        RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
        hoveredCard = hit.collider ? hit.collider.gameObject : null;

        if (hoveredCard != null && Input.GetKeyDown(KeyCode.Mouse0))
        {
            if (handCards.Contains(hoveredCard) && !usingOverSideCards && !usingUnderSideCards ||
                overSideCards.Contains(hoveredCard) && usingOverSideCards ||
                underSideCards.Contains(hoveredCard) && usingUnderSideCards)
            {
                if (hoveredCard != null)
                {
                    PlayCard(hoveredCard);
                }
                else
                {
                    Debug.LogError("Card not found");
                }
            }
        }
    }
    #endregion

    #region Play
    void PlayCard(GameObject cardInHand)
    {
        int cardValue = cardInHand.GetComponent<Card>().GetValue();
        if (!isTurn || gameManager.GetWinner() || (savedCardValue != 0 && savedCardValue != cardValue)) return;

        if (CanPlayCard(cardValue))
        {
            RemoveCardFromList(cardInHand);
            pile.AddCardsToPile(cardInHand);

            if (ShouldDiscard(cardValue))
            {
                pile.DiscardCardsInPile();
                savedCardValue = 0;
            }

            if (HasSameValueCard(cardValue) || cardValue == 2 || cardValue == 10) 
            {
                if (HasSameValueCard(cardValue) && cardValue != 2 && cardValue != 10)
                {
                    savedCardValue = cardValue;
                }
            }
            else
            {
                savedCardValue = 0;
                gameManager.NextTurn(cardInHand);
                CheckTurn();
            }

            if (handCards.Count < 3 && cardGenerator.GetDeck().Count != 0)
            {
                cardGenerator.DrawNewCard(3 - handCards.Count, true);
            }
        }
        else
        {
            if (underSideCards.Contains(cardInHand) && usingUnderSideCards)
            {
                pile.AddCardsToPile(cardInHand);
                RemoveCardFromList(cardInHand);
                PickUpPile(cardInHand);
            }
            else if (usingOverSideCards)
            {
                bool hasCardToPlay = false;

                for (int i = 0; i < overSideCards.Count; i++)
                {
                    if (CanPlayCard(overSideCards[i].GetComponent<Card>().GetValue()))
                    {
                        hasCardToPlay = true;
                    }
                }

                if (!hasCardToPlay)
                {
                    pile.AddCardsToPile(cardInHand);
                    RemoveCardFromList(cardInHand);
                    PickUpPile(cardInHand);
                }
            }
            else
            {
                bool hasCardToPlay = false;

                for (int i = 0; i < handCards.Count; i++)
                {
                    if (CanPlayCard(handCards[i].GetComponent<Card>().GetValue()))
                    {
                        hasCardToPlay = true;
                    }
                }

                if (!hasCardToPlay)
                {
                    pile.AddCardsToPile(cardInHand);
                    RemoveCardFromList(cardInHand);
                    PickUpPile(cardInHand);
                }
            }
        }
    }

    void PickUpPile(GameObject cardInHand)
    {
        List<GameObject> pileCards = pile.GetCardsInPile();

        if (pileCards.Count > 0)
        {
            handCards.AddRange(pileCards);
            pile.ClearPile();
            UpdateCardSortingOrder(handCards);

            Debug.Log($"Player picked up {pileCards.Count} cards from the pile.");
        }
        else
        {
            Debug.Log("Pile is empty. Nothing to pick up.");
        }

        savedCardValue = 0;
        gameManager.NextTurn(cardInHand);
        CheckTurn();
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

    bool HasSameValueCard(int cardValue)
    {
        foreach (GameObject card in handCards)
        {
            if (card.GetComponent<Card>().GetValue() == cardValue)
            {
                return true;
            }
        }
        return false;
    }
    #endregion

    public void SetTurn(bool b) => isTurn = b;

    public List<GameObject> GetCards()
    {
        if (usingOverSideCards) return overSideCards;
        if (usingUnderSideCards) return underSideCards;
        return handCards;
    }
}
