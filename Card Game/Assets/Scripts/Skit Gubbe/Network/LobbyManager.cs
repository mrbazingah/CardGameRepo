using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] NetworkRunner runnerPrefab;
    [SerializeField] TMP_Text roomCodeText;
    [SerializeField] Transform playerProfilesParent;
    [SerializeField] NetworkObject playerProfilePrefab;
    [SerializeField] Vector2 spawnPos;
    [SerializeField] Vector2 spawnOffset;

    NetworkRunner runner;
    Dictionary<PlayerRef, GameObject> playerProfiles = new();

    async void Start()
    {
        if (roomCodeText != null)
            roomCodeText.text = $"Room Code: {GameSession.RoomCode}";
        else
            Debug.LogWarning("RoomCodeText UI element is not assigned!");

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
            player // owner
        );

        profileNetObj.transform.SetParent(playerProfilesParent, false);
        playerProfiles[player] = profileNetObj.gameObject;

        // Now, ask the local player to set their display name if this profile belongs to them
        if (player == runner.LocalPlayer)
        {
            var profileNetwork = profileNetObj.GetComponent<PlayerProfileNetwork>();
            if (profileNetwork != null)
            {
                // Call the RPC to set the display name on the server
                profileNetwork.RPC_SendDisplayName(GameSession.DisplayName);
            }
        }
    }


    public void LeaveRoom()
    {
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
    }

    // INetworkRunnerCallbacks methods (empty implementations for now)
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
