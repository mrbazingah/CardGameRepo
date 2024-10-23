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

    bool winner;

    PlayerHand playerHand;
    AIHand aiHand;
    CardGenrator cardGenrator;

    void Awake()
    {
        cardGenrator = FindFirstObjectByType<CardGenrator>();

        if (cardGenrator.GetAIs().Count == 0)
        {
            aiHand = FindFirstObjectByType<AIHand>();
            playerHand = FindFirstObjectByType<PlayerHand>();
        }
    }

    void Start()
    {
        winText.gameObject.SetActive(false);
    }

    public void AsignStartPlayer()
    {
        List<GameObject> playerCards;
        List<GameObject> aiCards;

        if (playerHand != null)
        {
            playerCards = playerHand.GetCards();
            aiCards = aiHand.GetCards();
        }
        else
        {
            playerCards = cardGenrator.GetAIs()[0].GetCards();
            aiCards = cardGenrator.GetAIs()[1].GetCards();
        }

        int playerLowest = 20;

        for (int i = 0; i < playerCards.Count; i++)
        {
            int value = playerCards[i].GetComponent<Card>().GetValue();
            if (value < playerLowest && value != 2 && value != 10)
            {
                playerLowest = value;
            }
        }

        int aiLowest = 20;

        for (int i = 0; i < aiCards.Count; i++)
        {
            int value = aiCards[i].GetComponent<Card>().GetValue();
            if (value < aiLowest && value != 2 && value != 10)
            {
                aiLowest = value;
            }
        }

        if (playerHand != null)
        {
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
        else
        {
            if (playerLowest > aiLowest)
            {
                cardGenrator.GetAIs()[0].SetTurnNumber(1);
                cardGenrator.GetAIs()[1].SetTurnNumber(2);
            }
            else
            {
                cardGenrator.GetAIs()[0].SetTurnNumber(2);
                cardGenrator.GetAIs()[1].SetTurnNumber(1);
            }
        }
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

    public IEnumerator ProcessWin()
    {
        yield return new WaitForSeconds(0.001f);

        if (!winner && playerHand != null)
        {
            if (playerHand.GetCards().Count == 0)
            {
                winText.gameObject.SetActive(true);
                winText.text = "Player Wins!";
                winner = true;
            }
            else if (aiHand.GetCards().Count == 0)
            {
                winText.gameObject.SetActive(true);
                winText.text = "Gaper Bingzoid Wins!";
                winner = true;
            }
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
}
