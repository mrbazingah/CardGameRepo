using Fusion;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Linq;

[RequireComponent(typeof(NetworkRunner))]
public class GameManagerNetwork : NetworkBehaviour
{
    [Networked] public PlayerRef CurrentTurn { get; set; }
    [Networked] public bool GameStarted { get; set; }
    [Networked] public bool WinnerDeclared { get; set; }

    [Header("UI Elements")]
    [SerializeField] GameObject startButton;
    [SerializeField] GameObject pauseMenu;
    [SerializeField] GameObject winMenu;
    [SerializeField] TextMeshProUGUI winText;
    [SerializeField] TextMeshProUGUI scoreText;
    [SerializeField] TextMeshProUGUI addedScoreText;

    [Header("Scoring")]
    [SerializeField] int minScore;
    [SerializeField] int maxScore;

    int score; bool isPausedLocal; NetworkPlayerHand[] playerHands;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            GameStarted = false;
            WinnerDeclared = false;
            var players = Runner.ActivePlayers.ToList();
            CurrentTurn = players[0];
        }
        score = PlayerPrefs.GetInt("Score", 0);
    }

    void Awake()
    {
        playerHands = FindObjectsOfType<NetworkPlayerHand>();
    }

    void Update()
    {
        if (HasInputAuthority)
            ProcessPauseToggle();
    }

    public void LocalStartGame()
    {
        if (!HasInputAuthority)
            return;
        if (startButton)
            startButton.SetActive(false);
        RPC_RequestStartGame();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_RequestStartGame(RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
            return;
        AssignStartPlayer();
        var players = Runner.ActivePlayers.ToList();
        CurrentTurn = players[0];
        RPC_BroadcastStart();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_BroadcastStart(RpcInfo info = default)
    {
        GameStarted = true;
        if (startButton)
            startButton.SetActive(false);
    }

    void ProcessPauseToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isPausedLocal = !isPausedLocal; Time.timeScale = isPausedLocal ? 0f : 1f;
            if (pauseMenu)
                pauseMenu.SetActive(isPausedLocal);
        }
    }

    public void LocalEndTurn()
    {
        if (!HasInputAuthority || !GameStarted)
            return;
        RPC_RequestNextTurn();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_RequestNextTurn(RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
            return;
        AdvanceTurn(); RPC_BroadcastTurn();
    }

    void AdvanceTurn()
    {
        var players = Runner.ActivePlayers.ToList();
        int idx = players.IndexOf(CurrentTurn);
        idx = (idx + 1) % players.Count;
        CurrentTurn = players[idx];
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_BroadcastTurn(RpcInfo info = default)
    {
        // Turn updated for all clients
    }

    public void CheckProcessWin()
    {
        if (!Object.HasStateAuthority || WinnerDeclared || !GameStarted)
            return;
        var players = Runner.ActivePlayers.ToList();
        bool firstEmpty = playerHands[0].HandCount == 0;
        bool secondEmpty = playerHands.Length > 1 && playerHands[1].HandCount == 0;
        if (firstEmpty || secondEmpty)
        {
            WinnerDeclared = true;
            PlayerRef winner = firstEmpty ? players[1] : players[0];
            RPC_BroadcastWin(winner);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_BroadcastWin(PlayerRef winner, RpcInfo info = default)
    {
        if (winMenu)
            winMenu.SetActive(true);
        bool localWin = (Runner.LocalPlayer == winner);
        if (winText)
            winText.text = localWin ? "You Win!" : "Opponent Wins!";
        if (localWin) AddScore(); else SubtractScore();
    }

    void AddScore()
    {
        int amount = Random.Range(minScore, maxScore + 1); score += amount;
        if (scoreText) scoreText.text = "Score: " + score;
        if (addedScoreText) { addedScoreText.text = "+" + amount; addedScoreText.color = Color.green; }
        if (score > PlayerPrefs.GetInt("Highscore", 0)) PlayerPrefs.SetInt("Highscore", score);
        PlayerPrefs.SetInt("GamesWon", PlayerPrefs.GetInt("GamesWon") + 1); PlayerPrefs.SetInt("Score", score);
    }

    void SubtractScore()
    {
        int amount = Random.Range(minScore, maxScore + 1); if (amount > score) amount = score; score -= amount;
        if (scoreText) scoreText.text = "Score: " + score;
        if (addedScoreText) { addedScoreText.text = "-" + amount; addedScoreText.color = Color.red; }
        PlayerPrefs.SetInt("GamesLost", PlayerPrefs.GetInt("GamesLost") + 1); PlayerPrefs.SetInt("Score", score);
    }

    public void LocalRestartGame()
    {
        if (!HasInputAuthority) return; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void AssignStartPlayer()
    {
        int lowestVal = int.MaxValue; int startIdx = 0;
        for (int i = 0; i < playerHands.Length; i++) { int val = playerHands[i].GetLowestValueExcluding(2, 10); if (val < lowestVal) { lowestVal = val; startIdx = i; } }
        var players = Runner.ActivePlayers.ToList();
        CurrentTurn = players[startIdx];
    }
}