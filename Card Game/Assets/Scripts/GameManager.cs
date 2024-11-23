using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] int playerCount = 1;
    [SerializeField] int turn = 1;
    [SerializeField] TextMeshProUGUI winText;
    [SerializeField] GameObject pauseMenu;

    bool gameHasStarted;
    bool winner;
    bool isOpen;

    PlayerHand playerHand;
    AIHand aiHand;

    void Awake()
    {
        aiHand = FindFirstObjectByType<AIHand>();
        playerHand = FindFirstObjectByType<PlayerHand>();
    }

    void Start()
    {
        winText.gameObject.SetActive(false);
    }

    public void StartGame(GameObject button)
    {
        AsignStartPlayer();
        button.SetActive(false);
        Destroy(button);
        gameHasStarted = true;
    }

    public void AsignStartPlayer()
    {
        List<GameObject> playerCards = playerHand.GetCards();
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

        Debug.Log(Time.timeScale.ToString());
    }

    public void CloseMenu()
    {
        Time.timeScale = 1f;
        isOpen = false;

        Debug.Log(Time.timeScale.ToString());
    }

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

    public IEnumerator ProcessWin(string playerName)
    {
        yield return new WaitForSeconds(0.001f);

        if (!winner && playerHand != null)
        {
            if (playerHand.GetCards().Count == 0)
            {
                winText.gameObject.SetActive(true);
                winText.transform.position = new Vector2(-winText.transform.position.x, 0f);
                winner = true;
            }
            else if (aiHand.GetCards().Count == 0)
            {
                winText.gameObject.SetActive(true);
                winner = true;
            }

            winText.text = playerName + " Wins!";
        }
    }

    public void RestartGame()
    {
        int sceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(sceneIndex);
    }

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
}
