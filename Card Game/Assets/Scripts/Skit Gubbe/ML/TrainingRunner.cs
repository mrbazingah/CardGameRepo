// TrainingRunner.cs
// MonoBehaviour that drives AI vs AI self-play training.
//
// HOW TO USE:
//   1. Create a new empty scene called "ML Training".
//   2. Add an empty GameObject, attach this script.
//   3. Press Play — training runs at full speed (no visuals, no delays).
//   4. When done, the Q-table is saved to Application.persistentDataPath/qtable.json
//   5. Load that file in AIBehaviourML.cs to use the trained agent in your game.

using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class TrainingRunner : MonoBehaviour
{
    public enum TrainingMode
    {
        VsSimple,   // ML vs rule-based simple opponent (recommended first)
        VsMedium,   // ML vs rule-based medium opponent (plays 10 tactically)
        SelfPlay,   // ML vs itself
    }

    [Header("Training")]
    [Tooltip("Number of full games to simulate")]
    [SerializeField] int totalGames      = 500_000;
    [Tooltip("Games simulated per frame (higher = faster, but Unity may feel unresponsive)")]
    [SerializeField] int gamesPerFrame   = 2_000;
    [Tooltip("Safety limit: max steps per game to prevent infinite loops")]
    [SerializeField] int maxStepsPerGame = 600;
    [Tooltip("Print progress to console every N games")]
    [SerializeField] int logInterval     = 50_000;
    [Tooltip("VsSimple: learn basics.\nVsMedium: learn against a tactically stronger opponent.\nSelfPlay: refine strategy against itself.\nRecommended order: Simple → Medium → SelfPlay.")]
    [SerializeField] TrainingMode trainingMode = TrainingMode.VsSimple;

    [Header("Save")]
    [SerializeField] string saveFileName  = "qtable.json";
    [Tooltip("Also auto-save at this interval (0 = disabled)")]
    [SerializeField] int autoSaveInterval = 100_000;

    // ---------------------------------------------------------------
    //  Runtime state
    // ---------------------------------------------------------------
    QLearningAgent agent        = new QLearningAgent();
    SimSimpleAgent simpleAgent  = new SimSimpleAgent();
    SimMediumAgent mediumAgent  = new SimMediumAgent();
    SimGame        game         = new SimGame();

    int   gamesPlayed;
    int   mlWins, simpleWins;
    bool  trainingDone;
    string savePath;

    void Start()
    {
        savePath = Path.Combine(Application.persistentDataPath, saveFileName);
        Debug.Log($"[Training] Starting {totalGames:N0} games | Mode: {trainingMode}");
        Debug.Log($"[Training] Save path: {savePath}");

        // Uncomment ONLY to resume from a previous run WITH THE SAME state encoding:
        agent.Load(savePath);
        agent.Epsilon = 0.3f;

        // Always start fresh — required after any change to GetStateKey()
        //agent.Epsilon = 1.0f;
    }

    void Update()
    {
        if (trainingDone) return;

        int end = Mathf.Min(gamesPlayed + gamesPerFrame, totalGames);

        for (int g = gamesPlayed; g < end; g++)
            RunGame();

        gamesPlayed = end;

        // Periodic log
        if (gamesPlayed % logInterval == 0)
            PrintProgress();

        // Auto-save
        if (autoSaveInterval > 0 && gamesPlayed % autoSaveInterval == 0 && gamesPlayed > 0)
            agent.Save(savePath);

        if (gamesPlayed >= totalGames)
            FinishTraining();
    }

    // ---------------------------------------------------------------
    //  Simulate one full game
    // ---------------------------------------------------------------
    void RunGame()
    {
        game.Reset();

        switch (trainingMode)
        {
            case TrainingMode.VsSimple:  RunGameVsSimple();  break;
            case TrainingMode.VsMedium:  RunGameVsMedium();  break;
            case TrainingMode.SelfPlay:  RunGameSelfPlay();  break;
        }

        // Decay once per game so exploration lasts through most of training
        agent.DecayEpsilon();
    }

    // ML agent (player 0) vs Simple agent (player 1)
    // Only the ML agent learns — the simple agent always uses fixed rules.
    //
    // KEY FIX: Q-updates are deferred until the ML agent's NEXT turn so that
    // nextState is always from the ML agent's own perspective, not the opponent's.
    void RunGameVsSimple()
    {
        string prevMLState       = null;
        int    prevMLAction      = -1;
        float  accumulatedReward = 0f;

        for (int step = 0; step < maxStepsPerGame; step++)
        {
            int turn = game.currentTurn;

            if (turn == 0)
            {
                // ML agent's turn
                string state = game.GetStateKey();
                bool[] mask  = game.GetLegalActionMask();
                int action   = agent.ChooseAction(state, mask);

                // Now we know the true "next state" for the previous ML action
                // (after the opponent has already moved), so update it now
                if (prevMLState != null)
                {
                    agent.Learn(prevMLState, prevMLAction, accumulatedReward, state, mask, done: false);
                    accumulatedReward = 0f;
                }

                float reward = game.Step(action);

                if (game.gameOver)
                {
                    agent.Learn(state, action, reward, string.Empty, new bool[SimGame.NUM_ACTIONS], done: true);
                    if (game.winner == 0) mlWins++;
                    else                  simpleWins++;
                    return;
                }

                // Accumulate any intermediate reward (pile clear etc.) and defer the update
                accumulatedReward += reward;
                prevMLState  = state;
                prevMLAction = action;
            }
            else
            {
                // Simple agent's turn — no learning
                int   action = simpleAgent.ChooseAction(game);
                float reward = game.Step(action);

                if (game.gameOver)
                {
                    // Simple agent won — give ML agent a loss on its last action
                    if (prevMLState != null)
                        agent.Learn(prevMLState, prevMLAction, -1f, string.Empty, new bool[SimGame.NUM_ACTIONS], done: true);

                    if (game.winner == 0) mlWins++;
                    else                  simpleWins++;
                    return;
                }
            }
        }
    }

    // ML agent (player 0) vs Medium agent (player 1) — same deferred-update pattern as VsSimple
    void RunGameVsMedium()
    {
        string prevMLState       = null;
        int    prevMLAction      = -1;
        float  accumulatedReward = 0f;

        for (int step = 0; step < maxStepsPerGame; step++)
        {
            int turn = game.currentTurn;

            if (turn == 0)
            {
                string state = game.GetStateKey();
                bool[] mask  = game.GetLegalActionMask();
                int action   = agent.ChooseAction(state, mask);

                if (prevMLState != null)
                {
                    agent.Learn(prevMLState, prevMLAction, accumulatedReward, state, mask, done: false);
                    accumulatedReward = 0f;
                }

                float reward = game.Step(action);

                if (game.gameOver)
                {
                    agent.Learn(state, action, reward, string.Empty, new bool[SimGame.NUM_ACTIONS], done: true);
                    if (game.winner == 0) mlWins++;
                    else                  simpleWins++;
                    return;
                }

                accumulatedReward += reward;
                prevMLState  = state;
                prevMLAction = action;
            }
            else
            {
                int   action = mediumAgent.ChooseAction(game);
                float reward = game.Step(action);

                if (game.gameOver)
                {
                    if (prevMLState != null)
                        agent.Learn(prevMLState, prevMLAction, -1f, string.Empty, new bool[SimGame.NUM_ACTIONS], done: true);

                    if (game.winner == 0) mlWins++;
                    else                  simpleWins++;
                    return;
                }
            }
        }
    }

    // Both sides are the same ML agent (classic self-play)
    // Same deferred-update fix applied to both players.
    void RunGameSelfPlay()
    {
        string[] prevState  = new string[2];
        int[]    prevAction = new int[]   { -1, -1 };
        float[]  accReward  = new float[2];

        for (int step = 0; step < maxStepsPerGame; step++)
        {
            int    turn  = game.currentTurn;
            string state = game.GetStateKey();
            bool[] mask  = game.GetLegalActionMask();
            int    action = agent.ChooseAction(state, mask);

            // Update this player's previous action now that we know their true next state
            if (prevState[turn] != null)
            {
                agent.Learn(prevState[turn], prevAction[turn], accReward[turn], state, mask, done: false);
                accReward[turn] = 0f;
            }

            float reward = game.Step(action);

            if (game.gameOver)
            {
                agent.Learn(state, action, reward, string.Empty, new bool[SimGame.NUM_ACTIONS], done: true);

                int other = 1 - turn;
                if (prevState[other] != null)
                    agent.Learn(prevState[other], prevAction[other], -reward, string.Empty, new bool[SimGame.NUM_ACTIONS], done: true);

                if (game.winner == 0) mlWins++;
                else                  simpleWins++;
                return;
            }

            accReward[turn]  += reward;
            prevState[turn]   = state;
            prevAction[turn]  = action;
        }
    }

    // ---------------------------------------------------------------
    //  Finish
    // ---------------------------------------------------------------
    void FinishTraining()
    {
        trainingDone = true;
        agent.Save(savePath);
        PrintProgress();
        Debug.Log("[Training] Complete! Q-table saved. You can now use AIBehaviourML.cs in your game.");

#if UNITY_EDITOR
        EditorApplication.isPaused = true; // pause the editor when done
#endif
    }

    void PrintProgress()
    {
        float mlWR = gamesPlayed > 0 ? (float)mlWins / gamesPlayed * 100f : 0f;
        string mode = trainingMode.ToString();
        Debug.Log($"[Training | {mode}] {gamesPlayed:N0}/{totalGames:N0} games | " +
                  $"ML win rate: {mlWR:F1}% | " +
                  $"States learned: {agent.StateCount:N0} | " +
                  $"Epsilon: {agent.Epsilon:F3}");
    }

    // ---------------------------------------------------------------
    //  Editor helper: show save path in Inspector at any time
    // ---------------------------------------------------------------
    public string GetSavePath() =>
        Path.Combine(Application.persistentDataPath, saveFileName);
}
