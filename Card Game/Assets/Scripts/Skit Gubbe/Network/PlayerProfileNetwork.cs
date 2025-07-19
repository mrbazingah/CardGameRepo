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

        // Initialize UI name and icon
        if (!string.IsNullOrEmpty(NetworkDisplayName))
        {
            UpdateDisplayNameUI(NetworkDisplayName);
            lastDisplayName = NetworkDisplayName;
        }
        readyIcon.SetActive(IsReady);

        // Send display name if this is the local player
        if (Object.HasInputAuthority)
        {
            RPC_SendDisplayName(GameSession.DisplayName);

            // Hook up ready button only for local client
            var btn = GameObject.Find("Ready Button")?.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(ReadyPLayer);
                btn.interactable = true;
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
        if (Object.HasInputAuthority)
            RPC_ToggleReady();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_SendDisplayName(string displayName)
    {
        NetworkDisplayName = displayName;
    }
}
