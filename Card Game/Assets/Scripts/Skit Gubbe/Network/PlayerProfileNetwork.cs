using Fusion;
using TMPro;
using UnityEngine;

public class PlayerProfileNetwork : NetworkBehaviour
{
    [Networked]
    public string NetworkDisplayName { get; set; }

    [Networked]
    public Vector2 SpawnPosition { get; set; }

    [SerializeField] TMP_Text displayNameText;
    [SerializeField] GameObject readyIcon;

    Transform profilesParent;
    string lastDisplayName = "";

    // Called on every runner as soon as this object is spawned
    public override void Spawned()
    {
        profilesParent = LobbyManager.ProfilesParent;
        transform.SetParent(profilesParent, false);

        // Position locally under the UI parent
        transform.localPosition = new Vector3(SpawnPosition.x, SpawnPosition.y, transform.localPosition.z);

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
        if (NetworkDisplayName != lastDisplayName)
        {
            lastDisplayName = NetworkDisplayName;
            UpdateDisplayNameUI(lastDisplayName);
        }
    }

    private void UpdateDisplayNameUI(string newName)
    {
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
