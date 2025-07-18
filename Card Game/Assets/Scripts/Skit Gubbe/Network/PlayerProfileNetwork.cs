using Fusion;
using TMPro;
using UnityEngine;

public class PlayerProfileNetwork : NetworkBehaviour
{
    [Networked] public string NetworkDisplayName { get; set; }

    [SerializeField] TMP_Text displayNameText;
    [SerializeField] GameObject readyIcon;

    string lastDisplayName = "";

    // Called on every runner as soon as this object is spawned
    public override void Spawned()
    {
        // If this profile belongs to *me*, tell the server my display name
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
