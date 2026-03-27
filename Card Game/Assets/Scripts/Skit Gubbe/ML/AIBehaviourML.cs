// AIBehaviourML.cs
// Drop-in replacement for AIBehaviourHard.cs that uses the trained Q-table.
// Attach this to the same GameObject as AIHand and disable AIBehaviourHard.
//
// State encoding exactly matches SimGame.GetStateKey() so the trained Q-table
// produces consistent decisions in the live game.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class AIBehaviourML : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] float playDelay   = 0.8f;
    [SerializeField] float chanceDelay = 0.5f;

    [Header("Q-Table")]
    [SerializeField] string saveFileName = "qtable.json";

    // ---------------------------------------------------------------
    //  References — set via Inspector or found automatically
    // ---------------------------------------------------------------
    Pile          pile;
    CardGenerator cardGenerator;
    GameManager   gameManager;
    AudioManager  audioManager;
    AIHand        aiHand;
    PlayerHand    playerHand;

    QLearningAgent agent = new QLearningAgent();

    bool isPlaying;
    int  cardsPerPlayer;
    bool chanceGaveExtraTurn;

    // ---------------------------------------------------------------
    //  Unity lifecycle
    // ---------------------------------------------------------------
    void Awake()
    {
        pile          = FindFirstObjectByType<Pile>();
        cardGenerator = FindFirstObjectByType<CardGenerator>();
        gameManager   = FindFirstObjectByType<GameManager>();
        audioManager  = FindFirstObjectByType<AudioManager>();
        aiHand        = GetComponent<AIHand>();
        playerHand    = FindFirstObjectByType<PlayerHand>();
    }

    void Start()
    {
        cardsPerPlayer = cardGenerator.GetCardsPerPlayer();

        string path = Path.Combine(Application.persistentDataPath, saveFileName);
        agent.Load(path);
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;
        if (!gameManager.GetGameHasStarted()) return;

        if (aiHand.GetTurn() && !isPlaying)
            StartCoroutine(PlayTurn());
    }

    // ---------------------------------------------------------------
    //  Main turn coroutine
    // ---------------------------------------------------------------
    IEnumerator PlayTurn()
    {
        isPlaying = true;
        yield return new WaitForSeconds(playDelay);

        while (aiHand.GetTurn() && !gameManager.GetWinner())
        {
            // --- Underside phase: blind flip, no agent choice ---
            if (aiHand.GetHandCount() == 0 && aiHand.GetOverSideCount() == 0 && aiHand.GetUnderSideCount() > 0)
            {
                var underCards = aiHand.GetCards();
                if (underCards.Count > 0)
                {
                    int  idx          = Random.Range(0, underCards.Count);
                    var  cardObj      = underCards[idx];
                    int  val          = cardObj.GetComponent<Card>().GetValue();
                    bool canPlay      = CanPlay(val);

                    // Card goes to pile regardless of whether it's playable
                    aiHand.RemoveCard(cardObj);
                    pile.AddCardsToPile(cardObj);
                    audioManager.PlayCardSFX();

                    if (canPlay)
                    {
                        bool shouldDiscard = ShouldDiscard(val);
                        if (shouldDiscard) StartCoroutine(pile.DiscardCardsInPile());
                        StartCoroutine(gameManager.ProcessWin("AI"));
                        if (!gameManager.GetWinner())
                        {
                            bool extraTurn = (val == 2 || shouldDiscard);
                            if (extraTurn)
                            {
                                yield return new WaitForSeconds(playDelay);
                                continue;
                            }
                            gameManager.NextTurn(cardObj);
                        }
                    }
                    else
                    {
                        // Can't beat pile top — pick up the whole pile including this card
                        PickUpPile();
                    }
                }
                break;
            }

            // --- Normal turn: consult the Q-table ---
            string state  = BuildStateKey();
            bool[] mask   = BuildActionMask();
            int    action = agent.GreedyAction(state, mask);

            if (action == SimGame.ACTION_PICKUP)
            {
                PickUpPile();
                break;
            }

            if (action == SimGame.ACTION_CHANCE)
            {
                yield return StartCoroutine(DoChanceCard());
                if (chanceGaveExtraTurn && !gameManager.GetWinner())
                {
                    yield return new WaitForSeconds(playDelay);
                    continue;
                }
                break;
            }

            // --- Play a card of the chosen value ---
            int targetValue = action;
            var cards       = aiHand.GetCards();
            var cardObj2    = cards.FirstOrDefault(c => c.GetComponent<Card>().GetValue() == targetValue);

            if (cardObj2 == null)
            {
                // Fallback: play any legal card (should not happen if mask is correct)
                cardObj2 = cards.FirstOrDefault(c => CanPlay(c.GetComponent<Card>().GetValue()));
            }

            if (cardObj2 == null)
            {
                PickUpPile();
                break;
            }

            int playedVal = cardObj2.GetComponent<Card>().GetValue();
            PlayCard(cardObj2);
            StartCoroutine(gameManager.ProcessWin("AI"));

            if (gameManager.GetWinner()) break;

            bool giveExtraTurn = (playedVal == 2 || ShouldDiscard(playedVal));
            if (giveExtraTurn)
            {
                yield return new WaitForSeconds(playDelay);
                continue;
            }

            gameManager.NextTurn(cardObj2);
            break;
        }

        // Safety refill in case mid-turn draws were missed
        if (aiHand.GetHandCount() < cardsPerPlayer && cardGenerator.GetDeck().Count > 0)
            cardGenerator.DrawNewCard(cardsPerPlayer - aiHand.GetHandCount(), false);

        isPlaying = false;
    }

    // ---------------------------------------------------------------
    //  State encoding — exactly mirrors SimGame.GetStateKey()
    // ---------------------------------------------------------------
    string BuildStateKey()
    {
        var  active   = aiHand.GetCards();
        int  pileTop  = pile.GetCurrentCard(false);
        int  handCnt  = aiHand.GetHandCount();
        int  overCnt  = aiHand.GetOverSideCount();
        int  underCnt = aiHand.GetUnderSideCount();
        int  phase    = handCnt > 0 ? 0 : (overCnt > 0 ? 1 : 2);

        var  cardValues = active.Select(c => c.GetComponent<Card>().GetValue()).ToList();
        bool has2    = cardValues.Contains(2);
        bool has10   = cardValues.Contains(10);
        bool hasAce  = cardValues.Contains(14);

        // Lowest playable regular card (not 2 or 10) — mirrors SimGame exactly
        var regularPlayable = cardValues.Where(v => v != 2 && v != 10 && v >= pileTop).ToList();
        int lowestRegular   = regularPlayable.Count > 0 ? regularPlayable.Min() : 0;
        int regularCount    = System.Math.Min(regularPlayable.Count, 5);

        int myTotal  = System.Math.Min(handCnt + overCnt + underCnt, 12);

        int oppHandCnt = 0, oppOverCnt = 0, oppUnderCnt = 0;
        if (playerHand != null)
        {
            oppHandCnt  = playerHand.GetHandCards().Count;
            oppOverCnt  = playerHand.GetOverSideCards().Count;
            oppUnderCnt = playerHand.GetUnderSideCards().Count;
        }
        int oppTotal = System.Math.Min(oppHandCnt + oppOverCnt + oppUnderCnt, 12);

        // Bucket pile top: 0=empty, 1=low(2-6), 2=mid(7-9), 3=high(11-13), 4=ace
        int pileBucket = pileTop == 0  ? 0
                       : pileTop <= 6  ? 1
                       : pileTop <= 9  ? 2
                       : pileTop < 14  ? 3 : 4;

        // Bucket pile size: 0=empty, 1=small(1-3), 2=medium(4-7), 3=large(8+)
        int pileCount      = pile.GetCardsInPile().Count;
        int pileSizeBucket = pileCount == 0 ? 0
                           : pileCount <= 3 ? 1
                           : pileCount <= 7 ? 2 : 3;

        // Bucket lowest regular: 0=none, 1=low(3-6), 2=mid(7-9), 3=high(11-13), 4=ace
        int lrBucket = lowestRegular == 0  ? 0
                     : lowestRegular <= 6  ? 1
                     : lowestRegular <= 9  ? 2
                     : lowestRegular < 14  ? 3 : 4;

        // Opponent's current phase — tells us how close they are to winning
        int oppPhase = oppHandCnt > 0 ? 0 : (oppOverCnt > 0 ? 1 : 2);

        // Whether the deck is empty — changes the value of the chance card action
        bool deckEmpty = cardGenerator.GetDeck().Count == 0;

        return $"{pileBucket},{pileSizeBucket},{lrBucket},{has10},{has2},{hasAce},{regularCount},{myTotal},{oppTotal},{oppPhase},{deckEmpty},{phase}";
    }

    bool[] BuildActionMask()
    {
        bool[] mask = new bool[SimGame.NUM_ACTIONS];

        // Underside phase: forced flip — no real choice (mirrors SimGame)
        if (aiHand.GetHandCount() == 0 && aiHand.GetOverSideCount() == 0 && aiHand.GetUnderSideCount() > 0)
        {
            mask[SimGame.ACTION_PICKUP] = true;
            return mask;
        }

        var  cards      = aiHand.GetCards();
        int  pileTop    = pile.GetCurrentCard(false);
        bool hasPlayable = false;

        foreach (var c in cards)
        {
            int v = c.GetComponent<Card>().GetValue();
            if ((v == 2 || v == 10 || v >= pileTop) && v < SimGame.NUM_ACTIONS)
            {
                mask[v]      = true;
                hasPlayable  = true;
            }
        }

        if (!hasPlayable && pile.GetCardsInPile().Count > 0)
        {
            mask[SimGame.ACTION_PICKUP] = true;
            if (cardGenerator.GetDeck().Count > 0)
                mask[SimGame.ACTION_CHANCE] = true;
        }

        return mask;
    }

    // ---------------------------------------------------------------
    //  Card play helpers
    // ---------------------------------------------------------------
    void PlayCard(GameObject cardObj)
    {
        int value = cardObj.GetComponent<Card>().GetValue();
        if (!CanPlay(value)) return;

        aiHand.RemoveCard(cardObj);        // keep AIHand's internal list in sync
        pile.AddCardsToPile(cardObj);
        audioManager.PlayCardSFX();

        if (ShouldDiscard(value))
            StartCoroutine(pile.DiscardCardsInPile());
    }

    void PickUpPile()
    {
        if (gameManager.GetWinner()) return;
        var pileCards = pile.GetCardsInPile();
        pile.ClearPile();
        aiHand.AddCardsToHand(pileCards);  // keep AIHand's internal list in sync
        foreach (var c in pileCards)
        {
            c.GetComponent<Card>().RemoveChild();
            cardGenerator.ApplyCoverOnCards(c);
        }
        gameManager.NextTurn(null);
        audioManager.PlayShufflingSFX();
    }

    IEnumerator DoChanceCard()
    {
        chanceGaveExtraTurn = false;
        GameObject chanceCard = cardGenerator.GetChanceCard();
        if (chanceCard == null) yield break;

        yield return new WaitForSeconds(chanceDelay);

        int  val     = chanceCard.GetComponent<Card>().GetValue();
        bool canPlay = CanPlay(val);

        if (!canPlay)
        {
            // Chance card can't beat pile — pick up everything
            PickUpPile();
            yield break;
        }

        audioManager.PlayCardSFX();
        bool discard = ShouldDiscard(val);
        if (discard) StartCoroutine(pile.DiscardCardsInPile());

        StartCoroutine(gameManager.ProcessWin("AI"));

        // val==2 or pile cleared both give an extra turn (mirrors SimGame)
        if (val == 2 || discard)
        {
            chanceGaveExtraTurn = true;
        }
        else
        {
            gameManager.NextTurn(chanceCard);
        }
    }

    bool CanPlay(float value) =>
        value == 2 || value == 10 || value >= pile.GetCurrentCard(false);

    bool ShouldDiscard(int value)
    {
        if (value == 10) return true;
        var pileCards = pile.GetCardsInPile();
        if (pileCards.Count < 4) return false;
        int top = pileCards[pileCards.Count - 1].GetComponent<Card>().GetValue();
        for (int i = pileCards.Count - 4; i < pileCards.Count; i++)
            if (pileCards[i].GetComponent<Card>().GetValue() != top) return false;
        return true;
    }
}
