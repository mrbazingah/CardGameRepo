using Fusion;
using TMPro;
using UnityEngine;

public class PlayerProfileNetwork : NetworkBehaviour
{
    [Networked] public NetworkString<_128> DisplayName { get; set; }

    private TMP_Text nameText;

    public override void Spawned()
    {
        nameText = GetComponentInChildren<TMP_Text>();
        UpdateDisplayName();
    }

    public override void Render()
    {
        UpdateDisplayName();
    }

    private void UpdateDisplayName()
    {
        if (nameText == null)
            nameText = GetComponentInChildren<TMP_Text>();

        if (nameText != null)
            nameText.text = (string)DisplayName;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SendDisplayName(string displayName, RpcInfo info = default)
    {
        DisplayName = displayName;
    }
}
