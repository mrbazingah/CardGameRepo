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
    [SerializeField] Transform cardParent;

    [Header("Transform and Spacing")]
    [SerializeField] Transform handTransform;
    [SerializeField] Transform underSideTransform, overSideTransform;
    [SerializeField] float baseCardSpacing = 150f, maxHandWidth = 1000f, popUpHeight = 50f;
    [SerializeField] float sideBaseCardSpacing = 150f, sideMaxHandWidth = 1000f, overSideOffset;
    [SerializeField] Vector2 isTurnPos, isNotTurnPos;
    [SerializeField] float lerpSpeed;

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

    void Awake()
    {
        mainCam = Camera.main;
    }

    void Start()
    {
        PlayerInput playerInput = InputManager.Instance.GetPlayerInput();
        interactAction = playerInput.actions.FindAction("Interact");
    }

    // Deal receive
    public void ReceiveDeal(CardNetData[] hand, CardNetData[] underSide, CardNetData[] overSide)
    {
        Debug.Log($"[NPH] ReceiveDeal — hand={hand.Length} under={underSide.Length} over={overSide.Length}");
        foreach (CardNetData data in hand)
        {
            handCards.Add(SpawnCard(data, false));
        }

        foreach (CardNetData data in underSide)
        {
            underSideCards.Add(SpawnCard(data, true));
        }

        foreach (CardNetData data in overSide)
        {
            overSideCards.Add(SpawnCard(data, false));
        }

        SortHandCards();
    }

    GameObject SpawnCard(CardNetData data, bool covered)
    {
        GameObject card = Instantiate(cardPrefab);
        card.transform.parent = cardParent;
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

    // Sorting & layout
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
        ArrangeCards(handCards, handTransform, baseCardSpacing, maxHandWidth);
        ArrangeCards(overSideCards, overSideTransform, sideBaseCardSpacing, sideMaxHandWidth, overSideOffset);
        ArrangeCards(underSideCards, underSideTransform, sideBaseCardSpacing, sideMaxHandWidth);
    }

    void DetectHover()
    {
        Vector2 mousePos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        RaycastHit2D[] hits = Physics2D.RaycastAll(mousePos, Vector2.zero);
        hoveredCard = hits
            .OrderByDescending(h => h.collider.GetComponent<SpriteRenderer>().sortingOrder)
            .Select(h => h.collider.gameObject)
            .FirstOrDefault();

        bool gameStarted = NetworkGameManager.Instance != null && NetworkGameManager.Instance.GetGameHasStarted();
        if (hoveredCard != null && interactAction != null && interactAction.WasPressedThisFrame() && !gameStarted && selectedCards.Count < 2)
        {
            selectedCards.Add(hoveredCard);
            selectedCard = hoveredCard;
        }
    }

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

        bool gameStarted = NetworkGameManager.Instance != null && NetworkGameManager.Instance.GetGameHasStarted();
        if (gameStarted || selectedCards.Count != 2) return;

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

            CardNetData movedToOverSide = handCard.GetComponent<NetworkCard>().GetCardNetData();
            CardNetData movedToHand = sideCard.GetComponent<NetworkCard>().GetCardNetData();

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
                Debug.Log($"[NPH] Calling SwapCardsServerRpc — newOverSideCount={newOverSide.Length}");
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

    // Gets
    public List<GameObject> GetCurrentCards()
    {
        if (usingOverSideCards) return overSideCards;
        if (usingUnderSideCards) return underSideCards;
        return handCards;
    }

    public List<GameObject> GetHandCards() => handCards;
    public List<GameObject> GetOverSideCards() => overSideCards;
    public List<GameObject> GetUnderSideCards() => underSideCards;

    // Implemented when game loop is wired up
    public bool CanChance() => false;
    public bool GetTurn() => false;
}
