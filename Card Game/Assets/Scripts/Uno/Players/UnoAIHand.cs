using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UnoAIHand : MonoBehaviour
{
    #region Variables
    [Header("Cards")]
    [SerializeField] List<GameObject> handCards;
    [Header("Transform and Spacing")]
    [SerializeField] Transform handTransform;
    [SerializeField] float baseCardSpacing = 150f, verticalSpacing = 50f, maxHandWidth = 1000f;
    [Space]
    [SerializeField] Vector2 isTurnPos, isNotTurnPos;
    [Header("Turn and Play")]
    [SerializeField] bool isTurn;
    [SerializeField] int turnNumber;
    [SerializeField] float playDelay;
    [Space]
    [SerializeField] float lerpSpeed;
    [SerializeField] TextMeshProUGUI cardAmountText;
    [SerializeField] Vector2 cardAmountTextOffset;

    bool isPlaying;
    int cardsPerPlayer;
    bool isPaused;

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
        isPlaying = false;
        cardsPerPlayer = cardGenerator.GetCardsPerPlayer();
    }

    #region Set Cards
    public void SetTurnNumber(int i)
    {
        turnNumber = i;
    }

    public void AddHandCards(GameObject newCard)
    {
        handCards.Add(newCard);
    }
    
    void SetCardAmountText()
    {
        if (handCards.Count > 0)
        {
            cardAmountText.text = handCards.Count.ToString();
            Vector2 cardAmountTextPos = handCards[handCards.Count - 1].transform.position;
            cardAmountTextPos += cardAmountTextOffset;
            cardAmountText.transform.position = cardAmountTextPos;

            cardAmountText.gameObject.SetActive(true);
        }
        else
        {
            cardAmountText.gameObject.SetActive(false);
        }
    }
    #endregion

    void Update()
    {
        isPaused = Time.timeScale == 0f;
        if (isPaused) { return; }

        SetCardAmountText();
        SortCards();
        RemoveDubbleCards();
        CheckTurn();

        if (isTurn && !isPlaying)
        {
            StartCoroutine(PlayAITurnWithDelay());
        }
    }

    #region Sorting
    public void SortCards()
    {
        if (handCards.Count > 0)
        {
            ArrangeCards(handCards, handTransform, baseCardSpacing, verticalSpacing, maxHandWidth);
        }

        Vector2 currentPos = isTurn || !gameManager.GetGameHasStarted() ? isTurnPos : isNotTurnPos;
        handTransform.position = Vector2.Lerp(handTransform.position, currentPos, lerpSpeed * Time.deltaTime);
    }

    void ArrangeCards(List<GameObject> cards, Transform parent, float spacing, float verticalSpace, float maxWidth, float offset = 0)
    {
        if (cards.Count == 0) return;

        float cardSpacing = Mathf.Min(spacing, maxWidth / cards.Count);

        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].transform.SetParent(parent);

            cards[i].GetComponent<SpriteRenderer>().sortingOrder = i;
            cards[i].GetComponent<UnoCard>().GetBack().GetComponent<SpriteRenderer>().sortingOrder = i + 1;

            if (cards.Count > 1)
            {
                float horizontalOffset = cardSpacing * (i - (cards.Count - 1) / 2f);
                float normalizedPosition = 2f * i / (cards.Count - 1) - 1f;
                float verticalOffset = verticalSpace * (1 - normalizedPosition * normalizedPosition);
                Vector3 cardPosition = new Vector3(horizontalOffset + offset, verticalOffset + offset, 0);

                cards[i].transform.localPosition = Vector2.Lerp(cards[i].transform.localPosition, cardPosition, lerpSpeed * Time.deltaTime);
            }
            else
            {
                cards[i].transform.localPosition = Vector3.zero;
            }

            GameObject child = cards[i].GetComponent<UnoCard>().GetBack();
            if (handCards.Contains(cards[i]) && child != null)
            {
                child.GetComponent<SpriteRenderer>().sortingLayerName = "BackCard";
            }
        }
    }

    void RemoveDubbleCards()
    {
        if (handCards.Count == 0) { return; }

        for (int i = 0; i < handCards.Count; i++)
        {
            for (int ii = 0; ii < handCards.Count; ii++)
            {
                if (handCards[i] == handCards[ii] && ii != i)
                {
                    handCards.Remove(handCards[ii]);
                }
            }
        }
    }
    #endregion

    #region Turn
    public void SetTurn(bool b) => isTurn = b;

    void CheckTurn()
    {
        if (!gameManager.GetGameHasStarted()) return;
        isTurn = turnNumber == gameManager.GetTurn();
    }

    public bool GetTurn()
    {
        return isTurn;
    }
    #endregion

    #region Play
    IEnumerator PlayAITurnWithDelay()
    {
        if (!gameManager.GetGameHasStarted()) yield break;

        isPlaying = true;

        yield return new WaitForSeconds(playDelay);

        bool playAgain;
        do
        {
            playAgain = false;
            GameObject selectedCard = null;

            List<GameObject> currentCards = handCards;

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
                if (HasSameValueCard(cardValue))
                {
                    yield return new WaitForSeconds(0.1f);

                    playAgain = true;

                    StartCoroutine(gameManager.ProcessWin("AI"));

                    yield return new WaitForSeconds(playDelay);
                }
                else
                {
                    StartCoroutine(gameManager.ProcessWin("AI"));
                    gameManager.NextTurn(selectedCard);

                    playAgain = false;
                }
            }
            else
            {
                playAgain = false;
            }

        } while (playAgain);

        CheckTurn();

        if (handCards.Count < 3 && cardGenerator.GetDeck().Count != 0)
        {
            cardGenerator.DrawNewCard(cardsPerPlayer - handCards.Count, false);
        }

        isPlaying = false;
    }

    void PlayCard(GameObject cardInHand)
    {
        if (!isTurn || gameManager.GetWinner() || !gameManager.GetGameHasStarted()) return;

        int cardValue = cardInHand.GetComponent<Card>().GetValue();
        if (CanPlayCard(cardValue))
        {
            handCards.Remove(cardInHand);
            pile.AddCardsToPile(cardInHand);
            audioManager.PlayCardSFX();

            if (handCards.Count < 3 && cardGenerator.GetDeck().Count != 0)
            {
                cardGenerator.DrawNewCard(cardsPerPlayer - handCards.Count, false);
            }
        }
    }
    #endregion

    #region Play Conditions
    bool CanPlayCard(float cardValue) => cardValue >= pile.GetCurrentCard();

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
    #endregion
}
