using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    #region Variables
    [SerializeField] int playerCount = 1;
    [SerializeField] int turn = 1;
    [SerializeField] TextMeshProUGUI winText, scoreText, addedScoreText;
    [SerializeField] GameObject pauseMenu, winMenu;
    [Space]
    [SerializeField] int score, maxScore, minScore;

    bool gameHasStarted;
    bool winner;
    bool isOpen;

    PlayerHand playerHand;
    AIHand aiHand;
    #endregion

    void Awake()
    {
        aiHand = FindFirstObjectByType<AIHand>();
        playerHand = FindFirstObjectByType<PlayerHand>();
    }

    void Start()
    {
        winMenu.gameObject.SetActive(false);
        pauseMenu.gameObject.SetActive(false);
        score = PlayerPrefs.GetInt("Score");
    }

    #region Game Start
    public void StartGame(GameObject button)
    {
        AsignStartPlayer();
        button.SetActive(false);
        Destroy(button);
        gameHasStarted = true;
    }

    public void AsignStartPlayer()
    {
        List<GameObject> playerCards = playerHand.GetCurrentCards();
        int playerLowest = 20;

        for (int i = 0; i < playerCards.Count; i++)
        {
            int value = playerCards[i].GetComponent<Card>().GetValue();
            if (value < playerLowest && value != 2 && value != 10)
            {
                playerLowest = value;
            }
        }

        List<GameObject> aiCards = aiHand.GetCards();
        int aiLowest = 20;

        for (int i = 0; i < aiCards.Count; i++)
        {
            int value = aiCards[i].GetComponent<Card>().GetValue();
            if (value < aiLowest && value != 2 && value != 10)
            {
                aiLowest = value;
            }
        }

        if (playerLowest > aiLowest)
        {
            aiHand.SetTurnNumber(1);
            playerHand.SetTurnNumber(2);
        }
        else
        {
            playerHand.SetTurnNumber(1);
            aiHand.SetTurnNumber(2);
        }
    }
    #endregion

    #region PauseMenu
    void Update()
    {
        ProcessPauseMenu();
    }

    void ProcessPauseMenu()
    {
        if (!isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            OpenMenu();
        }
        else if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseMenu();
        }

        pauseMenu.SetActive(isOpen);
    }

    public void OpenMenu()
    {
        Time.timeScale = 0f;
        isOpen = true;
    }

    public void CloseMenu()
    {
        Time.timeScale = 1f;
        isOpen = false;
    }
    #endregion

    #region Turn and Win
    public void NextTurn(GameObject lastPlayed)
    {
        if (lastPlayed != null)
        {
            int value = lastPlayed.GetComponent<Card>().GetValue();
            if (value != 10 || value != 2)
            {
                turn++;
                if (turn > 2)
                {
                    turn = 1;
                }
            }
        }
        else
        {
            turn++;
            if (turn > 2)
            {
                turn = 1;
            }
        }
    }

    public IEnumerator ProcessWin(string playerName, int lastCardValue = -1)
    {
        yield return new WaitForSeconds(0.001f);

        if (!winner && playerHand != null)
        {
            if (playerHand.GetCurrentCards().Count == 0 && lastCardValue != 2 && lastCardValue != 10)
            {
                winMenu.gameObject.SetActive(true);

                int amount = Random.Range(minScore, maxScore + 1);
                AddScore(amount);

                winner = true;
            }
            else if (aiHand.GetCards().Count == 0 && lastCardValue != 2 && lastCardValue != 10)
            {
                winMenu.gameObject.SetActive(true);

                int amount = Random.Range(minScore, maxScore + 1);
                SubtractScore(amount);

                winner = true;
            }

            if (winner)
            {
                winText.text = playerName + " Wins!";
            }
        }
    }

    public void RestartGame()
    {
        int sceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(sceneIndex);
    }
    #endregion

    #region Score
    void AddScore(int amount)
    {
        score += amount;
        scoreText.text = "Score: " + score.ToString();
        addedScoreText.text = "+" + amount.ToString();

        addedScoreText.color = Color.green;

        if (score > PlayerPrefs.GetInt("Highscore"))
        {
            PlayerPrefs.SetInt("Highscore", score);
        }

        int gamesWon = PlayerPrefs.GetInt("GamesWon") + 1;
        PlayerPrefs.SetInt("GamesWon", gamesWon);

        PlayerPrefs.SetInt("Score", score);
    }

    void SubtractScore(int amount)
    {
        if (score <= 0)
        {
            score = 0; 
            amount = 0;
        }

        if (amount > score)
        {
            amount = score;
        }

        score -= amount;

        scoreText.text = "Score: " + score.ToString();
        addedScoreText.text = "-" + amount.ToString();

        addedScoreText.color = Color.red;

        int gamesLost = PlayerPrefs.GetInt("GamesLost") + 1;
        PlayerPrefs.SetInt("GamesLost", gamesLost);

        PlayerPrefs.SetInt("Score", score);
    }
    #endregion

    #region Gets
    public int GetPlayerCount()
    {
        return playerCount;
    }

    public int GetTurn()
    {
        return turn;
    }

    public bool GetWinner()
    {
        return winner;
    }

    public bool GetGameHasStarted()
    {
        return gameHasStarted;
    }
    #endregion
}
