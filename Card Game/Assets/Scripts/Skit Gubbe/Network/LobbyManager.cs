using UnityEngine;
using TMPro;

public class LobbyManager : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI codeDisplayText;
    [SerializeField] TextMeshProUGUI playerCountText;

    int playerCount = 0;

    void Start()
    {
        if (MultiplayerLobby.Instance != null)
        {
            codeDisplayText.text = "Room Code: " + MultiplayerLobby.Instance.RoomCode;
            playerCount = MultiplayerLobby.Instance.PlayerCount;
            UpdatePlayerCountText();
        }
    }

    void Update()
    {
        if (MultiplayerLobby.Instance == null) return;

        if (playerCount != MultiplayerLobby.Instance.PlayerCount)
        {
            playerCount = MultiplayerLobby.Instance.PlayerCount;
            UpdatePlayerCountText();
        }
    }

    void UpdatePlayerCountText()
    {
        playerCountText.text = playerCount + "/2";
    }
}
