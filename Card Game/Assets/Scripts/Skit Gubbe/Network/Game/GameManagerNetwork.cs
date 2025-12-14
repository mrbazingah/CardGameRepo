using Fusion;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Linq;

public class GameManagerNetwork : NetworkBehaviour
{
    [Networked] public PlayerRef CurrentTurn { get; set; }
    [Networked] public bool GameStarted { get; set; }
    [Networked] public bool WinnerDeclared { get; set; }
    [Networked] public int PlayersReady { get; set; }

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

    int score; 
    bool isPausedLocal; 
    NetworkPlayerHand[] playerHands;
    NetworkCardGenerator cardGenerator;
    [Networked] TickTimer startGameDelayTimer { get; set; }
    [Networked] TickTimer assignStartPlayerTimer { get; set; }
    bool hasStartedGameDelay;
    bool hasStartedAssignTimer;

    public override void Spawned()
    {
        Debug.Log($"GameManagerNetwork: Spawned. IsServer: {Runner?.IsServer}");
        
        if (Runner != null && Runner.IsServer)
        {
            GameStarted = false;
            WinnerDeclared = false;
            PlayersReady = 0;
            hasStartedGameDelay = false;
            hasStartedAssignTimer = false;
            var players = Runner.ActivePlayers.ToList();
            if (players.Count > 0)
            {
                CurrentTurn = players[0];
            }
        }
        score = PlayerPrefs.GetInt("Score", 0);
        
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
        }
    }

    void Awake()
    {
        RefreshPlayerHands();
        cardGenerator = FindObjectOfType<NetworkCardGenerator>();
    }

    void RefreshPlayerHands()
    {
        playerHands = FindObjectsOfType<NetworkPlayerHand>();
    }

    void Update()
    {
        if (HasInputAuthority)
        {
            ProcessPauseToggle();
        }

        // Refresh player hands periodically to catch newly spawned hands
        if (playerHands == null || playerHands.Length < 2)
        {
            RefreshPlayerHands();
        }
    }

    public void LocalStartGame()
    {
        // Any player can request game start (typically host)
        if (startButton)
            startButton.SetActive(false);
        RPC_RequestStartGame(Runner.LocalPlayer);
    }

    public void LocalPlayerReady()
    {
        // Any player can call this - no InputAuthority check needed
        RPC_PlayerReady(Runner.LocalPlayer);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_PlayerReady(PlayerRef player, RpcInfo info = default)
    {
        if (Runner == null || !Runner.IsServer)
            return;

        PlayersReady++;
        Debug.Log($"Player {player} ready. Total ready: {PlayersReady}");

        // Check if all players are ready
        var players = Runner.ActivePlayers.ToList();
        if (PlayersReady >= players.Count && players.Count >= 2)
        {
            Debug.Log("All players are ready - Starting game!");
            // Start the game automatically
            if (!hasStartedGameDelay)
            {
                startGameDelayTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
                hasStartedGameDelay = true;
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_RequestStartGame(PlayerRef requester, RpcInfo info = default)
    {
        if (Runner == null || !Runner.IsServer)
            return;

        Debug.Log($"Game start requested by {requester}");

        // Wait a moment for cards to be dealt, then assign start player
        if (!hasStartedGameDelay)
        {
            startGameDelayTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
            hasStartedGameDelay = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Only server handles game state
        if (Runner == null || !Runner.IsServer)
            return;

        // First timer: Start the game (triggers card dealing in NetworkCardGenerator)
        if (hasStartedGameDelay && startGameDelayTimer.Expired(Runner))
        {
            RPC_BroadcastStart();
            hasStartedGameDelay = false;
            
            // Start second timer to assign start player AFTER cards are dealt
            if (!hasStartedAssignTimer)
            {
                assignStartPlayerTimer = TickTimer.CreateFromSeconds(Runner, 1.5f);
                hasStartedAssignTimer = true;
            }
        }
        
        // Second timer: Assign start player (after cards have been dealt)
        if (hasStartedAssignTimer && assignStartPlayerTimer.Expired(Runner))
        {
            RefreshPlayerHands();
            AssignStartPlayer();
            RPC_BroadcastStartPlayer();
            hasStartedAssignTimer = false;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_BroadcastStart(RpcInfo info = default)
    {
        Debug.Log("RPC_BroadcastStart: Game is starting!");
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
        RPC_RequestNextTurn(0); // 0 means normal turn advance
    }

    public void LocalEndTurnWithCard(byte cardValue)
    {
        if (!HasInputAuthority || !GameStarted)
            return;
        // Special cards (2 and 10) don't advance turn
        if (cardValue == 2 || cardValue == 10)
        {
            return; // Don't advance turn
        }
        RPC_RequestNextTurn(cardValue);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_RequestNextTurn(byte lastCardValue, RpcInfo info = default)
    {
        if (Runner == null || !Runner.IsServer)
            return;
        
        // Special cards don't advance turn
        if (lastCardValue == 2 || lastCardValue == 10)
        {
            return;
        }
        
        AdvanceTurn();
        RPC_BroadcastTurn();
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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_BroadcastStartPlayer(RpcInfo info = default)
    {
        Debug.Log($"Start player assigned: {CurrentTurn}");
    }

    public void CheckProcessWin()
    {
        if (Runner == null || !Runner.IsServer || WinnerDeclared || !GameStarted)
            return;

        RefreshPlayerHands();
        
        if (playerHands == null || playerHands.Length < 2)
            return;

        var players = Runner.ActivePlayers.ToList();
        if (players.Count < 2)
            return;

        bool firstEmpty = playerHands[0] != null && playerHands[0].HandCount == 0;
        bool secondEmpty = playerHands.Length > 1 && playerHands[1] != null && playerHands[1].HandCount == 0;
        
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
        RefreshPlayerHands();
        
        if (playerHands == null || playerHands.Length < 2)
        {
            // Fallback: just use first player
            var players = Runner.ActivePlayers.ToList();
            if (players.Count > 0)
            {
                CurrentTurn = players[0];
            }
            return;
        }

        int lowestVal = int.MaxValue;
        int startIdx = 0;
        
        for (int i = 0; i < playerHands.Length; i++)
        {
            if (playerHands[i] == null)
                continue;
                
            int val = playerHands[i].GetLowestValueExcluding(2, 10);
            if (val < lowestVal)
            {
                lowestVal = val;
                startIdx = i;
            }
        }
        
        var playersList = Runner.ActivePlayers.ToList();
        if (startIdx < playersList.Count)
        {
            CurrentTurn = playersList[startIdx];
        }
    }
}