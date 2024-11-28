using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AIHand : MonoBehaviour
{
    #region Variables
    [Header("Cards")]
    [SerializeField] List<GameObject> handCards;
    [SerializeField] List<GameObject> underSideCards, overSideCards;
    [Header("Transform and Spacing")]
    [SerializeField] Transform handTransform;
    [SerializeField] float baseCardSpacing = 150f, verticalSpacing = 50f, maxHandWidth = 1000f;
    [Space]
    [SerializeField] Transform underSideTransform, overSideTransform;
    [SerializeField] float sideBaseCardSpacing = 150f, sideVerticalSpacing = 50f, sideMaxHandWidth = 1000f, overSideOffset;
    [Space]
    [SerializeField] Vector2 isTurnPos, isNotTurnPos;
    [Header("Turn and Play")]
    [SerializeField] bool isTurn;
    [SerializeField] int turnNumber;
    [SerializeField] float playDelay;
    [SerializeField] int chanceToPlayChance; //Precentage from 0% too 100%
    [Space]
    [SerializeField] float lerpSpeed;
    [SerializeField] TextMeshProUGUI cardAmountText;
    [SerializeField] Vector2 cardAmountTextOffset;

    bool usingOverSideCards, usingUnderSideCards;
    bool isPlaying;

    int cardsPerPlayer;

    bool isPaused;

    Pile pile;
    CardGenerator cardGenerator;
    GameManager gameManager;
    AudioManager audioManager;
    #endregion

    void Awake()
    {
        pile = FindFirstObjectByType<Pile>();
        cardGenerator = FindFirstObjectByType<CardGenerator>();
        gameManager = FindFirstObjectByType<GameManager>();
        audioManager = FindFirstObjectByType<AudioManager>();
    }

    void Start()
    {
        isPlaying = false;
        cardsPerPlayer = cardGenerator.GetCardsPerPlayer();

        if (PlayerPrefs.HasKey("AiChancePrecentage"))
        {
            chanceToPlayChance = PlayerPrefs.GetInt("AiChancePrecentage");
        }
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

    public void SetUnderSideCards(List<GameObject> newCards) => underSideCards = newCards;
    public void SetOverSideCards(List<GameObject> newCards) => overSideCards = newCards;

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

    public void SwitchOutSideCards()
    {
        // Create a list to store all card values and their corresponding GameObjects
        List<(int value, GameObject card)> allCards = new List<(int, GameObject)>();

        // Collect all cards from handCards
        for (int i = 0; i < handCards.Count; i++)
        {
            int value = handCards[i].GetComponent<Card>().GetValue();
            allCards.Add((value, handCards[i]));
        }

        // Collect all cards from overSideCards
        for (int i = 0; i < overSideCards.Count; i++)
        {
            int value = overSideCards[i].GetComponent<Card>().GetValue();
            allCards.Add((value, overSideCards[i]));
        }

        // Sort the combined list with special priority for cards 2, 10, and 14 (Ace)
        allCards.Sort((a, b) =>
        {
            // Check if either card is a special card
            bool aIsSpecial = (a.value == 2 || a.value == 10 || a.value == 14);
            bool bIsSpecial = (b.value == 2 || b.value == 10 || b.value == 14);

            // Prioritize special cards
            if (aIsSpecial && !bIsSpecial) return 1;  // a is special, b is not
            if (!aIsSpecial && bIsSpecial) return -1; // b is special, a is not
            if (aIsSpecial && bIsSpecial) return a.value.CompareTo(b.value); // Both are special; sort by value

            // For non-special cards, sort by value in ascending order
            return a.value.CompareTo(b.value);
        });

        // Distribute sorted cards: lowest values to handCards, highest (including special) to overSideCards
        for (int i = 0; i < handCards.Count; i++)
        {
            handCards[i] = allCards[i].card;

            // Remove covers on switched cards
            Card currentCardScript = handCards[i].GetComponent<Card>();
            if (currentCardScript.GetBack() == null)
            {
                cardGenerator.ApplyCoverOnCards(handCards[i]);
            }
        }

        for (int i = 0; i < overSideCards.Count; i++)
        {
            overSideCards[i] = allCards[handCards.Count + i].card;

            // Add covers to switched cards
            Card currentCardScript = overSideCards[i].GetComponent<Card>();
            if (currentCardScript.GetBack() != null)
            {
                currentCardScript.RemoveChild();
            }

            overSideCards[i].GetComponent<SpriteRenderer>().sortingOrder = i + 3;
        }
    }
    #endregion

    void Update()
    {
        isPaused = Time.timeScale == 0f;
        if (isPaused) { return; }

        SetCardAmountText();
        UpdateSideUsage();
        UpdateColliders();
        SortCards();
        RemoveDubbleCards();
        CheckTurn();

        if (isTurn && !isPlaying)
        {
            StartCoroutine(PlayAITurnWithDelay());
        }
    }

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
        if (handCards.Count > 0)
        {
            ArrangeCards(handCards, handTransform, baseCardSpacing, verticalSpacing, maxHandWidth);
        }

        ArrangeCards(overSideCards, overSideTransform, sideBaseCardSpacing, sideVerticalSpacing, sideMaxHandWidth, overSideOffset);
        ArrangeCards(underSideCards, underSideTransform, sideBaseCardSpacing, sideVerticalSpacing, sideMaxHandWidth);

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

            if (cards == handCards || cards == underSideCards)
            {
                cards[i].GetComponent<SpriteRenderer>().sortingOrder = i;
                cards[i].GetComponent<Card>().GetBack().GetComponent<SpriteRenderer>().sortingOrder = i + 1;
            }
            else if (cards == overSideCards)
            {
                cards[i].GetComponent<SpriteRenderer>().sortingOrder = i + 3;
            }

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

            GameObject child = cards[i].GetComponent<Card>().GetBack();
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

            List<GameObject> currentCards = GetCards();

            List<GameObject> playableCards = new List<GameObject>();
            List<GameObject> specialCards = new List<GameObject>();

            foreach (GameObject card in currentCards)
            {
                Card cardComponent = card.GetComponent<Card>();
                int cardValue = cardComponent.GetValue();

                if (CanPlayCard(cardValue, false))
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

            if (playableCards.Count > 0 && !usingUnderSideCards)
            {
                playableCards.Sort((a, b) => a.GetComponent<Card>().GetValue().CompareTo(b.GetComponent<Card>().GetValue()));
                selectedCard = playableCards[0];
            }
            else if (specialCards.Count > 0 && !usingUnderSideCards)
            {
                selectedCard = specialCards[0];
            }
            else
            {
                if (usingUnderSideCards)
                {
                    int randomNumber = Random.Range(0, underSideCards.Count);
                    selectedCard = underSideCards[randomNumber];
                }
                else if (usingOverSideCards)
                {
                    int randomNumber = Random.Range(0, overSideCards.Count);
                    selectedCard = overSideCards[randomNumber];
                }
            }

            if (selectedCard != null)
            {
                PlayCard(selectedCard, false);

                int cardValue = selectedCard.GetComponent<Card>().GetValue();
                if (cardValue == 2 || ShouldDiscard(cardValue) || HasSameValueCard(cardValue))
                {
                    yield return new WaitForSeconds(0.1f);

                    playAgain = true;

                    if (GetCards().Count == 0 && (cardValue == 2 || cardValue == 10))
                    {
                        playAgain = false;
                        PickUpPile(selectedCard);
                    }

                    StartCoroutine(gameManager.ProcessWin("AI"));

                    yield return new WaitForSeconds(playDelay);
                }
                else
                {
                    if (GetCards().Count == 0 && cardValue == 14)
                    {
                        PickUpPile(selectedCard);
                    }
                    else
                    {
                        StartCoroutine(gameManager.ProcessWin("AI"));
                        gameManager.NextTurn(selectedCard);
                    }

                    playAgain = false;
                }
            }
            else
            {
                if (pile.GetCardsInPile().Count > 0)
                {
                    int random = 0;
                    if (cardGenerator.GetDeck().Count != 0)
                    {
                        random = Random.Range(0, 101);
                    }

                    if (random > chanceToPlayChance || cardGenerator.GetDeck().Count == 0)
                    {
                        PickUpPile(null);
                    }
                    else
                    {
                        playAgain = false;
                        PlayChanceCard();
                    }
                }

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

    void PlayCard(GameObject cardInHand, bool isChance)
    {
        if (!isTurn || gameManager.GetWinner() || !gameManager.GetGameHasStarted()) return;

        int cardValue = cardInHand.GetComponent<Card>().GetValue();
        if (CanPlayCard(cardValue, isChance))
        {
            RemoveCardFromList(cardInHand);
            pile.AddCardsToPile(cardInHand);
            audioManager.PlayCardSFX();

            if (ShouldDiscard(cardValue))
            {
                StartCoroutine(pile.DiscardCardsInPile());
            }

            if (handCards.Count < 3 && cardGenerator.GetDeck().Count != 0)
            {
                cardGenerator.DrawNewCard(cardsPerPlayer - handCards.Count, false);
            }

            if (isChance && !ShouldDiscard(cardValue) && cardValue != 2)
            {
                gameManager.NextTurn(cardInHand);
            }
        }
        else if (isChance)
        {
            RemoveCardFromList(cardInHand);
            PickUpPile(cardInHand);
        }
        else if (usingUnderSideCards || usingOverSideCards)
        {
            pile.AddCardsToPile(cardInHand);
            RemoveCardFromList(cardInHand);
            PickUpPile(cardInHand);
        }
    }

    public void PlayChanceCard()
    {
        StartCoroutine(ProcessChanceCard());
    }

    IEnumerator ProcessChanceCard()
    {
        GameObject cardFromDeck = cardGenerator.GetChanceCard();
        if (cardFromDeck == null) { yield break; }

        PlayCard(cardFromDeck, true);
    }
    #endregion

    #region Play Conditions
    void PickUpPile(GameObject cardInHand)
    {
        if (gameManager.GetWinner()) return;

        List<GameObject> pileCards = pile.GetCardsInPile();
        pile.ClearPile();

        handCards.AddRange(pileCards);
        gameManager.NextTurn(cardInHand);

        for (int i = 0; i < handCards.Count; i++)
        {
            handCards[i].GetComponent<Card>().RemoveChild();
            cardGenerator.ApplyCoverOnCards(handCards[i]);
        }

        audioManager.PlayShufflingSFX();
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
        if (cardValue == 10)
        {
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
                return true;
            }
        }

        return false;
    }

    bool CanPlayCard(float cardValue, bool isChance) => cardValue >= pile.GetCurrentCard(isChance) || cardValue == 10 || cardValue == 2;

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
            if (CanPlayCard(currentList[i].GetComponent<Card>().GetValue(), false))
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

    #region Gets
    public List<GameObject> GetCards()
    {
        UpdateSideUsage();

        if (usingOverSideCards) return overSideCards;
        if (usingUnderSideCards) return underSideCards;
        return handCards;
    }
    #endregion
}
