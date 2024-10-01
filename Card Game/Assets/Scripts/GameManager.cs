using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] int playerCount = 1;
    [SerializeField] int turn;
    [SerializeField] List<PlayerHand> allPlayers;
    [SerializeField] GameObject playerPrefab;

    void Start()
    {
        turn = playerCount;
    }

    public void AsignStartPlayer()
    {
        int lowestPLayer = 20;

        for (int i = 0; i < allPlayers.Count; i++)
        {
            PlayerHand player = FindFirstObjectByType<PlayerHand>();

            for (int ii = 0; ii < 3; ii++)
            {
                if (player.GetCards()[ii].GetComponent<Card>().GetValue() < lowestPLayer)
                {
                    lowestPLayer = i;
                }
            }
        }

        allPlayers[lowestPLayer].SetTurn(true);
    }

    public bool NextTurn(List<GameObject> hand, GameObject lastPlayed)
    {
        int index = 0;

        for (int i = 0; i < hand.Count; i++)
        {
            int value = hand[i].GetComponent<Card>().GetValue();
            if (value != lastPlayed.GetComponent<Card>().GetValue() || value != 2 || value != 10)
            {
                index++;
            }
        }

        bool isTurn;
        if (index > 0)
        {
            isTurn = false;
        }
        else
        {
            isTurn = true;
        }

        return isTurn;
    }

    public int GetPlayerCount()
    {
        return playerCount;
    }
}
