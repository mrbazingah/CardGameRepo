using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerHand : MonoBehaviour
{
    #region Variables
    [Header("Cards")]
    [SerializeField] List<GameObject> handCards;
    [SerializeField] List<GameObject> underSideCards, overSideCards;
    [Header("Transform")]
    [SerializeField] Transform handTransform;
    [SerializeField] float baseCardSpacing = 150f, maxHandWidth = 1000f, popUpHeight = 50f;
    [Space]
    [SerializeField] Transform underSideTransform, overSideTransform;
    [SerializeField] float sideBaseCardSpacing = 150f, sideMaxHandWidth = 1000f, overSideOffset;
    [Header("Turn")]
    [SerializeField] bool isTurn;
    [SerializeField] bool canEndTurn;
    [SerializeField] int turnNumber;
    [SerializeField] GameObject endTurnButton;
    [Space]
    [SerializeField] float playChanceDelay;
    [SerializeField] float lerpSpeed;
    [SerializeField] TextMeshProUGUI cardAmountText;
    [SerializeField] Vector2 cardAmountTextOffset;

    GameObject hoveredCard;
    bool usingOverSideCards, usingUnderSideCards;

    int cardsPerPlayer;
    int savedCardValue;
    bool hasDiscarded;

    List<GameObject> selectedCards = new List<GameObject>(0);

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

    void Start()
    {
        cardsPerPlayer = cardGenerator.GetCardsPerPlayer();
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
        SortCards();
        DetectHover();
        UpdateSideUsage();
        UpdateColliders();
        ChangeSideCards();
        CheckTurn();
        SetCardAmountText();

        handTransform.position = isTurn || !gameManager.GetGameHasStarted() ? new Vector2(0f, -3.55f) : new Vector2(0f, -4.5f);
    }

    void SetCardAmountText()
    {
        if (handCards.Count > 0)
        {
            cardAmountText.text = handCards.Count.ToString();
            Vector2 cardAmountTextPos = handCards[0].transform.position;
            cardAmountTextPos = new Vector2(cardAmountTextPos.x + cardAmountTextOffset.x, cardAmountTextOffset.y + handTransform.position.y);
            cardAmountText.transform.position = cardAmountTextPos;

            cardAmountText.gameObject.SetActive(true);
        }
        else
        {
            cardAmountText.gameObject.SetActive(false);
        }
    }

    void ChangeSideCards()
    {
        if (gameManager.GetGameHasStarted() || selectedCards.Count != 2) { return; }

        GameObject handCard = null;
        GameObject sideCard = null;
        GameObject lastSelectedCard = null;

        if (handCards.Contains(selectedCards[0]) && overSideCards.Contains(selectedCards[1]))
        {
            handCard = selectedCards[0];
            sideCard = selectedCards[1];

            for (int i = 0; i < handCards.Count; i++)
            {
                if (handCards[i] == handCard)
                {
                    handCards[i] = sideCard;
                    break;
                }
            }

            for (int i = 0; i < overSideCards.Count; i++)
            {
                if (overSideCards[i] == sideCard)
                {
                    overSideCards[i] = handCard;
                    break;
                }
            }

            audioManager.PlayCardSFX();
            handCards.Sort((a, b) => a.GetComponent<Card>().GetValue().CompareTo(b.GetComponent<Card>().GetValue()));
        }
        else if (handCards.Contains(selectedCards[1]) && overSideCards.Contains(selectedCards[0]))
        {
            handCard = selectedCards[1];
            sideCard = selectedCards[0];

            for (int i = 0; i < handCards.Count; i++)
            {
                if (handCards[i] == handCard)
                {
                    handCards[i] = sideCard;
                    break;
                }
            }

            for (int i = 0; i < overSideCards.Count; i++)
            {
                if (overSideCards[i] == sideCard)
                {
                    overSideCards[i] = handCard;
                    break;
                }
            }

            audioManager.PlayCardSFX();
            handCards.Sort((a, b) => a.GetComponent<Card>().GetValue().CompareTo(b.GetComponent<Card>().GetValue()));
        }
        else
        {
            lastSelectedCard = selectedCards[1];
        }

        selectedCards = new List<GameObject>(0);

        if (lastSelectedCard != null)
        {
            selectedCards.Add(lastSelectedCard);
        }
    }

    #region Turn
    public void SetTurnNumber(int i)
    {
        turnNumber = i;
    }

    void CheckTurn()
    {
        if (!gameManager.GetGameHasStarted()) return;
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

    public void SortCards()
    {
        ArrangeCards(handCards, handTransform, baseCardSpacing, maxHandWidth);
        ArrangeCards(overSideCards, overSideTransform, sideBaseCardSpacing, sideMaxHandWidth, overSideOffset);
        ArrangeCards(underSideCards, underSideTransform, sideBaseCardSpacing, sideMaxHandWidth);
    }

    void ArrangeCards(List<GameObject> cards, Transform parent, float spacing, float maxWidth, float offset = 0)
    {
        if (cards.Count == 0) return;

        float cardSpacing = Mathf.Min(spacing, maxWidth / cards.Count);

        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].transform.SetParent(parent);

            if (cards == handCards)
            {
                cards[i].GetComponent<SpriteRenderer>().sortingOrder = i;
            }
            else if (cards == overSideCards)
            {
                cards[i].GetComponent<SpriteRenderer>().sortingOrder = i + 3;
            }
            else
            {
                cards[i].GetComponent<SpriteRenderer>().sortingOrder = i;
                cards[i].GetComponent<Card>().GetBack().GetComponent<SpriteRenderer>().sortingOrder = i + 1;
            }

            if (cards.Count > 1)
            {
                float horizontalOffset = cardSpacing * (i - (cards.Count - 1) / 2f);
                Vector2 cardPosition = cards[i] == hoveredCard ? new Vector2(horizontalOffset + offset, offset + popUpHeight) : new Vector2(horizontalOffset + offset, offset);
                cards[i].transform.localPosition = Vector2.Lerp(cards[i].transform.localPosition, cardPosition, lerpSpeed * Time.deltaTime);
            }
            else
            {
                cards[i].transform.localPosition = Vector3.zero;
            }

            for (int ii  = 0; ii < cards.Count; ii++)
            {
                if (cards[i] == cards[ii] && ii != i)
                {
                    cards.RemoveAt(ii);
                }
            }
        }
    }

    public float GetPopUpHeight()
    {
        return popUpHeight;
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
            }

            if (!gameManager.GetGameHasStarted() && selectedCards.Count < 2)
            {
                selectedCards.Add(hoveredCard);
            }
        }
    }
    #endregion

    #region Play
    void PlayCard(GameObject cardInHand, bool isChanceCard)
    {
        if (!gameManager.GetGameHasStarted()) return;

        int cardValue = cardInHand.GetComponent<Card>().GetValue();
        if (!isTurn || gameManager.GetWinner() || (savedCardValue != 0 && savedCardValue != cardValue)) return;

        if (CanPlayCard(cardValue, isChanceCard, cardInHand))
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
                
                if (handCards.Count > 0 && cardGenerator.GetDeck().Count > 0 && !isChanceCard)
                {
                    cardGenerator.DrawNewCard(cardsPerPlayer - handCards.Count, true);
                }
            }
            else
            {
                savedCardValue = 0;
                canEndTurn = false;
                gameManager.NextTurn(cardInHand);
                CheckTurn();

                if (!isChanceCard)
                {
                    cardGenerator.DrawNewCard(cardsPerPlayer - handCards.Count, true);
                }
            }

            handCards.Sort((a, b) => a.GetComponent<Card>().GetValue().CompareTo(b.GetComponent<Card>().GetValue()));
        }
        else if (isChanceCard)
        {
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

        StartCoroutine(gameManager.ProcessWin("Player"));
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

        handCards.Sort((a, b) => a.GetComponent<Card>().GetValue().CompareTo(b.GetComponent<Card>().GetValue()));

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
        if (cardFromDeck == null) { yield break; }

        yield return new WaitForSeconds(playChanceDelay);

        PlayCard(cardFromDeck, true);
    }

    public void PickupAtBeginning()
    {
        if (gameManager.GetGameHasStarted()) return;

        cardGenerator.DrawNewCard(cardsPerPlayer - handCards.Count, true);
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

    public bool CanPlayCard(float cardValue, bool isChance, GameObject cardInHand)
    {
        if (cardValue >= pile.GetCurrentCard(isChance) || cardValue == 10 || cardValue == 2)
        {
            if (cardInHand != null)
            {
                GetCurrentCards().Remove(cardInHand);
                UpdateSideUsage();
            }

            if ((cardValue == 10 || cardValue == 2 || cardValue == 14) && GetCurrentCards().Count == 0)
            {
                return false;
            }

            return true;
        }

        return false;
    }

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
            if (CanPlayCard(currentList[i].GetComponent<Card>().GetValue(), false, null))
            {
                hasCardToPlay = true;
            }
        }

        return hasCardToPlay;
    }

    public bool CanChance()
    {
        return isTurn && !gameManager.GetWinner() && !HasCardToPlay(handCards);
    }
    #endregion

    public List<GameObject> GetCurrentCards()
    {
        if (usingOverSideCards) return overSideCards;
        if (usingUnderSideCards) return underSideCards;
        return handCards;
    }

    public List<GameObject> GetHandCards()
    {
        return handCards;
    }

    public List<GameObject> GetOverSideCards()
    {
        return overSideCards;
    }

    public List<GameObject> GetUnderSideCards()
    {
        return underSideCards;
    }

    public int GetCardsIndex()
    {
        if (usingOverSideCards) return 0;
        if (usingUnderSideCards) return 1;
        return 2;
    }
}
