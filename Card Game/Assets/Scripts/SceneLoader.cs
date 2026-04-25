using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour
{
    [SerializeField] Button returnButton;

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
        Time.timeScale = 1f;
    }

    void Update()
    {
        ReturnWithESC();
    }

    void ReturnWithESC()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && returnButton != null)
        {
            returnButton.onClick.Invoke();
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
