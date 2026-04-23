using UnityEngine;

public class MultiplayerLobbyUI : MultiplayerLobby
{
    [SerializeField] GameObject hostPanel;
    [SerializeField] GameObject codePanel;

    public void OpenHostPanel()
    {
        hostPanel.SetActive(true);
    }

    public void HostLobby()
    {
        _ = CreateLobby();
    }
}
