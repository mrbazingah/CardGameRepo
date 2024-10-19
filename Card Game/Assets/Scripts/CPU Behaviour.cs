using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIHand : MonoBehaviour
{
    [SerializeField] List<GameObject> handCards;
    [SerializeField] List<GameObject> underSideCards, overSideCards;
    [Space]
    [SerializeField] Transform handTransform;
    [SerializeField] float baseCardSpacing = 150f, verticalSpacing = 50f, maxHandWidth = 1000f;
    [Space]
    [SerializeField] Transform underSideTransform, overSideTransform;
    [SerializeField] float sideBaseCardSpacing = 150f, sideVerticalSpacing = 50f, sideMaxHandWidth = 1000f, overSideOffset;
    [Space]
    [SerializeField] bool isTurn;
    [SerializeField] int turnNumber;
    [SerializeField] float playDelay;
    [SerializeField] float lerpSpeed;

    bool usingOverSideCards, usingUnderSideCards;
    bool isPlaying;

    Pile pile;
    CardGenrator cardGenerator;
    GameManager gameManager;
    AudioManager audioManager;

    void Awake()
    {
        pile = FindFirstObjectByType<Pile>();
        cardGenerator = FindFirstObjectByType<CardGenrator>();
        gameManager = FindFirstObjectByType<GameManager>();
        audioManager = FindFirstObjectByType<AudioManager>();
    }

    void Start()
    {
        isPlaying = false;
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
    #endregion

    void Update()
    {
        UpdateSideUsage();
        UpdateColliders();
        SortCards();
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

                cards[i].transform.localPosition = Vector2.Lerp(cards[i].transform.localPosition, cardPosition, lerpSpeed);
            }
            else
            {
                cards[i].transform.localPosition = Vector3.zero;
            }

            GameObject child = cards[i].GetComponent<Card>().GetChild();
            if (handCards.Contains(cards[i]) && child != null)
            {
                child.GetComponent<SpriteRenderer>().sortingLayerName = "BackCard";
            }
        }
    }
    #endregion

    #region Turn
    public void SetTurn(bool b) => isTurn = b;

    void CheckTurn()
    {
        isTurn = turnNumber == gameManager.GetTurn();
    }
    #endregion

    #region Play
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
                PlayCard(selectedCard);

                int cardValue = selectedCard.GetComponent<Card>().GetValue();
                if (cardValue == 2 || ShouldDiscard(cardValue) || HasSameValueCard(cardValue))
                {
                    playAgain = true;
                    yield return new WaitForSeconds(playDelay);
                }
                else
                {
                    gameManager.NextTurn(selectedCard);
                    playAgain = false;
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
            audioManager.PlayCardSFX();

            if (handCards.Count < 3 && cardGenerator.GetDeck().Count != 0)
            {
                cardGenerator.DrawNewCard(3 - handCards.Count, false);
            }

            if (ShouldDiscard(cardInHand.GetComponent<Card>().GetValue()))
            {
                StartCoroutine(pile.DiscardCardsInPile());
            }
        }
        else if (usingUnderSideCards || usingOverSideCards)
        {
            pile.AddCardsToPile(cardInHand);
            RemoveCardFromList(cardInHand);
            PickUpPile();
            gameManager.NextTurn(cardInHand);
        }
    }

    void PickUpPile()
    {
        List<GameObject> pileCards = pile.GetCardsInPile();
        handCards.AddRange(pileCards);

        for (int i = 0; i < handCards.Count; i++)
        {
            handCards[i].GetComponent<Card>().RemoveChild();
            cardGenerator.ApplyCoverOnCards(handCards[i]);
        }

        audioManager.PlayShufflingSFX();
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

    public List<GameObject> GetCards()
    {
        if (usingOverSideCards) return overSideCards;
        if (usingUnderSideCards) return underSideCards;
        return handCards;
    }
}
