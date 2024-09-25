using System.Collections.Generic;
using UnityEngine;

public class PlayerHand : MonoBehaviour
{
    [Header("Hand")]
    [SerializeField] List<GameObject> handCards;
    [SerializeField] Transform handTransform;
    [SerializeField] float baseCardSpacing = 150f;
    [SerializeField] float verticalSpacing = 50f;
    [SerializeField] float maxHandWidth = 1000f;
    [SerializeField] float popUpHeight = 50f;
    [Header("Side")]
    [SerializeField] List<GameObject> underSideCards;
    [SerializeField] List<GameObject> overSideCards;
    [SerializeField] Transform underSideTransform;
    [SerializeField] Transform overSideTransform;
    [SerializeField] float sideBaseCardSpacing = 150f;
    [SerializeField] float sideVerticalSpacing = 50f;
    [SerializeField] float sideMaxHandWidth = 1000f;
    [SerializeField] float overSideOffset;

    GameObject hoveredCard;
    Pile pile;
    CardGenrator cardGenerator;

    void Start()
    {
        pile = FindFirstObjectByType<Pile>();
        cardGenerator = FindFirstObjectByType<CardGenrator>();
    }

    public void AddHandCards(GameObject newCard)
    {
        handCards.Add(newCard);
        for (int i = 0; i < handCards.Count; i++)
        {
            handCards[i].GetComponent<SpriteRenderer>().sortingOrder = i;
        }
    }

    public void SetUnderSideCards(List<GameObject> newCards)
    {
        underSideCards = newCards;
    }

    public void SetOverSideCards(List<GameObject> newCards)
    {
        overSideCards = newCards;
    }

    void Update()
    {
        SortCards();
        DetectHover();

        if (!CanPlayAnyCard() && handCards.Count != 0)
        {
            PickUpPile();
        }

        // Check if hand and deck are empty, then allow playing over-side cards
        if (handCards.Count == 0 && cardGenerator.GetDeck().Count == 0 && Input.GetKeyDown(KeyCode.Mouse0))
        {
            UseOverSideCards();
        }
    }

    public void SortCards()
    {
        if (handCards.Count == 0) return;

        float cardSpacing = Mathf.Min(baseCardSpacing, maxHandWidth / handCards.Count);

        for (int i = 0; i < handCards.Count; i++)
        {
            handCards[i].transform.SetParent(handTransform);

            float horizontalOffset = cardSpacing * (i - (handCards.Count - 1) / 2f);
            float normalizedPosition = 2f * i / (handCards.Count - 1) - 1f;
            float verticalOffset = verticalSpacing * (1 - normalizedPosition * normalizedPosition);

            Vector3 basePosition = new Vector3(horizontalOffset, verticalOffset, 0);

            if (handCards[i] == hoveredCard)
            {
                if (handCards.Count == 1)
                {
                    handCards[i].transform.localPosition = new Vector2(0, 0);
                }
                else
                {
                    handCards[i].transform.localPosition = basePosition + new Vector3(0, popUpHeight, 0);
                }
            }
            else
            {
                if (handCards.Count == 1)
                {
                    handCards[i].transform.localPosition = new Vector2(0, 0);
                }
                else
                {
                    handCards[i].transform.localPosition = basePosition;
                }
            }
        }

        float sideCardSpacing = Mathf.Min(sideBaseCardSpacing, sideMaxHandWidth / underSideCards.Count);

        for (int i = 0; i < underSideCards.Count; i++)
        {
            underSideCards[i].transform.SetParent(underSideTransform);

            float horizontalOffset = sideCardSpacing * (i - (underSideCards.Count - 1) / 2f);
            float normalizedPosition = 2f * i / (underSideCards.Count - 1) - 1f;
            float verticalOffset = sideVerticalSpacing * (1 - normalizedPosition * normalizedPosition);

            underSideCards[i].transform.localPosition = new Vector3(horizontalOffset, verticalOffset, 0);
        }

        for (int i = 0; i < overSideCards.Count; i++)
        {
            overSideCards[i].transform.SetParent(overSideTransform);

            float horizontalOffset = sideCardSpacing * (i - (overSideCards.Count - 1) / 2f);
            float normalizedPosition = 2f * i / (overSideCards.Count - 1) - 1f;
            float verticalOffset = sideVerticalSpacing * (1 - normalizedPosition * normalizedPosition);

            overSideCards[i].transform.localPosition = new Vector3(horizontalOffset + overSideOffset, verticalOffset + overSideOffset, 0);
        }
    }

    void DetectHover()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

        if (hit.collider != null)
        {
            GameObject hitObject = hit.collider.gameObject;
            if (handCards.Contains(hitObject))
            {
                hoveredCard = hitObject;
                if (Input.GetKeyDown(KeyCode.Mouse0))
                {
                    PlayCard(hitObject);
                }
            }
            else
            {
                hoveredCard = null;
            }
        }
        else
        {
            hoveredCard = null;
        }
    }

    void PlayCard(GameObject cardInHand)
    {
        int cardInPile = pile.GetCurrentCard();
        int cardInHandValue = cardInHand.GetComponent<Card>().GetValue();
        if (cardInPile <= cardInHandValue || cardInHandValue == 10 || cardInHandValue == 2)
        {
            pile.AddCardsToPile(cardInHand);
            handCards.Remove(cardInHand);

            if (handCards.Count < 3 && cardGenerator.GetDeck().Count != 0)
            {
                cardGenerator.DrawNewCard(1);
            }

            if (cardInHandValue == 10)
            {
                pile.DiscardCardsInPile();
            }
        }
    }

    bool CanPlayAnyCard()
    {
        int cardInPile = pile.GetCurrentCard();
        foreach (GameObject card in handCards)
        {
            int cardValue = card.GetComponent<Card>().GetValue();
            if (cardValue >= cardInPile || cardValue == 10 || cardValue == 2)
            {
                return true;
            }
        }
        return false;
    }

    void PickUpPile()
    {
        List<GameObject> pileCards = pile.GetCardsInPile();
        foreach (GameObject card in pileCards)
        {
            AddHandCards(card);
        }
        pile.ClearPile();
    }

    // New function to use over-side cards
    void UseOverSideCards()
    {
        // Allow playing over-side cards
        foreach (GameObject sideCard in overSideCards)
        {
            int cardInPile = pile.GetCurrentCard();
            int cardValue = sideCard.GetComponent<Card>().GetValue();
            if (cardValue >= cardInPile || cardValue == 10 || cardValue == 2)
            {
                PlaySideCard(sideCard, overSideCards);
                return;
            }
            else
            {
                PickUpPile();
            }
        }
    }

    // Play an over-side card but keep it in the list
    void PlaySideCard(GameObject sideCard, List<GameObject> sideCardsList)
    {
        int cardInPile = pile.GetCurrentCard();
        int cardValue = sideCard.GetComponent<Card>().GetValue();
        if (cardInPile <= cardValue || cardValue == 10 || cardValue == 2)
        {
            pile.AddCardsToPile(sideCard);

            if (cardValue == 10)
            {
                pile.DiscardCardsInPile();
            }
        }
    }
}