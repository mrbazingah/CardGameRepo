using TMPro;
using UnityEngine;

public class PlayerProfile : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI displayNameText;
    string displayName;

    void Start()
    {
        displayName = PlayerPrefs.GetString("DisplayName");
        displayNameText.text =  displayName;
    }

    public string GetDisplayName()
    {
        return displayName;
    }
}
