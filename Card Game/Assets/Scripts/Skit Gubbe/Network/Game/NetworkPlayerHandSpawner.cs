using Fusion;
using Fusion.Sockets;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Spawns NetworkPlayerHand objects for each player when they join.
/// This ensures each player gets their own hand at the bottom of the screen.
/// 
/// SETUP: Place this in your Multiplayer Scene and assign the NetworkPlayerHand prefab.
/// </summary>
public class NetworkPlayerHandSpawner : NetworkBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] NetworkPrefabRef playerHandPrefab;
    [SerializeField] Vector2 playerHandPosition = new Vector2(0, -4); // Bottom of screen

    bool hasSpawnedInitialHands = false;

    public override void Spawned()
    {
        Debug.Log($"NetworkPlayerHandSpawner: Spawned. HasStateAuthority: {Object.HasStateAuthority}, IsServer: {Runner?.IsServer}");
        
        if (Runner != null)
        {
            Runner.AddCallbacks(this);
        }

        TrySpawnInitialHands();
    }

    public override void FixedUpdateNetwork()
    {
        // Keep trying to spawn hands until we have state authority and have spawned them
        if (!hasSpawnedInitialHands)
        {
            TrySpawnInitialHands();
        }
    }

    void TrySpawnInitialHands()
    {
        // Check if we're the server/host (state authority for scene objects)
        if (Runner == null || !Runner.IsServer)
        {
            return;
        }

        // Spawn a hand for each existing player
        var players = Runner.ActivePlayers.ToList();
        
        if (players.Count == 0)
        {
            return; // No players yet, wait
        }

        Debug.Log($"NetworkPlayerHandSpawner: Spawning hands for {players.Count} players (IsServer: {Runner.IsServer})");
        
        foreach (var player in players)
        {
            SpawnPlayerHand(player);
        }

        hasSpawnedInitialHands = true;
    }

    void SpawnPlayerHand(PlayerRef player)
    {
        // Only server can spawn
        if (Runner == null || !Runner.IsServer)
        {
            return;
        }

        // Check if prefab is assigned
        if (playerHandPrefab == default)
        {
            Debug.LogError("NetworkPlayerHandSpawner: playerHandPrefab is not assigned!");
            return;
        }

        // Check if hand already exists for this player
        var existingHands = FindObjectsOfType<NetworkPlayerHand>();
        if (existingHands.Any(h => h.Object != null && h.Object.InputAuthority == player))
        {
            Debug.Log($"NetworkPlayerHand already exists for player {player}");
            return; // Hand already exists
        }

        Debug.Log($"NetworkPlayerHandSpawner: Spawning hand for player {player}");

        // Spawn hand with player's input authority (so only they can see/interact with it)
        var handObj = Runner.Spawn(
            playerHandPrefab,
            (Vector3)playerHandPosition,
            Quaternion.identity,
            player
        );

        if (handObj != null)
        {
            Debug.Log($"NetworkPlayerHandSpawner: Successfully spawned NetworkPlayerHand for player {player} at position {playerHandPosition}");
        }
        else
        {
            Debug.LogError($"NetworkPlayerHandSpawner: Failed to spawn hand for player {player}");
        }
    }

    // INetworkRunnerCallbacks implementation
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // Use IsServer instead of HasStateAuthority for scene objects
        if (runner.IsServer)
        {
            Debug.Log($"NetworkPlayerHandSpawner: OnPlayerJoined - Player {player}");
            SpawnPlayerHand(player);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}




