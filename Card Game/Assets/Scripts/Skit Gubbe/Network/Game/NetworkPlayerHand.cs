using Fusion;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class NetworkPlayerHand : NetworkBehaviour
{
    [Header("Cards")]
    [SerializeField] Transform handTransform;
    [SerializeField] Transform underSideTransform;
    [SerializeField] Transform overSideTransform;
    
    [Header("Spacing")]
    [SerializeField] float baseCardSpacing = 150f;
    [SerializeField] float maxHandWidth = 1000f;
    [SerializeField] float sideBaseCardSpacing = 150f;
    [SerializeField] float sideMaxHandWidth = 1000f;
    [SerializeField] float popUpHeight = 50f;
    [SerializeField] float overSideOffset;
    [SerializeField] float lerpSpeed;
    
    [Header("UI - Found at runtime if not assigned")]
    [SerializeField] GameObject endTurnButton;
    [SerializeField] GameObject readyButton;
    [SerializeField] GameObject chanceNotice;
    [SerializeField] TextMeshProUGUI cardAmountText;
    [SerializeField] Vector2 cardAmountTextOffset;
    [SerializeField] Vector2 isTurnPos;
    [SerializeField] Vector2 isNotTurnPos;
    
    [Header("UI Search Names - Used to find UI in scene")]
    [SerializeField] string readyButtonName = "Ready Button";
    [SerializeField] string endTurnButtonName = "End Turn Button";
    [SerializeField] string chanceNoticeName = "Chance Notice";
    
    [Header("Chance")]
    [SerializeField] float playChanceDelay = 1f;
    
    [SerializeField] LayerMask cardLayerMask = -1;
    
    NetworkRunner runner;
    GameManagerNetwork gm;
    NetworkCardGenerator cardGenerator;
    NetworkPile pile;
    
    // Local card GameObjects (not NetworkObjects)
    List<GameObject> handCards = new List<GameObject>();
    List<GameObject> underSideCards = new List<GameObject>();
    List<GameObject> overSideCards = new List<GameObject>();
    
    [Networked] byte savedCardValue { get; set; }
    [Networked] bool hasDiscarded { get; set; }
    [Networked] bool canEndTurn { get; set; }
    [Networked] public int NetworkedHandCount { get; set; }
    
    bool usingOverSideCards;
    bool usingUnderSideCards;
    GameObject hoveredCard;
    int cardsPerPlayer;
    
    public int HandCount => GetCurrentCards().Count;

    public override void FixedUpdateNetwork()
    {
        // Update networked hand count so opponent can see it
        if (Object.HasStateAuthority)
        {
            int currentCount = HandCount;
            if (currentCount != NetworkedHandCount)
            {
                NetworkedHandCount = currentCount;
            }
        }
    }

    public override void Spawned()
    {
        runner = Runner;
        gm = FindObjectOfType<GameManagerNetwork>();
        cardGenerator = FindObjectOfType<NetworkCardGenerator>();
        pile = FindObjectOfType<NetworkPile>();
        
        // Find UI elements in scene if not assigned in prefab
        FindUIElements();
        
        if (cardGenerator != null)
        {
            cardsPerPlayer = cardGenerator.GetCardsPerPlayer();
        }
        
        Debug.Log($"NetworkPlayerHand: Spawned for player {Object.InputAuthority}. HasInputAuthority: {HasInputAuthority}");
        
        if (HasInputAuthority)
        {
            SetupLocalPlayerUI();
        }
    }

    void FindUIElements()
    {
        // Find UI elements by name if not already assigned
        if (readyButton == null && !string.IsNullOrEmpty(readyButtonName))
        {
            var found = GameObject.Find(readyButtonName);
            if (found != null)
            {
                readyButton = found;
                Debug.Log($"NetworkPlayerHand: Found ready button '{readyButtonName}'");
            }
        }
        
        if (endTurnButton == null && !string.IsNullOrEmpty(endTurnButtonName))
        {
            var found = GameObject.Find(endTurnButtonName);
            if (found != null)
            {
                endTurnButton = found;
                Debug.Log($"NetworkPlayerHand: Found end turn button '{endTurnButtonName}'");
            }
        }
        
        if (chanceNotice == null && !string.IsNullOrEmpty(chanceNoticeName))
        {
            var found = GameObject.Find(chanceNoticeName);
            if (found != null)
            {
                chanceNotice = found;
                Debug.Log($"NetworkPlayerHand: Found chance notice '{chanceNoticeName}'");
            }
        }
        
        // Create transforms for card positioning if not assigned
        if (handTransform == null)
        {
            var handGO = new GameObject("HandTransform");
            handGO.transform.SetParent(transform);
            handGO.transform.localPosition = Vector3.zero;
            handTransform = handGO.transform;
            Debug.Log("NetworkPlayerHand: Created HandTransform");
        }
        
        if (underSideTransform == null)
        {
            var underGO = new GameObject("UnderSideTransform");
            underGO.transform.SetParent(transform);
            underGO.transform.localPosition = new Vector3(0, 0.5f, 0);
            underSideTransform = underGO.transform;
            Debug.Log("NetworkPlayerHand: Created UnderSideTransform");
        }
        
        if (overSideTransform == null)
        {
            var overGO = new GameObject("OverSideTransform");
            overGO.transform.SetParent(transform);
            overGO.transform.localPosition = new Vector3(0, 0.5f, 0);
            overSideTransform = overGO.transform;
            Debug.Log("NetworkPlayerHand: Created OverSideTransform");
        }
    }

    void SetupLocalPlayerUI()
    {
        // Hide end turn button initially
        if (endTurnButton != null)
        {
            endTurnButton.SetActive(false);
        }
        
        // Show and hook up ready button
        if (readyButton != null)
        {
            readyButton.SetActive(true);
            var button = readyButton.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnReadyButtonClicked);
                Debug.Log("NetworkPlayerHand: Ready button hooked up");
            }
        }
        else
        {
            Debug.LogWarning("NetworkPlayerHand: Ready button not found! Players won't be able to ready up.");
        }
    }

    void OnReadyButtonClicked()
    {
        if (gm != null && !gm.GameStarted)
        {
            gm.LocalPlayerReady();
            if (readyButton != null)
            {
                readyButton.SetActive(false);
            }
        }
    }

    void Update()
    {
        if (gm == null)
            return;

        // Hide ready button once game starts
        if (gm.GameStarted && readyButton != null && readyButton.activeSelf)
        {
            readyButton.SetActive(false);
        }

        if (!gm.GameStarted)
        {
            // Show ready button if game hasn't started yet
            if (HasInputAuthority && readyButton != null)
            {
                readyButton.SetActive(true);
            }
            return;
        }

        if (HasInputAuthority)
        {
            ChangeSideCards();
            SetCardAmountText();
            UpdateSideUsage();
            DetectHover();
            SortCards();
            CanEndTurn();
            CheckTurn();
            
            if (chanceNotice != null)
            {
                chanceNotice.SetActive(CanChance());
            }
        }
    }

    #region Card Management - Local Card Creation
    /// <summary>
    /// Creates a local Card GameObject from a byte value.
    /// Called by RPCs to generate cards locally on all clients.
    /// </summary>
    GameObject CreateLocalCard(byte value, bool faceUp = false)
    {
        if (cardGenerator == null)
        {
            cardGenerator = FindObjectOfType<NetworkCardGenerator>();
            if (cardGenerator == null)
            {
                Debug.LogError("NetworkPlayerHand: Cannot create card - NetworkCardGenerator not found!");
                return null;
            }
        }
        
        // Get prefab and sprites from cardGenerator (now public)
        GameObject prefab = cardGenerator.cardPrefab;
        Sprite[] sprites = cardGenerator.cardSprites;
        
        if (prefab == null)
        {
            Debug.LogError("NetworkPlayerHand: Card prefab not found in NetworkCardGenerator!");
            return null;
        }
        
        GameObject card = Instantiate(prefab);
        Card cardComponent = card.GetComponent<Card>();
        if (cardComponent != null)
        {
            cardComponent.SetValue(value);
        }
        
        // Set sprite (value 2-14 maps to sprite indices)
        SpriteRenderer sr = card.GetComponent<SpriteRenderer>();
        if (sr != null && sprites != null && sprites.Length > 0)
        {
            int spriteIndex = value - 2; // Value 2 maps to index 0
            if (spriteIndex >= 0 && spriteIndex < sprites.Length)
            {
                sr.sprite = sprites[spriteIndex];
            }
            sr.gameObject.SetActive(faceUp);
        }
        
        // Apply back cover if face down
        if (!faceUp)
        {
            GameObject backPrefab = cardGenerator.backCardPrefab;
            
            if (backPrefab != null)
            {
                GameObject back = Instantiate(backPrefab);
                back.transform.SetParent(card.transform);
                back.transform.localPosition = Vector3.zero;
                if (cardComponent != null)
                {
                    cardComponent.ApplyChild(back);
                }
                if (sr != null)
                {
                    var backSr = back.GetComponent<SpriteRenderer>();
                    if (backSr != null)
                    {
                        backSr.sortingOrder = sr.sortingOrder + 1;
                    }
                }
            }
        }
        
        return card;
    }
    
    /// <summary>
    /// Adds hand cards locally from byte values (called by RPC).
    /// </summary>
    public void AddHandCardsLocally(byte[] cardValues)
    {
        if (cardValues == null || cardValues.Length == 0)
            return;
            
        foreach (byte value in cardValues)
        {
            GameObject card = CreateLocalCard(value, false);
            if (card != null)
            {
                handCards.Add(card);
            }
        }
        
        SortHandCards();
        SortCards();
    }
    
    /// <summary>
    /// Sets side cards locally from byte values (called by RPC).
    /// </summary>
    public void SetSideCardsLocally(byte[] underSideValues, byte[] overSideValues)
    {
        // Clear existing
        foreach (var card in underSideCards) { if (card != null) Destroy(card); }
        foreach (var card in overSideCards) { if (card != null) Destroy(card); }
        underSideCards.Clear();
        overSideCards.Clear();
        
        // Create under side cards
        if (underSideValues != null)
        {
            for (int i = 0; i < underSideValues.Length; i++)
            {
                GameObject card = CreateLocalCard(underSideValues[i], false);
                if (card != null)
                {
                    underSideCards.Add(card);
                    var sr = card.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.sortingOrder = i;
                    }
                }
            }
        }
        
        // Create over side cards
        if (overSideValues != null)
        {
            for (int i = 0; i < overSideValues.Length; i++)
            {
                GameObject card = CreateLocalCard(overSideValues[i], false);
                if (card != null)
                {
                    overSideCards.Add(card);
                    var sr = card.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.sortingOrder = i + 3;
                    }
                }
            }
        }
        
        SortCards();
    }
    
    /// <summary>
    /// Adds a chance card locally (called by RPC).
    /// </summary>
    public void AddChanceCardLocally(byte value)
    {
        GameObject card = CreateLocalCard(value, true); // Chance cards are face up
        if (card != null)
        {
            handCards.Add(card);
            SortHandCards();
            SortCards();
        }
    }

    void SortHandCards()
    {
        handCards = handCards.OrderBy(c => 
        {
            var card = c.GetComponent<Card>();
            return card != null ? card.GetValue() : 0;
        }).ToList();
    }

    void UpdateSideUsage()
    {
        usingOverSideCards = handCards.Count == 0 && overSideCards.Count > 0;
        usingUnderSideCards = handCards.Count == 0 && overSideCards.Count == 0 && underSideCards.Count > 0;
    }

    List<GameObject> GetCurrentCards()
    {
        if (usingOverSideCards) return overSideCards;
        if (usingUnderSideCards) return underSideCards;
        return handCards;
    }
    
    public int GetLowestValueExcluding(int exclude1, int exclude2)
    {
        var currentCards = GetCurrentCards();
        int lowest = int.MaxValue;
        
        foreach (var cardObj in currentCards)
        {
            if (cardObj == null) continue;
            var card = cardObj.GetComponent<Card>();
            if (card == null) continue;
            
            int value = card.GetValue();
            if (value != exclude1 && value != exclude2 && value < lowest)
            {
                lowest = value;
            }
        }
        
        return lowest == int.MaxValue ? 0 : lowest;
    }
    #endregion

    #region UI
    void SetCardAmountText()
    {
        var currentCards = GetCurrentCards();
        if (currentCards.Count > 0 && cardAmountText != null)
        {
            cardAmountText.text = currentCards.Count.ToString();
            Vector2 cardAmountTextPos = currentCards[0].transform.position;
            cardAmountTextPos = new Vector2(cardAmountTextPos.x + cardAmountTextOffset.x, cardAmountTextOffset.y + handTransform.position.y);
            cardAmountText.transform.position = cardAmountTextPos;
            cardAmountText.gameObject.SetActive(true);
        }
        else if (cardAmountText != null)
        {
            cardAmountText.gameObject.SetActive(false);
        }
    }

    void CanEndTurn()
    {
        if (endTurnButton != null && HasInputAuthority)
        {
            bool myTurn = gm.CurrentTurn == Object.InputAuthority;
            endTurnButton.SetActive(canEndTurn && myTurn);
        }
    }

    void CheckTurn()
    {
        if (!gm.GameStarted)
            return;
    }
    #endregion

    #region Sorting and Arrangement
    void SortCards()
    {
        ArrangeCards(handCards, handTransform, baseCardSpacing, maxHandWidth);
        ArrangeCards(overSideCards, overSideTransform, sideBaseCardSpacing, sideMaxHandWidth, overSideOffset);
        ArrangeCards(underSideCards, underSideTransform, sideBaseCardSpacing, sideMaxHandWidth);

        if (handTransform != null)
        {
            bool isTurn = gm.CurrentTurn == Object.InputAuthority;
            Vector2 currentPos = isTurn || !gm.GameStarted ? isTurnPos : isNotTurnPos;
            handTransform.position = Vector2.Lerp(handTransform.position, currentPos, lerpSpeed * Time.deltaTime);
        }
    }

    void ArrangeCards(List<GameObject> cards, Transform parent, float spacing, float maxWidth, float offset = 0)
    {
        if (cards.Count == 0 || parent == null)
            return;

        float cardSpacing = Mathf.Min(spacing, maxWidth / cards.Count);

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null)
                continue;

            var go = cards[i];
            go.transform.SetParent(parent);

            bool isHovered = (cards[i] == hoveredCard);
            Vector2 cardPosition;

            if (cards == handCards)
            {
                float horizontalOffset = cardSpacing * (i - (cards.Count - 1) / 2f);
                float verticalOffset = isHovered ? offset + popUpHeight : offset;
                cardPosition = new Vector2(horizontalOffset + offset, verticalOffset);
                
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sortingOrder = i;
                }
            }
            else
            {
                float horizontalOffset = cardSpacing * (i - (cards.Count - 1) / 2f);
                float verticalOffset = isHovered ? offset + popUpHeight : offset;
                cardPosition = new Vector2(horizontalOffset + offset, verticalOffset);
                
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    if (cards == overSideCards)
                    {
                        sr.sortingOrder = i + 3;
                    }
                    else
                    {
                        sr.sortingOrder = i;
                    }
                }
            }

            go.transform.localPosition = Vector2.Lerp(
                go.transform.localPosition,
                cardPosition,
                lerpSpeed * Time.deltaTime
            );
        }
    }
    #endregion

    #region Side Card Swapping
    void ChangeSideCards()
    {
        if (!gm.GameStarted || !HasInputAuthority)
            return;

        // This would need to track selected cards - simplified for now
        // Full implementation would require tracking selected cards state
    }
    #endregion

    #region Turn Management
    public void EndTurn()
    {
        if (!HasInputAuthority || !gm.GameStarted)
            return;

        RPC_RequestEndTurn();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_RequestEndTurn(RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
            return;

        savedCardValue = 0;
        canEndTurn = false;
        gm.LocalEndTurn();
    }

    public bool GetTurn()
    {
        return gm.CurrentTurn == Object.InputAuthority;
    }
    #endregion

    #region Card Playing
    void DetectHover()
    {
        if (!HasInputAuthority || !gm.GameStarted)
            return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, Mathf.Infinity, cardLayerMask);
        hoveredCard = hit.collider ? hit.collider.GetComponentInParent<Card>()?.gameObject : null;

        bool myTurn = gm.CurrentTurn == Object.InputAuthority;
        
        if (hoveredCard != null && Input.GetMouseButtonDown(0) && myTurn)
        {
            var currentCards = GetCurrentCards();
            if (currentCards.Contains(hoveredCard))
            {
                var card = hoveredCard.GetComponent<Card>();
                if (card != null && pile.GetCurrentCard(false) != 10 && !ShouldDiscard(0))
                {
                    RPC_PlayCard((byte)card.GetValue());
                }
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_PlayCard(byte cardValue, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || !gm.GameStarted)
            return;

        bool myTurn = gm.CurrentTurn == info.Source;
        if (!myTurn || gm.WinnerDeclared)
            return;

        // Check if can play (must match saved value if set)
        if (savedCardValue != 0 && savedCardValue != cardValue)
            return;

        // Find the card GameObject in hand
        GameObject cardObj = FindCardInHand(cardValue);
        if (cardObj == null)
            return;

        bool isChanceCard = false; // TODO: Track chance cards if needed

        if (CanPlayCard(cardValue, isChanceCard, cardObj))
        {
            PlayCardSuccess(cardObj, cardValue, isChanceCard, info.Source);
        }
        else
        {
            PlayCardFailed(cardObj, isChanceCard, info.Source);
        }

        // Broadcast to all clients to remove card locally
        RPC_RemoveCardFromHand(info.Source, cardValue);

        // Check win condition
        gm.CheckProcessWin();
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_RemoveCardFromHand(PlayerRef player, byte cardValue, RpcInfo info = default)
    {
        // All clients remove the card locally
        if (Object.InputAuthority == player)
        {
            GameObject cardObj = FindCardInHand(cardValue);
            if (cardObj != null)
            {
                RemoveCardFromList(cardObj);
                Destroy(cardObj);
            }
        }
    }
    
    GameObject FindCardInHand(byte value)
    {
        var currentCards = GetCurrentCards();
        foreach (var cardObj in currentCards)
        {
            if (cardObj == null) continue;
            var card = cardObj.GetComponent<Card>();
            if (card != null && card.GetValue() == value)
            {
                return cardObj;
            }
        }
        return null;
    }

    void PlayCardSuccess(GameObject cardObj, byte cardValue, bool isChanceCard, PlayerRef player)
    {
        // Add to pile (host only)
        pile.RPC_AddCardToPile(cardValue, player);

        // Handle discard
        if (ShouldDiscard(cardValue))
        {
            pile.RPC_DiscardPile();
            savedCardValue = 0;
            canEndTurn = false;
        }

        // Handle special cards and turn logic
        if (HasSameValueCard(cardValue) || cardValue == 2 || cardValue == 10 || hasDiscarded)
        {
            if (HasSameValueCard(cardValue) && cardValue != 2 && cardValue != 10)
            {
                savedCardValue = cardValue;
                canEndTurn = true;
            }

            hasDiscarded = false;
            
            // Draw cards to maintain hand size
            if (handCards.Count > 0 && cardGenerator != null && cardGenerator.GetDeckCount() > 0 && !isChanceCard)
            {
                cardGenerator.RPC_DrawNewCard(player, cardsPerPlayer - handCards.Count);
            }
        }
        else
        {
            savedCardValue = 0;
            canEndTurn = false;
            
            // Only advance turn if not special card
            if (cardValue != 2 && cardValue != 10)
            {
                gm.LocalEndTurnWithCard(cardValue);
            }
            
            // Draw cards
            if (!isChanceCard && cardGenerator != null && cardGenerator.GetDeckCount() > 0)
            {
                cardGenerator.RPC_DrawNewCard(player, cardsPerPlayer - handCards.Count);
            }
        }

        SortHandCards();
    }

    void PlayCardFailed(GameObject cardObj, bool isChanceCard, PlayerRef player)
    {
        var card = cardObj.GetComponent<Card>();
        if (card == null) return;
        
        byte cardValue = (byte)card.GetValue();
        
        if (isChanceCard)
        {
            PickUpPile();
        }
        else
        {
            var currentCards = GetCurrentCards();
            bool hasCardToPlay = HasCardToPlay(currentCards);

            if (!hasCardToPlay)
            {
                pile.RPC_AddCardToPile(cardValue, player);
                PickUpPile();
            }
        }
    }

    void RemoveCardFromList(GameObject cardObj)
    {
        if (usingOverSideCards)
        {
            overSideCards.Remove(cardObj);
        }
        else if (usingUnderSideCards)
        {
            underSideCards.Remove(cardObj);
        }
        else
        {
            handCards.Remove(cardObj);
        }
    }

    void PickUpPile()
    {
        if (pile == null || !HasInputAuthority)
            return;

        RPC_RequestPickUpPile();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_RequestPickUpPile(RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || pile == null)
            return;

        pile.RPC_PickUpPile(Object.InputAuthority);
        savedCardValue = 0;
        canEndTurn = false;
        gm.LocalEndTurnWithCard(0);
    }

    public void PlayChanceCard()
    {
        if (!HasInputAuthority || !CanChance())
            return;

        RPC_RequestChanceCard();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_RequestChanceCard(RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || cardGenerator == null)
            return;

        cardGenerator.RPC_GetChanceCard(Object.InputAuthority, info);
    }
    #endregion

    #region Play Conditions
    bool ShouldDiscard(byte cardValue)
    {
        if (cardValue == 10 && GetCurrentCards().Count != 0)
        {
            hasDiscarded = true;
            return true;
        }

        int stepsBack = cardValue == 0 ? 1 : 2;
        int pileCount = pile.GetPileCount();

        if (pileCount >= 4)
        {
            var pileValues = pile.GetPileValues();
            if (pileValues.Count >= 4)
            {
                byte lastCardValue = pileValues[pileValues.Count - 1];
                bool allSame = true;

                // Check if last 4 cards are same value
                for (int i = pileValues.Count - stepsBack; i >= pileValues.Count - 4 && i >= 0; i--)
                {
                    if (pileValues[i] != lastCardValue)
                    {
                        allSame = false;
                        break;
                    }
                }

                if (allSame)
                {
                    if (cardValue != 0)
                    {
                        hasDiscarded = true;
                    }
                    return true;
                }
            }
        }

        hasDiscarded = false;
        return false;
    }

    public bool CanPlayCard(byte cardValue, bool isChance, GameObject cardInHand)
    {
        byte currentPileCard = pile.GetCurrentCard(isChance);
        
        if (cardValue >= currentPileCard || cardValue == 10 || cardValue == 2)
        {
            var currentCards = GetCurrentCards();
            if (cardInHand != null)
            {
                currentCards.Remove(cardInHand);
            }
            UpdateSideUsage();

            if ((cardValue == 10 || cardValue == 2 || cardValue == 14) && GetCurrentCards().Count == 0)
            {
                return false;
            }

            return true;
        }

        return false;
    }

    bool HasSameValueCard(byte cardValue)
    {
        foreach (var cardObj in handCards)
        {
            if (cardObj == null) continue;
            var card = cardObj.GetComponent<Card>();
            if (card != null && card.GetValue() == cardValue)
            {
                return true;
            }
        }
        return false;
    }

    bool HasCardToPlay(List<GameObject> currentList)
    {
        foreach (var cardObj in currentList)
        {
            if (cardObj == null) continue;
            var card = cardObj.GetComponent<Card>();
            if (card != null && CanPlayCard((byte)card.GetValue(), false, cardObj))
            {
                return true;
            }
        }
        return false;
    }

    public bool CanChance()
    {
        if (!gm.GameStarted || gm.WinnerDeclared)
            return false;

        bool myTurn = gm.CurrentTurn == Object.InputAuthority;
        if (!myTurn)
            return false;

        var currentCards = GetCurrentCards();
        bool hasCardToPlay = HasCardToPlay(currentCards);
        
        if (cardGenerator == null)
            return false;

        bool deckHasCards = cardGenerator.GetDeckCount() > 0;

        if (PlayerPrefs.HasKey("HasOpenedSettings"))
        {
            return PlayerPrefs.HasKey("CanChance") && !hasCardToPlay && deckHasCards;
        }
        else
        {
            return !hasCardToPlay && deckHasCards;
        }
    }
    #endregion

    #region Utility
    public int GetLowestValueExcluding(params byte[] excludes)
    {
        return handCards.Where(c => c != null)
                       .Select(c => c.GetComponent<Card>())
                       .Where(card => card != null && !excludes.Contains((byte)card.GetValue()))
                       .Select(card => card.GetValue())
                       .DefaultIfEmpty(int.MaxValue)
                       .Min();
    }
    #endregion
}
