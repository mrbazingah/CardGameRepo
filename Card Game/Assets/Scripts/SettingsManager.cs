using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    [Header("Input Fields")]
    [SerializeField] TMP_InputField cardsPerPlayerField;
    [SerializeField] TMP_InputField aiChancePrecentageField;
    [SerializeField] TMP_InputField invisibleCardField;
    [Header("Toggle")]
    [SerializeField] Toggle invisibleCardToggle;
    [Header("Default Settings")]
    [SerializeField] int defaultCardsPerPlayer;
    [SerializeField] int defaultAiChancePrecentage;
    [SerializeField] int defaultInvisibleCard;

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

        int invisibleCard;
        if (!PlayerPrefs.HasKey("InvisibleCard"))
        {
            invisibleCard = defaultInvisibleCard;
            invisibleCardToggle.isOn = false;
        }
        else
        {
            invisibleCard = PlayerPrefs.GetInt("InvisibleCard");
        }

        invisibleCardField.text = invisibleCard.ToString();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            CheckCardsPerPlayer();
            CheckAiPrecentage();
            CheckInvisibleCard();
        }

        invisibleCardField.gameObject.SetActive(invisibleCardToggle.isOn);
    }

    void CheckCardsPerPlayer()
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

    void CheckInvisibleCard()
    {
        bool isOn = invisibleCardToggle.isOn;
        if (!isOn) { return; }

        int number;
        int.TryParse(invisibleCardField.text, out number);

        if (number > 13)
        {
            number = 13;
        }

        invisibleCardField.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        invisibleCardField.text = number.ToString();
    }
}
