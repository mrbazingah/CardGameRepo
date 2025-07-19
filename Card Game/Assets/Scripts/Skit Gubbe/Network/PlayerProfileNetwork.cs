using Fusion;
using TMPro;
using UnityEngine;
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

    public override void Spawned()
    {
        // Parent and position
        Transform profilesParent = LobbyManager.ProfilesParent;
        transform.SetParent(profilesParent, false);
        if (!positionSet && SpawnPosition != default)
        {
            transform.localPosition = new Vector3(SpawnPosition.x, SpawnPosition.y, transform.localPosition.z);
            positionSet = true;
        }

        // Force UI update here regardless of state change
        UpdateDisplayNameUI(NetworkDisplayName);
        displayNameText.text = NetworkDisplayName;
        lastDisplayName = NetworkDisplayName;

        readyIcon.SetActive(IsReady);  // <-- force UI to match actual IsReady state
        lastReady = IsReady;           // <-- sync lastReady so FixedUpdateNetwork doesn't immediately retrigger

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
        // Name update
        if (NetworkDisplayName != lastDisplayName)
        {
            lastDisplayName = NetworkDisplayName;
            UpdateDisplayNameUI(lastDisplayName);
        }

        // Ready icon sync
        if (IsReady != lastReady)
        {
            lastReady = IsReady;
            readyIcon.SetActive(IsReady);
        }
    }

    private void UpdateDisplayNameUI(string newName)
    {
        string displayName = newName;
        if (IsHost)
            displayName += " (Host)";

        if (displayNameText != null)
            displayNameText.text = displayName;
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
            // Directly toggle if we're the server
            IsReady = !IsReady;
        }
        else if (Object.HasInputAuthority)
        {
            // Ask the server to toggle it
            RPC_ToggleReady();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_SendDisplayName(string displayName)
    {
        NetworkDisplayName = displayName;
    }
}
