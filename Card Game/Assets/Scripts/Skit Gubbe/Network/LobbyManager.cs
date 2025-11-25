using Fusion;
using Fusion.Sockets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] NetworkRunner runnerPrefab;
    [SerializeField] TMP_Text roomCodeText;
    [SerializeField] Transform playerProfilesParent;
    [SerializeField] NetworkObject playerProfilePrefab;
    [SerializeField] Vector2 spawnPos;
    [SerializeField] Vector2 spawnOffset;
    [SerializeField] GameObject startGameButton; // only for host

    [SerializeField] GameObject loadingPanel;
    [SerializeField] float loadingSwitchDelay;
    [SerializeField] TMP_Text playerCountText;    // shows current count
    [SerializeField] Button readyButton;          // button to toggle ready state

    public static Transform ProfilesParent;
    public static bool ServerShutdown = false;

    List<int> playersReady = new List<int>();

    NetworkRunner runner;
    Dictionary<PlayerRef, GameObject> playerProfiles = new();

    void Awake()
    {
        ProfilesParent = playerProfilesParent;
    }

    async void Start()
    {
        loadingPanel.SetActive(true);
        StartCoroutine(LoadingProcess());

        if (roomCodeText != null)
            roomCodeText.text = $"Room Code: {GameSession.RoomCode}";
        else
            Debug.LogWarning("RoomCodeText UI element is not assigned!");

        runner = Instantiate(runnerPrefab);
        DontDestroyOnLoad(runner.gameObject);
        runner.ProvideInput = true;
        runner.AddCallbacks(this);

        // Ensure scene manager is set up for scene loading
        if (runner.gameObject.GetComponent<NetworkSceneManagerDefault>() == null)
        {
            runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        }

        if (string.IsNullOrEmpty(GameSession.RoomCode))
        {
            Debug.LogError("No room code set! Cannot start game.");
            return;
        }

        var startGameArgs = new StartGameArgs
        {
            SessionName = GameSession.RoomCode,
            Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
            GameMode = GameSession.IsHost ? GameMode.Host : GameMode.Client
        };

        StartGameResult result = await runner.StartGame(startGameArgs);

        if (!result.Ok)
        {
            Debug.LogWarning($"Failed to start/join game with code {GameSession.RoomCode}. Returning to Start scene.");
            SceneManager.LoadScene("Start Scene");
        }

        startGameButton.SetActive(false);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        loadingPanel.SetActive(false);

        Debug.Log($"Player joined: {player}");

        if (!runner.IsServer)
            return;

        if (playerProfiles.ContainsKey(player))
        {
            Debug.Log($"Player {player} profile already exists.");
            return;
        }

        int index = playerProfiles.Count;
        Vector2 positionOffset = spawnOffset * index;
        Vector2 spawnPositionAdjusted = spawnPos + positionOffset;

        NetworkObject profileNetObj = runner.Spawn(
            playerProfilePrefab,
            (Vector3)spawnPositionAdjusted,
            Quaternion.identity,
            player
        );

        profileNetObj.transform.SetParent(playerProfilesParent, false);
        var net = profileNetObj.GetComponent<PlayerProfileNetwork>();
        net.SpawnPosition = spawnPositionAdjusted;
        net.IsHost = (player == runner.LocalPlayer);

        playerProfiles[player] = profileNetObj.gameObject;

        // update player count UI
        if (playerCountText != null)
            playerCountText.text = playerProfiles.Count + "/2";

        // hook up ready button for the local player
        if (player == runner.LocalPlayer && readyButton != null)
        {
            readyButton.onClick.RemoveAllListeners();
            readyButton.onClick.AddListener(net.ReadyPLayer);
            readyButton.interactable = true;
        }
    }

    public void OnPlayerReady()
    {
        for (int i = 0; i < playerProfiles.Count; i++)
        {
            if (playerProfiles.ElementAt(i).Value.GetComponent<PlayerProfileNetwork>().IsReady)
            {
                Debug.Log($"Player {i} is ready.");
                playersReady.Add(1);
            }
            else
            {
                Debug.Log($"Player {i} is not ready.");
                playersReady.RemoveAt(0);
            }

            if (playersReady.Count == 2)
            {
                Debug.Log("All Players are ready");
                for (int j = 0; j < playerProfiles.Count; j++)
                {
                    startGameButton.SetActive(true);
                }
            }
        }
    }

    public void OnStartGame()
    {
        if (runner == null || !runner.IsServer)
            return;

        // Ensure we have at least 2 players
        if (runner.ActivePlayers.Count() < 2)
        {
            Debug.LogWarning("Cannot start game: Need at least 2 players");
            return;
        }

        // Load the multiplayer scene for everyone
        // Scene index 4 is the Multiplayer Scene (0=Start, 1=Lobby, 2=Singleplayer Lobby, 3=Singleplayer, 4=Multiplayer)
        runner.LoadScene(SceneRef.FromIndex(4));
    }

    IEnumerator LoadingProcess()
    {
        TextMeshProUGUI text = loadingPanel.GetComponentInChildren<TextMeshProUGUI>();

        while (true)
        {
            text.text = "Loading.";

            yield return new WaitForSeconds(loadingSwitchDelay);
            text.text = "Loading..";

            yield return new WaitForSeconds(loadingSwitchDelay);
            text.text = "Loading...";

            yield return new WaitForSeconds(loadingSwitchDelay);
        }
    }

    public async void LeaveRoom()
    {
        if (runner != null)
        {
            await runner.Shutdown();
            Destroy(runner.gameObject);
        }

        GameSession.DisplayName = null;
        GameSession.RoomCode = null;
        SceneManager.LoadScene("Start Scene");
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player left: {player}");

        if (playerProfiles.TryGetValue(player, out var profile))
        {
            Destroy(profile);
            playerProfiles.Remove(player);
        }

        // update player count UI
        if (playerCountText != null)
            playerCountText.text = playerProfiles.Count + "/2";
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"Shutdown detected: {shutdownReason}");

        if (!GameSession.IsHost)
        {
            Debug.Log("Client disconnected. Returning to Start Scene...");

            ServerShutdown = true;
            SceneManager.LoadScene("Start Scene");
        }
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        if (runner.ActivePlayers.Count() >= 2)
        {
            Debug.Log("Connection refused: Lobby full.");
            request.Refuse();
        }
        else
        {
            request.Accept();
        }
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogWarning($"Connection failed: {reason}");
        if (!GameSession.IsHost)
        {
            SceneManager.LoadScene("Start Scene");
        }
    }

    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}