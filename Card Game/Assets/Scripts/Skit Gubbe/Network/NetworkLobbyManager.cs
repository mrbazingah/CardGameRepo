using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkLobbyManager : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI codeDisplayText;
    [SerializeField] TextMeshProUGUI playerCountText;
    [SerializeField] GameObject startGameButton;

    [Header("Settings")]
    [SerializeField] TMP_InputField cardsPerPlayerField;
    [SerializeField] Toggle canChanceToggle;
    [SerializeField] int defaultCardsPerPlayer = 5;

    int playerCount = 0;
    int trackedCardsPerPlayer;
    bool trackedCanChance;

    void Start()
    {
        if (NetworkLobby.Instance != null)
        {
            codeDisplayText.text = "Room Code: " + NetworkLobby.Instance.RoomCode;
            playerCount = NetworkLobby.Instance.PlayerCount;
            UpdatePlayerCountText();
        }

        startGameButton.SetActive(false);

        SetupSettings();
    }

    void SetupSettings()
    {
        if (NetworkLobby.Instance == null) return;

        if (NetworkLobby.Instance.IsHost)
        {
            int cards = PlayerPrefs.HasKey("CardsPerPlayer") ? PlayerPrefs.GetInt("CardsPerPlayer") : defaultCardsPerPlayer;
            bool canChance = !PlayerPrefs.HasKey("CanChance") || PlayerPrefs.GetInt("CanChance") == 1;

            cardsPerPlayerField.text = cards.ToString();
            canChanceToggle.isOn = canChance;
            trackedCardsPerPlayer = cards;
            trackedCanChance = canChance;

            cardsPerPlayerField.interactable = true;
            canChanceToggle.interactable = true;

            _ = NetworkLobby.Instance.UpdateLobbySettings(cards, canChance);
        }
        else
        {
            int cards = NetworkLobby.Instance.LobbyCardsPerPlayer;
            bool canChance = NetworkLobby.Instance.LobbyCanChance;

            cardsPerPlayerField.text = cards.ToString();
            canChanceToggle.isOn = canChance;
            trackedCardsPerPlayer = cards;
            trackedCanChance = canChance;

            cardsPerPlayerField.interactable = false;
            canChanceToggle.interactable = false;
        }
    }

    void Update()
    {
        if (NetworkLobby.Instance == null) return;

        if (playerCount != NetworkLobby.Instance.PlayerCount)
        {
            playerCount = NetworkLobby.Instance.PlayerCount;
            UpdatePlayerCountText();
        }

        if (!NetworkLobby.Instance.IsHost)
        {
            int lobbyCards = NetworkLobby.Instance.LobbyCardsPerPlayer;
            bool lobbyCanChance = NetworkLobby.Instance.LobbyCanChance;

            if (trackedCardsPerPlayer != lobbyCards)
            {
                trackedCardsPerPlayer = lobbyCards;
                cardsPerPlayerField.text = lobbyCards.ToString();
            }

            if (trackedCanChance != lobbyCanChance)
            {
                trackedCanChance = lobbyCanChance;
                canChanceToggle.isOn = lobbyCanChance;
            }
        }
    }

    #region UI
    void UpdatePlayerCountText()
    {
        playerCountText.text = playerCount + "/2";
    }

    public void ActivateStartButton()
    {
        startGameButton.SetActive(true);
    }
    #endregion

    #region Actions
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
    #endregion

    #region Settings
    public void CheckCardsPerPlayer()
    {
        if (NetworkLobby.Instance == null || !NetworkLobby.Instance.IsHost) return;

        int.TryParse(cardsPerPlayerField.text, out int number);

        if (number == 0) number = 3;
        else if (number > 20) number = 20;

        TextMeshProUGUI cardText = cardsPerPlayerField.GetComponentInChildren<TextMeshProUGUI>();
        cardText.alignment = TextAlignmentOptions.Center;

        cardsPerPlayerField.text = number.ToString();
        PlayerPrefs.SetInt("CardsPerPlayer", number);
        trackedCardsPerPlayer = number;

        _ = NetworkLobby.Instance.UpdateLobbySettings(number, canChanceToggle.isOn);
    }

    public void CheckCanChance()
    {
        if (NetworkLobby.Instance == null || !NetworkLobby.Instance.IsHost) return;

        if (canChanceToggle.isOn)
            PlayerPrefs.SetInt("CanChance", 1);
        else
            PlayerPrefs.DeleteKey("CanChance");

        trackedCanChance = canChanceToggle.isOn;

        int.TryParse(cardsPerPlayerField.text, out int cards);
        if (cards == 0) cards = defaultCardsPerPlayer;

        _ = NetworkLobby.Instance.UpdateLobbySettings(cards, canChanceToggle.isOn);
    }
    #endregion
}
