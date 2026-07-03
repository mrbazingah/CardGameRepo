using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class NetworkPlayerHand : NetworkBehaviour
{
    [Header("Prefabs & Sprites")]
    [SerializeField] GameObject cardPrefab;      // prefab with NetworkCard component
    [SerializeField] GameObject backCardPrefab;
    [SerializeField] List<Sprite> cardSprites;   // 52 sprites in deck order (matches CardId)
    [SerializeField] Transform deckTransform;

    [Header("Transform and Spacing")]
    [SerializeField] Transform handTransform;
    [SerializeField] Transform underSideTransform, overSideTransform;
    [SerializeField] float baseCardSpacing = 150f, maxHandWidth = 1000f, popUpHeight = 50f;
    [SerializeField] float sideBaseCardSpacing = 150f, sideMaxHandWidth = 1000f, overSideOffset;
    [SerializeField] Vector2 isTurnPos, isNotTurnPos;
    [SerializeField] float lerpSpeed;

    [Header("Play Phase")]
    [SerializeField] GameObject startGameButton;
    [SerializeField] GameObject endTurnButton;

    [SerializeField] List<GameObject> handCards = new List<GameObject>();
    [SerializeField] List<GameObject> underSideCards = new List<GameObject>();
    [SerializeField] List<GameObject> overSideCards = new List<GameObject>();

    bool usingOverSideCards, usingUnderSideCards;
    Camera mainCam;
    GameObject hoveredCard;

    List<GameObject> selectedCards = new List<GameObject>(0);
    GameObject selectedCard;
    GameObject previousSelectedCard;
    InputAction interactAction;

    // Local mirror of turn state, fed by NetworkGameManager's targeted RPCs.
    bool canEndTurn;
    int savedCardValue;
    Vector2 handTransformVelocity;

    void Awake()
    {
        mainCam = Camera.main;
    }

    void Start()
    {
        PlayerInput playerInput = InputManager.Instance.GetPlayerInput();
        interactAction = playerInput.actions.FindAction("Interact");

        if (endTurnButton != null) { endTurnButton.SetActive(false); }
        if (startGameButton != null) { startGameButton.SetActive(true); }
    }

    // ---------------------------------------------------------------------
    // Deal / draw / pickup receiving
    // ---------------------------------------------------------------------

    public void ReceiveDeal(CardNetData[] hand, CardNetData[] underSide, CardNetData[] overSide)
    {
        Debug.Log($"[NPH] ReceiveDeal — hand={hand.Length} under={underSide.Length} over={overSide.Length}");
        foreach (CardNetData data in hand) { handCards.Add(SpawnCard(data, false)); }
        foreach (CardNetData data in underSide) { underSideCards.Add(SpawnCard(data, true)); }
        foreach (CardNetData data in overSide) { overSideCards.Add(SpawnCard(data, false)); }

        SortHandCards();
    }

    // Server-confirmed draws after a play.
    public void ReceiveDrawnCards(CardNetData[] cards)
    {
        foreach (CardNetData data in cards) { handCards.Add(SpawnCard(data, false)); }
        SortHandCards();
    }

    // Server-confirmed pile pickup: cards spawn at the pile and lerp into the hand.
    public void ReceivePileCards(CardNetData[] cards)
    {
        foreach (CardNetData data in cards)
        {
            GameObject card = SpawnCard(data, false);
            if (deckTransform != null) { card.transform.position = deckTransform.position; }
            handCards.Add(card);
        }
        SortHandCards();
    }

    public void SetTurnState(bool endTurnAllowed, int savedValue)
    {
        canEndTurn = endTurnAllowed;
        savedCardValue = savedValue;
    }

    GameObject SpawnCard(CardNetData data, bool covered)
    {
        GameObject card = Instantiate(cardPrefab);
        card.transform.parent = deckTransform;
        card.transform.localPosition = Vector3.zero;

        NetworkCard nc = card.GetComponent<NetworkCard>();
        nc.SetCardId(data.CardId);
        nc.SetValue(data.Value);

        SpriteRenderer sr = card.GetComponent<SpriteRenderer>();
        sr.sprite = cardSprites[data.CardId];
        sr.color = Color.white;

        if (covered)
        {
            GameObject back = Instantiate(backCardPrefab);
            back.transform.parent = card.transform;
            back.transform.localPosition = Vector3.zero;
            back.GetComponent<SpriteRenderer>().sortingOrder = sr.sortingOrder + 1;
            nc.ApplyChild(back);
        }

        return card;
    }

    // ---------------------------------------------------------------------
    // UI hooks
    // ---------------------------------------------------------------------

    // Wire the in-scene Start button to this.
    public void OnStartGamePressed()
    {
        if (NetworkGameManager.Instance == null) { return; }
        if (startGameButton != null) { startGameButton.SetActive(false); }
        NetworkGameManager.Instance.RequestStartGame();
    }

    // Wire the in-scene End Turn button to this.
    public void OnEndTurnPressed()
    {
        if (NetworkGameManager.Instance == null || !canEndTurn) { return; }
        canEndTurn = false;
        NetworkGameManager.Instance.EndTurnServerRpc();
    }

    // ---------------------------------------------------------------------
    // Update loop
    // ---------------------------------------------------------------------

    public void SortHandCards()
    {
        handCards.Sort((a, b) => a.GetComponent<NetworkCard>().GetValue().CompareTo(b.GetComponent<NetworkCard>().GetValue()));
    }

    void Update()
    {
        UpdateSideUsage();
        UpdateColliders();
        DetectHover();
        ChangeSideCards();
        UpdateTurnUI();
        ArrangeCards(handCards, handTransform, baseCardSpacing, maxHandWidth);
        ArrangeCards(overSideCards, overSideTransform, sideBaseCardSpacing, sideMaxHandWidth, overSideOffset);
        ArrangeCards(underSideCards, underSideTransform, sideBaseCardSpacing, sideMaxHandWidth);
    }

    bool GameStarted => NetworkGameManager.Instance != null && NetworkGameManager.Instance.GetGameHasStarted();
    bool IsMyTurn => NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsMyTurn;

    void UpdateTurnUI()
    {
        if (endTurnButton != null) { endTurnButton.SetActive(GameStarted && IsMyTurn && canEndTurn); }
        if (startGameButton != null && GameStarted && startGameButton.activeSelf) { startGameButton.SetActive(false); }

        // Slide the hand up on your turn, down otherwise (mirrors singleplayer).
        if (handTransform != null)
        {
            Vector2 target = (IsMyTurn || !GameStarted) ? isTurnPos : isNotTurnPos;
            handTransform.position = Vector2.SmoothDamp(handTransform.position, target, ref handTransformVelocity, 1f / Mathf.Max(lerpSpeed, 0.01f));
        }

        // Gray out unplayable hand cards during your turn.
        int pileTop = NetworkPile.Instance != null ? NetworkPile.Instance.GetTopValue() : 0;
        foreach (GameObject card in handCards)
        {
            NetworkCard nc = card.GetComponent<NetworkCard>();
            bool playable = CanPlayValue(nc.GetValue(), pileTop) && (savedCardValue == 0 || nc.GetValue() == savedCardValue);
            nc.ChangeColor(!GameStarted || !IsMyTurn || playable);
        }
    }

    static bool CanPlayValue(int value, int pileTop)
    {
        return value >= pileTop || value == 2 || value == 10;
    }

    // ---------------------------------------------------------------------
    // Input
    // ---------------------------------------------------------------------

    void DetectHover()
    {
        Vector2 mousePos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        RaycastHit2D[] hits = Physics2D.RaycastAll(mousePos, Vector2.zero);
        hoveredCard = hits
            .OrderByDescending(h => h.collider.GetComponent<SpriteRenderer>().sortingOrder)
            .Select(h => h.collider.gameObject)
            .FirstOrDefault();

        if (hoveredCard == null || interactAction == null || !interactAction.WasPressedThisFrame()) { return; }

        if (!GameStarted)
        {
            // Pre-game: clicking selects cards for the side-card swap.
            if (selectedCards.Count < 2)
            {
                selectedCards.Add(hoveredCard);
                selectedCard = hoveredCard;
            }
            return;
        }

        TryRequestPlay(hoveredCard);
    }

    void TryRequestPlay(GameObject card)
    {
        if (!IsMyTurn || NetworkGameManager.Instance == null) { return; }

        // Only cards in the currently active pile are clickable.
        bool inActive =
            (handCards.Contains(card) && !usingOverSideCards && !usingUnderSideCards) ||
            (overSideCards.Contains(card) && usingOverSideCards) ||
            (underSideCards.Contains(card) && usingUnderSideCards);
        if (!inActive) { return; }

        NetworkCard nc = card.GetComponent<NetworkCard>();
        int value = nc.GetValue();
        int pileTop = NetworkPile.Instance != null ? NetworkPile.Instance.GetTopValue() : 0;

        // Underside cards are played blind — always send.
        if (!usingUnderSideCards)
        {
            if (savedCardValue != 0 && value != savedCardValue) { return; }

            bool playable = CanPlayValue(value, pileTop);
            if (!playable)
            {
                // Dumping an unplayable card (and picking up the pile) is only
                // legal when nothing in the active pile can be played.
                List<GameObject> active = GetCurrentCards();
                foreach (GameObject c in active)
                {
                    if (CanPlayValue(c.GetComponent<NetworkCard>().GetValue(), pileTop)) { return; }
                }
            }
        }

        Debug.Log($"[NPH] Requesting play — id={nc.GetCardId()} value={value}");
        NetworkGameManager.Instance.PlayCardServerRpc(nc.GetCardNetData());
    }

    // Called from NetworkGameManager when the server confirms our own play
    // (routed through OpponentActionClientRpc being skipped for the actor —
    // the actor removes the card here via the targeted confirmation path).
    public void RemovePlayedCard(CardNetData card)
    {
        List<GameObject>[] lists = { handCards, overSideCards, underSideCards };
        foreach (List<GameObject> list in lists)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].GetComponent<NetworkCard>().GetCardId() == card.CardId)
                {
                    Destroy(list[i]);
                    list.RemoveAt(i);
                    return;
                }
            }
        }
    }

    // ---------------------------------------------------------------------
    // Pre-game side card swap (unchanged)
    // ---------------------------------------------------------------------

    void ChangeSideCards()
    {
        if (selectedCard != null && selectedCard != previousSelectedCard)
        {
            selectedCard.GetComponent<NetworkCard>().SetHighlight(true);
            if (previousSelectedCard != null)
                previousSelectedCard.GetComponent<NetworkCard>().SetHighlight(false);
            previousSelectedCard = selectedCard;
        }
        else if (selectedCards.Count == 0 && previousSelectedCard != null)
        {
            previousSelectedCard.GetComponent<NetworkCard>().SetHighlight(false);
        }

        if (GameStarted || selectedCards.Count != 2) return;

        GameObject lastSelectedCard = null;

        if (!SwapHandAndOverSideCard(out GameObject handCard, out GameObject sideCard, 0, 1) &&
            !SwapHandAndOverSideCard(out handCard, out sideCard, 1, 0))
        {
            lastSelectedCard = selectedCards[1];
        }

        if (previousSelectedCard != null) previousSelectedCard.GetComponent<NetworkCard>().SetHighlight(false);
        if (handCard != null) handCard.GetComponent<NetworkCard>().SetHighlight(false);
        if (sideCard != null) sideCard.GetComponent<NetworkCard>().SetHighlight(false);

        selectedCards = new List<GameObject>(0);
        previousSelectedCard = null;
        selectedCard = null;

        if (lastSelectedCard != null)
        {
            selectedCards.Add(lastSelectedCard);
            selectedCard = lastSelectedCard;
        }
    }

    bool SwapHandAndOverSideCard(out GameObject handCard, out GameObject sideCard, int handIndex, int sideIndex)
    {
        if (handCards.Contains(selectedCards[handIndex]) && overSideCards.Contains(selectedCards[sideIndex]))
        {
            handCard = selectedCards[handIndex];
            sideCard = selectedCards[sideIndex];

            for (int i = 0; i < handCards.Count; i++)
            {
                if (handCards[i] == handCard) { handCards[i] = sideCard; break; }
            }

            for (int i = 0; i < overSideCards.Count; i++)
            {
                if (overSideCards[i] == sideCard) { overSideCards[i] = handCard; break; }
            }

            SortHandCards();

            if (NetworkCardGenerator.Instance != null)
            {
                CardNetData[] newOverSide = overSideCards
                    .Select(go => go.GetComponent<NetworkCard>().GetCardNetData())
                    .ToArray();
                NetworkCardGenerator.Instance.SwapCardsServerRpc(newOverSide);
            }
            else
            {
                Debug.LogError("[NPH] SwapCardsServerRpc skipped — NetworkCardGenerator.Instance is null");
            }

            return true;
        }

        handCard = null;
        sideCard = null;
        return false;
    }

    // ---------------------------------------------------------------------
    // Layout
    // ---------------------------------------------------------------------

    void UpdateSideUsage()
    {
        usingOverSideCards = handCards.Count == 0 && overSideCards.Count > 0;
        usingUnderSideCards = handCards.Count == 0 && overSideCards.Count == 0 && underSideCards.Count > 0;
    }

    void UpdateColliders()
    {
        for (int i = 0; i < underSideCards.Count; i++)
        {
            underSideCards[i].GetComponent<BoxCollider2D>().enabled = usingUnderSideCards;
        }
    }

    void ArrangeCards(List<GameObject> cards, Transform parent, float spacing, float maxWidth, float offset = 0)
    {
        if (cards.Count == 0) return;

        float cardSpacing = Mathf.Min(spacing, maxWidth / cards.Count);

        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].transform.SetParent(parent);

            SpriteRenderer sr = cards[i].GetComponent<SpriteRenderer>();
            NetworkCard nc = cards[i].GetComponent<NetworkCard>();

            if (cards == handCards)
            {
                sr.sortingOrder = i;
            }
            else if (cards == overSideCards)
            {
                sr.sortingOrder = i + 3;
            }
            else
            {
                sr.sortingOrder = i;
                if (nc.GetBack() != null)
                {
                    nc.GetBack().GetComponent<SpriteRenderer>().sortingOrder = i + 1;
                }
            }

            float horizontalOffset = cards.Count > 1 ? cardSpacing * (i - (cards.Count - 1) / 2f) : 0f;
            bool isHovered = (cards[i] == hoveredCard);

            Vector2 targetPos;
            if (cards == handCards)
            {
                float verticalOffset = isHovered ? offset + popUpHeight : offset;
                targetPos = new Vector2(horizontalOffset + offset, verticalOffset);
            }
            else
            {
                nc.basePosition = new Vector2(horizontalOffset + offset, offset);
                float verticalOffset = isHovered ? offset + popUpHeight : offset;
                targetPos = new Vector2(horizontalOffset + offset, verticalOffset);
            }

            cards[i].transform.localPosition = Vector2.Lerp(
                cards[i].transform.localPosition,
                targetPos,
                lerpSpeed * Time.deltaTime
            );
        }
    }

    // ---------------------------------------------------------------------
    // Gets
    // ---------------------------------------------------------------------

    public List<GameObject> GetCurrentCards()
    {
        if (usingOverSideCards) return overSideCards;
        if (usingUnderSideCards) return underSideCards;
        return handCards;
    }

    public List<GameObject> GetHandCards() => handCards;
    public List<GameObject> GetOverSideCards() => overSideCards;
    public List<GameObject> GetUnderSideCards() => underSideCards;

    public bool CanChance() => false;
    public bool GetTurn() => IsMyTurn;
}