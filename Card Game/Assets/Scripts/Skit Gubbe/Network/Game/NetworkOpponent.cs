using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.Netcode;

public class NetworkOpponent : NetworkBehaviour
{
    #region Variables
    [Header("Cards")]
    [SerializeField] List<GameObject> handCards;
    [SerializeField] List<GameObject> underSideCards, overSideCards;
    [Header("Transform and Spacing")]
    [SerializeField] Transform handTransform;
    [SerializeField] float baseCardSpacing, maxHandWidth;
    [Space]
    [SerializeField] Transform underSideTransform, overSideTransform;
    [SerializeField] float sideBaseCardSpacing, sideMaxHandWidth, overSideOffset;
    [Space]
    [SerializeField] Vector2 isTurnPos, isNotTurnPos;
    [Header("Turn and Play")]
    [SerializeField] bool isTurn;
    [SerializeField] int turnNumber;
    [SerializeField] float playDelay;
    [SerializeField] float chanceDelay;
    [SerializeField, Range(0, 100)] int chanceToPlayChance = 50;
    [Space]
    [SerializeField] float lerpSpeed;
    [SerializeField] TextMeshProUGUI cardAmountText;
    [SerializeField] Vector2 cardAmountTextOffset;

    bool usingOverSideCards, usingUnderSideCards;
    bool isPlaying;

    Pile pile;
    CardGenerator cardGenerator;
    GameManager gameManager;
    AudioManager audioManager;
    #endregion

    public void AddHandCards(GameObject newCard)
    {
        handCards.Add(newCard);
        UpdateCardSortingOrder(handCards);
    }

    public void SetUnderSideCards(List<GameObject> newCards) => underSideCards = newCards;
    public void SetOverSideCards(List<GameObject> newCards) => overSideCards = newCards;

    void UpdateCardSortingOrder(List<GameObject> cards)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].GetComponent<SpriteRenderer>().sortingOrder = i;
        }
    }
}
