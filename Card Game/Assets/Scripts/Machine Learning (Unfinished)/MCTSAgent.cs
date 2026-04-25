// MCTSAgent.cs
// Monte Carlo Tree Search for Skit Gubbe.
//
// HOW IT WORKS:
//   Each decision runs IterationsPerMove MCTS simulations split across
//   Determinizations sampled worlds (opponent hand re-randomized each world).
//   Averaging visit counts across worlds gives robust decisions despite hidden info.
//
//   One MCTS iteration = Selection → Expansion → Rollout → Backpropagation.
//
// NO TRAINING FILE NEEDED — intelligence comes from search depth at decision time.
// Increase IterationsPerMove for a harder AI; decrease for a weaker one.

using System;
using System.Collections.Generic;

public class MCTSAgent
{
    // ------------------------------------------------------------------
    //  Tunable parameters
    // ------------------------------------------------------------------
    public int   IterationsPerMove   = 2000;  // total simulations per decision
    public int   Determinizations    = 20;    // worlds sampled (handles hidden info)
    public float ExplorationConstant = 1.414f;
    public int   MaxRolloutDepth     = 80;    // cap on random playout length

    private Random rng;

    public MCTSAgent(int seed = -1)
    {
        rng = seed < 0 ? new Random() : new Random(seed);
    }

    // ------------------------------------------------------------------
    //  Main entry point — returns the best action index.
    //  observingPlayer: 0 or 1 (the AI making the decision).
    // ------------------------------------------------------------------
    public int ChooseAction(SimGame realGame, int observingPlayer)
    {
        bool[] mask = realGame.GetLegalActionMask();

        // Skip search if only one option
        int legalCount = 0, onlyAction = -1;
        for (int a = 0; a < SimGame.NUM_ACTIONS; a++)
            if (mask[a]) { legalCount++; onlyAction = a; }
        if (legalCount == 1) return onlyAction;

        // Accumulate visit counts across all determinized worlds
        int[] totalVisits = new int[SimGame.NUM_ACTIONS];
        int   itersPerWorld = Math.Max(1, IterationsPerMove / Determinizations);

        for (int d = 0; d < Determinizations; d++)
        {
            SimGame world = SimGameCloner.Determinize(realGame, observingPlayer, rng);
            MCTSNode root = MakeRoot(world);

            for (int i = 0; i < itersPerWorld; i++)
            {
                SimGame sim = SimGameCloner.Clone(world);
                RunIteration(root, sim, observingPlayer);
            }

            foreach (var child in root.Children)
                totalVisits[child.ActionTaken] += child.Visits;
        }

        // Best = most visited legal action across all worlds
        int best = -1, bestVis = -1;
        for (int a = 0; a < SimGame.NUM_ACTIONS; a++)
        {
            if (mask[a] && totalVisits[a] > bestVis)
            {
                bestVis = totalVisits[a];
                best    = a;
            }
        }

        return best >= 0 ? best : onlyAction;
    }

    // ------------------------------------------------------------------
    //  Build root node from current game state
    // ------------------------------------------------------------------
    MCTSNode MakeRoot(SimGame game)
    {
        return new MCTSNode
        {
            Parent        = null,
            ActionTaken   = -1,
            UntriedActions = LegalActions(game),
        };
    }

    // ------------------------------------------------------------------
    //  Single MCTS iteration on a cloned game
    // ------------------------------------------------------------------
    void RunIteration(MCTSNode root, SimGame game, int searchingPlayer)
    {
        MCTSNode node = root;

        // --- Selection: descend using UCB1 until a non-fully-expanded node ---
        while (node.IsFullyExpanded && !node.IsLeaf)
        {
            node = node.BestUCBChild(ExplorationConstant);
            game.Step(node.ActionTaken);
            if (game.gameOver) break;
        }

        // --- Expansion: add one untried child ---
        if (!game.gameOver && !node.IsFullyExpanded)
        {
            int idx    = rng.Next(node.UntriedActions.Count);
            int action = node.UntriedActions[idx];
            node.UntriedActions.RemoveAt(idx);

            game.Step(action);

            var child = new MCTSNode
            {
                Parent         = node,
                ActionTaken    = action,
                UntriedActions = game.gameOver ? new List<int>() : LegalActions(game),
            };
            node.Children.Add(child);
            node = child;
        }

        // --- Simulation: random playout ---
        float result = Rollout(game, searchingPlayer);

        // --- Backpropagation ---
        MCTSNode n = node;
        while (n != null)
        {
            n.Visits++;
            n.TotalValue += result;
            n = n.Parent;
        }
    }

    // ------------------------------------------------------------------
    //  Random playout until terminal or depth limit
    // ------------------------------------------------------------------
    float Rollout(SimGame game, int searchingPlayer)
    {
        for (int step = 0; step < MaxRolloutDepth && !game.gameOver; step++)
            game.Step(RandomAction(game.GetLegalActionMask()));

        if (!game.gameOver) return 0f;
        return game.winner == searchingPlayer ? 1f : -1f;
    }

    // ------------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------------
    List<int> LegalActions(SimGame game)
    {
        bool[] mask   = game.GetLegalActionMask();
        var    result = new List<int>();
        for (int a = 0; a < SimGame.NUM_ACTIONS; a++)
            if (mask[a]) result.Add(a);
        return result;
    }

    int RandomAction(bool[] mask)
    {
        int count = 0;
        for (int a = 0; a < mask.Length; a++)
            if (mask[a]) count++;

        int pick = rng.Next(count);
        for (int a = 0; a < mask.Length; a++)
        {
            if (!mask[a]) continue;
            if (pick == 0) return a;
            pick--;
        }
        return 0;
    }
}
