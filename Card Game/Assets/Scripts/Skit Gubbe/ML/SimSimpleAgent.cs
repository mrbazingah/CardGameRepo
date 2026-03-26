// SimSimpleAgent.cs
// A rule-based opponent for training. Mirrors the strategy of the real game's AIHand:
//   - Always plays the lowest non-special card it can
//   - Only plays 2 or 10 when no other card is playable
//   - Picks up the pile when it can't play anything

using System.Collections.Generic;

public class SimSimpleAgent
{
    public int ChooseAction(SimGame game)
    {
        SimGame.SimPlayer me = game.players[game.currentTurn];

        // Underside: forced random flip — action doesn't matter, SimGame handles it
        if (me.IsUsingUnderSide)
            return SimGame.ACTION_PICKUP;

        bool[] mask    = game.GetLegalActionMask();
        var    active  = me.ActiveCards();

        var regular = new List<int>(); // non-special playable cards
        var special  = new List<int>(); // 2 and 10

        foreach (int v in active)
        {
            if (!game.CanPlay(v)) continue;
            if (v == 2 || v == 10) special.Add(v);
            else                   regular.Add(v);
        }

        // 1. Play the lowest regular card
        if (regular.Count > 0)
        {
            regular.Sort();
            return regular[0];
        }

        // 2. Only use special cards (2 or 10) as a last resort
        if (special.Count > 0)
        {
            special.Sort();    // play 2 before 10
            return special[0];
        }

        // 3. Nothing playable — pick up the pile
        if (mask[SimGame.ACTION_PICKUP]) return SimGame.ACTION_PICKUP;
        if (mask[SimGame.ACTION_CHANCE]) return SimGame.ACTION_CHANCE;

        return SimGame.ACTION_PICKUP;
    }
}
