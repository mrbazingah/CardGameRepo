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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            CheckCardsPerPlayer();
            CheckAiPrecentage();
        }
    }

    void CheckCardsPerPlayer()
    {
        int number;
        int.TryParse(cardsPerPlayerField.text, out number);

        if (number == 0)
        {
            number = 3;
        }
        else if (number > 26)
        {
            number = 26;
        }

        cardsPerPlayerField.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        cardsPerPlayerField.text = number.ToString();   
        PlayerPrefs.SetInt("CardsPerPlayer", number);
    }

    void CheckAiPrecentage()
    {
        int number;
        int.TryParse(aiChancePrecentageField.text, out number);

        if (number > 100)
        {
            number = 100;
        }
        
        aiChancePrecentageField.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        aiChancePrecentageField.text = number.ToString();
        PlayerPrefs.SetInt("AiChancePrecentage", number);
    }
}
