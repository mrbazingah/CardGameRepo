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
        var    active  = me.ActiveCards();
        int    pileTop = game.PileTop;

        var regular = new List<int>();
        var special  = new List<int>();

        foreach (int v in active)
        {
            if (!game.CanPlay(v)) continue;
            if (v == 2 || v == 10) special.Add(v);
            else                   regular.Add(v);
        }

        // Tactically play 10 to clear a mid-to-high pile (>= 9), even if regular cards exist.
        // This mirrors AIBehaviourHard's safety-score logic that rates 10 as always 1.0.
        if (pileTop >= 9 && special.Contains(10))
            return 10;

        // Play the lowest regular card
        if (regular.Count > 0)
        {
            regular.Sort();
            return regular[0];
        }

        // Use 2 before 10 — extra turn is more valuable than a guaranteed clear on an empty/low pile
        if (special.Contains(2))  return 2;
        if (special.Contains(10)) return 10;

        // Stuck: prefer chance when pile is large (risky but avoids picking up many cards)
        if (mask[SimGame.ACTION_CHANCE] && game.pile.Count >= 4)
            return SimGame.ACTION_CHANCE;

        if (mask[SimGame.ACTION_PICKUP]) return SimGame.ACTION_PICKUP;
        if (mask[SimGame.ACTION_CHANCE]) return SimGame.ACTION_CHANCE;

        return SimGame.ACTION_PICKUP;
    }
}
