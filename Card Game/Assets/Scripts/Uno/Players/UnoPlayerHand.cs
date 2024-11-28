using System.Collections;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using TMPro;
using UnityEngine;

public class UnoPlayerHand : MonoBehaviour
{
    #region Variables
    [Header("Cards")]
    [SerializeField] List<GameObject> handCards;
    [Header("Transform and Spacing")]
    [SerializeField] Transform handTransform;
    [SerializeField] float baseCardSpacing = 150f, maxHandWidth = 1000f, popUpHeight = 50f;
    [Space]
    [SerializeField] Vector2 isTurnPos, isNotTurnPos;
    [Header("Turn and Play")]
    [SerializeField] bool isTurn;
    [SerializeField] bool canEndTurn;
    [SerializeField] int turnNumber;
    [SerializeField] GameObject endTurnButton;
    [Space]
    [SerializeField] float lerpSpeed;
    [SerializeField] TextMeshProUGUI cardAmountText;
    [SerializeField] Vector2 cardAmountTextOffset;

    GameObject hoveredCard;

    int cardsPerPlayer;
    int savedCardValue;

    bool isPaused;

    List<GameObject> selectedCards = new List<GameObject>(0);

    UnoPile pile;
    UnoCardGenerator cardGenerator;
    GameManager gameManager;
    AudioManager audioManager;
    #endregion

    void Awake()
    {
        pile = FindFirstObjectByType<UnoPile>();
        cardGenerator = FindFirstObjectByType<UnoCardGenerator>();
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

    void UpdateCardSortingOrder(List<GameObject> cards)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].GetComponent<SpriteRenderer>().sortingOrder = i;
        }
    }

    public void PickupAtBeginning()
    {
        if (gameManager.GetGameHasStarted()) return;

        cardGenerator.DrawNewCard(cardsPerPlayer - handCards.Count, true);
    }

    #endregion

    void Update()
    {
        isPaused = Time.timeScale == 0f;
        if (isPaused) { return; }

        SetCardAmountText();
        SortCards();
        CanEndTurn();
        CheckTurn();
        DetectHover();
    }

    #region Sorting
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

    public void SortHandCards()
    {
        handCards.Sort((a, b) => a.GetComponent<UnoCard>().GetValue().CompareTo(b.GetComponent<UnoCard>().GetValue()));
    }

    public void SortCards()
    {
        ArrangeCards(handCards, handTransform, baseCardSpacing, maxHandWidth);

        Vector2 currentPos = isTurn || !gameManager.GetGameHasStarted() ? isTurnPos : isNotTurnPos;
        handTransform.position = Vector2.Lerp(handTransform.position, currentPos, lerpSpeed * Time.deltaTime);
    }

    void ArrangeCards(List<GameObject> cards, Transform parent, float spacing, float maxWidth, float offset = 0)
    {
        if (cards.Count == 0) return;

        float cardSpacing = Mathf.Min(spacing, maxWidth / cards.Count);

        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].transform.SetParent(parent);

            cards[i].GetComponent<SpriteRenderer>().sortingOrder = i;

            Vector2 cardPosition;
            if (cards.Count > 1)
            {
                float horizontalOffset = cardSpacing * (i - (cards.Count - 1) / 2f);
                cardPosition = cards[i] == hoveredCard ? new Vector2(horizontalOffset + offset, offset + popUpHeight) : new Vector2(horizontalOffset + offset, offset);
            }
            else
            {
                cardPosition = cards[i] == hoveredCard ? new Vector2(offset, offset + popUpHeight) : new Vector2(offset, offset);
            }

            cards[i].transform.localPosition = Vector2.Lerp(cards[i].transform.localPosition, cardPosition, lerpSpeed * Time.deltaTime);

            for (int ii = 0; ii < cards.Count; ii++)
            {
                if (cards[i] == cards[ii] && ii != i)
                {
                    cards.RemoveAt(ii);
                }
            }
        }
    }
    #endregion

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

    #region Play
    void DetectHover()
    {
        RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
        hoveredCard = hit.collider ? hit.collider.gameObject : null;

        if (hoveredCard != null && Input.GetKeyDown(KeyCode.Mouse0))
        {
            PlayCard(hoveredCard);

            if (!gameManager.GetGameHasStarted() && selectedCards.Count < 2)
            {
                selectedCards.Add(hoveredCard);
            }
        }
    }

    void PlayCard(GameObject cardInHand)
    {
        if (!gameManager.GetGameHasStarted()) return;

        int cardValue = cardInHand.GetComponent<UnoCard>().GetValue();
        if (!isTurn || gameManager.GetWinner() || (savedCardValue != 0 && savedCardValue != cardValue)) return;

        if (CanPlayCard(cardValue, cardInHand))
        {
            audioManager.PlayCardSFX();
            RemoveCardFromList(cardInHand);
            pile.AddCardsToPile(cardInHand);

            if (HasSameValueCard(cardValue))
            {
                savedCardValue = cardValue;
                canEndTurn = true;
            }
            else
            {
                savedCardValue = 0;
                canEndTurn = false;
                gameManager.NextTurn(cardInHand);
                CheckTurn();
            }

            if(handCards.Count > 0 && cardGenerator.GetDeck().Count > 0)
            {
                cardGenerator.DrawNewCard(cardsPerPlayer - handCards.Count, true);
            }

            SortHandCards();
        }
        else
        {
            //Draw card until can play
        }

        StartCoroutine(gameManager.ProcessWin("Player"));
    }
    #endregion

    #region Play Conditions
    void RemoveCardFromList(GameObject cardInHand)
    {
        handCards.Remove(cardInHand);
    }

    public bool CanPlayCard(float cardValue, GameObject cardInHand)
    {
        if (cardValue >= pile.GetCurrentCard())
        {
            if (cardInHand != null)
            {
                handCards.Remove(cardInHand);
            }

            return true;
        }

        return false;
    }

    bool HasSameValueCard(int cardValue)
    {
        foreach (GameObject card in handCards)
        {
            if (card.GetComponent<UnoCard>().GetValue() == cardValue)
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
            if (CanPlayCard(currentList[i].GetComponent<UnoCard>().GetValue(), null))
            {
                hasCardToPlay = true;
            }
        }

        return hasCardToPlay;
    }
    #endregion

    public List<GameObject> GetHandCards()
    {
        return handCards;
    }
}
