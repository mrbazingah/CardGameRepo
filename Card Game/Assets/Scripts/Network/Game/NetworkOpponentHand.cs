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

    [Header("Play Phase")]
    [SerializeField] Transform pilePoint;   // pile position; pickup cards animate from here

    List<GameObject> handCards = new List<GameObject>();
    List<GameObject> underSideCards = new List<GameObject>();
    List<GameObject> overSideCards = new List<GameObject>();

    bool usingOverSideCards, usingUnderSideCards;

    // IMPORTANT: this must NOT be a field of type NetworkList/NetworkVariable.
    // NGO's codegen registers any NetworkVariableBase-typed field on a NetworkBehaviour
    // as a network variable and requires it initialized at declaration. We only need a
    // reference to the generator's list, so resolve it through a property instead.
    bool subscribed;
    bool overSideDirty;

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

    IEnumerator BindToOpponentList()
    {
        while (OpponentList == null) { yield return null; }

        OpponentList.OnListChanged += OnOpponentOverSideChanged;
        subscribed = true;

        Debug.Log($"[NOH] Bound to opponent overSide list — IsServer={IsServer}");
        RebuildOverSideFromList();
    }

    void OnOpponentOverSideChanged(NetworkListEvent<CardNetData> _)
    {
        // Coalesce multiple list events in a frame into one rebuild (see LateUpdate).
        overSideDirty = true;
    }

    void LateUpdate()
    {
        if (!overSideDirty) { return; }
        overSideDirty = false;
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

    // ---------------------------------------------------------------------
    // Deal / play phase events
    // ---------------------------------------------------------------------

    // Deal receive — hand + underSide only. OverSide is driven by the NetworkList.
    public void ReceiveDeal(CardNetData[] hand, CardNetData[] underSide)
    {
        Debug.Log($"[NOH] ReceiveDeal — hand={hand.Length} under={underSide.Length}");
        foreach (CardNetData data in hand) { handCards.Add(SpawnCoveredCard(data)); }
        foreach (CardNetData data in underSide) { underSideCards.Add(SpawnCoveredCard(data)); }
    }

    // The opponent played a card. Their hand/under displays lose one covered card;
    // their overSide display is driven by the generator's NetworkList and updates
    // automatically when the server removes the played card from it.
    public void OnOpponentPlayedCard(CardNetData card)
    {
        if (handCards.Count > 0)
        {
            RemoveOneCoveredCard(handCards);
        }
        else if (overSideCards.Count > 0)
        {
            // Handled by the NetworkList sync — nothing to do here.
        }
        else if (underSideCards.Count > 0)
        {
            RemoveOneCoveredCard(underSideCards);
        }
    }

    public void OnOpponentDrewCards(int count)
    {
        for (int i = 0; i < count; i++) { handCards.Add(SpawnCoveredCard()); }
    }

    public void OnOpponentPickedUpPile(int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject card = SpawnCoveredCard();
            if (pilePoint != null) { card.transform.position = pilePoint.position; }
            handCards.Add(card);
        }
    }

    void RemoveOneCoveredCard(List<GameObject> list)
    {
        GameObject card = list[list.Count - 1];
        list.RemoveAt(list.Count - 1);
        Destroy(card);
    }

    // ---------------------------------------------------------------------
    // Card spawning
    // ---------------------------------------------------------------------

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

    // ---------------------------------------------------------------------
    // Layout
    // ---------------------------------------------------------------------

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
            if (cards[i] == null) { continue; }

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

    // ---------------------------------------------------------------------
    // OverSide sync + swap animation
    // ---------------------------------------------------------------------

    // Diff-based rebuild of the opponent's face-up overSide stack.
    // Cards present in both old and new state keep their GameObject and position,
    // so an unrelated card never moves. A card that left the overSide animates
    // toward the hand and is destroyed on arrival. A card that entered the
    // overSide spawns at the hand position and lerps to its slot.
    public void SyncOverSide(CardNetData[] newOverSide)
    {
        Debug.Log($"[NOH] SyncOverSide — newCount={newOverSide.Length}");

        bool initialDeal = overSideCards.Count == 0;

        List<GameObject> updated = new List<GameObject>(newOverSide.Length);
        List<GameObject> leftovers = new List<GameObject>(overSideCards);

        foreach (CardNetData data in newOverSide)
        {
            GameObject existing = leftovers.Find(go => go != null && go.GetComponent<NetworkCard>().GetCardId() == data.CardId);
            if (existing != null)
            {
                leftovers.Remove(existing);
                updated.Add(existing);
            }
            else
            {
                GameObject card = SpawnFaceCard(data);
                card.transform.SetParent(overSideTransform);

                if (!initialDeal)
                {
                    card.transform.position = handTransform.position;
                }

                updated.Add(card);
            }
        }

        foreach (GameObject removed in leftovers)
        {
            if (removed != null)
            {
                // During the play phase a card leaving the overSide went to the
                // pile, not the hand — animate toward the pile in that case.
                bool gameStarted = NetworkGameManager.Instance != null && NetworkGameManager.Instance.GetGameHasStarted();
                Vector3 target = gameStarted && pilePoint != null ? pilePoint.position : handTransform.position;
                StartCoroutine(AnimateAwayAndDestroy(removed, target));
            }
        }

        overSideCards = updated;
    }

    IEnumerator AnimateAwayAndDestroy(GameObject card, Vector3 target)
    {
        card.transform.SetParent(cardParent);

        SpriteRenderer sr = card.GetComponent<SpriteRenderer>();
        if (sr != null) { sr.sortingOrder = 50; }

        while (card != null && (card.transform.position - target).sqrMagnitude > 1f)
        {
            card.transform.position = Vector3.Lerp(card.transform.position, target, lerpSpeed * Time.deltaTime);
            yield return null;
        }

        if (card != null) { Destroy(card); }
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

    // ---------------------------------------------------------------------
    // Getters
    // ---------------------------------------------------------------------

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
    public bool GetTurn() => NetworkGameManager.Instance != null
        && NetworkGameManager.Instance.GetGameHasStarted()
        && !NetworkGameManager.Instance.IsMyTurn;

    public void AddHandCards(GameObject card) => handCards.Add(card);
    public void SetUnderSideCards(List<GameObject> newCards) => underSideCards = newCards;
    public void SetOverSideCards(List<GameObject> newCards) => overSideCards = newCards;
    public void SwitchOutSideCards() { }
}