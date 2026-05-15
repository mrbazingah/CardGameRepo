using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NetworkOpponentHand : NetworkBehaviour
{
    [Header("Prefabs")]
    [SerializeField] GameObject cardPrefab;
    [SerializeField] GameObject backCardPrefab;
    [SerializeField] List<Sprite> cardSprites;
    [SerializeField] Transform cardParent;

    [Header("Transform and Spacing")]
    [SerializeField] Transform handTransform;
    [SerializeField] Transform underSideTransform, overSideTransform;
    [SerializeField] float baseCardSpacing = 150f, maxHandWidth = 1000f;
    [SerializeField] float sideBaseCardSpacing = 150f, sideMaxHandWidth = 1000f, overSideOffset;
    [SerializeField] float lerpSpeed;

    List<GameObject> handCards = new List<GameObject>();
    List<GameObject> underSideCards = new List<GameObject>();
    List<GameObject> overSideCards = new List<GameObject>();

    bool usingOverSideCards, usingUnderSideCards;

    // -------------------------------------------------------------------------
    // Deal receive — only counts arrive, not card values
    // -------------------------------------------------------------------------

    public void ReceiveDeal(int handCount, int underCount, CardNetData[] opponentOverSide)
    {
        Debug.Log($"[NOH] ReceiveDeal — handCount={handCount} underCount={underCount} over={opponentOverSide.Length}");
        for (int i = 0; i < handCount; i++) { handCards.Add(SpawnCoveredCard()); }
        for (int i = 0; i < underCount; i++) { underSideCards.Add(SpawnCoveredCard()); }
        foreach (CardNetData data in opponentOverSide) { overSideCards.Add(SpawnFaceCard(data)); }
    }

    GameObject SpawnCoveredCard()
    {
        GameObject card = Instantiate(cardPrefab, cardParent);
        card.transform.localPosition = Vector3.zero;

        SpriteRenderer sr = card.GetComponent<SpriteRenderer>();

        GameObject back = Instantiate(backCardPrefab, card.transform);
        back.transform.localPosition = Vector3.zero;
        back.GetComponent<SpriteRenderer>().sortingOrder = sr.sortingOrder + 1;

        card.GetComponent<NetworkCard>().ApplyChild(back);

        return card;
    }

    GameObject SpawnFaceCard(CardNetData data)
    {
        GameObject card = Instantiate(cardPrefab, cardParent);
        card.transform.localPosition = Vector3.zero;

        NetworkCard nc = card.GetComponent<NetworkCard>();
        nc.SetCardId(data.CardId);
        nc.SetValue(data.Value);

        SpriteRenderer sr = card.GetComponent<SpriteRenderer>();
        sr.sprite = cardSprites[data.CardId];
        sr.color = Color.white;

        return card;
    }

    // -------------------------------------------------------------------------
    // Layout
    // -------------------------------------------------------------------------

    void Update()
    {
        UpdateSideUsage();
        ArrangeCards(handCards, handTransform, baseCardSpacing, maxHandWidth);
        ArrangeCards(overSideCards, overSideTransform, sideBaseCardSpacing, sideMaxHandWidth, overSideOffset);
        ArrangeCards(underSideCards, underSideTransform, sideBaseCardSpacing, sideMaxHandWidth);
    }

    void UpdateSideUsage()
    {
        usingOverSideCards = handCards.Count == 0 && overSideCards.Count > 0;
        usingUnderSideCards = handCards.Count == 0 && overSideCards.Count == 0 && underSideCards.Count > 0;
    }

    void ArrangeCards(List<GameObject> cards, Transform parent, float spacing, float maxWidth, float offset = 0)
    {
        if (cards.Count == 0) { return; }

        float cardSpacing = Mathf.Min(spacing, maxWidth / cards.Count);

        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].transform.SetParent(parent);

            SpriteRenderer sr = cards[i].GetComponent<SpriteRenderer>();
            NetworkCard nc = cards[i].GetComponent<NetworkCard>();

            if (cards == overSideCards)
            {
                sr.sortingOrder = i + 3;
            }
            else
            {
                sr.sortingOrder = i;
            }

            if (nc.GetBack() != null) { nc.GetBack().GetComponent<SpriteRenderer>().sortingOrder = sr.sortingOrder + 1; }

            float horizontalOffset = cards.Count > 1 ? cardSpacing * (i - (cards.Count - 1) / 2f) : 0f;

            cards[i].transform.localPosition = Vector2.Lerp(cards[i].transform.localPosition, new Vector2(horizontalOffset + offset, offset), lerpSpeed * Time.deltaTime);
        }
    }

    // -------------------------------------------------------------------------
    // Called when opponent plays a card — removes one card from the display
    // -------------------------------------------------------------------------

    public void RemoveCardFromDisplay(bool fromHand = true)
    {
        List<GameObject> source = fromHand ? handCards : GetCurrentCards();
        if (source.Count == 0) { return; }

        GameObject card = source[source.Count - 1];
        source.RemoveAt(source.Count - 1);
        Destroy(card);

        UpdateSideUsage();
    }

    public void AddCardToDisplay()
    {
        handCards.Add(SpawnCoveredCard());
    }

    // -------------------------------------------------------------------------
    // Getters
    // -------------------------------------------------------------------------

    public List<GameObject> GetCurrentCards()
    {
        if (usingOverSideCards) { return overSideCards; }
        if (usingUnderSideCards) { return underSideCards; }
        return handCards;
    }

    public List<GameObject> GetCards() => GetCurrentCards();
    public List<GameObject> GetHandCards() => handCards;
    public List<GameObject> GetOverSideCards() => overSideCards;
    public List<GameObject> GetUnderSideCards() => underSideCards;

    public bool CanChance() => false;
    public bool GetTurn() => false;

    public void AddHandCards(GameObject card) => handCards.Add(card);
    public void SetUnderSideCards(List<GameObject> newCards) => underSideCards = newCards;
    public void SetOverSideCards(List<GameObject> newCards) => overSideCards = newCards;
    public void SwitchOutSideCards() { }
}
