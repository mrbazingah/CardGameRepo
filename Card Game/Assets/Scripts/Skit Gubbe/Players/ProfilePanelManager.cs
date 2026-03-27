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
        }

        displayNameInputField.text = displayName;

        Close();
    }

    public void OnChange()
    {
        displayName = displayNameInputField.text;
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
