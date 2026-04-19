using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// Place this component on a NetworkObject (requires a NetworkObject component) in
// the Multiplayer Lobby Scene.  It:
//   • Displays the 4-digit room code
//   • Shows each connected player's display name
//   • Lets the host disconnect / go back to the Start Scene
//
// SerializeField setup:
//   RoomCodeText        — TextMeshProUGUI showing the code
//   PlayerListContainer — Transform (e.g. a Vertical Layout Group) for name slots
//   PlayerSlotPrefab    — A simple prefab with a TextMeshProUGUI on its root
//   BackButton          — Button (visible to all, disconnects and returns to menu)
public class MultiplayerLobbyManager : NetworkBehaviour
{
    [Header("Room Code")]
    [SerializeField] TextMeshProUGUI roomCodeText;

    [Header("Player List")]
    [SerializeField] Transform  playerListContainer;
    [SerializeField] GameObject playerSlotPrefab;

    [Header("Navigation")]
    [SerializeField] Button backButton;

    // Server-side name registry: clientId -> display name
    readonly Dictionary<ulong, string> playerNames = new();

    public override void OnNetworkSpawn()
    {
        // Display room code (populated for both host and client by NetworkLobby)
        if (NetworkLobby.Instance != null)
            roomCodeText.text = "Room Code: " + NetworkLobby.Instance.RoomCode;

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback    += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback   += OnClientDisconnected;
        }

        // Every player submits their own display name to the server
        string myName = PlayerPrefs.GetString("DisplayName", "Player");
        SubmitNameServerRpc(myName, default);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback    -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback   -= OnClientDisconnected;
        }
    }

    // ── Server callbacks ───────────────────────────────────────────────────

    void OnClientConnected(ulong clientId)
    {
        // Name arrives via SubmitNameServerRpc; nothing to do here yet
    }

    void OnClientDisconnected(ulong clientId)
    {
        playerNames.Remove(clientId);
        BroadcastPlayerListRpc(BuildNameString());
    }

    // ── RPCs ───────────────────────────────────────────────────────────────

    // string[] can't be serialized by NGO — names are packed as a single
    // pipe-delimited string and split on the receiving end.

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void SubmitNameServerRpc(string displayName, RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        playerNames[sender] = displayName;
        BroadcastPlayerListRpc(BuildNameString());
    }

    [Rpc(SendTo.Everyone)]
    void BroadcastPlayerListRpc(string packedNames)
    {
        string[] names = packedNames.Length > 0
            ? packedNames.Split('|')
            : new string[0];
        RefreshUI(names);
    }

    // ── UI ─────────────────────────────────────────────────────────────────

    void RefreshUI(string[] names)
    {
        foreach (Transform child in playerListContainer)
            Destroy(child.gameObject);

        foreach (string name in names)
        {
            GameObject slot = Instantiate(playerSlotPrefab, playerListContainer);
            slot.GetComponentInChildren<TextMeshProUGUI>().text = name;
        }
    }

    // Called by the Back / Leave button
    public void LeaveAndReturn()
    {
        if (NetworkLobby.Instance != null)
            NetworkLobby.Instance.Disconnect();

        UnityEngine.SceneManagement.SceneManager.LoadScene("Start Scene");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    // Joins all display names with '|' so they fit in a single RPC string.
    string BuildNameString()
    {
        var names = new List<string>(playerNames.Values);
        return string.Join("|", names);
    }
}
