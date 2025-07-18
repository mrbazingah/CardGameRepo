using UnityEngine;
using Fusion;
using UnityEngine.SceneManagement;
using TMPro;
using System.Threading.Tasks;
using System.Collections;

public static class GameSession
{
    public static string RoomCode;
    public static bool IsHost;
    public static string DisplayName;
}

public class MultiplayerManager : MonoBehaviour
{
    [Header("Join/Host Logic")]
    [SerializeField] GameObject hostPanel;
    [SerializeField] GameObject codePanel;
    [SerializeField] GameObject loadingPanel;
    [SerializeField] GameObject errorPanel;
    [SerializeField] float loadingSwitchDelay;
    [SerializeField] TMP_InputField joinInputField;
    [Header("Display Name")]
    [SerializeField] GameObject profilePanel;
    [SerializeField] TMP_InputField displayNameInputField;
    [SerializeField] string defaultDisplayName = "Player";

    NetworkRunner tempRunner;

    void Start()
    {
        Close();

        string savedName = PlayerPrefs.GetString("DisplayName");
        if (string.IsNullOrEmpty(savedName))
        {
            savedName = defaultDisplayName;
        }

        displayNameInputField.text = savedName;
        GameSession.DisplayName = savedName;

        StartCoroutine(LoadingProcess());
    }

    public void Close()
    {
        hostPanel.SetActive(false);
        codePanel.SetActive(false);
        profilePanel.SetActive(false);
        errorPanel.SetActive(false);
        loadingPanel.SetActive(false);
    }

    public void OpenHostPanel()
    {
        hostPanel.SetActive(true);
    }

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

        bool canJoin = await ValidateRoomCode(inputCode);
        if (canJoin)
        {
            SceneManager.LoadScene("Lobby Scene");
        }
        else
        {
            Debug.LogWarning("Room code not found or unable to join.");

            loadingPanel.SetActive(false);
            errorPanel.SetActive(true);
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
            Scene = SceneRef.None
        };

        StartGameResult result = await tempRunner.StartGame(startGameArgs);

        bool success = result.Ok;

        if (!success)
        {
            GameSession.RoomCode = null; // <-- clear room code if join failed
            Destroy(tempRunner.gameObject);
        }
        else
        {
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
        GameSession.DisplayName = newName;
    }
}