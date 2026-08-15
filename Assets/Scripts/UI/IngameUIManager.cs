using System;
using System.Collections.Generic;
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
        AudioManager.Instance?.PauseMusic();
        pauseMenuPanel.SetActive(true);
        optionsMenuPanel?.SetActive(false);
        SelectSelectable(firstSelectedButton != null ? firstSelectedButton : resumeButton);
    }

    /// <summary>Resumes gameplay and closes the authored menus.</summary>
    public void ResumeGame()
    {
        GameManager.Instance?.ResumeGame();
        AudioManager.Instance?.ResumeMusic();
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
    [SerializeField] private Selectable optionsFirstSelectedControl;
    [SerializeField] private Selectable[] optionsControls;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    /// <summary>Opens the authored options panel while gameplay remains paused.</summary>
    public void OpenOptions()
    {
        if (!gameplaySceneActive)
        {
            return;
        }

        pauseMenuPanel?.SetActive(false);
        optionsMenuPanel?.SetActive(true);
        SyncAudioSliders();
        SelectSelectable(optionsFirstSelectedControl != null ? optionsFirstSelectedControl : optionsFirstSelectedButton);
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
        SelectSelectable(firstSelectedButton != null ? firstSelectedButton : resumeButton);
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
            AudioManager.Instance?.ResumeMusic();
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
        RegisterAudioSliderListeners();
    }

    private void UnregisterButtonListeners()
    {
        resumeButton?.onClick.RemoveListener(ResumeGame);
        optionsButton?.onClick.RemoveListener(OpenOptions);
        mainMenuButton?.onClick.RemoveListener(LoadMainMenu);
        quitButton?.onClick.RemoveListener(QuitGame);
        optionsBackButton?.onClick.RemoveListener(CloseOptions);
        UnregisterAudioSliderListeners();
    }

    private void CloseMenus()
    {
        pauseMenuPanel?.SetActive(false);
        optionsMenuPanel?.SetActive(false);
        EventSystem.current?.SetSelectedGameObject(null);
    }

    private static void SelectSelectable(Selectable selectable)
    {
        if (EventSystem.current != null && selectable != null && selectable.IsActive() && selectable.IsInteractable())
        {
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }

    private void ConfigureNavigation()
    {
        ConfigureVerticalNavigation(resumeButton, optionsButton, null);
        ConfigureVerticalNavigation(optionsButton, mainMenuButton, resumeButton);
        ConfigureVerticalNavigation(mainMenuButton, quitButton, optionsButton);
        ConfigureVerticalNavigation(quitButton, null, mainMenuButton);

        if (optionsControls == null || optionsControls.Length == 0)
        {
            optionsControls = optionsMenuPanel != null
                ? optionsMenuPanel.GetComponentsInChildren<Selectable>(true)
                : Array.Empty<Selectable>();
        }

        List<Selectable> navigableOptions = new List<Selectable>();
        foreach (Selectable control in optionsControls)
        {
            if (control != null && control != optionsBackButton)
            {
                navigableOptions.Add(control);
            }
        }

        for (int index = 0; index < navigableOptions.Count; index++)
        {
            Selectable up = index > 0 ? navigableOptions[index - 1] : null;
            Selectable down = index + 1 < navigableOptions.Count ? navigableOptions[index + 1] : optionsBackButton;
            ConfigureVerticalNavigation(navigableOptions[index], down, up);
        }

        Selectable lastOption = navigableOptions.Count > 0
            ? navigableOptions[navigableOptions.Count - 1]
            : optionsFirstSelectedControl != null ? optionsFirstSelectedControl : optionsFirstSelectedButton;
        ConfigureVerticalNavigation(optionsBackButton, null, lastOption);
    }

    private static void ConfigureVerticalNavigation(Selectable selectable, Selectable down, Selectable up)
    {
        if (selectable == null)
        {
            return;
        }

        selectable.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            wrapAround = false,
            selectOnUp = up,
            selectOnDown = down,
            selectOnLeft = null,
            selectOnRight = null
        };
    }

    private void RegisterAudioSliderListeners()
    {
        masterVolumeSlider?.onValueChanged.RemoveListener(SetMasterVolume);
        musicVolumeSlider?.onValueChanged.RemoveListener(SetMusicVolume);
        sfxVolumeSlider?.onValueChanged.RemoveListener(SetSFXVolume);

        masterVolumeSlider?.onValueChanged.AddListener(SetMasterVolume);
        musicVolumeSlider?.onValueChanged.AddListener(SetMusicVolume);
        sfxVolumeSlider?.onValueChanged.AddListener(SetSFXVolume);
    }

    private void UnregisterAudioSliderListeners()
    {
        masterVolumeSlider?.onValueChanged.RemoveListener(SetMasterVolume);
        musicVolumeSlider?.onValueChanged.RemoveListener(SetMusicVolume);
        sfxVolumeSlider?.onValueChanged.RemoveListener(SetSFXVolume);
    }

    private void SyncAudioSliders()
    {
        if (AudioManager.Instance == null)
        {
            return;
        }

        masterVolumeSlider?.SetValueWithoutNotify(AudioManager.Instance.MasterVolume);
        musicVolumeSlider?.SetValueWithoutNotify(AudioManager.Instance.MusicVolume);
        sfxVolumeSlider?.SetValueWithoutNotify(AudioManager.Instance.SFXVolume);
    }

    private static void SetMasterVolume(float value)
    {
        AudioManager.Instance?.SetMasterVolume(value);
    }

    private static void SetMusicVolume(float value)
    {
        AudioManager.Instance?.SetMusicVolume(value);
    }

    private static void SetSFXVolume(float value)
    {
        AudioManager.Instance?.SetSFXVolume(value);
    }

    #endregion
}
