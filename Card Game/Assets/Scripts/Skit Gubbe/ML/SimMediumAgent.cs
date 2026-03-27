// SimMediumAgent.cs
// A medium-difficulty rule-based opponent for training.
// Harder than SimSimpleAgent because it:
//   - Tactically clears high or mid piles with a 10 (even when regular cards are available)
//   - Prefers a chance card over picking up when the pile is large (riskier, but avoids hand bloat)
//   - Otherwise mirrors the simple agent's lowest-regular-card strategy

using System.Collections.Generic;

public class SimMediumAgent
{
    public int ChooseAction(SimGame game)
    {
        SimGame.SimPlayer me = game.players[game.currentTurn];

        // Underside: forced random flip — action doesn't matter, SimGame handles it
        if (me.IsUsingUnderSide)
            return SimGame.ACTION_PICKUP;

        bool[] mask    = game.GetLegalActionMask();
        int    pileTop = game.PileTop;

        // Tactically play 10 to clear a mid-to-high pile (>= 9), even if regular cards exist
        if (pileTop >= 9 && mask[SimGame.ACTION_10])
            return SimGame.ACTION_10;

        // Play the lowest regular card
        if (mask[SimGame.ACTION_REGULAR]) return SimGame.ACTION_REGULAR;

        // Use 2 before 10 — extra turn is more valuable than a clear on a low pile
        if (mask[SimGame.ACTION_2])   return SimGame.ACTION_2;
        if (mask[SimGame.ACTION_10])  return SimGame.ACTION_10;
        if (mask[SimGame.ACTION_ACE]) return SimGame.ACTION_ACE;

        // Stuck: prefer chance when pile is large (risky but avoids picking up many cards)
        if (mask[SimGame.ACTION_CHANCE] && game.pile.Count >= 4)
            return SimGame.ACTION_CHANCE;

        if (mask[SimGame.ACTION_PICKUP]) return SimGame.ACTION_PICKUP;
        if (mask[SimGame.ACTION_CHANCE]) return SimGame.ACTION_CHANCE;

        return SimGame.ACTION_PICKUP;
    }
}
