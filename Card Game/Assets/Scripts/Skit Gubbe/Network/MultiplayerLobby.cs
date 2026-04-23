using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;

public class MultiplayerLobby : MonoBehaviour
{
    public static MultiplayerLobby Instance { get; private set; }

    [SerializeField] float heartbeatTimer = 15;

    public string RoomCode { get; private set; }
    public int PlayerCount { get; private set; }

    bool isHost;
    bool readyToQuit;

    SceneLoader SceneLoader;
    Lobby hostLobby;
    
    async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Application.wantsToQuit += OnWantsToQuit;

        await UnityServices.InitializeAsync();

        AuthenticationService.Instance.SignedIn += () =>
        {
            Debug.Log("Signed in " + AuthenticationService.Instance.PlayerId);
        };

        await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    void OnDestroy()
    {
        Application.wantsToQuit -= OnWantsToQuit;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    bool OnWantsToQuit()
    {
        if (readyToQuit || hostLobby == null) { return true; }

        _ = QuitCleanup();

        return false;
    }

    async Task QuitCleanup()
    {
        await LeaveLobby();
        readyToQuit = true;
        Application.Quit();
    }

    protected void Start()
    {
        SceneLoader = FindAnyObjectByType<SceneLoader>();
    }

    public async Task LeaveLobby()
    {
        if (hostLobby == null) return;

        StopAllCoroutines();

        try
        {
            if (isHost)
            {
                await LobbyService.Instance.DeleteLobbyAsync(hostLobby.Id);
            }
            else
            {
                await LobbyService.Instance.RemovePlayerAsync(hostLobby.Id, AuthenticationService.Instance.PlayerId);
            }
                
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
        finally
        {
            hostLobby = null;
            RoomCode = null;
            PlayerCount = 0;

            gameObject.SetActive(false);
            Destroy(gameObject);

            Instance = null;

            SceneLoader.LoadScene("Start Scene");
        }
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
            isHost = true;
            RoomCode = roomCode;
            PlayerCount = lobby.Players.Count;

            StartCoroutine(HandleLobbyHeartbeat());
            StartCoroutine(HandleLobbyPollUpdate());

            Debug.Log("Created Lobby! " + lobby.Name + " " + lobby.MaxPlayers + " Room Code: " + roomCode);

            SceneLoader.LoadScene("Multiplayer Lobby Scene");
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    protected async Task<bool> JoinLobby(string roomCode)
    {
        try
        {
            QueryLobbiesOptions queryOptions = new QueryLobbiesOptions
            {
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.S1, roomCode, QueryFilter.OpOptions.EQ)
                }
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);

            if (response.Results.Count == 0)
            {
                Debug.Log("No lobby found with code: " + roomCode);
                return false;
            }

            if (response.Results[0].AvailableSlots == 0)
            {
                Debug.Log("Lobby is full: " + roomCode);
                return false;
            }

            Lobby lobby = await LobbyService.Instance.JoinLobbyByIdAsync(response.Results[0].Id);

            hostLobby = lobby;
            isHost = false;
            RoomCode = roomCode;
            PlayerCount = lobby.Players.Count;

            StartCoroutine(HandleLobbyPollUpdate());

            Debug.Log("Joined lobby: " + lobby.Name + " Code: " + roomCode);

            SceneManager.LoadScene("Multiplayer Lobby Scene");
            return true;
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            return false;
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

    IEnumerator HandleLobbyPollUpdate()
    {
        while (hostLobby != null)
        {
            yield return new WaitForSeconds(1.5f);

            var pollTask = LobbyService.Instance.GetLobbyAsync(hostLobby.Id);
            yield return new WaitUntil(() => pollTask.IsCompleted);

            if (!pollTask.IsFaulted)
            {
                hostLobby = pollTask.Result;
                PlayerCount = hostLobby.Players.Count;
            }
        }
    }

    /*
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
    */
}