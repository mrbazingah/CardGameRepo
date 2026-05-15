using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// Displays the opponent's cards as backs only — values are never sent to this client.
public class NetworkOpponentHand : NetworkBehaviour
{
    [Header("Prefabs")]
    [SerializeField] GameObject backCardPrefab;
    [SerializeField] Transform cardParent;

    [Header("Transform and Spacing")]
    [SerializeField] Transform handTransform;
    [SerializeField] Transform underSideTransform, overSideTransform;
    [SerializeField] float baseCardSpacing = 150f, maxHandWidth = 1000f;
    [SerializeField] float sideBaseCardSpacing = 150f, sideMaxHandWidth = 1000f, overSideOffset;
    [SerializeField] float lerpSpeed;

    List<GameObject> handCards      = new List<GameObject>();
    List<GameObject> underSideCards = new List<GameObject>();
    List<GameObject> overSideCards  = new List<GameObject>();

    bool usingOverSideCards, usingUnderSideCards;

    // -------------------------------------------------------------------------
    // Deal receive — only counts arrive, not card values
    // -------------------------------------------------------------------------

    public void ReceiveDeal(int handCount, int underCount, int overCount)
    {
        for (int i = 0; i < handCount; i++)
            handCards.Add(SpawnBack());

        for (int i = 0; i < underCount; i++)
            underSideCards.Add(SpawnBack());

        for (int i = 0; i < overCount; i++)
            overSideCards.Add(SpawnBack());
    }

    GameObject SpawnBack()
    {
        GameObject back = Instantiate(backCardPrefab, cardParent);
        back.transform.localPosition = Vector3.zero;
        return back;
    }

    // -------------------------------------------------------------------------
    // Layout
    // -------------------------------------------------------------------------

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
            cards[i].GetComponent<SpriteRenderer>().sortingOrder = i;

            float horizontalOffset = cards.Count > 1
                ? cardSpacing * (i - (cards.Count - 1) / 2f)
                : 0f;

            cards[i].transform.localPosition = Vector2.Lerp(
                cards[i].transform.localPosition,
                new Vector2(horizontalOffset + offset, offset),
                lerpSpeed * Time.deltaTime);
        }
    }

    // -------------------------------------------------------------------------
    // Called when opponent plays a card — removes one back from the display
    // -------------------------------------------------------------------------

    public void RemoveCardFromDisplay(bool fromHand = true)
    {
        List<GameObject> source = fromHand ? handCards : GetCurrentCards();
        if (source.Count == 0) return;

        GameObject back = source[source.Count - 1];
        source.RemoveAt(source.Count - 1);
        Destroy(back);

        UpdateSideUsage();
    }

    public void AddCardToDisplay()
    {
        handCards.Add(SpawnBack());
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

    public List<GameObject> GetCards()          => GetCurrentCards();
    public List<GameObject> GetHandCards()      => handCards;
    public List<GameObject> GetOverSideCards()  => overSideCards;
    public List<GameObject> GetUnderSideCards() => underSideCards;

    public bool CanChance() => false;
    public bool GetTurn()   => false;

    // Legacy stub methods kept for compatibility
    public void AddHandCards(GameObject card)                    => handCards.Add(card);
    public void SetUnderSideCards(List<GameObject> newCards)     => underSideCards = newCards;
    public void SetOverSideCards(List<GameObject> newCards)      => overSideCards = newCards;
    public void SwitchOutSideCards() { }
}
