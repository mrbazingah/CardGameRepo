using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] int playerCount = 1;
    [SerializeField] int turn;
    [SerializeField] List<PlayerHand> allPlayers;

    void Start()
    {
        turn = playerCount;
    }

    void CreatePlayers()
    {

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

    public void NextTurn(List<GameObject> hand, GameObject lastPlayed)
    {
        for (int i = 0; i < hand.Count; i++)
        {
            int value = GetComponent<Card>().GetValue();
            if (hand[i] != lastPlayed || value != 2 || value != 10)
            {
                turn++;
                if (turn >= playerCount)
                {
                    turn = 1;
                }
            }
        }
    }

    public int GetPlayerCount()
    {
        return playerCount;
    }
}
