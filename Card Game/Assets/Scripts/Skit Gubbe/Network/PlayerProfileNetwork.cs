using Fusion;
using TMPro;
using UnityEngine;

public class PlayerProfileNetwork : NetworkBehaviour
{
    [Networked] public string NetworkDisplayName { get; set; }

    [SerializeField] TMP_Text displayNameText;
    [SerializeField] GameObject readyIcon;

    string lastDisplayName = "";

    private void Start()
    {
        // Reparent to UI canvas on all clients
        Transform parentCanvas = GameObject.Find("PlayerProfilesParent")?.transform;
        if (parentCanvas != null)
            transform.SetParent(parentCanvas, false);
        else
            Debug.LogWarning("PlayerProfilesParent not found in scene.");
    }

    public override void FixedUpdateNetwork()
    {
        if (NetworkDisplayName != lastDisplayName)
        {
            lastDisplayName = NetworkDisplayName;
            UpdateDisplayNameUI(NetworkDisplayName);
        }
    }

    private void UpdateDisplayNameUI(string newName)
    {
        if (displayNameText != null)
            displayNameText.text = newName;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SendDisplayName(string displayName)
    {
        NetworkDisplayName = displayName;
    }

    public void ReadyPLayer()
    {
        readyIcon.SetActive(!readyIcon.activeSelf);
    }
}
