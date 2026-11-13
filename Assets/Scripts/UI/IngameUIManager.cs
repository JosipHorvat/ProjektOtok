using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

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
        if ((shopMenuUI != null && shopMenuUI.IsOpen) ||
            (workbenchMenuUI != null && workbenchMenuUI.IsOpen))
        {
            ResumeGame();
            return;
        }

        if (stationPrototypePanel != null && stationPrototypePanel.activeSelf)
        {
            ResumeGame();
            return;
        }

        if (inventoryMenuUI != null && inventoryMenuUI.IsOpen)
        {
            ResumeGame();
            return;
        }

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
        RestoreCursorState();
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

        inventoryMenuUI?.HideForOtherModal();
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

    #region Inventory Menu

    private InventoryMenuUI inventoryMenuUI;
    private ShopMenuUI shopMenuUI;
    private WorkbenchMenuUI workbenchMenuUI;
    private GameObject stationPrototypePanel;
    private GameObject stationPrototypePreviouslySelectedObject;
    private bool hasSavedCursorState;
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;

    /// <summary>Opens the inventory modal, pauses gameplay, and routes input into UI navigation.</summary>
    public void OpenInventoryMenu()
    {
        OpenInventoryMenu(null);
    }

    /// <summary>Opens the safe-room stash modal while preserving the existing pause and cursor flow.</summary>
    public void OpenStashMenu(SafeRoomStash stash)
    {
        if (stash == null)
        {
            return;
        }

        OpenInventoryMenu(stash);
    }

    /// <summary>Opens the configured Credits shop and pauses gameplay through the existing modal owner.</summary>
    public void OpenShopMenu(ShopStation shopStation)
    {
        if (!gameplaySceneActive || shopStation == null || shopMenuUI == null || GameManager.Instance == null ||
            GameManager.Instance.IsPaused)
        {
            return;
        }

        stationPrototypePreviouslySelectedObject = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;
        pauseMenuPanel?.SetActive(false);
        optionsMenuPanel?.SetActive(false);
        inventoryMenuUI?.HideForOtherModal();
        GameManager.Instance.PauseGame();
        AudioManager.Instance?.PauseMusic();
        SaveAndShowCursor();
        if (!shopMenuUI.Open(shopStation))
        {
            ResumeGame();
        }
    }

    /// <summary>Opens the workbench modal for the player in range and pauses gameplay.</summary>
    public void OpenWorkbenchMenu(WorkbenchStation workbenchStation)
    {
        if (!gameplaySceneActive || workbenchStation == null || workbenchMenuUI == null || GameManager.Instance == null ||
            GameManager.Instance.IsPaused)
        {
            return;
        }

        stationPrototypePreviouslySelectedObject = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;
        pauseMenuPanel?.SetActive(false);
        optionsMenuPanel?.SetActive(false);
        inventoryMenuUI?.HideForOtherModal();
        GameManager.Instance.PauseGame();
        AudioManager.Instance?.PauseMusic();
        SaveAndShowCursor();
        if (!workbenchMenuUI.Open(workbenchStation))
        {
            ResumeGame();
        }
    }

    private void OpenInventoryMenu(SafeRoomStash stash)
    {
        if ((shopMenuUI != null && shopMenuUI.IsOpen) ||
            (workbenchMenuUI != null && workbenchMenuUI.IsOpen) ||
            (stationPrototypePanel != null && stationPrototypePanel.activeSelf))
        {
            ResumeGame();
        }

        if (!gameplaySceneActive || inventoryMenuUI == null || GameManager.Instance == null)
        {
            return;
        }

        bool alreadyPausedByMenu =
            (pauseMenuPanel != null && pauseMenuPanel.activeSelf) ||
            (optionsMenuPanel != null && optionsMenuPanel.activeSelf);
        if (!alreadyPausedByMenu)
        {
            GameManager.Instance.PauseGame();
            AudioManager.Instance?.PauseMusic();
        }

        pauseMenuPanel?.SetActive(false);
        optionsMenuPanel?.SetActive(false);
        SaveAndShowCursor();
        bool opened = stash != null ? inventoryMenuUI.OpenStash(stash) : inventoryMenuUI.Open();
        if (!opened)
        {
            if (alreadyPausedByMenu)
            {
                pauseMenuPanel?.SetActive(true);
            }
            else
            {
                ResumeGame();
            }
        }
    }

    /// <summary>Opens a paused, non-transactional prototype menu for a shop or workbench station.</summary>
    public void OpenStationPrototype(string stationTitle, string prototypeMessage)
    {
        if (!gameplaySceneActive || GameManager.Instance == null || GameManager.Instance.IsPaused ||
            (stationPrototypePanel != null && stationPrototypePanel.activeSelf))
        {
            return;
        }

        stationPrototypePreviouslySelectedObject = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;
        inventoryMenuUI?.HideForOtherModal();
        pauseMenuPanel?.SetActive(false);
        optionsMenuPanel?.SetActive(false);
        GameManager.Instance.PauseGame();
        AudioManager.Instance?.PauseMusic();
        SaveAndShowCursor();
        BuildStationPrototypePanel();

        Transform card = stationPrototypePanel.transform.Find("Card");
        Text titleText = card != null ? card.Find("Title")?.GetComponent<Text>() : null;
        Text messageText = card != null ? card.Find("Message")?.GetComponent<Text>() : null;
        Text statusText = card != null ? card.Find("Prototype Status")?.GetComponent<Text>() : null;
        if (titleText != null)
        {
            titleText.text = string.IsNullOrWhiteSpace(stationTitle) ? "Station" : stationTitle;
        }

        if (messageText != null)
        {
            messageText.text = string.IsNullOrWhiteSpace(prototypeMessage)
                ? "This station is a prototype. No items or currency will be changed."
                : prototypeMessage;
        }

        if (statusText != null)
        {
            statusText.text = CreateStationPrototypeOptions(stationTitle);
        }

        stationPrototypePanel.SetActive(true);
        Button closeButton = card != null ? card.Find("Close Station Menu")?.GetComponent<Button>() : null;
        SelectSelectable(closeButton);
    }

    private static string CreateStationPrototypeOptions(string stationTitle)
    {
        if (!string.IsNullOrWhiteSpace(stationTitle) && stationTitle.IndexOf("shop", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "PROTOTYPE CATALOG\nPistol ammunition  —  Not available yet\nRifle ammunition  —  Not available yet\nWeapon supplies  —  Not available yet\n\nCurrency and buy/sell transactions are not enabled.";
        }

        return "PROTOTYPE SERVICES\nWeapon tuning  —  Not available yet\nMagazine work  —  Not available yet\nAttachment installation  —  Not available yet\n\nNo item will be consumed or modified.";
    }

    private void BuildStationPrototypePanel()
    {
        if (stationPrototypePanel != null)
        {
            return;
        }

        stationPrototypePanel = new GameObject("Station Prototype Menu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        stationPrototypePanel.transform.SetParent(transform, false);
        RectTransform rootRect = stationPrototypePanel.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        Image backdrop = stationPrototypePanel.GetComponent<Image>();
        backdrop.color = new Color(0.01f, 0.014f, 0.02f, 0.86f);
        backdrop.raycastTarget = true;

        GameObject cardObject = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardObject.transform.SetParent(stationPrototypePanel.transform, false);
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.2f, 0.12f);
        cardRect.anchorMax = new Vector2(0.8f, 0.88f);
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;
        cardObject.GetComponent<Image>().color = new Color(0.055f, 0.065f, 0.08f, 1f);

        Font stationFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CreateStationLabel("Title", cardRect, stationFont, string.Empty, 34, TextAnchor.MiddleCenter,
            new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.97f));
        Text message = CreateStationLabel("Message", cardRect, stationFont, string.Empty, 19, TextAnchor.MiddleCenter,
            new Vector2(0.08f, 0.66f), new Vector2(0.92f, 0.82f));
        message.horizontalOverflow = HorizontalWrapMode.Wrap;
        Text status = CreateStationLabel("Prototype Status", cardRect, stationFont, string.Empty, 21, TextAnchor.UpperLeft,
            new Vector2(0.12f, 0.22f), new Vector2(0.88f, 0.62f));
        status.horizontalOverflow = HorizontalWrapMode.Wrap;
        CreateStationButton("Close Station Menu", cardRect, stationFont, "Close  /  Back",
            new Vector2(0.34f, 0.06f), new Vector2(0.66f, 0.17f), ResumeGame);
        stationPrototypePanel.SetActive(false);
    }

    private static Text CreateStationLabel(
        string objectName,
        Transform parent,
        Font font,
        string text,
        int fontSize,
        TextAnchor alignment,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(parent, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = anchorMin;
        labelRect.anchorMax = anchorMax;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        Text label = labelObject.GetComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private Button CreateStationButton(
        string objectName,
        Transform parent,
        Font font,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = anchorMin;
        buttonRect.anchorMax = anchorMax;
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        Image background = buttonObject.GetComponent<Image>();
        background.color = new Color(0.17f, 0.21f, 0.27f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(onClick);
        Text buttonLabel = CreateStationLabel("Label", buttonRect, font, label, 20, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one);
        buttonLabel.raycastTarget = false;
        return button;
    }

    private void SaveAndShowCursor()
    {
        if (!hasSavedCursorState)
        {
            previousCursorLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            hasSavedCursorState = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreCursorState()
    {
        if (!hasSavedCursorState)
        {
            return;
        }

        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;
        hasSavedCursorState = false;
    }

    #endregion


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
        inventoryMenuUI = GetComponent<InventoryMenuUI>();
        shopMenuUI = GetComponent<ShopMenuUI>();
        workbenchMenuUI = GetComponent<WorkbenchMenuUI>();
        if (shopMenuUI == null)
        {
            shopMenuUI = gameObject.AddComponent<ShopMenuUI>();
        }

        if (workbenchMenuUI == null)
        {
            workbenchMenuUI = gameObject.AddComponent<WorkbenchMenuUI>();
        }

        ConfigureNavigation();
        UpdateSceneAvailability(SceneManager.GetActiveScene().name);
        CloseMenus();
    }

    private void OnEnable()
    {
        inputActions?.Player.Enable();
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ItemPickupFeedbackUI.OpenInventoryRequested += OpenInventoryMenu;
        RegisterButtonListeners();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        ItemPickupFeedbackUI.OpenInventoryRequested -= OpenInventoryMenu;
        UnregisterButtonListeners();
        inputActions?.Player.Disable();
        if (Instance == this)
        {
            ResumeGame();
        }
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
        if (!gameplaySceneActive || inputActions == null)
        {
            return;
        }

        if ((shopMenuUI != null && shopMenuUI.IsOpen) ||
            (workbenchMenuUI != null && workbenchMenuUI.IsOpen))
        {
            if (inputActions.Player.Pause.WasPressedThisFrame())
            {
                ResumeGame();
                return;
            }

            if (inputActions.Player.OpenInventory.WasPressedThisFrame())
            {
                ResumeGame();
                OpenInventoryMenu();
            }

            return;
        }

        if (stationPrototypePanel != null && stationPrototypePanel.activeSelf)
        {
            if (inputActions.Player.Pause.WasPressedThisFrame())
            {
                ResumeGame();
                return;
            }

            if (inputActions.Player.OpenInventory.WasPressedThisFrame())
            {
                ResumeGame();
                OpenInventoryMenu();
            }

            return;
        }

        if (inputActions.Player.Pause.WasPressedThisFrame())
        {
            TogglePauseMenu();
            return;
        }

        if (inputActions.Player.OpenInventory.WasPressedThisFrame())
        {
            if (inventoryMenuUI != null && inventoryMenuUI.IsOpen)
            {
                ResumeGame();
            }
            else
            {
                OpenInventoryMenu();
            }
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
        inventoryMenuUI?.Close();
        shopMenuUI?.CloseFromOwner();
        workbenchMenuUI?.CloseFromOwner();
        if (stationPrototypePanel != null)
        {
            stationPrototypePanel.SetActive(false);
        }

        pauseMenuPanel?.SetActive(false);
        optionsMenuPanel?.SetActive(false);
        if (stationPrototypePreviouslySelectedObject != null && stationPrototypePreviouslySelectedObject.activeInHierarchy)
        {
            EventSystem.current?.SetSelectedGameObject(stationPrototypePreviouslySelectedObject);
        }
        else
        {
            EventSystem.current?.SetSelectedGameObject(null);
        }

        stationPrototypePreviouslySelectedObject = null;
        RestoreCursorState();
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
