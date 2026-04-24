using UnityEngine;
using TMPro;

public class NetworkLobbyManager : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI codeDisplayText;
    [SerializeField] TextMeshProUGUI playerCountText;
    [SerializeField] GameObject startGameButton;

    int playerCount = 0;

    void Start()
    {
        if (NetworkLobby.Instance != null)
        {
            codeDisplayText.text = "Room Code: " + NetworkLobby.Instance.RoomCode;
            playerCount = NetworkLobby.Instance.PlayerCount;
            UpdatePlayerCountText();
        }

        startGameButton.SetActive(false);
    }

    void Update()
    {
        if (NetworkLobby.Instance == null) return;

        if (playerCount != NetworkLobby.Instance.PlayerCount)
        {
            playerCount = NetworkLobby.Instance.PlayerCount;
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
        if (NetworkLobby.Instance != null)
        {
            //MultiplayerLobby.Instance.StartGame();
            Debug.Log("Start Game");
        }
    }

    public async void LeaveLobby()
    {
        if (NetworkLobby.Instance != null)
        {
            await NetworkLobby.Instance.LeaveLobby();
        }
    }
}
