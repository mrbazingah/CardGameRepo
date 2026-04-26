using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkLobbyManager : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI codeDisplayText;
    [SerializeField] TextMeshProUGUI playerCountText;
    [SerializeField] GameObject startGameButton;

    [Header("Player Profiles")]
    [SerializeField] GameObject playerProfilePrefab;
    [SerializeField] Transform profileContainer;
    [SerializeField] Vector2 profileSpawnPos;
    [SerializeField] float profileYOffset;

    [Header("Ready")]
    [SerializeField] TextMeshProUGUI readyButtonText;

    [Header("Settings")]
    [SerializeField] TMP_InputField cardsPerPlayerField;
    [SerializeField] Toggle canChanceToggle;
    [SerializeField] int defaultCardsPerPlayer = 5;

    readonly List<NetworkPlayerProfile> spawnedProfiles = new();
    int trackedCardsPerPlayer;
    bool trackedCanChance;
    bool localReady;

    void Start()
    {
        if (NetworkLobby.Instance != null)
        {
            codeDisplayText.text = "Room Code: " + NetworkLobby.Instance.RoomCode;
            NetworkLobby.Instance.OnLobbyUpdated += RefreshProfiles;
        }

        startGameButton.SetActive(false);
        SetupSettings();
        RefreshProfiles();
    }

    void OnDestroy()
    {
        if (NetworkLobby.Instance != null)
            NetworkLobby.Instance.OnLobbyUpdated -= RefreshProfiles;
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
        if (NetworkLobby.Instance == null || NetworkLobby.Instance.IsHost) return;

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

    void RefreshProfiles()
    {
        IReadOnlyList<Player> players = NetworkLobby.Instance?.Players;
        if (players == null) return;

        for (int i = spawnedProfiles.Count - 1; i >= 0; i--)
        {
            bool stillPresent = false;
            foreach (var p in players)
            {
                if (p.Id == spawnedProfiles[i].PlayerId) { stillPresent = true; break; }
            }

            if (!stillPresent)
            {
                Destroy(spawnedProfiles[i].gameObject);
                spawnedProfiles.RemoveAt(i);
            }
        }

        bool allReady = players.Count > 1;
        foreach (var player in players)
        {
            string name = GetPlayerData(player, "DisplayName", "Player");
            bool isReady = GetPlayerData(player, "Ready", "0") == "1";
            bool isHostPlayer = player.Id == NetworkLobby.Instance.HostId;

            if (!isReady) allReady = false;

            var profile = spawnedProfiles.Find(p => p.PlayerId == player.Id);
            if (profile == null)
            {
                profile = Instantiate(playerProfilePrefab, profileContainer).GetComponent<NetworkPlayerProfile>();
                Vector2 pos = isHostPlayer ? profileSpawnPos : new Vector2(profileSpawnPos.x, profileSpawnPos.y + profileYOffset);
                profile.transform.localPosition = pos;
                spawnedProfiles.Add(profile);
            }

            string displayName = isHostPlayer ? name + " (Host)" : name;
            profile.Setup(player.Id, displayName, isReady);
        }

        startGameButton.SetActive(allReady && NetworkLobby.Instance.IsHost);
        playerCountText.text = players.Count + "/2";
    }

    static string GetPlayerData(Player player, string key, string fallback)
    {
        if (player.Data != null && player.Data.TryGetValue(key, out var obj))
            return obj.Value;
        return fallback;
    }

    public void OnReadyPressed()
    {
        if (NetworkLobby.Instance == null) return;

        localReady = !localReady;

        if (readyButtonText != null)
            readyButtonText.text = localReady ? "Cancel" : "Ready";

        _ = NetworkLobby.Instance.SetPlayerReady(localReady);
    }

    public async void LeaveLobby()
    {
        if (NetworkLobby.Instance != null)
            await NetworkLobby.Instance.LeaveLobby();
    }

    public void StartGame()
    {
        if (NetworkLobby.Instance != null && NetworkLobby.Instance.IsHost)
        {
            NetworkLobby.Instance.StartGame();
        }
    }

    public void CheckCardsPerPlayer()
    {
        if (NetworkLobby.Instance == null || !NetworkLobby.Instance.IsHost) return;

        int.TryParse(cardsPerPlayerField.text, out int number);

        if (number == 0) number = 3;
        else if (number > 20) number = 20;

        cardsPerPlayerField.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
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
}
