// SimGameCloner.cs
// Deep-copies a SimGame for use in MCTS tree search.
// Also handles determinization — re-sampling the opponent's unknown hand
// from the pool of unseen cards so MCTS can reason under hidden information.

using System;
using System.Collections.Generic;

public static class SimGameCloner
{
    // Full deep-copy. Every list is new so mutations don't bleed back.
    public static SimGame Clone(SimGame src)
    {
        var dst = new SimGame();
        CopyInto(src, dst);
        return dst;
    }

    // Determinized clone: the opponent's hand cards + the deck are pooled together,
    // shuffled, and re-dealt so the opponent receives the same NUMBER of cards but
    // unknown VALUES. Averaging many MCTS searches over different determinizations
    // gives robust decisions despite hidden information.
    //
    //   observingPlayer — the AI that is making the decision (0 or 1).
    public static SimGame Determinize(SimGame src, int observingPlayer, Random rng)
    {
        var dst = Clone(src);

        int opp = 1 - observingPlayer;

        // Pool: opponent's hand (unknown values) + deck (unknown order)
        var hidden = new List<int>(dst.players[opp].hand);
        hidden.AddRange(dst.deck);
        Shuffle(hidden, rng);

        int oppHandSize = dst.players[opp].hand.Count;

        dst.players[opp].hand.Clear();
        dst.deck.Clear();

        for (int i = 0; i < oppHandSize && i < hidden.Count; i++)
            dst.players[opp].hand.Add(hidden[i]);

        for (int i = oppHandSize; i < hidden.Count; i++)
            dst.deck.Add(hidden[i]);

        return dst;
    }

    static void CopyInto(SimGame src, SimGame dst)
    {
        dst.currentTurn = src.currentTurn;
        dst.gameOver    = src.gameOver;
        dst.winner      = src.winner;
        dst.pile        = new List<int>(src.pile);
        dst.deck        = new List<int>(src.deck);
        dst.players     = new SimGame.SimPlayer[2];

        for (int i = 0; i < 2; i++)
        {
            dst.players[i] = new SimGame.SimPlayer
            {
                hand      = new List<int>(src.players[i].hand),
                overSide  = new List<int>(src.players[i].overSide),
                underSide = new List<int>(src.players[i].underSide),
            };
        }
    }

    static void Shuffle(List<int> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }
}
