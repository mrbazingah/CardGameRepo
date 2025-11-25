using Fusion;
using Fusion.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameSession
{
    public static string RoomCode;
    public static bool IsHost;
    public static string DisplayName;
}

public class MultiplayerManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Join/Host Logic")]
    [SerializeField] GameObject hostPanel;
    [SerializeField] GameObject codePanel;
    [SerializeField] GameObject loadingPanel;
    [SerializeField] GameObject errorPanel;
    [SerializeField] float loadingSwitchDelay;
    [SerializeField] TMP_InputField joinInputField;
    [Space]
    [SerializeField] string roomFullError;
    [SerializeField] string roomNotFoundError;
    [SerializeField] string roomShutdownError;
    [Header("Display Name")]
    [SerializeField] GameObject profilePanel;
    [SerializeField] TMP_InputField displayNameInputField;
    [SerializeField] string defaultDisplayName = "Player";

    enum JoinResult { Success, Full, NotFound }

    NetworkRunner tempRunner;
    NetConnectFailedReason? lastJoinFailReason;

    void Start()
    {
        Close();

        string savedName = PlayerPrefs.GetString("DisplayName");
        if (string.IsNullOrEmpty(savedName))
            savedName = defaultDisplayName;

        displayNameInputField.text = savedName;
        GameSession.DisplayName = savedName;

        StartCoroutine(LoadingProcess());

        if (LobbyManager.ServerShutdown)
        {
            errorPanel.SetActive(true);
            errorPanel.GetComponentInChildren<TMP_Text>().text = roomShutdownError;

            LobbyManager.ServerShutdown = false;
        }
    }

    public void Close()
    {
        hostPanel.SetActive(false);
        codePanel.SetActive(false);
        profilePanel.SetActive(false);
        errorPanel.SetActive(false);
        loadingPanel.SetActive(false);
    }

    public void OpenHostPanel() => hostPanel.SetActive(true);

    public void OpenCodePanel()
    {
        codePanel.SetActive(true);
        errorPanel.SetActive(false);
        hostPanel.SetActive(false);
    }

    public async void OnJoinClicked()
    {
        string inputCode = joinInputField.text.Trim();
        if (string.IsNullOrEmpty(inputCode))
        {
            Debug.LogWarning("Please enter a valid room code");
            return;
        }

        GameSession.RoomCode = inputCode;
        GameSession.IsHost = false;

        loadingPanel.SetActive(true);
        codePanel.SetActive(false);

        JoinResult joinResult = await ValidateRoomCode(inputCode);

        loadingPanel.SetActive(false);
        errorPanel.SetActive(true);

        switch (joinResult)
        {
            case JoinResult.Success:
                SceneManager.LoadScene("Lobby Scene");
                break;
            case JoinResult.Full:
                errorPanel.GetComponentInChildren<TMP_Text>().text = roomFullError;
                break;
            case JoinResult.NotFound:
                errorPanel.GetComponentInChildren<TMP_Text>().text = roomNotFoundError;
                break;
        }
    }

    IEnumerator LoadingProcess()
    {
        while (true)
        {
            TextMeshProUGUI text = loadingPanel.GetComponentInChildren<TextMeshProUGUI>();
            text.text = "Loading.";
            yield return new WaitForSeconds(loadingSwitchDelay);
            text.text = "Loading..";
            yield return new WaitForSeconds(loadingSwitchDelay);
            text.text = "Loading...";
            yield return new WaitForSeconds(loadingSwitchDelay);
        }
    }

    public void OnHostClicked()
    {
        string code = GenerateRoomCode();
        GameSession.RoomCode = code;
        GameSession.IsHost = true;
        Debug.Log($"Hosting room with code: {code}");
        
        // Load Lobby Scene (index 1 in build settings)
        SceneManager.LoadScene(1);
    }

    private string GenerateRoomCode()
    {
        int code = UnityEngine.Random.Range(0, 10000);
        return code.ToString("D4");
    }

    private async Task<JoinResult> ValidateRoomCode(string code)
    {
        lastJoinFailReason = null;
        tempRunner = new GameObject("TempNetworkRunner").AddComponent<NetworkRunner>();
        DontDestroyOnLoad(tempRunner.gameObject);
        tempRunner.AddCallbacks(this);

        var startGameArgs = new StartGameArgs
        {
            SessionName = code,
            GameMode = GameMode.Client,
            Scene = SceneRef.None
        };

        StartGameResult result = await tempRunner.StartGame(startGameArgs);
        if (result.Ok)
        {
            await tempRunner.Shutdown();
            Destroy(tempRunner.gameObject);
            return JoinResult.Success;
        }
        else
        {
            Destroy(tempRunner.gameObject);
            if (lastJoinFailReason == NetConnectFailedReason.ServerFull)
                return JoinResult.Full;
            else
                return JoinResult.NotFound;
        }
    }

    public void OpenProfilePanel() => profilePanel.SetActive(true);

    public void OnDisplayNameChanged()
    {
        string newName = displayNameInputField.text.Trim();
        if (string.IsNullOrEmpty(newName))
            newName = defaultDisplayName;
        PlayerPrefs.SetString("DisplayName", newName);
        GameSession.DisplayName = newName;
    }

    // INetworkRunnerCallbacks:
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        lastJoinFailReason = reason;
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
