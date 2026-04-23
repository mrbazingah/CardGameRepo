using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class MultiplayerLobbyUIHandler : MultiplayerLobby
{
    [SerializeField] GameObject hostPanel;
    [SerializeField] GameObject codePanel;
    [SerializeField] GameObject loadingPanel;
    [SerializeField] TextMeshProUGUI loadingText;
    [SerializeField] float loadTextDelay;
    [SerializeField] TMP_InputField codeInputField;

    void Start()
    {
        CloseAll();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) && codePanel.activeInHierarchy) 
        {
            JoinLobby();
        }
    }

    public void HostLobby()
    {
        _ = CreateLobby();
    }

    public void JoinLobby()
    {
        OpenLoadingPanel();
        _ = HandleJoinLobby();
    }

    async Task HandleJoinLobby()
    {
        bool success = await JoinLobby(codeInputField.text.Trim());
        if (!success) CloseLoadingPanel();
    }

    public void OpenHostPanel()
    {
        hostPanel.SetActive(true);
    }

    public void OpenCodePanel()
    {
        codePanel.SetActive(true);
    }

    public void OpenLoadingPanel()
    {
        StartCoroutine(HandleLoading());
    }

    public void CloseHostPanel()
    {
        hostPanel.SetActive(false);
    }

    public void CloseCodePanel()
    {
        codePanel.SetActive(false);
    }

    public void CloseLoadingPanel()
    {
        loadingPanel.SetActive(false);
    }

    IEnumerator HandleLoading()
    {
        loadingPanel.SetActive(true);

        while (true)
        {
            loadingText.text = "Loading...";

            yield return new WaitForSeconds(loadTextDelay);

            loadingText.text = "Loading.";

            yield return new WaitForSeconds(loadTextDelay);

            loadingText.text = "Loading..";

            yield return new WaitForSeconds(loadTextDelay);
        }
    }

    public void CloseAll()
    {
        hostPanel.SetActive(false);
        codePanel.SetActive(false);
        loadingPanel.SetActive(false);
    }
}
