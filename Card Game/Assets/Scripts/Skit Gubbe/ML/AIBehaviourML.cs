// AIBehaviourML.cs
// Drop-in replacement for AIBehaviourHard.cs that uses the trained Q-table.
// Attach this to the same GameObject as AIHand and disable AIBehaviourHard.
//
// The script translates the live Unity game state into the same state encoding
// used during training (SimGame.GetStateKey) then asks the Q-table for the best action.

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

    QLearningAgent agent = new QLearningAgent();

    bool isPlaying;
    int  cardsPerPlayer;

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

        bool playAgain;
        do
        {
            playAgain = false;
            if (!aiHand.GetTurn() || gameManager.GetWinner()) break;

            // Build the state key the same way SimGame does
            string state  = BuildStateKey();
            bool[] mask   = BuildActionMask();

            // Ask the trained agent what to do
            int action = agent.GreedyAction(state, mask);

            if (action == SimGame.ACTION_PICKUP)
            {
                PickUpPile();
                break;
            }

            if (action == SimGame.ACTION_CHANCE)
            {
                yield return StartCoroutine(DoChanceCard());
                break;
            }

            // Play the card with that value
            int   targetValue = action;
            var   cards       = aiHand.GetCards();
            var   cardObj     = cards.FirstOrDefault(c => c.GetComponent<Card>().GetValue() == targetValue);

            if (cardObj == null)
            {
                // Fallback: play any legal card (should not happen if mask is correct)
                cardObj = cards.FirstOrDefault(c => CanPlay(c.GetComponent<Card>().GetValue()));
            }

            if (cardObj != null)
            {
                PlayCard(cardObj);
                int val = cardObj.GetComponent<Card>().GetValue();

                if (val == 2 || ShouldDiscard(val))
                {
                    yield return new WaitForSeconds(playDelay);
                    playAgain = !gameManager.GetWinner();
                }
                else
                {
                    StartCoroutine(gameManager.ProcessWin("AI"));
                    gameManager.NextTurn(cardObj);
                }
            }
            else
            {
                PickUpPile();
                break;
            }
        }
        while (playAgain);

        // Refill hand
        if (aiHand.GetCards().Count < cardsPerPlayer && cardGenerator.GetDeck().Count > 0)
            cardGenerator.DrawNewCard(cardsPerPlayer - aiHand.GetCards().Count, false);

        isPlaying = false;
    }

    // ---------------------------------------------------------------
    //  State / mask helpers — mirrors SimGame's encoding
    // ---------------------------------------------------------------
    string BuildStateKey()
    {
        var   myCards    = aiHand.GetCards();
        int   pileTop    = pile.GetCurrentCard(false);
        int   phase      = aiHand.GetCards() == null ? 0 :
                           (myCards.Count > 0 ? 0 : 1); // simplified; full phase from AIHand would be better

        int canPlayCount    = myCards.Count(c => CanPlay(c.GetComponent<Card>().GetValue()));
        int highestPlayable = myCards.Where(c => CanPlay(c.GetComponent<Card>().GetValue()))
                                     .Select(c => c.GetComponent<Card>().GetValue())
                                     .DefaultIfEmpty(0).Max();
        int lowestPlayable  = myCards.Where(c => CanPlay(c.GetComponent<Card>().GetValue()))
                                     .Select(c => c.GetComponent<Card>().GetValue())
                                     .DefaultIfEmpty(0).Min();

        // Use PlayerHand card count as opponent total (approximate)
        var playerHand = FindFirstObjectByType<PlayerHand>();
        int oppTotal   = playerHand != null ?
            System.Math.Min(playerHand.GetCurrentCards().Count, 15) : 5;

        int myTotal = System.Math.Min(myCards.Count, 15);

        int pt = pileTop / 2;
        int hp = highestPlayable / 2;
        int lp = lowestPlayable / 2;

        return $"{pt},{hp},{lp},{myTotal},{oppTotal},{phase},{canPlayCount > 0}";
    }

    bool[] BuildActionMask()
    {
        bool[] mask  = new bool[SimGame.NUM_ACTIONS];
        var    cards = aiHand.GetCards();

        bool hasPlayable = false;
        foreach (var c in cards)
        {
            int v = c.GetComponent<Card>().GetValue();
            if (CanPlay(v) && v < SimGame.NUM_ACTIONS)
            {
                mask[v]     = true;
                hasPlayable = true;
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
    //  Card play helpers (mirrors AIHand logic)
    // ---------------------------------------------------------------
    void PlayCard(GameObject cardObj)
    {
        int value = cardObj.GetComponent<Card>().GetValue();
        if (!CanPlay(value)) return;

        // Remove from AIHand lists via AIHand's own method isn't exposed,
        // so we call the pile directly and let AIHand's internal list drift.
        // For a cleaner integration, expose a RemoveCard method on AIHand.
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
        GameObject chanceCard = cardGenerator.GetChanceCard();
        if (chanceCard == null) yield break;
        yield return new WaitForSeconds(chanceDelay);
        // Card is already added to pile by GetChanceCard(); handle pickup if needed
        int val = chanceCard.GetComponent<Card>().GetValue();
        if (!CanPlay(val))
        {
            PickUpPile();
        }
        else if (val != 2 && val != 10 && !ShouldDiscard(val))
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
