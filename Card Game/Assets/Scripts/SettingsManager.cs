using TMPro;
using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    [SerializeField] TMP_InputField cardsPerPlayerField;

    void Start()
    {
        int cardsPerPlayer = PlayerPrefs.GetInt("CardsPerPlayer");
        if (cardsPerPlayer == 0)
        {
            cardsPerPlayer = 3;
        }

        cardsPerPlayerField.text = cardsPerPlayer.ToString();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            CheckCardsPerPlayer();
            CheckOther();
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

    void CheckOther()
    {

    }
}
