using Fusion;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// IMPORTANT: NetworkObjects must be SPAWNED, not manually placed!
/// 
/// This script finds manually placed NetworkPlayerHand GameObjects (without NetworkObject component),
/// and spawns NetworkObject versions of them with the correct InputAuthority.
/// 
/// ALTERNATIVE: Use NetworkPlayerHandSpawner with a prefab instead (recommended).
/// </summary>
public class NetworkPlayerHandAssigner : NetworkBehaviour, INetworkRunnerCallbacks
{
    [Header("Manual Setup - Use Prefab Instead!")]
    [Tooltip("If you manually placed NetworkPlayerHand objects, create a prefab from one and use NetworkPlayerHandSpawner instead.")]
    [SerializeField] NetworkPrefabRef playerHandPrefab;
    [SerializeField] Vector2 playerHandPosition = new Vector2(0, -4);
    
    Dictionary<PlayerRef, NetworkPlayerHand> assignedHands = new Dictionary<PlayerRef, NetworkPlayerHand>();

    public override void Spawned()
    {
        if (Runner != null)
        {
            Runner.AddCallbacks(this);
        }

        if (!Object.HasStateAuthority)
            return;

        // Spawn hands for existing players
        var players = Runner.ActivePlayers.ToList();
        foreach (var player in players)
        {
            SpawnHandForPlayer(player);
        }
    }

    void SpawnHandForPlayer(PlayerRef player)
    {
        // Skip if already assigned
        if (assignedHands.ContainsKey(player))
        {
            return;
        }

        if (playerHandPrefab == null || playerHandPrefab == NetworkPrefabRef.Empty)
        {
            Debug.LogError("NetworkPlayerHandAssigner: playerHandPrefab is not assigned! Please assign a NetworkPlayerHand prefab.");
            return;
        }

        // Spawn hand with player's input authority
        var handObj = Runner.Spawn(
            playerHandPrefab,
            (Vector3)playerHandPosition,
            Quaternion.identity,
            player
        );

        var hand = handObj.GetComponent<NetworkPlayerHand>();
        if (hand != null)
        {
            assignedHands[player] = hand;
            Debug.Log($"Spawned NetworkPlayerHand for player {player} at position {playerHandPosition}");
        }
        else
        {
            Debug.LogError($"Spawned object doesn't have NetworkPlayerHand component!");
        }
    }

    // INetworkRunnerCallbacks implementation
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (Object.HasStateAuthority)
        {
            AssignHandToPlayer(player);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (assignedHands.ContainsKey(player))
        {
            assignedHands.Remove(player);
        }
    }

    public void onInput(NetworkRunner runner, NetworkInputData data) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInputData data) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, System.ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
}

