using Fusion;
using TMPro;
using UnityEngine;

public class PlayerProfileNetwork : NetworkBehaviour
{
    [Networked] public string NetworkDisplayName { get; set; }

    [SerializeField] private TMP_Text displayNameText;

    // Local copy of the last known value
    private string lastDisplayName = "";

    public override void FixedUpdateNetwork()
    {
        // Only run this on clients to update UI when value changes
        if (Object.HasStateAuthority || Object.HasInputAuthority)
        {
            // If the value has changed since last check
            if (NetworkDisplayName != lastDisplayName)
            {
                lastDisplayName = NetworkDisplayName;
                UpdateDisplayNameUI(NetworkDisplayName);
            }
        }
    }

    private void UpdateDisplayNameUI(string newName)
    {
        if (displayNameText != null)
        {
            displayNameText.text = newName;
        }
    }

    // RPC to set display name from the client to the server
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SendDisplayName(string displayName)
    {
        NetworkDisplayName = displayName;
    }
}
