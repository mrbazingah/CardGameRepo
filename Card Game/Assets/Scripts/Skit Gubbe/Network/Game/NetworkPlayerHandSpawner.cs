using Fusion;
using UnityEngine;
using System.Linq;

/// <summary>
/// Spawns NetworkPlayerHand objects for each player when they join.
/// This ensures each player gets their own hand at the bottom of the screen.
/// </summary>
public class NetworkPlayerHandSpawner : NetworkBehaviour
{
    [SerializeField] NetworkPrefabRef playerHandPrefab;
    [SerializeField] Vector2 playerHandPosition = new Vector2(0, -4); // Bottom of screen
    [SerializeField] Vector2 opponentHandPosition = new Vector2(0, 4); // Top of screen (for reference)

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        // Spawn a hand for each player
        var players = Runner.ActivePlayers.ToList();
        foreach (var player in players)
        {
            SpawnPlayerHand(player);
        }
    }

    void SpawnPlayerHand(PlayerRef player)
    {
        // Check if hand already exists for this player
        var existingHands = FindObjectsOfType<NetworkPlayerHand>();
        if (existingHands.Any(h => h.Object != null && h.Object.InputAuthority == player))
        {
            return; // Hand already exists
        }

        // Spawn hand with player's input authority (so only they can see/interact with it)
        var handObj = Runner.Spawn(
            playerHandPrefab,
            (Vector3)playerHandPosition,
            Quaternion.identity,
            player
        );

        Debug.Log($"Spawned NetworkPlayerHand for player {player} at position {playerHandPosition}");
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (Object.HasStateAuthority)
        {
            SpawnPlayerHand(player);
        }
    }
}

