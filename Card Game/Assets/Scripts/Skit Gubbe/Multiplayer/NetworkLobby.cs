using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

// Persistent singleton that manages Unity Relay + Lobby for room-code multiplayer.
// Place on a GameObject in the Start Scene alongside the NetworkManager prefab.
public class NetworkLobby : MonoBehaviour
{
    public static NetworkLobby Instance { get; private set; }

    const string ROOM_CODE_KEY  = "RoomCode";
    const string RELAY_CODE_KEY = "RelayCode";
    const int MAX_PLAYERS    = 8;

    Lobby currentLobby;
    Coroutine heartbeatCoroutine;

    public string RoomCode { get; private set; }
    public bool IsReady  { get; private set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    async void Start()
    {
        await InitUGS();
    }

    async Task InitUGS()
    {
        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            IsReady = true;
        }
        catch (Exception e)
        {
            Debug.LogError("[NetworkLobby] UGS init failed: " + e.Message);
        }
    }

    // Creates a relay allocation + lobby and starts NGO as host.
    // Returns false on failure.
    public async Task<bool> Host()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MAX_PLAYERS - 1);
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            RoomCode = UnityEngine.Random.Range(1000, 9999).ToString();

            CreateLobbyOptions opts = new CreateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    {
                        ROOM_CODE_KEY,
                        new DataObject(DataObject.VisibilityOptions.Public, RoomCode, DataObject.IndexOptions.S1)
                    },
                    {
                        RELAY_CODE_KEY,
                        new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode)
                    }
                }
            };

            currentLobby = await LobbyService.Instance.CreateLobbyAsync("Lobby", MAX_PLAYERS, opts);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, "dtls"));
            NetworkManager.Singleton.StartHost();

            heartbeatCoroutine = StartCoroutine(HeartbeatLoop());

            // Load lobby scene — clients follow automatically via NGO's SceneManager
            NetworkManager.Singleton.SceneManager.LoadScene("Multiplayer Lobby Scene", LoadSceneMode.Single);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[NetworkLobby] Host failed: " + e.Message);
            return false;
        }
    }

    // Finds the lobby by 4-digit room code and joins it.
    // Returns false if the code is wrong or the lobby is full.
    public async Task<bool> Join(string code)
    {
        try
        {
            QueryResponse query = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
            {
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.S1, code, QueryFilter.OpOptions.EQ),
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0",  QueryFilter.OpOptions.GT)
                }
            });

            if (query.Results.Count == 0) return false;

            currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(query.Results[0].Id);

            // Cache room code so the lobby scene can display it
            RoomCode = currentLobby.Data[ROOM_CODE_KEY].Value;

            string relayJoinCode  = currentLobby.Data[RELAY_CODE_KEY].Value;
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

            NetworkManager.Singleton.StartClient();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[NetworkLobby] Join failed: " + e.Message);
            return false;
        }
    }

    public void Disconnect()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);
        _ = DeleteLobby();
        RoomCode = null;
    }

    IEnumerator HeartbeatLoop()
    {
        while (currentLobby != null)
        {
            yield return new WaitForSeconds(15f);
            LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
        }
    }

    async Task DeleteLobby()
    {
        if (currentLobby == null) return;
        try { await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id); }
        catch { }
        currentLobby = null;
    }

    void OnDestroy()
    {
        if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);
        _ = DeleteLobby();
    }
}
