using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System.Linq;

public class AIBehaviourHard : MonoBehaviour
{
    #region Variables
    [Header("Cards")]
    [SerializeField] List<GameObject> handCards;
    [SerializeField] List<GameObject> underSideCards, overSideCards;

    [Header("Transforms and UI")]
    [SerializeField] Transform handTransform;
    [SerializeField] float baseCardSpacing, maxHandWidth;
    [SerializeField] Transform underSideTransform, overSideTransform;
    [SerializeField] float sideBaseCardSpacing, sideMaxHandWidth, overSideOffset;
    [SerializeField] Vector2 isTurnPos, isNotTurnPos;
    [SerializeField] TextMeshProUGUI cardAmountText;
    [SerializeField] Vector2 cardAmountTextOffset;

    [Header("Turn and Timing")]
    [SerializeField] bool isTurn;
    [SerializeField] int turnNumber;
    [SerializeField] float playDelay = 1.0f;
    [SerializeField] float chanceDelay = 0.5f;
    [SerializeField, Range(0, 100)] int chanceToPlayChance = 50;
    [SerializeField] float lerpSpeed = 5f;

    bool usingOverSideCards, usingUnderSideCards;
    bool isPlaying, isPaused;
    int cardsPerPlayer;

    // References
    Pile pile;
    CardGenerator cardGenerator;
    GameManager gameManager;
    AudioManager audioManager;

    #endregion

    #region Memory System

    public enum CardLocationType
    {
        AIHand,
        AIFaceUp,
        AIFaceDown,
        PlayerHand,
        PlayerFaceUp,
        PlayerFaceDown,
        Pile,
        Discarded,
        Unknown
    }

    public class CardMemory
    {
        public int value;
        public CardLocationType location;
        public bool isKnown;
    }

    private List<CardMemory> cardMemory = new List<CardMemory>();
    private List<int> unknownCards = new List<int>();

    #endregion

    #region Unity Setup
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
            chanceToPlayChance = PlayerPrefs.GetInt("AiChancePrecentage");

        InitializeMemory();
    }

    void InitializeMemory()
    {
        // Assume 52 cards in deck; adjust if needed
        cardMemory.Clear();
        for (int value = 2; value <= 14; value++)
        {
            int count = (value == 14) ? 4 : 4; // Standard deck, 4 per value
            for (int i = 0; i < count; i++)
            {
                cardMemory.Add(new CardMemory { value = value, location = CardLocationType.Unknown, isKnown = false });
            }
        }
        unknownCards = cardMemory.Select(c => c.value).ToList();
    }

    #endregion

    void Update()
    {
        isPaused = Time.timeScale == 0f;
        if (isPaused) return;

        SetCardAmountText();
        UpdateSideUsage();
        SortCards();
        CheckTurn();

        if (isTurn && !isPlaying)
            StartCoroutine(PlayAITurnWithDelay());
    }


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

    #region Memory Update Methods

    // Call this when AI or player plays a card
    public void OnCardPlayed(int value, bool byAI)
    {
        var card = cardMemory.FirstOrDefault(c => c.value == value && c.isKnown == false);
        if (card != null)
        {
            card.isKnown = true;
            card.location = CardLocationType.Pile;
        }
        else
        {
            // fallback: just add as known
            cardMemory.Add(new CardMemory { value = value, location = CardLocationType.Pile, isKnown = true });
        }
    }

    // When pile is discarded (10 played or 4-of-a-kind)
    public void OnPileDiscarded()
    {
        foreach (var card in cardMemory)
        {
            if (card.location == CardLocationType.Pile)
                card.location = CardLocationType.Discarded;
        }
    }

    // When player picks up pile
    public void OnPlayerPickupPile(List<int> pileValues)
    {
        foreach (int v in pileValues)
        {
            var card = cardMemory.FirstOrDefault(c => c.value == v);
            if (card == null)
                cardMemory.Add(new CardMemory { value = v, location = CardLocationType.PlayerHand, isKnown = true });
            else
            {
                card.isKnown = true;
                card.location = CardLocationType.PlayerHand;
            }
        }
    }

    // When AI draws a card from deck
    public void OnAIDrawCard(int value)
    {
        var card = cardMemory.FirstOrDefault(c => c.value == value);
        if (card == null)
            cardMemory.Add(new CardMemory { value = value, location = CardLocationType.AIHand, isKnown = true });
        else
        {
            card.isKnown = true;
            card.location = CardLocationType.AIHand;
        }
    }

    #endregion

    #region Core AI Turn Logic

    IEnumerator PlayAITurnWithDelay()
    {
        if (!gameManager.GetGameHasStarted()) yield break;

        isPlaying = true;
        yield return new WaitForSeconds(playDelay);

        bool playAgain;
        do
        {
            playAgain = false;

            List<GameObject> currentCards = GetCards();
            List<GameObject> playableCards = currentCards
                .Where(c => CanPlayCard(c.GetComponent<Card>().GetValue(), false))
                .ToList();

            if (playableCards.Count == 0)
            {
                // No playable cards
                HandleNoPlayableCards();
                break;
            }

            GameObject bestCard = DecideBestCard(playableCards);
            PlayCard(bestCard, false);

            int cardValue = bestCard.GetComponent<Card>().GetValue();
            OnCardPlayed(cardValue, true); // Update memory

            if (cardValue == 2 || cardValue == 10)
                playAgain = true;

            if (ShouldDiscard(cardValue))
                OnPileDiscarded();

            yield return new WaitForSeconds(playDelay);
        } while (playAgain);

        isPlaying = false;
        gameManager.NextTurn(null);
    }

    GameObject DecideBestCard(List<GameObject> playableCards)
    {
        // Evaluate each card based on what player might have
        float bestScore = float.NegativeInfinity;
        GameObject bestCard = playableCards[0];

        foreach (GameObject card in playableCards)
        {
            int value = card.GetComponent<Card>().GetValue();
            float score = EvaluateCardSafety(value);
            if (score > bestScore)
            {
                bestScore = score;
                bestCard = card;
            }
        }

        return bestCard;
    }

    float EvaluateCardSafety(int value)
    {
        // Estimate risk of player beating this card
        List<int> playerKnownCards = cardMemory
            .Where(c => c.location == CardLocationType.PlayerHand && c.isKnown)
            .Select(c => c.value)
            .ToList();

        List<int> remainingUnknown = cardMemory
            .Where(c => c.location == CardLocationType.Unknown)
            .Select(c => c.value)
            .ToList();

        // Probability that player can beat this card
        int higherCards = remainingUnknown.Count(v => v >= value) + playerKnownCards.Count(v => v >= value);
        float risk = (float)higherCards / Mathf.Max(1, remainingUnknown.Count + playerKnownCards.Count);

        // Reward special plays
        if (value == 10) return 1.0f; // Always strong
        if (value == 2) return 0.8f;  // Extra turn

        return 1f - risk; // safer = higher score
    }

    void HandleNoPlayableCards()
    {
        int random = Random.Range(0, 101);
        if (random > chanceToPlayChance || cardGenerator.GetDeck().Count == 0)
            PickUpPile(null);
        else
            PlayChanceCard();
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
        if (!isTurn || !isPlaying) { yield break; }

        GameObject cardFromDeck = cardGenerator.GetChanceCard();
        if (cardFromDeck == null)
        {
            isPlaying = false;
            isTurn = false;
            yield break;
        }

        yield return new WaitForSeconds(chanceDelay);

        PlayCard(cardFromDeck, true);
    }
    #endregion

    #region Utility Methods (existing)
    void SetCardAmountText()
    {
        if (handCards.Count > 0)
        {
            cardAmountText.text = handCards.Count.ToString();
            Vector2 cardAmountTextPos = (Vector2)handCards[^1].transform.position + cardAmountTextOffset;
            cardAmountText.transform.position = cardAmountTextPos;
            cardAmountText.gameObject.SetActive(true);
        }
        else cardAmountText.gameObject.SetActive(false);
    }

    void UpdateSideUsage()
    {
        usingOverSideCards = handCards.Count == 0 && overSideCards.Count > 0;
        usingUnderSideCards = handCards.Count == 0 && overSideCards.Count == 0 && underSideCards.Count > 0;
    }

    void SortCards()
    {
        // Simple positioning logic unchanged for brevity
        Vector2 currentPos = isTurn ? isTurnPos : isNotTurnPos;
        handTransform.position = Vector2.Lerp(handTransform.position, currentPos, lerpSpeed * Time.deltaTime);
    }

    public List<GameObject> GetCards()
    {
        if (usingOverSideCards) return overSideCards;
        if (usingUnderSideCards) return underSideCards;
        return handCards;
    }

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
}
