using UnityEngine;
using Fusion;
using UnityEngine.SceneManagement;
using TMPro; // if you use TextMeshPro for UI

public class LobbyManager : MonoBehaviour
{
    public NetworkRunner runnerPrefab;
    private NetworkRunner runner;

    public TMP_InputField joinInputField;  // For player to type room code
    public TMP_Text hostCodeText;          // To display generated room code

    public async void HostGame()
    {
        string roomCode = GenerateRoomCode();
        hostCodeText.text = $"Room Code: {roomCode}";

        runner = Instantiate(runnerPrefab);
        DontDestroyOnLoad(runner.gameObject);

        await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Host,
            SessionName = roomCode,
            Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex)
        });
    }

    public async void JoinGame()
    {
        string roomCode = joinInputField.text.Trim();

        if (string.IsNullOrEmpty(roomCode))
        {
            Debug.LogWarning("Enter a valid room code!");
            return;
        }

        runner = Instantiate(runnerPrefab);
        DontDestroyOnLoad(runner.gameObject);

        await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = roomCode,
            Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex)
        });
    }

    // Add this method anywhere inside the LobbyManager class
    private string GenerateRoomCode()
    {
        int code = UnityEngine.Random.Range(0, 10000); // 0 to 9999 inclusive
        return code.ToString("D4"); // Pads with zeros, e.g. "0007"
    }
}
