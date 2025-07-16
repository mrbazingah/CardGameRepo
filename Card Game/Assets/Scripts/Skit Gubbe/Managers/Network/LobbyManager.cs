using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public NetworkRunner runnerPrefab;
    private NetworkRunner runner;

    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private Transform playerProfilesParent;
    [SerializeField] private GameObject playerProfilePrefab;
    [SerializeField] private Vector2 spawnPos;
    [SerializeField] private Vector2 spawnOffset;

    private Dictionary<PlayerRef, PlayerProfileNetwork> playerProfiles = new();

    private async void Start()
    {
        // Display room code
        if (roomCodeText != null)
            roomCodeText.text = $"Room Code: {GameSession.RoomCode}";
        else
            Debug.LogWarning("RoomCodeText UI element is not assigned!");

        // Set up and start runner
        runner = Instantiate(runnerPrefab);
        DontDestroyOnLoad(runner.gameObject);
        runner.ProvideInput = true;
        runner.AddCallbacks(this);

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
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player joined: {player}");

        if (playerProfilePrefab == null || playerProfilesParent == null)
        {
            Debug.LogWarning("Player profile prefab or parent not assigned!");
            return;
        }

        Vector2 spawnPosition = spawnPos;
        if (playerProfiles.Count > 0)
        {
            spawnPosition += spawnOffset * playerProfiles.Count;
        }

        PlayerProfileNetwork profile = runner.Spawn(playerProfilePrefab, spawnPosition, Quaternion.identity, player).GetComponent<PlayerProfileNetwork>();

        string displayName = PlayerPrefs.GetString("DisplayName", $"Player {player.PlayerId}");
        if (player == runner.LocalPlayer && GameSession.IsHost)
            displayName += " (Host)";

        profile.DisplayName = displayName;

        profile.transform.SetParent(playerProfilesParent, false);

        playerProfiles[player] = profile;
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player left: {player}");

        if (playerProfiles.TryGetValue(player, out var profile))
        {
            if (runner.IsServer)
            {
                runner.Despawn(profile.GetComponent<NetworkObject>());
            }
            playerProfiles.Remove(player);
        }
    }

    // === Fully implement all INetworkRunnerCallbacks methods ===

    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
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
