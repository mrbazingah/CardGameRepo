using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerHand : NetworkBehaviour
{
    [Header("Prefabs & Sprites")]
    [SerializeField] GameObject cardPrefab;      // prefab with NetworkCard component
    [SerializeField] GameObject backCardPrefab;
    [SerializeField] List<Sprite> cardSprites;   // 52 sprites in deck order (matches CardId)
    [SerializeField] Transform cardParent;

    [Header("Transform and Spacing")]
    [SerializeField] Transform handTransform;
    [SerializeField] Transform underSideTransform, overSideTransform;
    [SerializeField] float baseCardSpacing = 150f, maxHandWidth = 1000f, popUpHeight = 50f;
    [SerializeField] float sideBaseCardSpacing = 150f, sideMaxHandWidth = 1000f, overSideOffset;
    [SerializeField] Vector2 isTurnPos, isNotTurnPos;
    [SerializeField] float lerpSpeed;

    List<GameObject> handCards    = new List<GameObject>();
    List<GameObject> underSideCards = new List<GameObject>();
    List<GameObject> overSideCards  = new List<GameObject>();

    bool usingOverSideCards, usingUnderSideCards;

    // -------------------------------------------------------------------------
    // Deal receive
    // -------------------------------------------------------------------------

    public void ReceiveDeal(CardNetData[] hand, CardNetData[] underSide, CardNetData[] overSide)
    {
        foreach (CardNetData data in hand)
            handCards.Add(SpawnCard(data, false));

        foreach (CardNetData data in underSide)
            underSideCards.Add(SpawnCard(data, true));

        foreach (CardNetData data in overSide)
            overSideCards.Add(SpawnCard(data, false));

        SortHandCards();
    }

    GameObject SpawnCard(CardNetData data, bool covered)
    {
        GameObject card = Instantiate(cardPrefab, cardParent);
        card.transform.localPosition = Vector3.zero;

        NetworkCard nc = card.GetComponent<NetworkCard>();
        nc.SetCardId(data.CardId);
        nc.SetValue(data.Value);

        SpriteRenderer sr = card.GetComponent<SpriteRenderer>();
        sr.sprite = cardSprites[data.CardId];
        sr.color = Color.white;

        if (covered)
        {
            GameObject back = Instantiate(backCardPrefab, card.transform);
            back.transform.localPosition = Vector3.zero;
            back.GetComponent<SpriteRenderer>().sortingOrder = sr.sortingOrder + 1;
            nc.ApplyChild(back);
        }

        return card;
    }

    // -------------------------------------------------------------------------
    // Sorting & layout
    // -------------------------------------------------------------------------

    public void SortHandCards()
    {
        handCards.Sort((a, b) =>
            a.GetComponent<NetworkCard>().GetValue()
             .CompareTo(b.GetComponent<NetworkCard>().GetValue()));
    }

    void Update()
    {
        UpdateSideUsage();
        ArrangeCards(handCards,      handTransform,      baseCardSpacing, maxHandWidth);
        ArrangeCards(overSideCards,  overSideTransform,  sideBaseCardSpacing, sideMaxHandWidth, overSideOffset);
        ArrangeCards(underSideCards, underSideTransform, sideBaseCardSpacing, sideMaxHandWidth);
    }

    void UpdateSideUsage()
    {
        usingOverSideCards  = handCards.Count == 0 && overSideCards.Count > 0;
        usingUnderSideCards = handCards.Count == 0 && overSideCards.Count == 0 && underSideCards.Count > 0;
    }

    void ArrangeCards(List<GameObject> cards, Transform parent, float spacing, float maxWidth, float offset = 0)
    {
        if (cards.Count == 0) return;

        float cardSpacing = Mathf.Min(spacing, maxWidth / cards.Count);

        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].transform.SetParent(parent);

            SpriteRenderer sr = cards[i].GetComponent<SpriteRenderer>();
            NetworkCard nc    = cards[i].GetComponent<NetworkCard>();

            if (cards == handCards)
            {
                sr.sortingOrder = i;
            }
            else if (cards == overSideCards)
            {
                sr.sortingOrder = i + 3;
            }
            else // underSide
            {
                sr.sortingOrder = i;
                if (nc.GetBack() != null)
                    nc.GetBack().GetComponent<SpriteRenderer>().sortingOrder = i + 1;
            }

            float horizontalOffset = cards.Count > 1
                ? cardSpacing * (i - (cards.Count - 1) / 2f)
                : 0f;

            Vector2 targetPos = new Vector2(horizontalOffset + offset, offset);

            if (cards != handCards)
                nc.basePosition = targetPos;

            cards[i].transform.localPosition = Vector2.Lerp(
                cards[i].transform.localPosition,
                targetPos,
                lerpSpeed * Time.deltaTime);
        }
    }

    // -------------------------------------------------------------------------
    // Getters
    // -------------------------------------------------------------------------

    public List<GameObject> GetCurrentCards()
    {
        if (usingOverSideCards)  return overSideCards;
        if (usingUnderSideCards) return underSideCards;
        return handCards;
    }

    public List<GameObject> GetHandCards()      => handCards;
    public List<GameObject> GetOverSideCards()  => overSideCards;
    public List<GameObject> GetUnderSideCards() => underSideCards;

    // Implemented when game loop is wired up
    public bool CanChance() => false;
    public bool GetTurn()   => false;
}
