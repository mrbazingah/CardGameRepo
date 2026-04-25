using TMPro;
using UnityEngine;

public class PlayerProfile : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI displayNameText;

    void Start()
    {
        displayNameText.text =  PlayerPrefs.GetString("DisplayName");
    }
}
