using System.Collections.Generic;
using UnityEngine;

public class UnoCardGenerator : MonoBehaviour
{
    [SerializeField] GameObject cardPrefab;
    [SerializeField] GameObject backCardPrefab;
    [SerializeField] GameObject deckImage;
    [SerializeField] List<Sprite> cardSprites;
    [SerializeField] GameObject cardParent;
    [Space]
    [SerializeField] int cardsPerPlayer;
    [SerializeField] int numberOfCards = 52;
    [Space]
    [SerializeField] List<GameObject> deck;

    List<string> cardColor;

    UnoPlayerHand player;
    UnoAIHand ai;
    AudioManager audioManager;

    void Awake()
    {
        player = FindFirstObjectByType<UnoPlayerHand>();
        ai = FindFirstObjectByType<UnoAIHand>();
        audioManager = FindFirstObjectByType<AudioManager>();
    }

    void Start()
    {
        GenerateCards();
        DealPlayerCards();
        DealAiCards();
    }

    void GenerateCards()
    {
        for (int i = 0; i < 2; i++)
        {
            int colorIndex = 0;

            cardColor = new List<string>(4);
            cardColor.Add("Red");
            cardColor.Add("Blue");
            cardColor.Add("Green");
            cardColor.Add("Yellow");

            deck = new List<GameObject>(numberOfCards);
            int currentValue = 0;

            for (int ii = 0; ii < numberOfCards; ii++)
            {
                currentValue++;

                GameObject card = Instantiate(cardPrefab);

                card.GetComponent<SpriteRenderer>().sprite = cardSprites[ii];

                if (currentValue == 13 && colorIndex < 2)
                {
                    card.GetComponent<UnoCard>().SetValue(14);
                }
                else
                {
                    card.GetComponent<UnoCard>().SetValue(currentValue);
                }

                card.name = currentValue.ToString() + " of " + cardColor[colorIndex];

                if (currentValue == 13)
                {
                    currentValue = 0;
                    colorIndex++;
                }

                deck.Add(card);
                card.transform.SetParent(cardParent.transform);
                card.transform.localPosition = Vector3.zero;
            }
        }
    }

    void DealPlayerCards()
    {
        for (int i = 0; i < cardsPerPlayer; i++)
        {
            int randomNumber = Random.Range(0, deck.Count);
            GameObject obj = deck[randomNumber];

            player.AddHandCards(obj);
            deck.Remove(obj);
        }

        audioManager.PlayShufflingSFX();
        player.SortHandCards();
    }

    void DealAiCards()
    {
        for (int i = 0; i < cardsPerPlayer; i++)
        {
            int randomNumber = Random.Range(0, deck.Count);
            GameObject obj = deck[randomNumber];

            ApplyCoverOnCards(obj);
            ai.AddHandCards(obj);
            deck.Remove(obj);
        }
    }

    void Update()
    {
        if (deck.Count == 0)
        {
            Destroy(deckImage);
        }
    }

    public void DrawNewCard(int amount, bool isPlayer)
    {
        if (deck.Count <= 0) { return; }

        for (int i = 0; i < amount; i++)
        {
            int randomNumber = Random.Range(0, deck.Count);
            GameObject obj = deck[randomNumber];

            if (isPlayer)
            {
                player.AddHandCards(obj);
            }
            else
            {
                ApplyCoverOnCards(obj);
                ai.AddHandCards(obj);
            }

            deck.Remove(obj);
        }
    }

    public void ApplyCoverOnCards(GameObject card)
    {
        GameObject backOfCard = Instantiate(backCardPrefab);
        backOfCard.transform.SetParent(card.transform);
        backOfCard.transform.localPosition = Vector3.zero;
        backOfCard.GetComponent<SpriteRenderer>().sortingOrder = card.GetComponent<SpriteRenderer>().sortingOrder + 1;

        card.GetComponent<UnoCard>().ApplyChild(backOfCard);
    }

    public int GetCardsPerPlayer()
    {
        return cardsPerPlayer;
    }

    public List<GameObject> GetDeck()
    {
        return deck;
    }
}
