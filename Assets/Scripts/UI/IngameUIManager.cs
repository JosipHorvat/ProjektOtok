using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Owns the persistent authored UI used only while a gameplay scene is active.</summary>
public sealed class IngameUIManager : MonoBehaviour
{
    public static IngameUIManager Instance { get; private set; }

    #region Pause Menu

    [Header("Pause Menu")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button firstSelectedButton;

    /// <summary>Opens or closes the authored pause menu.</summary>
    public void TogglePauseMenu()
    {
        if (!gameplaySceneActive || pauseMenuPanel == null || GameManager.Instance == null)
        {
            return;
        }

        if (pauseMenuPanel.activeSelf)
        {
            ResumeGame();
            return;
        }

        GameManager.Instance.PauseGame();
        pauseMenuPanel.SetActive(true);
        optionsMenuPanel?.SetActive(false);
        SelectButton(firstSelectedButton != null ? firstSelectedButton : resumeButton);
    }

    /// <summary>Resumes gameplay and closes the authored menus.</summary>
    public void ResumeGame()
    {
        GameManager.Instance?.ResumeGame();
        CloseMenus();
    }

    /// <summary>Saves the active game and loads the configured main menu.</summary>
    public void LoadMainMenu()
    {
        if (!gameplaySceneActive)
        {
            return;
        }

        SaveManager.Instance?.SaveGame();
        ResumeGame();
        SceneManagement.Instance?.LoadMainMenu();
    }

    /// <summary>Saves the active game and quits the application.</summary>
    public void QuitGame()
    {
        if (!gameplaySceneActive)
        {
            return;
        }

        SaveManager.Instance?.SaveGame();
        ResumeGame();
        SceneManagement.Instance?.QuitGame();
    }

    #endregion

    #region Options Menu

    [Header("Options Menu")]
    [SerializeField] private GameObject optionsMenuPanel;
    [SerializeField] private Button optionsBackButton;
    [SerializeField] private Button optionsFirstSelectedButton;
    [SerializeField] private Button[] optionsButtons;

    /// <summary>Opens the authored options panel while gameplay remains paused.</summary>
    public void OpenOptions()
    {
        if (!gameplaySceneActive)
        {
            return;
        }

        pauseMenuPanel?.SetActive(false);
        optionsMenuPanel?.SetActive(true);
        SelectButton(optionsFirstSelectedButton != null ? optionsFirstSelectedButton : optionsBackButton);
    }

    /// <summary>Returns from the authored options panel to the pause panel.</summary>
    public void CloseOptions()
    {
        if (!gameplaySceneActive)
        {
            return;
        }

        optionsMenuPanel?.SetActive(false);
        pauseMenuPanel?.SetActive(true);
        SelectButton(firstSelectedButton != null ? firstSelectedButton : resumeButton);
    }

    #endregion

    #region Scene Availability and Input

    private bool gameplaySceneActive;

    private InputSystem_Actions inputActions;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        inputActions = new InputSystem_Actions();
        inputActions.Player.Enable();
        ConfigureNavigation();
        UpdateSceneAvailability(SceneManager.GetActiveScene().name);
        CloseMenus();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        RegisterButtonListeners();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnregisterButtonListeners();
        inputActions?.Player.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        inputActions?.Dispose();
    }

    private void Update()
    {
        if (gameplaySceneActive && inputActions != null && inputActions.Player.Pause.WasPressedThisFrame())
        {
            TogglePauseMenu();
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateSceneAvailability(scene.name);
        CloseMenus();
        ConfigureNavigation();
    }

    private void UpdateSceneAvailability(string sceneName)
    {
        gameplaySceneActive = SceneManagement.Instance == null
            ? !string.Equals(sceneName, "Bootstrap", StringComparison.OrdinalIgnoreCase)
            : SceneManagement.Instance.IsGameplayScene(sceneName);
        if (!gameplaySceneActive)
        {
            GameManager.Instance?.ResumeGame();
        }
    }

    private void RegisterButtonListeners()
    {
        resumeButton?.onClick.RemoveListener(ResumeGame);
        resumeButton?.onClick.AddListener(ResumeGame);
        optionsButton?.onClick.RemoveListener(OpenOptions);
        optionsButton?.onClick.AddListener(OpenOptions);
        mainMenuButton?.onClick.RemoveListener(LoadMainMenu);
        mainMenuButton?.onClick.AddListener(LoadMainMenu);
        quitButton?.onClick.RemoveListener(QuitGame);
        quitButton?.onClick.AddListener(QuitGame);
        optionsBackButton?.onClick.RemoveListener(CloseOptions);
        optionsBackButton?.onClick.AddListener(CloseOptions);
    }

    private void UnregisterButtonListeners()
    {
        resumeButton?.onClick.RemoveListener(ResumeGame);
        optionsButton?.onClick.RemoveListener(OpenOptions);
        mainMenuButton?.onClick.RemoveListener(LoadMainMenu);
        quitButton?.onClick.RemoveListener(QuitGame);
        optionsBackButton?.onClick.RemoveListener(CloseOptions);
    }

    private void CloseMenus()
    {
        pauseMenuPanel?.SetActive(false);
        optionsMenuPanel?.SetActive(false);
        EventSystem.current?.SetSelectedGameObject(null);
    }

    private static void SelectButton(Button button)
    {
        if (EventSystem.current != null && button != null && button.IsActive() && button.IsInteractable())
        {
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
    }

    private void ConfigureNavigation()
    {
        ConfigureVerticalNavigation(resumeButton, optionsButton, null);
        ConfigureVerticalNavigation(optionsButton, mainMenuButton, resumeButton);
        ConfigureVerticalNavigation(mainMenuButton, quitButton, optionsButton);
        ConfigureVerticalNavigation(quitButton, null, mainMenuButton);

        if (optionsButtons == null || optionsButtons.Length == 0)
        {
            optionsButtons = optionsMenuPanel != null ? optionsMenuPanel.GetComponentsInChildren<Button>(true) : Array.Empty<Button>();
        }

        int optionButtonCount = optionsButtons.Length;
        if (optionButtonCount > 0 && optionsButtons[optionButtonCount - 1] == optionsBackButton)
        {
            optionButtonCount--;
        }

        for (int index = 0; index < optionButtonCount; index++)
        {
            Button up = index > 0 ? optionsButtons[index - 1] : null;
            Button down = index + 1 < optionButtonCount ? optionsButtons[index + 1] : optionsBackButton;
            ConfigureVerticalNavigation(optionsButtons[index], down, up);
        }

        ConfigureVerticalNavigation(optionsBackButton, null, optionButtonCount > 0 ? optionsButtons[optionButtonCount - 1] : optionsFirstSelectedButton);
    }

    private static void ConfigureVerticalNavigation(Button button, Button down, Button up)
    {
        if (button == null)
        {
            return;
        }

        button.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            wrapAround = false,
            selectOnUp = up,
            selectOnDown = down,
            selectOnLeft = null,
            selectOnRight = null
        };
    }

    #endregion
}
