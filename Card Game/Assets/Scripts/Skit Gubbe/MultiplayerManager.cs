using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public static class GameSession
{
    public static string RoomCode;
    public static bool IsHost;
}

public class MultiplayerManager : MonoBehaviour
{
    [SerializeField] GameObject hostPanel;
    [SerializeField] GameObject codePanel;

    [SerializeField] TMP_Text generatedCodeText;   // To display generated code on hostPanel
    [SerializeField] TMP_InputField joinInputField; // For player to enter code in codePanel

    void Start()
    {
        Close();
    }

    public void Close()
    {
        hostPanel.SetActive(false);
        codePanel.SetActive(false);
    }

    public void OpenHostPanel()
    {
        hostPanel.SetActive(true);
        GenerateAndShowCode();
    }

    public void OpenCodePanel()
    {
        codePanel.SetActive(true);
        hostPanel.SetActive(false);
    }

    private void GenerateAndShowCode()
    {
        string code = GenerateRoomCode();
        generatedCodeText.text = $"Room Code: {code}";
        GameSession.RoomCode = code;
        GameSession.IsHost = true;
    }

    public void OnJoinClicked()
    {
        string inputCode = joinInputField.text.Trim();

        if (string.IsNullOrEmpty(inputCode))
        {
            Debug.LogWarning("Please enter a valid room code");
            return;
        }

        GameSession.RoomCode = inputCode;
        GameSession.IsHost = false;

        // Load the lobby/game scene
        SceneManager.LoadScene("LobbyScene");
    }

    public void OnHostClicked()
    {
        // Host button pressed: just load lobby, code already generated
        SceneManager.LoadScene("LobbyScene");
    }

    private string GenerateRoomCode()
    {
        int code = UnityEngine.Random.Range(0, 10000);
        return code.ToString("D4");
    }
}
