using UnityEngine;
using Fusion;
using UnityEngine.SceneManagement;
using TMPro;  // Import TextMeshPro namespace

public class LobbyManager : MonoBehaviour
{
    public NetworkRunner runnerPrefab;
    private NetworkRunner runner;

    [SerializeField] private TMP_Text roomCodeText;  // Reference to the UI Text where code is displayed

    private async void Start()
    {
        // Display the room code as soon as the lobby scene loads
        if (roomCodeText != null)
            roomCodeText.text = $"Room Code: {GameSession.RoomCode}";
        else
            Debug.LogWarning("RoomCodeText UI element is not assigned in the Inspector!");

        runner = Instantiate(runnerPrefab);
        DontDestroyOnLoad(runner.gameObject);

        if (string.IsNullOrEmpty(GameSession.RoomCode))
        {
            Debug.LogError("No room code set! Cannot start game.");
            return;
        }

        var startGameArgs = new StartGameArgs
        {
            SessionName = GameSession.RoomCode,
            Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex)
        };

        if (GameSession.IsHost)
        {
            startGameArgs.GameMode = GameMode.Host;
            Debug.Log($"Hosting game with code: {GameSession.RoomCode}");
        }
        else
        {
            startGameArgs.GameMode = GameMode.Client;
            Debug.Log($"Joining game with code: {GameSession.RoomCode}");
        }

        StartGameResult result = await runner.StartGame(startGameArgs);
        Debug.Log($"StartGame result: {result}");

        if (result.Ok)
        {
            Debug.Log("Successfully started/joined the game.");
        }
        else
        {
            Debug.LogWarning($"Failed to start/join game with code {GameSession.RoomCode}. Returning to Start scene.");
            SceneManager.LoadScene("Start Scene");
        }
    }
}
