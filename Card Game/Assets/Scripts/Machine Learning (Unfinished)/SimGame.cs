// SimGame.cs
// Headless, pure C# simulation of Skit Gubbe.
// No MonoBehaviour, no GameObjects, no visuals — runs thousands of games per second.
// Mirrors the rules in AIHand.cs / PlayerHand.cs exactly.

using System;
using System.Collections.Generic;
using System.Linq;

public class SimGame
{
    // ---------------------------------------------------------------
    //  Player state
    // ---------------------------------------------------------------
    public class SimPlayer
    {
        public List<int> hand      = new List<int>();
        public List<int> overSide  = new List<int>(); // face-up side cards
        public List<int> underSide = new List<int>(); // face-down side cards

        public bool IsUsingOverSide  => hand.Count == 0 && overSide.Count > 0;
        public bool IsUsingUnderSide => hand.Count == 0 && overSide.Count == 0 && underSide.Count > 0;
        public bool HasNoCards       => hand.Count == 0 && overSide.Count == 0 && underSide.Count == 0;

        // Returns whichever zone is currently active
        public List<int> ActiveCards()
        {
            if (hand.Count > 0)      return hand;
            if (overSide.Count > 0)  return overSide;
            return underSide;
        }
    }

    // ---------------------------------------------------------------
    //  Action encoding — 6 strategic decisions only.
    //  Regular card play is always "lowest playable" (hard-coded),
    //  so the Q-table only learns the meaningful choices.
    // ---------------------------------------------------------------
    public const int ACTION_REGULAR = 0;  // play lowest playable regular card (3-9, 11-13)
    public const int ACTION_2       = 1;  // play a 2
    public const int ACTION_10      = 2;  // play a 10
    public const int ACTION_ACE     = 3;  // play an ace (14)
    public const int ACTION_PICKUP  = 4;  // pick up pile
    public const int ACTION_CHANCE  = 5;  // play chance card from deck
    public const int NUM_ACTIONS    = 6;

    // ---------------------------------------------------------------
    //  Game state
    // ---------------------------------------------------------------
    public SimPlayer[] players     = new SimPlayer[2];
    public List<int>   pile        = new List<int>();
    public List<int>   deck        = new List<int>();
    public int         currentTurn;   // 0 or 1
    public bool        gameOver;
    public int         winner = -1;

    private const int CARDS_PER_PLAYER = 3;
    private Random rng;

    public SimGame(int seed = -1)
    {
        rng = seed < 0 ? new Random() : new Random(seed);
    }

    // ---------------------------------------------------------------
    //  Reset / deal a new game
    // ---------------------------------------------------------------
    public void Reset()
    {
        gameOver = false;
        winner   = -1;
        pile.Clear();

        // Build standard deck: values 2-14 (Ace=14), 4 suits each
        deck.Clear();
        for (int v = 2; v <= 14; v++)
            for (int s = 0; s < 4; s++)
                deck.Add(v);
        Shuffle(deck);

        players[0] = new SimPlayer();
        players[1] = new SimPlayer();

        // Deal: 3 hand + 3 face-down + 3 face-up per player
        foreach (var p in players)
        {
            for (int i = 0; i < CARDS_PER_PLAYER; i++) p.hand.Add(PopDeck());
            for (int i = 0; i < 3; i++)               p.underSide.Add(PopDeck());
            for (int i = 0; i < 3; i++)               p.overSide.Add(PopDeck());
        }

        // Optimise overSide: put best cards face-up (mirrors AIHand.SwitchOutSideCards)
        foreach (var p in players)
            OptimiseSideCards(p);

        // Starting player: whoever has the lowest non-special card
        int low0 = LowestNonSpecial(players[0].hand);
        int low1 = LowestNonSpecial(players[1].hand);
        currentTurn = (low0 <= low1) ? 0 : 1;
    }

    // Swap lowest hand cards with highest overSide cards so best cards are visible
    void OptimiseSideCards(SimPlayer p)
    {
        var all = p.hand.Concat(p.overSide).OrderBy(v => v).ToList();
        p.hand    = all.Take(CARDS_PER_PLAYER).ToList();
        p.overSide = all.Skip(CARDS_PER_PLAYER).ToList();
    }

    // ---------------------------------------------------------------
    //  Core rules
    // ---------------------------------------------------------------
    public int PileTop => pile.Count > 0 ? pile[pile.Count - 1] : 0;

    // A card can always be played if it's 2 or 10; otherwise must be >= pile top
    public bool CanPlay(int value) =>
        value == 2 || value == 10 || value >= PileTop;

    // Pile is discarded when a 10 is played or when the top 4 cards are the same value
    bool ShouldDiscard(int value)
    {
        if (value == 10) return true;
        if (pile.Count < 4) return false;

        int top = pile[pile.Count - 1];
        for (int i = pile.Count - 4; i < pile.Count; i++)
            if (pile[i] != top) return false;
        return true;
    }

    // ---------------------------------------------------------------
    //  Legal action mask for the current player
    // ---------------------------------------------------------------
    public bool[] GetLegalActionMask()
    {
        bool[] mask = new bool[NUM_ACTIONS];
        SimPlayer me = players[currentTurn];

        // Underside cards: forced random flip — no real choice
        if (me.IsUsingUnderSide)
        {
            mask[ACTION_PICKUP] = true; // signals "flip" — Step() handles the actual logic
            return mask;
        }

        bool hasPlayable = false;
        foreach (int v in me.ActiveCards())
        {
            if (!CanPlay(v)) continue;
            hasPlayable = true;
            if      (v == 2)  mask[ACTION_2]       = true;
            else if (v == 10) mask[ACTION_10]      = true;
            else if (v == 14) mask[ACTION_ACE]     = true;
            else              mask[ACTION_REGULAR] = true;
        }

        // If nothing playable: either pick up pile or play a chance card
        if (!hasPlayable && pile.Count > 0)
        {
            mask[ACTION_PICKUP] = true;
            if (deck.Count > 0) mask[ACTION_CHANCE] = true;
        }

        return mask;
    }

    // ---------------------------------------------------------------
    //  Apply an action. Returns reward for the acting player:
    //  +1 = win, -1 = lose, 0 = game continues
    // ---------------------------------------------------------------
    public float Step(int action)
    {
        SimPlayer me = players[currentTurn];

        // --- Underside: blind flip, no agent choice ---
        if (me.IsUsingUnderSide)
        {
            int idx = rng.Next(me.underSide.Count);
            int val = me.underSide[idx];
            me.underSide.RemoveAt(idx);
            pile.Add(val);

            if (CanPlay(val))
            {
                if (ShouldDiscard(val)) pile.Clear();
                else if (val != 2) currentTurn = 1 - currentTurn;
                // val == 2 gives extra turn (no currentTurn flip)
            }
            else
            {
                // Card couldn't play: pick up the pile
                me.hand.AddRange(pile);
                pile.Clear();
                currentTurn = 1 - currentTurn;
            }

            RefillHand(me);
            return CheckWin();
        }

        // --- Pick up pile ---
        if (action == ACTION_PICKUP)
        {
            int pickedUp = pile.Count;
            me.hand.AddRange(pile);
            pile.Clear();
            currentTurn = 1 - currentTurn;
            // Scale penalty with pile size: picking up 10 cards is far worse than 2
            return -0.02f * Math.Max(1, pickedUp);
        }

        // --- Chance card (draw from deck and play blind) ---
        if (action == ACTION_CHANCE)
        {
            if (deck.Count == 0) { currentTurn = 1 - currentTurn; return 0f; }

            int chanceVal    = PopDeck();
            bool canPlayChance = CanPlay(chanceVal); // check BEFORE adding to pile
            pile.Add(chanceVal);                     // add so ShouldDiscard sees it

            if (canPlayChance)
            {
                if (ShouldDiscard(chanceVal))   pile.Clear();
                else if (chanceVal != 2 && chanceVal != 10) currentTurn = 1 - currentTurn;
            }
            else
            {
                // Failed chance card — pick up pile including the chance card
                int pickedUp = pile.Count;
                me.hand.AddRange(pile);
                pile.Clear();
                currentTurn = 1 - currentTurn;
                RefillHand(me);
                return -0.02f * pickedUp;
            }

            RefillHand(me);
            return CheckWin();
        }

        // --- Play a card ---
        int cardValue;
        if (action == ACTION_REGULAR)
        {
            // Always play the lowest playable regular card (not 2, 10, or Ace)
            var regulars = me.ActiveCards()
                .Where(v => v != 2 && v != 10 && v != 14 && CanPlay(v))
                .OrderBy(v => v).ToList();
            if (regulars.Count == 0) { currentTurn = 1 - currentTurn; return 0f; }
            cardValue = regulars[0];
        }
        else if (action == ACTION_2)   cardValue = 2;
        else if (action == ACTION_10)  cardValue = 10;
        else if (action == ACTION_ACE) cardValue = 14;
        else { currentTurn = 1 - currentTurn; return 0f; }

        RemoveOne(me.ActiveCards(), cardValue);
        pile.Add(cardValue);

        bool discard = ShouldDiscard(cardValue);
        if (discard) pile.Clear();

        RefillHand(me);
        float reward = CheckWin();
        if (gameOver) return reward;

        // Small bonus for clearing the pile — good tactical move
        if (discard) reward += 0.1f;

        // Small progress reward for successfully playing a card — densifies the reward signal
        // so Q-values converge faster in long games (most turns otherwise return 0)
        reward += 0.01f;

        // 2 and discarding the pile both give an extra turn
        bool extraTurn = (cardValue == 2 || discard);
        if (!extraTurn) currentTurn = 1 - currentTurn;

        return reward;
    }

    // ---------------------------------------------------------------
    //  State encoding (from acting player's perspective)
    //  Compact enough for a dictionary Q-table.
    // ---------------------------------------------------------------
    public string GetStateKey()
    {
        SimPlayer me  = players[currentTurn];
        SimPlayer opp = players[1 - currentTurn];

        int pileTop = PileTop;
        int phase   = me.hand.Count > 0 ? 0 : (me.overSide.Count > 0 ? 1 : 2);

        var active = me.ActiveCards();

        // The specific cards that matter most for decision-making
        bool has2   = active.Contains(2);
        bool has10  = active.Contains(10);
        bool hasAce = active.Contains(14);

        // Lowest playable regular card (not 2, 10, or Ace — those have their own actions)
        var regularPlayable = active.Where(v => v != 2 && v != 10 && v != 14 && CanPlay(v)).ToList();
        int lowestRegular   = regularPlayable.Count > 0 ? regularPlayable.Min() : 0;
        int regularCount    = Math.Min(regularPlayable.Count, 5);

        int myTotal  = Math.Min(me.hand.Count  + me.overSide.Count  + me.underSide.Count,  12);
        int oppTotal = Math.Min(opp.hand.Count + opp.overSide.Count + opp.underSide.Count, 12);

        // Bucket pile top: 0=empty, 1=low(2-6), 2=mid(7-9), 3=high(11-13), 4=ace
        int pileBucket = pileTop == 0  ? 0
                       : pileTop <= 6  ? 1
                       : pileTop <= 9  ? 2
                       : pileTop < 14  ? 3 : 4;

        // Bucket pile size: 0=empty, 1=small(1-3), 2=medium(4-7), 3=large(8+)
        // Critical: picking up 2 cards vs 11 cards is completely different risk
        int pileSizeBucket = pile.Count == 0 ? 0
                           : pile.Count <= 3 ? 1
                           : pile.Count <= 7 ? 2 : 3;

        // Bucket lowest regular: 0=none, 1=low(3-6), 2=mid(7-9), 3=high(11-13)
        // Ace is excluded from regular (has its own action), so no bucket 4 needed
        int lrBucket = lowestRegular == 0  ? 0
                     : lowestRegular <= 6  ? 1
                     : lowestRegular <= 9  ? 2 : 3;

        // Opponent's current phase — tells us how close they are to winning
        int oppPhase = opp.hand.Count > 0 ? 0 : (opp.overSide.Count > 0 ? 1 : 2);

        // Whether the deck is empty — changes the value of the chance card action
        bool deckEmpty = deck.Count == 0;

        return $"{pileBucket},{pileSizeBucket},{lrBucket},{has10},{has2},{hasAce},{regularCount},{myTotal},{oppTotal},{oppPhase},{deckEmpty},{phase}";
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------
    void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j   = rng.Next(i + 1);
            int tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    int PopDeck()
    {
        int i = rng.Next(deck.Count);
        int v = deck[i];
        deck.RemoveAt(i);
        return v;
    }

    void RemoveOne(List<int> list, int value)
    {
        int idx = list.IndexOf(value);
        if (idx >= 0) list.RemoveAt(idx);
    }

    void RefillHand(SimPlayer p)
    {
        while (p.hand.Count < CARDS_PER_PLAYER && deck.Count > 0)
            p.hand.Add(PopDeck());
    }

    int LowestNonSpecial(List<int> cards)
    {
        int low = 20;
        foreach (int v in cards)
            if (v != 2 && v != 10 && v < low) low = v;
        return low;
    }

    float CheckWin()
    {
        if (players[currentTurn].HasNoCards)
        {
            winner   = currentTurn;
            gameOver = true;
            return 1f;   // acting player wins
        }
        if (players[1 - currentTurn].HasNoCards)
        {
            winner   = 1 - currentTurn;
            gameOver = true;
            return -1f;  // acting player loses
        }
        return 0f;
    }
}
