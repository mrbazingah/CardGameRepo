using UnityEngine;
using TMPro;

public class LobbyManager : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI codeDisplayText;
    [SerializeField] TextMeshProUGUI playerCountText;
    [SerializeField] GameObject startGameButton;

    int playerCount = 0;

    void Start()
    {
        if (MultiplayerLobby.Instance != null)
        {
            codeDisplayText.text = "Room Code: " + MultiplayerLobby.Instance.RoomCode;
            playerCount = MultiplayerLobby.Instance.PlayerCount;
            UpdatePlayerCountText();
        }

        startGameButton.SetActive(false);
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

    public void ActivateStartButton() 
    { 
        startGameButton.SetActive(true);
    }

    public void StartGame()
    {
        if (MultiplayerLobby.Instance != null)
        {
            //MultiplayerLobby.Instance.StartGame();
            Debug.Log("Start Game");
        }
    }

    public async void LeaveLobby()
    {
        if (MultiplayerLobby.Instance != null)
        {
            await MultiplayerLobby.Instance.LeaveLobby();

        }
    }
}
