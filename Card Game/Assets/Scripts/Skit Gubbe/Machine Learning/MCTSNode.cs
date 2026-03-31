// MCTSNode.cs
// One node in the MCTS search tree.
// Stores visit statistics and the list of actions not yet expanded.

using System;
using System.Collections.Generic;

public class MCTSNode
{
    public MCTSNode      Parent;
    public int           ActionTaken;     // action that produced this node (-1 for root)
    public List<MCTSNode> Children       = new List<MCTSNode>();
    public List<int>     UntriedActions;  // legal actions from this position not yet explored

    public int   Visits;
    public float TotalValue; // cumulative from the SEARCHING player's perspective

    public bool IsFullyExpanded => UntriedActions == null || UntriedActions.Count == 0;
    public bool IsLeaf          => Children.Count == 0;

    // UCB1: balance exploitation (win rate) with exploration (under-visited children).
    public float UCB1(float c = 1.414f)
    {
        if (Visits == 0) return float.MaxValue;
        double exploit = TotalValue / Visits;
        double explore = c * Math.Sqrt(Math.Log(Parent.Visits) / Visits);
        return (float)(exploit + explore);
    }

    // During tree walk: pick child with highest UCB1.
    public MCTSNode BestUCBChild(float c = 1.414f)
    {
        MCTSNode best    = null;
        float    bestVal = float.NegativeInfinity;
        foreach (var child in Children)
        {
            float v = child.UCB1(c);
            if (v > bestVal) { bestVal = v; best = child; }
        }
        return best;
    }

    // Final action selection: most visited child (robust, less variance than best win rate).
    public MCTSNode MostVisitedChild()
    {
        MCTSNode best = null;
        int      most = -1;
        foreach (var child in Children)
            if (child.Visits > most) { most = child.Visits; best = child; }
        return best;
    }
}
