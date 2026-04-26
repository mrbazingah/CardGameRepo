using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance { get; private set; }

    public bool IsConnected { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    async void Start()
    {
        if (NetworkLobby.Instance == null)
        {
            Debug.LogError("RelayManager: NetworkLobby not found. Cannot start session.");
            return;
        }

        try
        {
            if (NetworkLobby.Instance.IsHost)
                await StartAsHost();
            else
                await StartAsClient();

            IsConnected = true;
            RegisterNetworkCallbacks();
        }
        catch (Exception e)
        {
            Debug.LogError("RelayManager failed to connect: " + e.Message);
        }
    }

    void OnDestroy()
    {
        UnregisterNetworkCallbacks();

        if (Instance == this)
            Instance = null;
    }

    // -------------------------------------------------------------------------
    // Connect
    // -------------------------------------------------------------------------

    async Task StartAsHost()
    {
        // 1 max connection = 1 guest joining the host
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        Debug.Log("Relay join code created: " + joinCode);

        // Write the join code into the lobby so the guest can read it
        await NetworkLobby.Instance.SetRelayCode(joinCode);

        // Point NGO's transport at the relay server
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

        NetworkManager.Singleton.StartHost();

        Debug.Log("NGO session started as Host.");
    }

    async Task StartAsClient()
    {
        string joinCode = await WaitForRelayCode();

        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

        // Point NGO's transport at the same relay server the host is on
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

        NetworkManager.Singleton.StartClient();

        Debug.Log("NGO session started as Client.");
    }

    async Task<string> WaitForRelayCode()
    {
        string lobbyId = NetworkLobby.Instance?.LobbyId;
        if (string.IsNullOrEmpty(lobbyId))
            throw new Exception("RelayManager: No lobby ID available to poll for relay code.");

        const float timeout = 30f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            try
            {
                var lobby = await LobbyService.Instance.GetLobbyAsync(lobbyId);

                if (lobby.Data != null &&
                    lobby.Data.TryGetValue("RelayCode", out var entry) &&
                    !string.IsNullOrEmpty(entry.Value))
                {
                    Debug.Log("Relay code received: " + entry.Value);
                    return entry.Value;
                }
            }
            catch (LobbyServiceException e)
            {
                Debug.LogWarning("RelayManager: Lobby poll failed: " + e.Reason);
            }

            await Task.Delay(2000);
            elapsed += 2f;
        }

        throw new TimeoutException("RelayManager: Timed out waiting for relay code from host.");
    }

    // -------------------------------------------------------------------------
    // Disconnect handling — mirrors NetworkLobby's poll failure logic
    // -------------------------------------------------------------------------

    void RegisterNetworkCallbacks()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
    }

    void UnregisterNetworkCallbacks()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
    }

    // Fires on the client when it loses connection to the host
    void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer) return;

        ShutdownAndReturnToStart(LobbyDisconnectReason.HostLeft);
    }

    // Fires on either side when the underlying transport fails
    void OnTransportFailure()
    {
        ShutdownAndReturnToStart(LobbyDisconnectReason.ConnectionLost);
    }

    void ShutdownAndReturnToStart(LobbyDisconnectReason reason)
    {
        UnregisterNetworkCallbacks();

        NetworkLobby.PendingDisconnectReason = reason;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        // Destroy the lobby object without going through LeaveLobby —
        // the lobby is already gone once the game session has started
        if (NetworkLobby.Instance != null)
        {
            NetworkLobby.Instance.StopAllCoroutines();
            Destroy(NetworkLobby.Instance.gameObject);
        }

        SceneManager.LoadScene("Start Scene");
    }
}
