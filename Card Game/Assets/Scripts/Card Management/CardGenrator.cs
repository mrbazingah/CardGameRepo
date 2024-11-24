using System.Collections;
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
    [Space]
    [SerializeField] float chanceCardDelay;

    List<string> cardSuits;

    bool canDrawChanceCard = true;

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

            card.GetComponent<Card>().GetComponent<SpriteRenderer>().sprite = cardSprites[i];

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
        for (int i = 0; i < cardsPerPlayer; i++)
        {
            int randomNumber = Random.Range(0, deck.Count);
            GameObject obj = deck[randomNumber];

            player.AddHandCards(obj);
            deck.Remove(obj);
        }

        audioManager.PlayShufflingSFX();

        List<GameObject> underSideCards = new List<GameObject>(3);
        List<GameObject> overSideCards = new List<GameObject>(3);

        for (int i = 0; i < 6; i++)
        {
            int randomNumber = Random.Range(0, deck.Count);
            GameObject obj = deck[randomNumber];

            obj.GetComponent<SpriteRenderer>().sortingOrder = i;

            if (i <= 2)
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


        List<GameObject> underSideCards = new List<GameObject>(3);
        List<GameObject> overSideCards = new List<GameObject>(3);

        for (int i = 0; i < 6; i++)
        {
            int randomNumber = Random.Range(0, deck.Count);
            GameObject obj = deck[randomNumber];

            obj.GetComponent<SpriteRenderer>().sortingOrder = i;

            if (i <= 2)
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

        ai.SwitchOutSideCards();
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

            audioManager.PlayCardSFX();

            deck.Remove(obj);
        }
    }
    
    public void ApplyCoverOnCards(GameObject card)
    {
        GameObject backOfCard = Instantiate(backCardPrefab);
        backOfCard.transform.SetParent(card.transform);
        backOfCard.transform.localPosition = Vector3.zero;
        backOfCard.GetComponent<SpriteRenderer>().sortingOrder = card.GetComponent<SpriteRenderer>().sortingOrder + 1;

        card.GetComponent<Card>().ApplyChild(backOfCard);
    }

    public GameObject GetChanceCard()
    {
        int randomNumber = Random.Range(0, deck.Count);
        GameObject chanceCard = deck[randomNumber];

        if ((!player.CanChance() && player.GetTurn()) || (!ai.CanChance() && ai.GetTurn()) || !canDrawChanceCard) { return null; }

        StartCoroutine(ChanceCardDelay());

        deck.Remove(chanceCard);
        pile.AddCardsToPile(chanceCard);
        chanceCard.GetComponent<SpriteRenderer>().sortingOrder = 100;

        audioManager.PlayCardSFX();

        return chanceCard;
    }

    IEnumerator ChanceCardDelay()
    {
        canDrawChanceCard = false;

        yield return new WaitForSeconds(chanceCardDelay);

        canDrawChanceCard = true;
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
