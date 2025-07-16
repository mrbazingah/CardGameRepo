using Fusion;
using TMPro;
using UnityEngine;

public class PlayerProfileNetwork : NetworkBehaviour
{
    [Networked(OnChanged = nameof(OnDisplayNameChanged))]
    public NetworkString<_32> DisplayName { get; set; }

    private TMP_Text nameText;

    public override void Spawned()
    {
        nameText = GetComponentInChildren<TMP_Text>();

        // If this profile belongs to the local player on the client, send display name to server
        if (Object.HasStateAuthority == false && Object.InputAuthority == Runner.LocalPlayer)
        {
            string localDisplayName = PlayerPrefs.GetString("DisplayName", $"Player {Object.InputAuthority.PlayerId}");
            RPC_SendDisplayName(localDisplayName);
        }

        UpdateDisplayNameUI();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SendDisplayName(string displayName, RpcInfo info = default)
    {
        DisplayName = displayName;
    }

    private static void OnDisplayNameChanged(Changed<PlayerProfileNetwork> changed)
    {
        changed.Behaviour.UpdateDisplayNameUI();
    }

    private void UpdateDisplayNameUI()
    {
        if (nameText == null)
            nameText = GetComponentInChildren<TMP_Text>();

        if (nameText != null)
            nameText.text = DisplayName.Value.ToString();
    }
}
