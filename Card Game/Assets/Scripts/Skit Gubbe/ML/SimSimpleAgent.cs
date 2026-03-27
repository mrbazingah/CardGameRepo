// SimSimpleAgent.cs
// A rule-based opponent for training. Mirrors the strategy of the real game's AIHand:
//   - Always plays the lowest non-special card it can
//   - Only plays 2 or 10 when no other card is playable
//   - Picks up the pile when it can't play anything

public class SimSimpleAgent
{
    public int ChooseAction(SimGame game)
    {
        SimGame.SimPlayer me = game.players[game.currentTurn];

        // Underside: forced random flip — action doesn't matter, SimGame handles it
        if (me.IsUsingUnderSide)
            return SimGame.ACTION_PICKUP;

        bool[] mask = game.GetLegalActionMask();

        // 1. Play the lowest regular card
        if (mask[SimGame.ACTION_REGULAR]) return SimGame.ACTION_REGULAR;

        // 2. Use special cards as a last resort — 2 before 10 before Ace
        if (mask[SimGame.ACTION_2])   return SimGame.ACTION_2;
        if (mask[SimGame.ACTION_10])  return SimGame.ACTION_10;
        if (mask[SimGame.ACTION_ACE]) return SimGame.ACTION_ACE;

        // 3. Nothing playable — pick up the pile
        if (mask[SimGame.ACTION_PICKUP]) return SimGame.ACTION_PICKUP;
        if (mask[SimGame.ACTION_CHANCE]) return SimGame.ACTION_CHANCE;

        return SimGame.ACTION_PICKUP;
    }
}
