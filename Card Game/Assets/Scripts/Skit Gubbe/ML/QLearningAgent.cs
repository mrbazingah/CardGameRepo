// QLearningAgent.cs
// A Q-table agent that learns which card to play through self-play.
// No external ML libraries needed — pure C#.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class QTableData
{
    public List<string> keys   = new List<string>();
    public List<string> values = new List<string>(); // comma-separated floats per state
}

public class QLearningAgent
{
    // Q[stateKey][action] = expected reward
    private Dictionary<string, float[]> qTable = new Dictionary<string, float[]>();

    // ---------------------------------------------------------------
    //  Hyperparameters — tune these if needed
    // ---------------------------------------------------------------
    public float LearningRate  = 0.1f;   // how fast weights update
    public float Discount      = 0.99f;  // how much future rewards matter — needs to be high for long games
    public float Epsilon       = 1.0f;   // starting exploration rate
    public float EpsilonDecay  = 0.99999f; // applied once per GAME, not per step
    public float EpsilonMin    = 0.05f;

    private System.Random rng = new System.Random();

    // ---------------------------------------------------------------
    //  Choose action (epsilon-greedy during training)
    // ---------------------------------------------------------------
    public int ChooseAction(string state, bool[] legalMask)
    {
        if (rng.NextDouble() < Epsilon)
            return RandomLegal(legalMask);   // explore

        return GreedyAction(state, legalMask);   // exploit
    }

    // Greedy only — use this when running in the actual game (no exploration)
    public int GreedyAction(string state, bool[] legalMask)
    {
        float[] q    = GetQ(state);
        int     best = -1;
        float   bestVal = float.NegativeInfinity;

        for (int a = 0; a < SimGame.NUM_ACTIONS; a++)
        {
            if (legalMask[a] && q[a] > bestVal)
            {
                bestVal = q[a];
                best    = a;
            }
        }

        return best >= 0 ? best : RandomLegal(legalMask);
    }

    // ---------------------------------------------------------------
    //  Q-learning update (Bellman equation)
    // ---------------------------------------------------------------
    public void Learn(
        string state,
        int    action,
        float  reward,
        string nextState,
        bool[] nextLegalMask,
        bool   done)
    {
        float[] q = GetQ(state);

        float target;
        if (done)
        {
            target = reward;
        }
        else
        {
            float maxNext = BestQValue(nextState, nextLegalMask);
            target = reward + Discount * maxNext;
        }

        q[action] += LearningRate * (target - q[action]);
    }

    // ---------------------------------------------------------------
    //  Persistence — save/load the Q-table as JSON
    // ---------------------------------------------------------------
    public void Save(string filePath)
    {
        var data = new QTableData();
        foreach (var kv in qTable)
        {
            data.keys.Add(kv.Key);
            data.values.Add(string.Join(",", kv.Value));
        }

        File.WriteAllText(filePath, JsonUtility.ToJson(data, prettyPrint: true));
        Debug.Log($"[QL] Saved {qTable.Count} states to: {filePath}");
    }

    public void Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"[QL] No Q-table found at: {filePath}");
            return;
        }

        var data = JsonUtility.FromJson<QTableData>(File.ReadAllText(filePath));
        qTable.Clear();

        for (int i = 0; i < data.keys.Count; i++)
        {
            var parts = data.values[i].Split(',');
            var q = new float[SimGame.NUM_ACTIONS];
            for (int j = 0; j < parts.Length && j < SimGame.NUM_ACTIONS; j++)
                float.TryParse(parts[j], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out q[j]);
            qTable[data.keys[i]] = q;
        }

        Epsilon = EpsilonMin; // no random exploration when loaded for real play
        Debug.Log($"[QL] Loaded {qTable.Count} states from: {filePath}");
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------
    float[] GetQ(string state)
    {
        if (!qTable.TryGetValue(state, out var q))
        {
            q = new float[SimGame.NUM_ACTIONS];
            qTable[state] = q;
        }
        return q;
    }

    float BestQValue(string state, bool[] legalMask)
    {
        float[] q   = GetQ(state);
        float   max = float.NegativeInfinity;
        for (int a = 0; a < SimGame.NUM_ACTIONS; a++)
            if (legalMask[a] && q[a] > max) max = q[a];
        return max == float.NegativeInfinity ? 0f : max;
    }

    int RandomLegal(bool[] mask)
    {
        var legal = new List<int>();
        for (int i = 0; i < mask.Length; i++)
            if (mask[i]) legal.Add(i);
        return legal.Count > 0 ? legal[rng.Next(legal.Count)] : 0;
    }

    // Call once per game, not per step
    public void DecayEpsilon()
    {
        if (Epsilon > EpsilonMin)
            Epsilon *= EpsilonDecay;
    }

    public int StateCount => qTable.Count;
}
