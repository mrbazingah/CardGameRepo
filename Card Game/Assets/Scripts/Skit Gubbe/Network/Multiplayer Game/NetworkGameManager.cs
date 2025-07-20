using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkGameManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private int handSize = 5;
    [SerializeField] private int maxPlayers = 2;
    [SerializeField] private string gameScene = "GameScene";

    private NetworkRunner runner;
    private List<PlayerEntity> _players = new List<PlayerEntity>();

    public static NetworkGameManager Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    public async void StartHostOrClient(bool asHost)
    {
        runner = gameObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;
        runner.AddCallbacks(this);
        await runner.StartGame(new StartGameArgs
        {
            GameMode = asHost ? GameMode.Host : GameMode.Client,
            SessionName = "Room",
            Scene = SceneManager.GetActiveScene().buildIndex
        });
    }

    // Only this one actually does something; all others are empty stubs below:
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;
        var go = runner.Spawn(
          playerPrefab,
          Vector3.zero,
          Quaternion.identity,
          onBeforeSpawn: (r, o) => o.GetComponent<PlayerEntity>().Init(player)
        );
        _players.Add(go.GetComponent<PlayerEntity>());

        if (_players.Count == maxPlayers)
            StartGame();
    }

    void StartGame()
    {
        var gen = FindObjectOfType<NetworkCardGenerator>();
        gen.DealHands(_players.ToArray(), handSize);
        runner.SetActiveScene(gameScene, LoadSceneMode.Single);
    }

    public void PlayCardRequest(PlayerEntity player, NetworkObject card)
    {
        if (!runner.IsServer) return;
        var pile = FindObjectOfType<NetworkPile>();
        pile.AddCard(card);
        // TODO: advance your turn state
    }

    //----- INetworkRunnerCallbacks stubs -----
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason reason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken token) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> buffer) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}
