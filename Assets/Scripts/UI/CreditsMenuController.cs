using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Handles the Credits scene's local return input and return button.</summary>
public sealed class CreditsMenuController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    private Button returnButton;

    private void Awake()
    {
        returnButton = GetComponentInChildren<Button>(true);
        returnButton?.onClick.AddListener(ReturnToMainMenu);
    }

    private void OnDestroy()
    {
        returnButton?.onClick.RemoveListener(ReturnToMainMenu);
    }

    private void Update()
    {
        bool keyboardBack = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool controllerBack = Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame;

        if (keyboardBack || controllerBack)
        {
            ReturnToMainMenu();
        }
    }

    /// <summary>Returns from the Credits scene to the main menu without saving gameplay progress.</summary>
    public void ReturnToMainMenu()
    {
        if (global::SceneManagement.Instance != null)
        {
            string mainMenuScene = global::SceneManagement.Instance.MainMenuSceneName;
            global::SceneManagement.Instance.LoadScene(string.IsNullOrWhiteSpace(mainMenuScene) ? MainMenuSceneName : mainMenuScene);
            return;
        }

        SceneManager.LoadScene(MainMenuSceneName);
    }
}
