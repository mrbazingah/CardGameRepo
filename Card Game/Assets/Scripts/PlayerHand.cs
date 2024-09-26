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

    bool usingOverSideCards;
    bool usingUnderSideCards;

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

        if (handCards.Count == 0 && overSideCards.Count != 0)
        {
            usingOverSideCards = true;
            usingUnderSideCards = false;
        }
        else if (handCards.Count == 0 && overSideCards.Count == 0 && underSideCards.Count != 0)
        {
            usingOverSideCards = false;
            usingUnderSideCards = true; 
        }
        else if (handCards.Count != 0)
        {
            usingOverSideCards = false;
            usingUnderSideCards = false;
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
            if (handCards.Contains(hitObject) && !usingOverSideCards && !usingUnderSideCards)
            {
                hoveredCard = hitObject;
                if (Input.GetKeyDown(KeyCode.Mouse0))
                {
                    PlayCard(hitObject);
                }
            }
            else if (overSideCards.Contains(hitObject) && usingOverSideCards && !usingUnderSideCards)
            {
                hoveredCard = hitObject;
                if (Input.GetKeyDown(KeyCode.Mouse0))
                {
                    PlayCard(hitObject);
                }
            }
            else if (underSideCards.Contains(hitObject) && usingUnderSideCards)
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

        if (CanPlayCard(cardInHandValue))
        {
            pile.AddCardsToPile(cardInHand);

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

            if (handCards.Count < 3 && cardGenerator.GetDeck().Count != 0)
            {
                cardGenerator.DrawNewCard(1);
            }

            if (cardInHandValue == 10)
            {
                pile.DiscardCardsInPile();
            }
        }
        else
        {
            int cardsToPlay = 0;

            if (usingOverSideCards)
            {
                foreach (GameObject card in overSideCards)
                {
                    int value = card.GetComponent<Card>().GetValue();
                    if (CanPlayCard(value))
                    {
                        cardsToPlay++;
                    }
                }
            }
            else if (usingUnderSideCards)
            {
                foreach (GameObject card in underSideCards)
                {
                    int value = card.GetComponent<Card>().GetValue();
                    if (CanPlayCard(value))
                    {
                        cardsToPlay++;
                    }
                }
            }
            else
            {
                foreach (GameObject card in handCards)
                {
                    int value = card.GetComponent<Card>().GetValue();
                    if (CanPlayCard(value))
                    {
                        cardsToPlay++;
                    }
                }
            }

            if (cardsToPlay <= 0)
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

                pile.AddCardsToPile(cardInHand);
                PickUpPile();
            }
        }

        Debug.Log(cardInHand);
    }

    bool CanPlayCard(float cardValue)
    {
        int cardInPile = pile.GetCurrentCard();

        if (cardValue >= cardInPile || cardValue == 10 || cardValue == 2)
        {
            return true;
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
}
