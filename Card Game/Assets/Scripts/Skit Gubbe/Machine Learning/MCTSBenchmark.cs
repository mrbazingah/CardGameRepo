// MCTSBenchmark.cs
// Headless benchmark that measures MCTS strength at various iteration counts
// and compares it against the Simple, Medium, and Q-learning agents.
//
// HOW TO USE:
//   1. Create a new empty scene called "MCTS Benchmark".
//   2. Add an empty GameObject, attach this script.
//   3. Set saveFileName to the qtable.json produced by TrainingRunner
//      (so MCTSvsQL tests the MCTS against your trained Q-learning agent).
//   4. Press Play. Results are printed to the Console when done.
//   5. Use the win rates to decide what IterationsPerMove to set on AIBehaviourMCTS.

using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MCTSBenchmark : MonoBehaviour
{
    [Header("Test Configuration")]
    [Tooltip("Games per matchup per iteration-count level")]
    [SerializeField] int gamesPerTest     = 500;
    [SerializeField] int gamesPerFrame    = 10; // keep small — MCTS games are expensive
    [SerializeField] int maxStepsPerGame  = 600;

    [Header("MCTS Settings")]
    [Tooltip("Iteration counts to benchmark — e.g. 100, 500, 1000, 2000, 5000")]
    [SerializeField] int[] iterationLevels = { 100, 500, 1000, 2000 };
    [SerializeField] int   determinizations    = 20;
    [SerializeField] float explorationConstant = 1.414f;

    [Header("Opponents")]
    [SerializeField] bool testVsSimple = true;
    [SerializeField] bool testVsMedium = true;
    [SerializeField] bool testVsQL     = true;
    [SerializeField] string saveFileName = "qtable.json";

    // -----------------------------------------------------------------------
    //  Internal state machine
    // -----------------------------------------------------------------------
    struct TestJob
    {
        public int   mctsIters;
        public string opponentName;
        public System.Func<SimGame, int> opponentAction;
    }

    List<TestJob> jobs        = new List<TestJob>();
    int           jobIndex    = 0;
    int           gamesPlayed = 0;
    int           mctsWins    = 0;
    bool          done        = false;

    SimGame        game         = new SimGame();
    SimSimpleAgent simpleAgent  = new SimSimpleAgent();
    SimMediumAgent mediumAgent  = new SimMediumAgent();
    QLearningAgent qlAgent      = new QLearningAgent();
    MCTSAgent      mctsAgent;

    void Start()
    {
        string path = Path.Combine(Application.persistentDataPath, saveFileName);
        qlAgent.Load(path);

        BuildJobList();

        Debug.Log($"[MCTSBenchmark] Starting {jobs.Count} test matchups × {gamesPerTest} games each.");
        StartNextJob();
    }

    void BuildJobList()
    {
        foreach (int iters in iterationLevels)
        {
            if (testVsSimple)
                jobs.Add(new TestJob { mctsIters = iters, opponentName = "Simple",
                    opponentAction = g => simpleAgent.ChooseAction(g) });

            if (testVsMedium)
                jobs.Add(new TestJob { mctsIters = iters, opponentName = "Medium",
                    opponentAction = g => mediumAgent.ChooseAction(g) });

            if (testVsQL)
                jobs.Add(new TestJob { mctsIters = iters, opponentName = "QL",
                    opponentAction = g => qlAgent.GreedyAction(g.GetStateKey(), g.GetLegalActionMask()) });
        }
    }

    void StartNextJob()
    {
        if (jobIndex >= jobs.Count) { FinishAll(); return; }

        TestJob job = jobs[jobIndex];
        mctsAgent = new MCTSAgent
        {
            IterationsPerMove   = job.mctsIters,
            Determinizations    = determinizations,
            ExplorationConstant = explorationConstant,
        };

        gamesPlayed = 0;
        mctsWins    = 0;

        Debug.Log($"[MCTSBenchmark] Test {jobIndex + 1}/{jobs.Count}: " +
                  $"MCTS({job.mctsIters} iters) vs {job.opponentName}");
    }

    void Update()
    {
        if (done) return;

        TestJob job = jobs[jobIndex];
        int end     = Mathf.Min(gamesPlayed + gamesPerFrame, gamesPerTest);

        for (int g = gamesPlayed; g < end; g++)
            RunGame(job);

        gamesPlayed = end;

        if (gamesPlayed >= gamesPerTest)
        {
            float wr = (float)mctsWins / gamesPlayed * 100f;
            Debug.Log($"[MCTSBenchmark] MCTS({job.mctsIters}) vs {job.opponentName}: " +
                      $"Win rate = {wr:F1}% ({mctsWins}/{gamesPlayed})");

            jobIndex++;
            StartNextJob();
        }
    }

    // -----------------------------------------------------------------------
    //  Simulate one game: MCTS (player 0) vs opponent (player 1)
    // -----------------------------------------------------------------------
    void RunGame(TestJob job)
    {
        game.Reset();
        const int MCTS_PLAYER = 0;

        for (int step = 0; step < maxStepsPerGame; step++)
        {
            if (game.gameOver) break;

            int action;
            if (game.currentTurn == MCTS_PLAYER)
                action = mctsAgent.ChooseAction(game, MCTS_PLAYER);
            else
                action = job.opponentAction(game);

            game.Step(action);
        }

        if (game.gameOver && game.winner == MCTS_PLAYER)
            mctsWins++;
    }

    // -----------------------------------------------------------------------
    //  Print summary when all jobs complete
    // -----------------------------------------------------------------------
    void FinishAll()
    {
        done = true;
        Debug.Log("[MCTSBenchmark] All tests complete. " +
                  "Use these win rates to pick IterationsPerMove for AIBehaviourMCTS.");

#if UNITY_EDITOR
        EditorApplication.isPaused = true;
#endif
    }
}
