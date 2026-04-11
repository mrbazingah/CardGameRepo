using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

public class CardGenerator : MonoBehaviour
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
    [Header("Debuging")]
    [SerializeField] int chanceCardDebugValue;
    [SerializeField] bool debugChanceCard;
    [SerializeField] bool removeSpecialCards;

    List<string> cardSuits;

    bool canDrawChanceCard = true;

    PlayerHand player;
    AIHand ai;
    Pile pile;
    AudioManager audioManager;
    SkinManager skinManager;

    void Awake()
    {
        player = FindFirstObjectByType<PlayerHand>();
        ai = FindFirstObjectByType<AIHand>();
        pile = FindFirstObjectByType<Pile>();  
        audioManager = FindFirstObjectByType<AudioManager>();
        skinManager = FindFirstObjectByType<SkinManager>();
    }

    void Start()
    {
        if (PlayerPrefs.HasKey("CardsPerPlayer"))
        {
            cardsPerPlayer = PlayerPrefs.GetInt("CardsPerPlayer");
        }

        GenerateDeck();
        DealPlayerCards();
        DealAiCards();
    }

    void GenerateDeck()
    {
        //cardSprites = skinManager.GetEquipedDeck();

        int suit = 0;

        cardSuits = new List<string>(4);
        cardSuits.Add("Hearts");
        cardSuits.Add("Diamond");
        cardSuits.Add("Spades");
        cardSuits.Add("Clubs");

        deck = new List<GameObject>(numberOfCards);
        int currentCardValue = 0;

        for (int i = 0; i < numberOfCards; i++)
        {
            currentCardValue++;
            if ((currentCardValue == 2 || currentCardValue == 10) && removeSpecialCards) { continue; } // Skip 2's if removeSpecialCards is true

            GameObject card = Instantiate(cardPrefab);

            SpriteRenderer cardSR = card.GetComponent<Card>().GetComponent<SpriteRenderer>();
            cardSR.sprite = cardSprites[i];
            cardSR.color = new Color(1, 1, 1, 0);

            if (currentCardValue == 1)
            {
                card.GetComponent<Card>().SetValue(14);
            }
            else
            {
                card.GetComponent<Card>().SetValue(currentCardValue);
            }

            card.name = currentCardValue.ToString() + " of " + cardSuits[suit];

            if (currentCardValue == 13)
            {
                currentCardValue = 0;
                suit++;
            }

            deck.Add(card);
            card.transform.SetParent(cardParent.transform);
            card.transform.localPosition = Vector3.zero;
        }
    }

    GameObject GenerateSingleCard()
    {
        chanceCardDebugValue = Mathf.Clamp(chanceCardDebugValue, 0, 14);

        int currentCardValue = chanceCardDebugValue; 
        if (currentCardValue == 14)
        {
            currentCardValue = 1; // Ace is represented as 1 in the deck
        }

        GameObject card = Instantiate(cardPrefab);

        card.GetComponent<Card>().GetComponent<SpriteRenderer>().sprite = cardSprites[currentCardValue - 1];

        if (currentCardValue == 1)
        {
            card.GetComponent<Card>().SetValue(14);
        }
        else
        {
            card.GetComponent<Card>().SetValue(currentCardValue);
        }

        card.name = currentCardValue.ToString() + " of " + cardSuits[0];

        deck.Add(card);
        card.transform.SetParent(cardParent.transform);
        card.transform.localPosition = Vector3.zero;

        return card;
    }

    void DealPlayerCards()
    {
        SpriteRenderer cardSR = null;

        for (int i = 0; i < cardsPerPlayer; i++)
        {
            int randomNumber = Random.Range(0, deck.Count);
            GameObject obj = deck[randomNumber];

            player.AddHandCards(obj);
            deck.Remove(obj);

            cardSR = obj.GetComponent<SpriteRenderer>();
            cardSR.color = new Color(1, 1, 1, 1);
        }

        audioManager.PlayShufflingSFX();

        List<GameObject> underSideCards = new List<GameObject>(3);
        List<GameObject> overSideCards = new List<GameObject>(3);

        for (int i = 0; i < 6; i++)
        {
            int randomNumber = Random.Range(0, deck.Count);
            GameObject obj = deck[randomNumber];

            cardSR = obj.GetComponent<SpriteRenderer>();
            cardSR.sortingOrder = i;
            cardSR.color = new Color(1, 1, 1, 1);

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
        player.SortHandCards();
    }

    void DealAiCards()
    {
        SpriteRenderer cardSR = null;

        for (int i = 0; i < cardsPerPlayer; i++)
        {
            int randomNumber = Random.Range(0, deck.Count);
            GameObject obj = deck[randomNumber];

            ApplyCoverOnCards(obj);
            ai.AddHandCards(obj);
            deck.Remove(obj);

            cardSR = obj.GetComponent<SpriteRenderer>();
            cardSR.color = new Color(1, 1, 1, 1);
        }

        List<GameObject> underSideCards = new List<GameObject>(3);
        List<GameObject> overSideCards = new List<GameObject>(3);

        for (int i = 0; i < 6; i++)
        {
            int randomNumber = Random.Range(0, deck.Count);
            GameObject obj = deck[randomNumber];

            cardSR = obj.GetComponent<SpriteRenderer>();
            cardSR.sortingOrder = i;
            cardSR.color = new Color(1, 1, 1, 1);

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

            deck.Remove(obj);

            obj.GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 1);
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

        //Sets chosen card to chance card as debug
        if (debugChanceCard)
        {
            chanceCard = null;

            for (int i = 0; i < deck.Count; i++)
            {
                int currentValue = deck[i].GetComponent<Card>().GetValue();
                if (currentValue == chanceCardDebugValue)
                {
                    chanceCard = deck[i];
                    break;
                }
            }

            if (chanceCard == null)
            {
                chanceCard = GenerateSingleCard();
            }
        }

        StartCoroutine(ChanceCardDelay());

        deck.Remove(chanceCard);
        pile.AddCardsToPile(chanceCard);

        SpriteRenderer chanceCardSR = chanceCard.GetComponent<SpriteRenderer>();
        chanceCardSR.color = new Color(1, 1, 1, 1);
        chanceCardSR.sortingOrder = 100;

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
