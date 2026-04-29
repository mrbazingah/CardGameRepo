using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour
{
    [SerializeField] Button returnButton;

    PlayerInput playerInput;
    InputAction returnAction;

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (playerInput == null || returnAction == null)
        {
            playerInput = InputManager.Instance.GetPlayerInput();
            returnAction = playerInput.actions.FindAction("Return");
        }

        ReturnWithESC();
    }

    void ReturnWithESC()
    {
        if (returnAction.WasPressedThisFrame() && returnButton != null)
        {
            returnButton.onClick.Invoke();
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
