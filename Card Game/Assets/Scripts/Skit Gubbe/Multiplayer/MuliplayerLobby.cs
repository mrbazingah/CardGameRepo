using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;

public class MuliplayerLobby : MonoBehaviour
{
    [SerializeField] float heartbeatTimer = 15;

    Lobby hostLobby;

    async void Start()
    {
        await UnityServices.InitializeAsync();

        AuthenticationService.Instance.SignedIn += () =>
        {
            Debug.Log("Signed in " + AuthenticationService.Instance.PlayerId);
        };

        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        CreateLobby();
        ListLobbies();
    }

    async void CreateLobby()
    {
        try
        {
            string lobbyName = "My Lobby";
            int maxPlayers = 2;
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers);

            hostLobby = lobby;

            StartCoroutine(HandleLobbyHeartbeat());

            Debug.Log("Ceated Lobby! " + lobby.Name + " " + lobby.MaxPlayers);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    IEnumerator HandleLobbyHeartbeat()
    {
        while (true)
        {
            if (hostLobby != null)
            {
                var heartbeatTask = LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id);
                yield return new WaitUntil(() => heartbeatTask.IsCompleted);

                if (heartbeatTask.IsFaulted) { Debug.LogError("Heartbeat failed: " + heartbeatTask.Exception); }
            }

            yield return new WaitForSeconds(heartbeatTimer);
        }
    }

    async Task ListLobbies()
    {
        try
        {
            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync();

            Debug.Log("Lobbies found: " + queryResponse.Results.Count);
            foreach (Lobby lobby in queryResponse.Results)
            {
                Debug.Log("Lobby Name: " + lobby.Name + " Players: " + lobby.Players.Count);
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }
}