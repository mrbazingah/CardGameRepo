using UnityEngine;
using Fusion;
using UnityEngine.SceneManagement;
using TMPro;
using System.Threading.Tasks;

public static class GameSession
{
    public static string RoomCode;
    public static bool IsHost;
}

public class MultiplayerManager : MonoBehaviour
{
    [Header("Join/Host Logic")]
    [SerializeField] GameObject hostPanel;
    [SerializeField] GameObject codePanel;
    [SerializeField] TMP_InputField joinInputField; 
    [SerializeField] TMP_Text errorText;
    [Header("Display Name")]
    [SerializeField] GameObject profilePanel;
    [SerializeField] TMP_InputField displayNameInputField;
    [SerializeField] string defaultDisplayName = "Player";

    private NetworkRunner tempRunner;

    void Start()
    {
        Close();
        displayNameInputField.text = PlayerPrefs.GetString("DisplayName");
        if (string.IsNullOrEmpty(displayNameInputField.text))
        {
            displayNameInputField.text = defaultDisplayName;
        }

        if (errorText != null) errorText.text = "";
    }

    public void Close()
    {
        hostPanel.SetActive(false);
        codePanel.SetActive(false);
        profilePanel.SetActive(false);
        if (errorText != null) errorText.text = "";
    }

    public void OpenHostPanel()
    {
        hostPanel.SetActive(true);
    }

    public void OpenCodePanel()
    {
        codePanel.SetActive(true);
        hostPanel.SetActive(false);
    }

    public async void OnJoinClicked()
    {
        string inputCode = joinInputField.text.Trim();

        if (string.IsNullOrEmpty(inputCode))
        {
            Debug.LogWarning("Please enter a valid room code");
            if (errorText != null) errorText.text = "Please enter a valid room code";
            return;
        }

        GameSession.RoomCode = inputCode;
        GameSession.IsHost = false;

        bool canJoin = await ValidateRoomCode(inputCode);
        if (canJoin)
        {
            // Room exists, load lobby scene
            SceneManager.LoadScene("Lobby Scene");
        }
        else
        {
            // Show error and stay in start scene
            Debug.LogWarning("Room code not found or unable to join.");
            if (errorText != null) errorText.text = "No lobby with that code found.";
        }
    }

    public void OnHostClicked()
    {
        string code = GenerateRoomCode();
        GameSession.RoomCode = code;
        GameSession.IsHost = true;

        Debug.Log($"Hosting room with code: {code}");
        SceneManager.LoadScene("Lobby Scene");
    }

    private string GenerateRoomCode()
    {
        int code = UnityEngine.Random.Range(0, 10000);
        return code.ToString("D4");
    }

    private async Task<bool> ValidateRoomCode(string code)
    {
        tempRunner = new GameObject("TempNetworkRunner").AddComponent<NetworkRunner>();
        DontDestroyOnLoad(tempRunner.gameObject);

        var startGameArgs = new StartGameArgs
        {
            SessionName = code,
            GameMode = GameMode.Client,
            Scene = SceneRef.None  // no scene load here, just connect attempt
        };

        StartGameResult result = await tempRunner.StartGame(startGameArgs);

        bool success = result.Ok;

        if (!success)
        {
            Destroy(tempRunner.gameObject);
        }
        else
        {
            // We successfully joined, so we should shut this temp runner down immediately
            await tempRunner.Shutdown();
            Destroy(tempRunner.gameObject);
        }

        return success;
    }

    public void OpenProfilePanel()
    {
        profilePanel.SetActive(true);
    }

    public void OnDisplayNameChanged()
    {
        string newName = displayNameInputField.text.Trim();

        if (string.IsNullOrEmpty(newName))
        {
            newName = defaultDisplayName;
        }
   
        PlayerPrefs.SetString("DisplayName", newName);
    }
}
