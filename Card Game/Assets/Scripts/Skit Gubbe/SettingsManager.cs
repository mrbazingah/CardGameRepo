using TMPro;
using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    [Header("Input Fields")]
    [SerializeField] TMP_InputField cardsPerPlayerField;
    [SerializeField] TMP_InputField aiChancePrecentageField;
    [Header("Default Settings")]
    [SerializeField] int defaultCardsPerPlayer;
    [SerializeField] int defaultAiChancePrecentage;

    void Start()
    {
        SetUpPlayerPrefs();
    }

    void SetUpPlayerPrefs()
    {
        int cardsPerPlayer;
        if (!PlayerPrefs.HasKey("CardsPerPlayer"))
        {
            cardsPerPlayer = defaultCardsPerPlayer;
        }
        else
        {
            cardsPerPlayer = PlayerPrefs.GetInt("CardsPerPlayer");
        }

        cardsPerPlayerField.text = cardsPerPlayer.ToString();

        int aiChancePrecentage;
        if (!PlayerPrefs.HasKey("AiChancePrecentage"))
        {
            aiChancePrecentage = defaultAiChancePrecentage;
        }
        else
        {
            aiChancePrecentage = PlayerPrefs.GetInt("AiChancePrecentage");
        }

        aiChancePrecentageField.text = aiChancePrecentage.ToString();
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

        TextMeshProUGUI cardText = cardsPerPlayerField.GetComponentInChildren<TextMeshProUGUI>();
        cardText.alignment = cardText.name == "Title" ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;

        cardsPerPlayerField.text = number.ToString();   
        PlayerPrefs.SetInt("CardsPerPlayer", number);
    }

    public void CheckAiPrecentage()
    {
        int number;
        int.TryParse(aiChancePrecentageField.text, out number);

        if (number > 100)
        {
            number = 100;
        }

        TextMeshProUGUI aiText = aiChancePrecentageField.GetComponentInChildren<TextMeshProUGUI>();
        aiText.alignment = aiText.name == "Title" ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;

        aiChancePrecentageField.text = number.ToString();
        PlayerPrefs.SetInt("AiChancePrecentage", number);
    }

    public void SaveAll()
    {
        int number;

        int.TryParse(aiChancePrecentageField.text, out number);
        PlayerPrefs.SetInt("AiChancePrecentage", number);

        int.TryParse(cardsPerPlayerField.text, out number);
        PlayerPrefs.SetInt("CardsPerPlayer", number);
    }
}
