using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Manages the multiplayer Host / Join UI in the Start Scene.
//
// UI hierarchy expected:
//   [Any GameObject] — MultiplayerMenuManager component
//   MultiplayerPanel (GameObject)
//     HostButton      (Button)  -> calls HostGame()
//     JoinButton      (Button)  -> calls OpenJoinPanel()
//     BackButton      (Button)  -> calls CloseMultiplayerPanel()
//   JoinPanel (GameObject, child of MultiplayerPanel or separate)
//     RoomCodeInput   (TMP_InputField)  — 4 digits, Content Type: Integer Number
//     ConfirmButton   (Button)  -> calls JoinGame()
//     BackButton      (Button)  -> calls CloseJoinPanel()
//   StatusText        (TextMeshProUGUI) — error / status messages
public class MultiplayerMenuManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] GameObject multiplayerPanel;
    [SerializeField] GameObject joinPanel;

    [Header("Join Panel")]
    [SerializeField] TMP_InputField roomCodeInput;

    [Header("Feedback")]
    [SerializeField] TextMeshProUGUI statusText;

    [Header("Buttons (disabled while connecting)")]
    [SerializeField] Button hostButton;
    [SerializeField] Button joinConfirmButton;

    void Start()
    {
        multiplayerPanel.SetActive(false);
        joinPanel.SetActive(false);
    }

    // ── Called by the Multiplayer button in the main menu ──────────────────
    public void OpenMultiplayerPanel()
    {
        multiplayerPanel.SetActive(true);
        joinPanel.SetActive(false);
        SetStatus("");
        SetButtons(true);
    }

    public void CloseMultiplayerPanel()
    {
        multiplayerPanel.SetActive(false);
        joinPanel.SetActive(false);
    }

    // ── Host ───────────────────────────────────────────────────────────────
    public void HostGame()
    {
        if (!CheckReady()) return;
        SetButtons(false);
        SetStatus("Creating lobby...");
        StartCoroutine(HostRoutine());
    }

    IEnumerator HostRoutine()
    {
        var task = NetworkLobby.Instance.Host();
        yield return new WaitUntil(() => task.IsCompleted);

        if (!task.Result)
        {
            SetStatus("Could not create lobby. Check your connection.");
            SetButtons(true);
        }
        // On success NGO's SceneManager loads the Multiplayer Lobby Scene
    }

    // ── Join ───────────────────────────────────────────────────────────────
    public void OpenJoinPanel()
    {
        joinPanel.SetActive(true);
        roomCodeInput.text = "";
        SetStatus("");
        SetButtons(true);
    }

    public void CloseJoinPanel()
    {
        joinPanel.SetActive(false);
    }

    public void JoinGame()
    {
        if (!CheckReady()) return;

        string code = roomCodeInput.text.Trim();
        if (code.Length != 4)
        {
            SetStatus("Enter a 4-digit code.");
            return;
        }

        SetButtons(false);
        SetStatus("Joining...");
        StartCoroutine(JoinRoutine(code));
    }

    IEnumerator JoinRoutine(string code)
    {
        var task = NetworkLobby.Instance.Join(code);
        yield return new WaitUntil(() => task.IsCompleted);

        if (!task.Result)
        {
            SetStatus("Room not found or full. Check the code.");
            SetButtons(true);
        }
        // On success NGO's SceneManager loads the Multiplayer Lobby Scene
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    bool CheckReady()
    {
        if (NetworkLobby.Instance == null || !NetworkLobby.Instance.IsReady)
        {
            SetStatus("Still connecting to services...");
            return false;
        }
        return true;
    }

    void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    void SetButtons(bool interactable)
    {
        if (hostButton      != null) hostButton.interactable      = interactable;
        if (joinConfirmButton != null) joinConfirmButton.interactable = interactable;
    }
}
