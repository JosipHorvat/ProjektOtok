using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controls the main menu and the authored save-slot canvas.
/// </summary>
public class MainMenuManager : MonoBehaviour
{

    [Header("Menu Buttons")]
    [SerializeField] private Button firstSelectedButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("Authored Save Slots UI")]
    [SerializeField] private GameObject saveSlotsCanvas;
    [SerializeField] private Button[] saveSlotButtons;
    [SerializeField] private Text[] saveSlotMetadata;

    [SerializeField] private Button[] deleteSlotButtons;
    [SerializeField] private Button returnButton;

    private GameObject mainMenuPanel;

    private void Start()
    {
        mainMenuPanel = newGameButton != null ? newGameButton.transform.parent.gameObject : null;
        ConfigureSaveSlotButtons();
        SelectFirstButton();

        newGameButton?.onClick.AddListener(StartNewGame);
        loadGameButton?.onClick.AddListener(LoadGame);
        optionsButton?.onClick.AddListener(OpenOptionsMenu);
        creditsButton?.onClick.AddListener(OpenCredits);
        quitButton?.onClick.AddListener(QuitGame);
    }

    private void OnDestroy()
    {
        newGameButton?.onClick.RemoveListener(StartNewGame);
        loadGameButton?.onClick.RemoveListener(LoadGame);
        optionsButton?.onClick.RemoveListener(OpenOptionsMenu);
        creditsButton?.onClick.RemoveListener(OpenCredits);
        quitButton?.onClick.RemoveListener(QuitGame);

        if (saveSlotButtons != null)
        {
            foreach (Button saveSlotButton in saveSlotButtons)
            {
                saveSlotButton?.onClick.RemoveAllListeners();
            }
        }

        if (deleteSlotButtons != null)
        {
            foreach (Button deleteButton in deleteSlotButtons)
            {
                deleteButton?.onClick.RemoveAllListeners();
            }
        }

        returnButton?.onClick.RemoveListener(CloseLoadGame);
    }

    /// <summary>Starts a new game in the first available save slot.</summary>
    public void StartNewGame()
    {
        if (SceneManagement.Instance == null)
        {
            Debug.LogError("MainMenuManager: SceneManagement is not available.", this);
            return;
        }

        SceneManagement.Instance.StartNewGame();
    }

    /// <summary>Opens the authored Load Game panel and refreshes all configured slots.</summary>
    public void LoadGame()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogError("MainMenuManager: SaveManager is not available.", this);
            return;
        }

        if (!HasValidSaveSlotReferences())
        {
            return;
        }

        RefreshSaveSlots();
        mainMenuPanel?.SetActive(false);
        saveSlotsCanvas.SetActive(true);
        SelectFirstAvailableSaveSlot();
    }

    /// <summary>Closes the Load Game panel and returns to the main menu.</summary>
    public void CloseLoadGame()
    {
        saveSlotsCanvas?.SetActive(false);
        mainMenuPanel?.SetActive(true);
        SelectFirstButton();
    }

    /// <summary>Loads an occupied save slot.</summary>
    public void LoadSaveSlot(int slotIndex)
    {
        if (SceneManagement.Instance == null || SaveManager.Instance == null)
        {
            return;
        }

        if (!SaveManager.Instance.HasSavedData(slotIndex))
        {
            return;
        }

        SceneManagement.Instance.LoadSavedGame(slotIndex);
    }

    /// <summary>Deletes an occupied save slot and refreshes the authored slot canvas.</summary>
    public void DeleteSaveSlot(int slotIndex)
    {
        if (SaveManager.Instance == null || !SaveManager.Instance.HasSavedData(slotIndex))
        {
            return;
        }

        SaveManager.Instance.DeleteSlot(slotIndex);
        RefreshSaveSlots();
        SelectFirstAvailableSaveSlot();
    }


    public void OpenOptionsMenu()
    {
        Debug.Log("MainMenuManager: Options menu is not implemented yet.", this);
    }

    /// <summary>Loads the dedicated non-gameplay Credits scene.</summary>
    public void OpenCredits()
    {
        if (SceneManagement.Instance == null)
        {
            Debug.LogError("MainMenuManager: SceneManagement is not available.", this);
            return;
        }

        SceneManagement.Instance.LoadScene("Credits");
    }

    /// <summary>Quits the running application or exits Play Mode in the Unity Editor.</summary>
    public void QuitGame()
    {
        SceneManagement.Instance?.QuitGame();
    }

    private void ConfigureSaveSlotButtons()
    {
        if (!HasValidSaveSlotReferences())
        {
            return;
        }

        for (int slotIndex = 0; slotIndex < saveSlotButtons.Length; slotIndex++)
        {
            int selectedSlotIndex = slotIndex;
            Button slotButton = saveSlotButtons[slotIndex];
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(() => LoadSaveSlot(selectedSlotIndex));

            Button deleteButton = deleteSlotButtons[slotIndex];
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => DeleteSaveSlot(selectedSlotIndex));
        }

        ConfigureExplicitNavigation();
        returnButton.onClick.RemoveAllListeners();
        returnButton.onClick.AddListener(CloseLoadGame);
        saveSlotsCanvas.SetActive(false);
    }

    private void RefreshSaveSlots()
    {
        int slotCount = Mathf.Min(saveSlotButtons.Length, saveSlotMetadata.Length, SaveManager.SlotCount);
        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            SaveSlotInfo slotInfo = SaveManager.Instance.GetSlotInfo(slotIndex);
            saveSlotButtons[slotIndex].interactable = true;
            deleteSlotButtons[slotIndex].gameObject.SetActive(slotInfo.IsOccupied);
            deleteSlotButtons[slotIndex].interactable = slotInfo.IsOccupied;
            saveSlotMetadata[slotIndex].text = BuildSlotLabel(slotInfo);
        }
    }

    private void SelectFirstAvailableSaveSlot()
    {
        Button selectedButton = returnButton;
        if (saveSlotButtons != null)
        {
            foreach (Button slotButton in saveSlotButtons)
            {
                if (slotButton != null && slotButton.interactable)
                {
                    selectedButton = slotButton;
                    break;
                }
            }
        }

        EventSystem.current?.SetSelectedGameObject(selectedButton != null ? selectedButton.gameObject : null);
    }

    private static string BuildSlotLabel(SaveSlotInfo slotInfo)
    {
        if (!slotInfo.IsOccupied)
        {
            return $"Slot {slotInfo.SlotIndex + 1}  -  Empty";
        }

        string sceneName = string.IsNullOrWhiteSpace(slotInfo.SceneName) ? "Unknown scene" : slotInfo.SceneName;
        string lastSaved = "Unknown date";
        if (DateTime.TryParse(slotInfo.LastSavedAtUtc, out DateTime savedAt))
        {
            lastSaved = savedAt.ToLocalTime().ToString("g");
        }

        TimeSpan playtime = TimeSpan.FromSeconds(Mathf.Max(0f, slotInfo.PlaytimeSeconds));
        return $"Slot {slotInfo.SlotIndex + 1}  |  {sceneName}  |  {lastSaved}  |  Playtime {playtime.Hours:D2}:{playtime.Minutes:D2}:{playtime.Seconds:D2}";
    }

    private bool HasValidSaveSlotReferences()
    {
        if (saveSlotsCanvas == null || returnButton == null || saveSlotButtons == null || saveSlotMetadata == null || deleteSlotButtons == null)
        {
            Debug.LogError("MainMenuManager: Assign Save Slots Canvas, Return button, slot buttons, and metadata fields.", this);
            return false;
        }

        if (saveSlotButtons.Length != SaveManager.SlotCount || saveSlotMetadata.Length != SaveManager.SlotCount || deleteSlotButtons.Length != SaveManager.SlotCount)
        {
            Debug.LogError($"MainMenuManager: The authored UI must contain exactly {SaveManager.SlotCount} slot buttons and metadata labels.", this);
            return false;
        }

        for (int index = 0; index < SaveManager.SlotCount; index++)
        {
            if (saveSlotButtons[index] == null || saveSlotMetadata[index] == null || deleteSlotButtons[index] == null)
            {
                Debug.LogError($"MainMenuManager: Save slot {index + 1} has an unassigned button or metadata label.", this);
                return false;
            }
        }

        return true;
    }

    private void SelectFirstButton()
    {
        EventSystem.current?.SetSelectedGameObject(firstSelectedButton != null ? firstSelectedButton.gameObject : null);
    }

    private void ConfigureExplicitNavigation()
    {
        for (int index = 0; index < saveSlotButtons.Length; index++)
        {
            Button slotButton = saveSlotButtons[index];
            Button deleteButton = deleteSlotButtons[index];
            Navigation slotNavigation = CreateNavigation(
                index > 0 ? saveSlotButtons[index - 1] : returnButton,
                index + 1 < saveSlotButtons.Length ? saveSlotButtons[index + 1] : returnButton,
                deleteButton,
                deleteButton);
            Navigation deleteNavigation = CreateNavigation(
                index > 0 ? deleteSlotButtons[index - 1] : returnButton,
                index + 1 < deleteSlotButtons.Length ? deleteSlotButtons[index + 1] : returnButton,
                slotButton,
                slotButton);
            slotButton.navigation = slotNavigation;
            deleteButton.navigation = deleteNavigation;
        }

        Navigation returnNavigation = CreateNavigation(
            saveSlotButtons.Length > 0 ? saveSlotButtons[saveSlotButtons.Length - 1] : null,
            null,
            null,
            null);
        returnButton.navigation = returnNavigation;
    }

    private static Navigation CreateNavigation(Button up, Button down, Button left, Button right)
    {
        return new Navigation
        {
            mode = Navigation.Mode.Explicit,
            wrapAround = false,
            selectOnUp = up,
            selectOnDown = down,
            selectOnLeft = left,
            selectOnRight = right
        };
    }

    
}
