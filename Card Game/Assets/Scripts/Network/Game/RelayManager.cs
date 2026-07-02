using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

// Lives in the Multiplayer Lobby Scene. Persists into the game scene (DontDestroyOnLoad)
// so NetworkCardGenerator can read CardsPerPlayer / CanChance.
//
// Option 1 flow:
//   Host:   Start Game button -> BeginHostGame() -> relay allocation -> publish code to lobby
//           -> StartHost -> wait for client connect -> NGO SceneManager.LoadScene(game scene).
//   Client: lobby poll surfaces the relay code -> join allocation -> StartClient
//           -> NGO scene sync automatically brings the client into the game scene.
public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance { get; private set; }

    [SerializeField] string gameSceneName = "Multiplayer Scene";

    public bool IsConnected { get; private set; }
    public int CardsPerPlayer { get; private set; }
    public bool CanChance { get; private set; }

    bool connectionStarted;
    bool sceneLoadTriggered;

    string errorMessage = "";
    string statusMessage = "idle";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Client watches the lobby for the relay code the host publishes.
        if (NetworkLobby.Instance != null && !NetworkLobby.Instance.IsHost)
        {
            NetworkLobby.Instance.OnLobbyUpdated += OnLobbyUpdated;
        }
    }

    void OnDestroy()
    {
        if (NetworkLobby.Instance != null)
            NetworkLobby.Instance.OnLobbyUpdated -= OnLobbyUpdated;

        UnregisterNetworkCallbacks();

        if (Instance == this)
            Instance = null;
    }

    // -------------------------------------------------------------------------
    // Host path — called by the Start Game button via NetworkLobbyManager
    // -------------------------------------------------------------------------

    public void BeginHostGame()
    {
        if (connectionStarted) return;
        if (NetworkLobby.Instance == null || !NetworkLobby.Instance.IsHost) return;

        connectionStarted = true;
        _ = ConnectAsHost();
    }

    async Task ConnectAsHost()
    {
        try
        {
            ReadLobbySettings();

            statusMessage = "creating relay allocation";
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log("[RM] Relay join code created: " + joinCode);

            statusMessage = "publishing relay code";
            await NetworkLobby.Instance.SetRelayCode(joinCode);

            statusMessage = "starting host";
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

            RegisterNetworkCallbacks();

            bool started = NetworkManager.Singleton.StartHost();
            Debug.Log($"[RM] StartHost returned {started} — IsListening={NetworkManager.Singleton.IsListening}");
            if (!started || !NetworkManager.Singleton.IsListening)
                throw new Exception("StartHost failed — NGO shut the session down during startup.");

            IsConnected = true;
            statusMessage = "waiting for opponent to connect";
            // Scene load happens in OnClientConnected when the second player arrives.
        }
        catch (Exception e)
        {
            Debug.LogError("[RM] Host connect failed: " + e.Message + "\n" + e.StackTrace);
            errorMessage = "[at: " + statusMessage + "] " + e.Message;
            connectionStarted = false;
        }
    }

    // -------------------------------------------------------------------------
    // Client path — triggered when the lobby poll surfaces the relay code
    // -------------------------------------------------------------------------

    void OnLobbyUpdated()
    {
        if (connectionStarted) return;

        string code = NetworkLobby.Instance != null ? NetworkLobby.Instance.LobbyRelayCode : null;
        if (string.IsNullOrEmpty(code)) return;

        connectionStarted = true;
        _ = ConnectAsClient(code);
    }

    async Task ConnectAsClient(string joinCode)
    {
        try
        {
            ReadLobbySettings();

            statusMessage = "joining relay allocation code=" + joinCode;
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            statusMessage = "starting client";
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

            RegisterNetworkCallbacks();

            bool started = NetworkManager.Singleton.StartClient();
            Debug.Log($"[RM] StartClient returned {started}");
            if (!started)
                throw new Exception("StartClient failed — NGO refused to start the client.");

            statusMessage = "connecting to host";
            // From here NGO handles it: connect -> scene sync into the game scene.
        }
        catch (Exception e)
        {
            Debug.LogError("[RM] Client connect failed: " + e.Message + "\n" + e.StackTrace);
            errorMessage = "[at: " + statusMessage + "] " + e.Message;
            connectionStarted = false;
        }
    }

    // -------------------------------------------------------------------------
    // Shared
    // -------------------------------------------------------------------------

    void ReadLobbySettings()
    {
        statusMessage = "reading lobby data";
        CardsPerPlayer = NetworkLobby.Instance.LobbyCardsPerPlayer;
        CanChance = NetworkLobby.Instance.LobbyCanChance;
    }

    void CleanupLobbyObject()
    {
        if (NetworkLobby.Instance == null) return;

        NetworkLobby.Instance.OnLobbyUpdated -= OnLobbyUpdated;
        NetworkLobby.Instance.StopAllCoroutines();
        Destroy(NetworkLobby.Instance.gameObject);
    }

    // -------------------------------------------------------------------------
    // NGO callbacks
    // -------------------------------------------------------------------------

    void RegisterNetworkCallbacks()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
    }

    void UnregisterNetworkCallbacks()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
    }

    void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[RM] Client connected id={clientId} count={NetworkManager.Singleton.ConnectedClientsIds.Count}");

        if (NetworkManager.Singleton.IsServer)
        {
            // Second player arrived — the host drives BOTH players into the game scene.
            if (!sceneLoadTriggered && NetworkManager.Singleton.ConnectedClientsIds.Count >= 2)
            {
                sceneLoadTriggered = true;
                statusMessage = "loading game scene";
                CleanupLobbyObject();
                NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            }
        }
        else if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // Client's own connection confirmed. NGO will move it to the game scene.
            IsConnected = true;
            statusMessage = "connected — waiting for scene sync";
            CleanupLobbyObject();
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer) return;
        ShutdownAndReturnToStart(LobbyDisconnectReason.HostLeft);
    }

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

        CleanupLobbyObject();

        // NGO is shut down at this point, so a plain scene load is correct here.
        SceneManager.LoadScene("Start Scene");
    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, Screen.width - 20, 40), "Status: " + statusMessage);
        if (!string.IsNullOrEmpty(errorMessage)) { GUI.Label(new Rect(10, 50, Screen.width - 20, 200), "RELAY ERROR: " + errorMessage); }
    }
}