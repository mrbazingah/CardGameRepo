using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHand : MonoBehaviour
{
    #region Variables
    [SerializeField] List<GameObject> handCards;
    [SerializeField] List<GameObject> underSideCards, overSideCards;
    [Space]
    [SerializeField] Transform handTransform;
    [SerializeField] float baseCardSpacing = 150f, verticalSpacing = 50f, maxHandWidth = 1000f, popUpHeight = 50f;
    [Space]
    [SerializeField] Transform underSideTransform, overSideTransform;
    [SerializeField] float sideBaseCardSpacing = 150f, sideVerticalSpacing = 50f, sideMaxHandWidth = 1000f, overSideOffset;
    [Space]
    [SerializeField] bool isTurn;
    [SerializeField] bool canEndTurn;
    [SerializeField] int turnNumber;
    [SerializeField] GameObject endTurnButton;
    [Space]
    [SerializeField] float playChanceDelay;
    [SerializeField] float lerpSpeed;

    GameObject hoveredCard;
    bool usingOverSideCards, usingUnderSideCards;

    int savedCardValue;
    bool hasDiscarded;

    Pile pile;
    CardGenrator cardGenerator;
    GameManager gameManager;
    AudioManager audioManager;
    #endregion

    void Awake()
    {
        pile = FindFirstObjectByType<Pile>();
        cardGenerator = FindFirstObjectByType<CardGenrator>();
        gameManager = FindFirstObjectByType<GameManager>();
        audioManager = FindFirstObjectByType<AudioManager>();
    }

    #region SetCards
    public void AddHandCards(GameObject newCard)
    {
        handCards.Add(newCard);
        UpdateCardSortingOrder(handCards);
    }

    public void SetUnderSideCards(List<GameObject> newCards) => underSideCards = newCards;
    public void SetOverSideCards(List<GameObject> newCards) => overSideCards = newCards;

    void UpdateCardSortingOrder(List<GameObject> cards)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].GetComponent<SpriteRenderer>().sortingOrder = i;
        }
    }
    #endregion

    void Update()
    {
        CanEndTurn();
        SortCards(false);
        DetectHover();
        UpdateSideUsage();
        UpdateColliders();
        CheckTurn();
    }

    #region Turn
    public void SetTurnNumber(int i)
    {
        turnNumber = i;
    }

    void CheckTurn()
    {
        isTurn = turnNumber == gameManager.GetTurn();
    }

    void CanEndTurn()
    {
        endTurnButton.SetActive(canEndTurn);
    }

    public void EndTurn()
    {
        gameManager.NextTurn(null);
        canEndTurn = false;
        savedCardValue = 0;
        endTurnButton.SetActive(false);
    }

    public bool GetTurn()
    {
        return isTurn;
    }
    #endregion

    #region Sorting
    void UpdateSideUsage()
    {
        usingOverSideCards = handCards.Count == 0 && overSideCards.Count > 0;
        usingUnderSideCards = handCards.Count == 0 && overSideCards.Count == 0 && underSideCards.Count > 0;
    }

    void UpdateColliders()
    {
        for (int i = 0; i < underSideCards.Count; i++)
        {
            BoxCollider2D boxCollider = underSideCards[i].GetComponent<BoxCollider2D>();
            boxCollider.enabled = usingUnderSideCards;
        }
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

                cards[i].transform.localPosition = cards[i] == hoveredCard ? cardPosition + new Vector3(0, popUpHeight, 0) : Vector2.Lerp(cards[i].transform.localPosition, cardPosition, lerpSpeed);
            }
            else
            {
                cards[i].transform.localPosition = Vector3.zero;
            }
        }
    }

    #region Hover
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
                    PlayCard(hoveredCard, false);
                }
                else
                {
                    Debug.LogError("Card not found");
                }
            }
        }
    }
    #endregion
    #endregion

    #region Play
    void PlayCard(GameObject cardInHand, bool isChanceCard)
    {
        int cardValue = cardInHand.GetComponent<Card>().GetValue();
        if (!isTurn || gameManager.GetWinner() || (savedCardValue != 0 && savedCardValue != cardValue)) return;

        if (CanPlayCard(cardValue))
        {
            audioManager.PlayCardSFX();

            RemoveCardFromList(cardInHand);
            pile.AddCardsToPile(cardInHand);

            if (ShouldDiscard(cardValue))
            {
                StartCoroutine(pile.DiscardCardsInPile());
                savedCardValue = 0;
                canEndTurn = false;
            }

            if (HasSameValueCard(cardValue) || cardValue == 2 || cardValue == 10 || hasDiscarded)
            {
                if (HasSameValueCard(cardValue) && cardValue != 2 && cardValue != 10)
                {
                    savedCardValue = cardValue;
                    canEndTurn = true;
                }

                hasDiscarded = false;
            }
            else
            {
                savedCardValue = 0;
                canEndTurn = false;
                gameManager.NextTurn(cardInHand);
                CheckTurn(); 
                cardGenerator.DrawNewCard(3 - handCards.Count, true);
            }
        }
        else if (isChanceCard)
        {
            pile.AddCardsToPile(cardInHand);
            RemoveCardFromList(cardInHand);
            PickUpPile(cardInHand);
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
                bool hasCardToPlay = HasCardToPlay(overSideCards);

                if (!hasCardToPlay)
                {
                    pile.AddCardsToPile(cardInHand);
                    RemoveCardFromList(cardInHand);
                    PickUpPile(cardInHand);
                }
            }
            else 
            {
                bool hasCardToPlay = HasCardToPlay(handCards);

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
        }
        
        savedCardValue = 0;
        canEndTurn = false;
        gameManager.NextTurn(cardInHand);
        audioManager.PlayShufflingSFX();
        CheckTurn();
    }

    public void PlayChanceCard()
    {
        StartCoroutine(ProcessChanceCard());
    }

    IEnumerator ProcessChanceCard()
    {
        GameObject cardFromDeck = cardGenerator.GetChanceCard();
        cardGenerator.GetDeck().Remove(cardFromDeck);

        yield return new WaitForSeconds(playChanceDelay);

        PlayCard(cardFromDeck, true);
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

    bool ShouldDiscard(int cardValue)
    {
        if (cardValue == 10 && (handCards.Count != 0 || overSideCards.Count != 0 || underSideCards.Count != 0))
        {
            hasDiscarded = true;
            return true;
        }

        List<GameObject> pileCards = pile.GetCardsInPile();

        if (pileCards.Count >= 4)
        {
            int lastCardValue = pileCards[pileCards.Count - 1].GetComponent<Card>().GetValue();
            bool allSame = true;

            for (int i = pileCards.Count - 2; i >= pileCards.Count - 4; i--)
            {
                int currentCardValue = pileCards[i].GetComponent<Card>().GetValue();
                if (currentCardValue != lastCardValue)
                {
                    allSame = false;
                    break;
                }
            }

            if (allSame)
            {
                hasDiscarded = true;
                return true;
            }
        }

        hasDiscarded = false;
        return false;
    }

    public bool CanPlayCard(float cardValue) => cardValue >= pile.GetCurrentCard() || cardValue == 10 || cardValue == 2;

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

    bool HasCardToPlay(List<GameObject> currentList)
    {
        bool hasCardToPlay = false;

        for (int i = 0; i < currentList.Count; i++)
        {
            if (CanPlayCard(currentList[i].GetComponent<Card>().GetValue()))
            {
                hasCardToPlay = true;
            }
        }

        return hasCardToPlay;
    }

    public bool CanChance()
    {
        bool b = !isTurn && !gameManager.GetWinner() && !HasCardToPlay(handCards);
        return b;
    }
    #endregion

    public List<GameObject> GetCards()
    {
        if (usingOverSideCards) return overSideCards;
        if (usingUnderSideCards) return underSideCards;
        return handCards;
    }
}
