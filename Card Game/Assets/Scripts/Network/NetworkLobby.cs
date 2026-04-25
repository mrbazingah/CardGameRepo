using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public enum LobbyDisconnectReason { None, HostLeft, ConnectionLost, ServiceError }

public class NetworkLobby : MonoBehaviour
{
    public static NetworkLobby Instance { get; private set; }
    public static LobbyDisconnectReason PendingDisconnectReason { get; protected set; }

    public event Action OnLobbyUpdated;

    [SerializeField] float heartbeatTimer = 15;

    public string RoomCode { get; private set; }
    public int PlayerCount { get; private set; }
    public bool IsHost => isHost;
    public string LocalPlayerId => AuthenticationService.Instance?.PlayerId;
    public string HostId => hostLobby?.HostId;
    public IReadOnlyList<Player> Players => hostLobby?.Players;

    public int LobbyCardsPerPlayer => GetLobbyInt("CardsPerPlayer", 3);
    public bool LobbyCanChance => GetLobbyInt("CanChance", 1) == 1;

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

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
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

            if (PendingDisconnectReason == LobbyDisconnectReason.None)
            {
                PendingDisconnectReason = LobbyDisconnectReason.ServiceError;
            }
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

            string displayName = PlayerPrefs.GetString("DisplayName", "Player");

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "DisplayName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, displayName) },
                        { "Ready", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "0") }
                    }
                },
                Data = new Dictionary<string, DataObject>
                {
                    { "RoomCode", new DataObject(DataObject.VisibilityOptions.Public, roomCode, DataObject.IndexOptions.S1) },
                    { "CardsPerPlayer", new DataObject(DataObject.VisibilityOptions.Member, "3") },
                    { "CanChance", new DataObject(DataObject.VisibilityOptions.Member, "1") }
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

            string displayName = PlayerPrefs.GetString("DisplayName", "Player");

            JoinLobbyByIdOptions joinOptions = new JoinLobbyByIdOptions
            {
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "DisplayName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, displayName) },
                        { "Ready", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "0") }
                    }
                }
            };

            Lobby lobby = await LobbyService.Instance.JoinLobbyByIdAsync(response.Results[0].Id, joinOptions);

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

    public async Task SetPlayerReady(bool ready)
    {
        if (hostLobby == null) return;

        try
        {
            hostLobby = await LobbyService.Instance.UpdatePlayerAsync(hostLobby.Id, AuthenticationService.Instance.PlayerId,
                new UpdatePlayerOptions
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "Ready", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ready ? "1" : "0") }
                    }
                });

            OnLobbyUpdated?.Invoke();
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

    IEnumerator HandleLobbyPollUpdate()
    {
        int consecutiveFailures = 0;

        while (hostLobby != null)
        {
            yield return new WaitForSeconds(2f);

            var pollTask = LobbyService.Instance.GetLobbyAsync(hostLobby.Id);
            yield return new WaitUntil(() => pollTask.IsCompleted);

            if (pollTask.IsFaulted)
            {
                var ex = pollTask.Exception?.InnerException as LobbyServiceException;

                if (ex != null && ex.Reason == LobbyExceptionReason.LobbyNotFound)
                {
                    if (!isHost)
                    {
                        PendingDisconnectReason = LobbyDisconnectReason.HostLeft;
                        _ = LeaveLobby();
                        yield break;
                    }
                }

                consecutiveFailures++;
                if (!isHost && consecutiveFailures >= 3)
                {
                    PendingDisconnectReason = LobbyDisconnectReason.ConnectionLost;
                    _ = LeaveLobby();
                    yield break;
                }
                // Transient error - skip and retry next interval
            }
            else
            {
                consecutiveFailures = 0;
                hostLobby = pollTask.Result;
                PlayerCount = hostLobby.Players.Count;
                OnLobbyUpdated?.Invoke();
            }
        }
    }

    public async Task UpdateLobbySettings(int cardsPerPlayer, bool canChance)
    {
        if (!isHost || hostLobby == null) return;

        try
        {
            UpdateLobbyOptions options = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "RoomCode", new DataObject(DataObject.VisibilityOptions.Public, RoomCode, DataObject.IndexOptions.S1) },
                    { "CardsPerPlayer", new DataObject(DataObject.VisibilityOptions.Member, cardsPerPlayer.ToString()) },
                    { "CanChance", new DataObject(DataObject.VisibilityOptions.Member, canChance ? "1" : "0") }
                }
            };

            hostLobby = await LobbyService.Instance.UpdateLobbyAsync(hostLobby.Id, options);
            OnLobbyUpdated?.Invoke();
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    int GetLobbyInt(string key, int fallback)
    {
        if (hostLobby?.Data == null || !hostLobby.Data.ContainsKey(key)) return fallback;
        return int.TryParse(hostLobby.Data[key].Value, out int v) ? v : fallback;
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
