using System.Collections;
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

    // IMPORTANT: this must NOT be a field of type NetworkList/NetworkVariable.
    // NGO's codegen registers any NetworkVariableBase-typed field on a NetworkBehaviour
    // as a network variable and requires it initialized at declaration. We only need a
    // reference to the generator's list, so resolve it through a property instead.
    bool subscribed;

    NetworkList<CardNetData> OpponentList
    {
        get
        {
            NetworkCardGenerator gen = NetworkCardGenerator.Instance;
            if (gen == null) { return null; }
            return IsServer ? gen.player1OverSide : gen.player0OverSide;
        }
    }

    public override void OnNetworkSpawn()
    {
        StartCoroutine(BindToOpponentList());
    }

    public override void OnNetworkDespawn()
    {
        if (subscribed && OpponentList != null)
        {
            OpponentList.OnListChanged -= OnOpponentOverSideChanged;
        }
        subscribed = false;
    }

    // The generator may spawn after this object, so wait for Instance before binding.
    IEnumerator BindToOpponentList()
    {
        while (OpponentList == null) { yield return null; }

        OpponentList.OnListChanged += OnOpponentOverSideChanged;
        subscribed = true;

        Debug.Log($"[NOH] Bound to opponent overSide list — IsServer={IsServer}");

        // Rebuild once in case the list was populated before we subscribed.
        RebuildOverSideFromList();
    }

    void OnOpponentOverSideChanged(NetworkListEvent<CardNetData> _)
    {
        RebuildOverSideFromList();
    }

    void RebuildOverSideFromList()
    {
        NetworkList<CardNetData> list = OpponentList;
        if (list == null) { return; }

        CardNetData[] arr = new CardNetData[list.Count];
        for (int i = 0; i < list.Count; i++) { arr[i] = list[i]; }

        SyncOverSide(arr);
    }

    // Deal receive — hand + underSide only. OverSide is driven by the NetworkList.
    public void ReceiveDeal(CardNetData[] hand, CardNetData[] underSide)
    {
        Debug.Log($"[NOH] ReceiveDeal — hand={hand.Length} under={underSide.Length}");
        foreach (CardNetData data in hand) { handCards.Add(SpawnCoveredCard(data)); }
        foreach (CardNetData data in underSide) { underSideCards.Add(SpawnCoveredCard(data)); }
    }

    GameObject SpawnCoveredCard(CardNetData data = default)
    {
        GameObject card = Instantiate(cardPrefab);
        card.transform.parent = cardParent;
        card.transform.localPosition = Vector3.zero;

        NetworkCard nc = card.GetComponent<NetworkCard>();
        nc.SetCardId(data.CardId);
        nc.SetValue(data.Value);

        SpriteRenderer sr = card.GetComponent<SpriteRenderer>();
        if (cardSprites != null && data.CardId < cardSprites.Count)
        {
            sr.sprite = cardSprites[data.CardId];
            sr.color = Color.white;
        }

        GameObject back = Instantiate(backCardPrefab);
        back.transform.parent = card.transform;
        back.transform.localPosition = Vector3.zero;
        back.GetComponent<SpriteRenderer>().sortingOrder = sr.sortingOrder + 1;
        nc.ApplyChild(back);

        return card;
    }

    GameObject SpawnFaceCard(CardNetData data)
    {
        GameObject card = Instantiate(cardPrefab);
        card.transform.parent = cardParent;
        card.transform.localPosition = Vector3.zero;

        NetworkCard nc = card.GetComponent<NetworkCard>();
        nc.SetCardId(data.CardId);
        nc.SetValue(data.Value);

        SpriteRenderer sr = card.GetComponent<SpriteRenderer>();
        if (cardSprites != null && data.CardId < cardSprites.Count)
        {
            sr.sprite = cardSprites[data.CardId];
            sr.color = Color.white;
        }
        else
        {
            Debug.LogWarning($"[NOH] cardSprites not set or CardId {data.CardId} out of range — assign sprites to the NetworkOpponent prefab.");
        }

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

            if (nc.GetBack() != null)
            { nc.GetBack().GetComponent<SpriteRenderer>().sortingOrder = sr.sortingOrder + 1; }

            float horizontalOffset = cards.Count > 1 ? cardSpacing * (i - (cards.Count - 1) / 2f) : 0f;

            cards[i].transform.localPosition = Vector2.Lerp(cards[i].transform.localPosition, new Vector2(horizontalOffset + offset, offset), lerpSpeed * Time.deltaTime);
        }
    }

    // Rebuilds the opponent's face-up overSide stack from authoritative data.
    // Called via OnListChanged whenever the opponent's NetworkList changes
    // (initial deal seed and every subsequent swap).
    public void SyncOverSide(CardNetData[] newOverSide)
    {
        Debug.Log($"[NOH] SyncOverSide — newCount={newOverSide.Length}");
        foreach (GameObject card in overSideCards) { Destroy(card); }
        overSideCards.Clear();
        foreach (CardNetData data in newOverSide)
        {
            overSideCards.Add(SpawnFaceCard(data));
        }
    }

    // Called when the opponent swaps a hand card with an overSide card
    public void HandleOpponentSwap(CardNetData movedToOverSide, CardNetData movedToHand)
    {
        Debug.Log($"[NOH] HandleOpponentSwap — overSideCount={overSideCards.Count} handCount={handCards.Count} movedToOverSide={movedToOverSide.CardId} movedToHand={movedToHand.CardId}");

        for (int i = 0; i < overSideCards.Count; i++)
        {
            if (overSideCards[i].GetComponent<NetworkCard>().GetCardId() == movedToHand.CardId)
            {
                Destroy(overSideCards[i]);
                overSideCards.RemoveAt(i);
                break;
            }
        }

        overSideCards.Add(SpawnFaceCard(movedToOverSide));

        for (int i = 0; i < handCards.Count; i++)
        {
            if (handCards[i].GetComponent<NetworkCard>().GetCardId() == movedToOverSide.CardId)
            {
                NetworkCard nc = handCards[i].GetComponent<NetworkCard>();
                nc.SetCardId(movedToHand.CardId);
                nc.SetValue(movedToHand.Value);
                break;
            }
        }
    }

    // Called when opponent plays a card — removes one card from the display
    public void RemoveCardFromDisplay(bool fromHand = true)
    {
        List<GameObject> source = fromHand ? handCards : GetCurrentCards();
        if (source.Count == 0) { return; }

        GameObject card = source[source.Count - 1];
        source.RemoveAt(source.Count - 1);
        Destroy(card);

        UpdateSideUsage();
    }

    public void AddCardToDisplay(CardNetData data = default)
    {
        handCards.Add(SpawnCoveredCard(data));
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