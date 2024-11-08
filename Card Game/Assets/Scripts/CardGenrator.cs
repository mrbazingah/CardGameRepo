using System.Collections.Generic;
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
    [SerializeField] float lerpSpeed;
    [SerializeField] PlayerHand otherPlayer;

    List<string> cardSuits;

    PlayerHand player;
    AIHand ai;
    GameManager gameManager;
    Pile pile;
    AudioManager audioManager;

    void Awake()
    {
        player = FindFirstObjectByType<PlayerHand>();
        ai = FindFirstObjectByType<AIHand>();
        gameManager = FindFirstObjectByType<GameManager>();
        pile = FindFirstObjectByType<Pile>();  
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

        audioManager.PlayShufflingSFX();

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
                ApplyCoverOnCards(obj);
            }
            else
            {
                overSideCards.Add(obj);
            }

            deck.Remove(obj);
        }

        player.SetUnderSideCards(underSideCards);
        player.SetOverSideCards(overSideCards);

        if (otherPlayer != null)
        {
            player = otherPlayer;
            DealPlayerCards();
        }
    }

    void DealAiCards()
    {
        if (otherPlayer != null) { return; }

        for (int ii = 0; ii < cardsPerPlayer; ii++)
        {
            int randomNumber = Random.Range(0, deck.Count);
            GameObject obj = deck[randomNumber];

            ApplyCoverOnCards(obj);
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
                ApplyCoverOnCards(obj);
            }
            else
            {
                overSideCards.Add(obj);
            }

            deck.Remove(obj);
        }

        ai.SetUnderSideCards(underSideCards);
        ai.SetOverSideCards(overSideCards);
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

            audioManager.PlayCardSFX();
            deck.Remove(obj);

            if (deck.Count == 0)
            {
                Destroy(deckImage);
            }
        }
    }
    
    public void ApplyCoverOnCards(GameObject obj)
    {
        GameObject card = Instantiate(backCardPrefab);
        card.transform.SetParent(obj.transform);
        card.transform.localPosition = Vector3.zero;
        card.GetComponent<SpriteRenderer>().sortingOrder = obj.GetComponent<SpriteRenderer>().sortingOrder + 1;

        obj.GetComponent<Card>().ApplyChild(card);
    }

    public GameObject GetChanceCard()
    {
        int randomNumber = Random.Range(0, deck.Count);
        GameObject obj = deck[randomNumber];

        if (!player.CanChance()) { return null; }

        deck.Remove(obj);
        pile.AddCardsToPile(obj);
        obj.GetComponent<SpriteRenderer>().sortingOrder = 100;

        audioManager.PlayCardSFX();

        return obj;
    }

    public List<GameObject> GetDeck()
    {
        return deck;
    }
}
