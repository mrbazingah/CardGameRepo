using Fusion;
using TMPro;
using UnityEngine;

public class PlayerProfileNetwork : NetworkBehaviour
{
    [Networked] public string NetworkDisplayName { get; set; }
    [Networked] public Vector2 SpawnPosition { get; set; }
    [Networked] public bool IsHost { get; set; }  // Flag to mark if this player is host

    [SerializeField] TMP_Text displayNameText;
    [SerializeField] GameObject readyIcon;

    Transform profilesParent;
    string lastDisplayName = "";
    bool positionSet = false;

    public override void Spawned()
    {
        profilesParent = LobbyManager.ProfilesParent;
        transform.SetParent(profilesParent, false);

        TrySetPosition();

        if (!string.IsNullOrEmpty(NetworkDisplayName))
        {
            UpdateDisplayNameUI(NetworkDisplayName);
            lastDisplayName = NetworkDisplayName;
        }

        if (Object.HasInputAuthority)
        {
            RPC_SendDisplayName(GameSession.DisplayName);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!positionSet)
        {
            TrySetPosition();
        }

        if (NetworkDisplayName != lastDisplayName)
        {
            lastDisplayName = NetworkDisplayName;
            UpdateDisplayNameUI(lastDisplayName);
        }
    }

    private void TrySetPosition()
    {
        if (!positionSet && SpawnPosition != default)
        {
            transform.localPosition = new Vector3(SpawnPosition.x, SpawnPosition.y, transform.localPosition.z);
            positionSet = true;
        }
    }

    void UpdateDisplayNameUI(string newName)
    {
        if (IsHost)
        {
            newName += " (Host)";
            GameSession.DisplayName = newName;
        }

        if (displayNameText != null)
            displayNameText.text = newName;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_SendDisplayName(string displayName)
    {
        NetworkDisplayName = displayName;
    }

    public void ReadyPLayer()
    {
        readyIcon.SetActive(!readyIcon.activeSelf);
    }
}
