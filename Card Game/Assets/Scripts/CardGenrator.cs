using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CardGenrator : MonoBehaviour
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

    List<string> cardSuits;

    int playerCount;

    PlayerHand player;
    AIHand ai;
    GameManager gameManager;
    Pile pile;

    void Awake()
    {
        player = FindFirstObjectByType<PlayerHand>();
        ai = FindFirstObjectByType<AIHand>();
        gameManager = FindFirstObjectByType<GameManager>();
        pile = FindFirstObjectByType<Pile>();  
    }

    void Start()
    {
        playerCount = gameManager.GetPlayerCount();

        GenerateCards();
        DealPlayerCards();
        DealAiCards();
    }

    void GenerateCards()
    {
        int suit = 0;

        cardSuits = new List<string>(4);
        cardSuits.Add("Hearts");
        cardSuits.Add("Diamond");
        cardSuits.Add("Spades");
        cardSuits.Add("Clubs");

        deck = new List<GameObject>(numberOfCards);
        int currentValue = 0;

        for (int i = 0; i < numberOfCards; i++)
        {
            currentValue++;

            GameObject card = Instantiate(cardPrefab);

            card.GetComponent<SpriteRenderer>().sprite = cardSprites[i];

            if (currentValue == 1)
            {
                card.GetComponent<Card>().SetValue(14);
            }
            else
            {
                card.GetComponent<Card>().SetValue(currentValue);
            }

            card.name = currentValue.ToString() + " of " + cardSuits[suit];

            if (currentValue == 13)
            {
                currentValue = 0;
                suit++;
            }

            deck.Add(card);
            card.transform.SetParent(cardParent.transform);
            card.transform.localPosition = Vector3.zero;
        }
    }

    void DealPlayerCards()
    {
        for (int ii = 0; ii < cardsPerPlayer; ii++)
        {
            int randomNumber = Random.Range(0, deck.Count);
            GameObject obj = deck[randomNumber];

            player.AddHandCards(obj);
            deck.Remove(obj);
        }


        List<GameObject> underSideCards = new List<GameObject>(3);
        List<GameObject> overSideCards = new List<GameObject>(3);

        for (int ii = 0; ii < 6; ii++)
        {
            int randomNumber = Random.Range(0, deck.Count);
            GameObject obj = deck[randomNumber];

            obj.GetComponent<SpriteRenderer>().sortingOrder = ii;

            if (ii <= 2)
            {
                underSideCards.Add(obj);

                GameObject card = Instantiate(backCardPrefab);
                card.transform.SetParent(obj.transform);
                card.transform.localPosition = Vector3.zero;
                card.GetComponent<SpriteRenderer>().sortingOrder = obj.GetComponent<SpriteRenderer>().sortingOrder + 1;

                obj.GetComponent<Card>().ApplyChild(card);
            }
            else
            {
                overSideCards.Add(obj);
            }

            deck.Remove(obj);
        }

        player.SetUnderSideCards(underSideCards);
        player.SetOverSideCards(overSideCards);

        player.SortCards(true);
    }

    void DealAiCards()
    {
        for (int ii = 0; ii < cardsPerPlayer; ii++)
        {
            int randomNumber = Random.Range(0, deck.Count);
            GameObject obj = deck[randomNumber];

            ai.AddHandCards(obj);
            deck.Remove(obj);
        }


        List<GameObject> underSideCards = new List<GameObject>(3);
        List<GameObject> overSideCards = new List<GameObject>(3);

        for (int ii = 0; ii < 6; ii++)
        {
            int randomNumber = Random.Range(0, deck.Count);
            GameObject obj = deck[randomNumber];

            obj.GetComponent<SpriteRenderer>().sortingOrder = ii;

            if (ii <= 2)
            {
                underSideCards.Add(obj);

                GameObject card = Instantiate(backCardPrefab);
                card.transform.SetParent(obj.transform);
                card.transform.localPosition = Vector3.zero;
                card.GetComponent<SpriteRenderer>().sortingOrder = obj.GetComponent<SpriteRenderer>().sortingOrder + 1;

                obj.GetComponent<Card>().ApplyChild(card);
            }
            else
            {
                overSideCards.Add(obj);
            }

            deck.Remove(obj);
        }

        ai.SetUnderSideCards(underSideCards);
        ai.SetOverSideCards(overSideCards);

        gameManager.AsignStartPlayer();
    }

    public void DrawNewCard(int amount, bool isPlayer)
    {
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
                ai.AddHandCards(obj);
            }

            deck.Remove(obj);

            if (deck.Count == 0)
            {
                Destroy(deckImage);
            }
        }
    }

    public GameObject GetChanceCard()
    {
        int randomNumber = Random.Range(0, deck.Count);
        GameObject obj = deck[randomNumber];

        if (!player.GetTurn() || player.CanPlayCard(obj.GetComponent<Card>().GetValue())) { return null; }

        deck.Remove(obj);
        obj.GetComponent<SpriteRenderer>().sortingOrder = 100;
        obj.transform.position = Vector2.zero;

        return obj;
    }

    public List<GameObject> GetDeck()
    {
        return deck;
    }
}
