using TMPro;
using UnityEngine;

public class ProfilePanelManager : MonoBehaviour
{
    [SerializeField] GameObject profilePanel;
    [SerializeField] TMP_InputField displayNameInputField;
    [SerializeField] string defaultDisplayName;
    [SerializeField] string displayName;

    void Start()
    {
        if (PlayerPrefs.HasKey("DisplayName"))
        {
            displayName = PlayerPrefs.GetString("DisplayName");
        }
        else
        {
            displayName = defaultDisplayName;
            PlayerPrefs.SetString("DisplayName", displayName);
        }

        displayNameInputField.text = displayName;

        Close();
    }

    public void OnChange()
    {
        displayName = displayNameInputField.text == "" ? defaultDisplayName : displayNameInputField.text;
        displayNameInputField.text = displayName;
        PlayerPrefs.SetString("DisplayName", displayName);
    }

    public void Open()
    {
        profilePanel.SetActive(true);
    }

    public void Close()
    {
        profilePanel.SetActive(false);
    }
}
