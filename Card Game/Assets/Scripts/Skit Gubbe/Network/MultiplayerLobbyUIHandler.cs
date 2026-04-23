using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class MultiplayerLobbyUIHandler : MultiplayerLobby
{
    [Header("Panels")]
    [SerializeField] GameObject hostPanel;
    [SerializeField] GameObject codePanel;
    [SerializeField] GameObject loadingPanel;
    [SerializeField] GameObject errorPanel;
    [Header("Loading Logic")]
    [SerializeField] TextMeshProUGUI loadingText;
    [SerializeField] float loadTextDelay;
    [SerializeField] TMP_InputField codeInputField;

    Coroutine loadingCoroutine;

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
        CloseCodePanel();
        _ = HandleJoinLobby();
    }

    async Task HandleJoinLobby()
    {
        bool success = await JoinLobby(codeInputField.text.Trim());
        if (!success)
        {
            CloseLoadingPanel();
            OpenErrorPanel();
        }
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
        loadingCoroutine = StartCoroutine(HandleLoading());
    }

    void OpenErrorPanel()
    {
        errorPanel.SetActive(true);
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
        if (loadingCoroutine != null) StopCoroutine(loadingCoroutine);
        loadingPanel.SetActive(false);
    }

    public void CloseErrorPanel()
    {
        errorPanel.SetActive(false);
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
        errorPanel.SetActive(false);
    }
}
