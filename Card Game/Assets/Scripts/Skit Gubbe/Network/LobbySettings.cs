using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class Settings
{
    public static int cardsPerPlayer = 3;
    public static bool canChance = true;
}

public class LobbySettings : MonoBehaviour
{
    [Header("Input Fields")]
    [SerializeField] TMP_InputField cardsPerPlayerField;
    [Header("Toggle")]
    [SerializeField] Toggle canChanceToggle;
    [Header("Default Settings")]
    [SerializeField] int defaultCardsPerPlayer;

    string cardsPerPlayerKey = "CardsPerPlayerMulti";
    string canChanceKey = "CanChanceMulti";

    void Start()
    {
        SetUpPlayerPrefs();
    }

    void SetUpPlayerPrefs()
    {
        int cardsPerPlayer;
        if (!PlayerPrefs.HasKey(cardsPerPlayerKey))
        {
            cardsPerPlayer = defaultCardsPerPlayer;
        }
        else
        {
            cardsPerPlayer = PlayerPrefs.GetInt(cardsPerPlayerKey);
        }

        cardsPerPlayerField.text = cardsPerPlayer.ToString();

        int canChance = PlayerPrefs.GetInt(canChanceKey);
        if (PlayerPrefs.HasKey(canChanceKey) || canChance == 0)
        {
            canChanceToggle.isOn = true;
            PlayerPrefs.SetInt(canChanceKey, 1);
        }
        else
        {
            canChanceToggle.isOn = false;
        }
    }

    public void CheckCardsPerPlayer()
    {
        int number;
        int.TryParse(cardsPerPlayerField.text, out number);

        if (number == 0)
        {
            number = 3;
        }
        else if (number > 20)
        {
            number = 20;
        }

        cardsPerPlayerField.text = number.ToString();
        PlayerPrefs.SetInt(cardsPerPlayerKey, number);
    }

    public void CheckCanChance()
    {
        if (canChanceToggle.isOn)
        {
            PlayerPrefs.SetInt(canChanceKey, 1);
            Settings.canChance = true;

        }
        else
        {
            PlayerPrefs.DeleteKey(canChanceKey);
        }
    }

    public void SaveAll()
    {
        int number;

        int.TryParse(cardsPerPlayerField.text, out number);
        PlayerPrefs.SetInt(cardsPerPlayerKey, number);

        CheckCanChance();
    }
}
