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
    
    [Header("UI")]
    [SerializeField] GameObject endTurnButton;
    [SerializeField] GameObject readyButton;
    [SerializeField] GameObject chanceNotice;
    [SerializeField] TextMeshProUGUI cardAmountText;
    [SerializeField] Vector2 cardAmountTextOffset;
    [SerializeField] Vector2 isTurnPos;
    [SerializeField] Vector2 isNotTurnPos;
    
    [Header("Chance")]
    [SerializeField] float playChanceDelay = 1f;
    
    [SerializeField] LayerMask cardLayerMask = -1;
    
    NetworkRunner runner;
    GameManagerNetwork gm;
    NetworkCardGenerator cardGenerator;
    NetworkPile pile;
    
    List<NetworkedCard> handCards = new List<NetworkedCard>();
    List<NetworkedCard> underSideCards = new List<NetworkedCard>();
    List<NetworkedCard> overSideCards = new List<NetworkedCard>();
    
    [Networked] byte savedCardValue { get; set; }
    [Networked] bool hasDiscarded { get; set; }
    [Networked] bool canEndTurn { get; set; }
    [Networked] public int NetworkedHandCount { get; set; }
    
    bool usingOverSideCards;
    bool usingUnderSideCards;
    NetworkedCard hoveredCard;
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
        
        if (cardGenerator != null)
        {
            cardsPerPlayer = cardGenerator.GetCardsPerPlayer();
        }
        
        if (HasInputAuthority)
        {
            if (endTurnButton != null)
            {
                endTurnButton.SetActive(false);
            }
            if (readyButton != null)
            {
                readyButton.SetActive(true);
                // Hook up ready button
                var button = readyButton.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(OnReadyButtonClicked);
                }
            }
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

    #region Card Management
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_AddCardToHand(NetworkObject card, PlayerRef who, RpcInfo info = default)
    {
        if (who == Object.InputAuthority)
        {
            LocalAdd(card);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SetSideCards(NetworkObject[] underCards, NetworkObject[] overCards, PlayerRef who, RpcInfo info = default)
    {
        if (who == Object.InputAuthority)
        {
            underSideCards.Clear();
            overSideCards.Clear();
            
            foreach (var cardObj in underCards)
            {
                if (cardObj != null)
                {
                    var nc = cardObj.GetComponent<NetworkedCard>();
                    if (nc != null)
                    {
                        underSideCards.Add(nc);
                    }
                }
            }
            
            foreach (var cardObj in overCards)
            {
                if (cardObj != null)
                {
                    var nc = cardObj.GetComponent<NetworkedCard>();
                    if (nc != null)
                    {
                        overSideCards.Add(nc);
                    }
                }
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_AddChanceCard(NetworkObject card, PlayerRef who, RpcInfo info = default)
    {
        if (who == Object.InputAuthority)
        {
            LocalAdd(card);
        }
    }

    public void LocalAdd(NetworkObject netObj)
    {
        if (netObj == null)
            return;

        var nc = netObj.GetComponent<NetworkedCard>();
        if (nc != null && !handCards.Contains(nc))
        {
            handCards.Add(nc);
            SortHandCards();
            SortCards();
        }
    }

    void SortHandCards()
    {
        handCards = handCards.OrderBy(c => c.Value).ToList();
    }

    void UpdateSideUsage()
    {
        usingOverSideCards = handCards.Count == 0 && overSideCards.Count > 0;
        usingUnderSideCards = handCards.Count == 0 && overSideCards.Count == 0 && underSideCards.Count > 0;
    }

    List<NetworkedCard> GetCurrentCards()
    {
        if (usingOverSideCards) return overSideCards;
        if (usingUnderSideCards) return underSideCards;
        return handCards;
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

    void ArrangeCards(List<NetworkedCard> cards, Transform parent, float spacing, float maxWidth, float offset = 0)
    {
        if (cards.Count == 0 || parent == null)
            return;

        float cardSpacing = Mathf.Min(spacing, maxWidth / cards.Count);

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null || cards[i].gameObject == null)
                continue;

            var go = cards[i].gameObject;
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
        hoveredCard = hit.collider ? hit.collider.GetComponentInParent<NetworkedCard>() : null;

        bool myTurn = gm.CurrentTurn == Object.InputAuthority;
        
        if (hoveredCard != null && Input.GetMouseButtonDown(0) && myTurn)
        {
            var currentCards = GetCurrentCards();
            if (currentCards.Contains(hoveredCard))
            {
                if (pile.GetCurrentCard(false) != 10 && !ShouldDiscard(0))
                {
                    RPC_PlayCard(hoveredCard.Object);
                }
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_PlayCard(NetworkObject cardObj, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || !gm.GameStarted)
            return;

        var nc = cardObj.GetComponent<NetworkedCard>();
        if (nc == null)
            return;

        bool myTurn = gm.CurrentTurn == info.Source;
        if (!myTurn || gm.WinnerDeclared)
            return;

        byte cardValue = nc.Value;
        bool isChanceCard = nc.IsChanceCard;

        // Check if can play (must match saved value if set)
        if (savedCardValue != 0 && savedCardValue != cardValue)
            return;

        if (CanPlayCard(cardValue, isChanceCard, nc))
        {
            PlayCardSuccess(nc, cardValue, isChanceCard);
        }
        else
        {
            PlayCardFailed(nc, isChanceCard);
        }

        // Check win condition
        gm.CheckProcessWin();
    }

    void PlayCardSuccess(NetworkedCard nc, byte cardValue, bool isChanceCard)
    {
        // Remove from hand
        RemoveCardFromList(nc);
        
        // Add to pile
        pile.RPC_AddCardToPile(cardValue, Object.InputAuthority);

        // Despawn original card
        if (runner != null)
        {
            runner.Despawn(nc.Object);
        }

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
                cardGenerator.RPC_DrawNewCard(Object.InputAuthority, cardsPerPlayer - handCards.Count);
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
                cardGenerator.RPC_DrawNewCard(Object.InputAuthority, cardsPerPlayer - handCards.Count);
            }
        }

        SortHandCards();
    }

    void PlayCardFailed(NetworkedCard nc, bool isChanceCard)
    {
        if (isChanceCard)
        {
            RemoveCardFromList(nc);
            PickUpPile();
        }
        else
        {
            var currentCards = GetCurrentCards();
            bool hasCardToPlay = HasCardToPlay(currentCards);

            if (!hasCardToPlay)
            {
                RemoveCardFromList(nc);
                pile.RPC_AddCardToPile(nc.Value, Object.InputAuthority);
                if (runner != null)
                {
                    runner.Despawn(nc.Object);
                }
                PickUpPile();
            }
        }
    }

    void RemoveCardFromList(NetworkedCard nc)
    {
        if (usingOverSideCards)
        {
            overSideCards.Remove(nc);
        }
        else if (usingUnderSideCards)
        {
            underSideCards.Remove(nc);
        }
        else
        {
            handCards.Remove(nc);
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

    public bool CanPlayCard(byte cardValue, bool isChance, NetworkedCard cardInHand)
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
        foreach (var card in handCards)
        {
            if (card.Value == cardValue)
            {
                return true;
            }
        }
        return false;
    }

    bool HasCardToPlay(List<NetworkedCard> currentList)
    {
        foreach (var card in currentList)
        {
            if (CanPlayCard(card.Value, false, null))
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
        return handCards.Where(c => !excludes.Contains(c.Value))
                       .Select(c => (int)c.Value)
                       .DefaultIfEmpty(int.MaxValue)
                       .Min();
    }
    #endregion
}
