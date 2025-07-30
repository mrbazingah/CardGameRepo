using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerProfileNetwork : NetworkBehaviour
{
    [Networked] public string NetworkDisplayName { get; set; }
    [Networked] public Vector2 SpawnPosition { get; set; }
    [Networked] public bool IsHost { get; set; }
    [Networked] public bool IsReady { get; set; }

    [SerializeField] TMP_Text displayNameText;
    [SerializeField] GameObject readyIcon;

    string lastDisplayName = "";
    bool positionSet = false;
    bool lastReady = false;

    LobbyManager lobbyManager;

    public override void Spawned()
    {
        lobbyManager = FindFirstObjectByType<LobbyManager>();

        // Parent and position
        Transform profilesParent = LobbyManager.ProfilesParent;
        transform.SetParent(profilesParent, false);

        if (!positionSet && SpawnPosition != default)
        {
            transform.localPosition = new Vector3(SpawnPosition.x, SpawnPosition.y, transform.localPosition.z);
            positionSet = true;
        }

        // Force initial UI update
        UpdateDisplayNameUI(NetworkDisplayName);
        displayNameText.text = NetworkDisplayName;
        lastDisplayName = NetworkDisplayName;

        readyIcon.SetActive(IsReady);  // force UI to match actual IsReady state

        // Send name if local player
        if (Object.HasInputAuthority)
        {
            RPC_SendDisplayName(GameSession.DisplayName);

            var button = GameObject.Find("Ready Button")?.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(ReadyPLayer);
                button.interactable = true;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (NetworkDisplayName != lastDisplayName)
        {
            lastDisplayName = NetworkDisplayName;
            UpdateDisplayNameUI(lastDisplayName);
        }
    }

    void Update()
    {
        readyIcon.SetActive(IsReady);
    }

    private void UpdateDisplayNameUI(string newName)
    {
        string displayName = newName;
        if (IsHost && !displayName.Contains("Host"))
            displayName += " (Host)";

        NetworkDisplayName = displayName;

        if (displayNameText != null)
            displayNameText.text = displayName;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_SendDisplayName(string displayName)
    {
        NetworkDisplayName = displayName;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_ToggleReady()
    {
        IsReady = !IsReady;
    }

    public void ReadyPLayer()
    {
        if (Object.HasStateAuthority)
        {
            IsReady = !IsReady;
        }
        else if (Object.HasInputAuthority)
        {
            RPC_ToggleReady();
        }

        lobbyManager.OnPlayerReady();
    }

    public void LoadGame()
    {
        SceneManager.LoadScene("Multiplayer Scene");
    }
}


