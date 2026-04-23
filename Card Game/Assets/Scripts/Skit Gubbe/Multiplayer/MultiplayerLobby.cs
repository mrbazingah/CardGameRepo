using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;

public class MultiplayerLobby : MonoBehaviour
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
    }

    protected async Task CreateLobby()
    {
        try
        {
            string lobbyName = "My Lobby";
            int maxPlayers = 2;

            string roomCode = await GenerateUniqueRoomCode();

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "RoomCode", new DataObject(DataObject.VisibilityOptions.Public, roomCode, DataObject.IndexOptions.S1) }
                }
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

            hostLobby = lobby;

            StartCoroutine(HandleLobbyHeartbeat());

            Debug.Log("Created Lobby! " + lobby.Name + " " + lobby.MaxPlayers + " Room Code: " + roomCode);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    async Task<string> GenerateUniqueRoomCode()
    {
        while (true)
        {
            string code = Random.Range(1000, 10000).ToString();

            QueryLobbiesOptions queryOptions = new QueryLobbiesOptions
            {
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.S1, code, QueryFilter.OpOptions.EQ)
                }
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);

            if (response.Results.Count == 0)
                return code;
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