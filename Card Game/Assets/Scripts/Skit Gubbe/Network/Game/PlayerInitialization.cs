using UnityEngine;
using Unity.Netcode;

public class PlayerInitialization : NetworkBehaviour
{
    const int requiredPlayers = 2;

    NetworkCardGenerator cardGenerator;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    void OnClientConnected(ulong clientId)
    {
        int connectedPlayers = NetworkManager.Singleton.ConnectedClients.Count;

        if (connectedPlayers == requiredPlayers)
        {
            AllPlayersConnected();
        }
    }

    void AllPlayersConnected()
    {
        Debug.Log("Both players connected!");
        cardGenerator.DealPlayerCards();
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null && IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }
}
