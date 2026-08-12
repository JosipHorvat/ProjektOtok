using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Authoritative configurable-slot save service for player progress and world state.</summary>
public sealed class SaveManager : MonoBehaviour
{
    public const int DefaultSlotCount = 6;
    public static SaveManager Instance { get; private set; }
    [SerializeField] private SaveSystemSettings settings;
    [SerializeField] private string fileName = "GameData.json";
    [SerializeField] private GameObject saveIndicatorPrefab;
    [SerializeField] private Transform saveIndicatorParent;
    [SerializeField] private bool encrypData;
    private GameData gameData;
    private List<ISaveManager> saveManagers = new List<ISaveManager>();
    private FileDataHandler dataHandler;
    private float sessionStartTime;

    public int ActiveSlot { get; private set; }
    public GameData ActiveGameData => gameData;
    public SaveSystemSettings Settings => settings;
    public int ConfiguredSlotCount => Mathf.Max(1, settings != null ? settings.maxSaveSlots : DefaultSlotCount);
    public static int SlotCount => Instance != null ? Instance.ConfiguredSlotCount : DefaultSlotCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        settings = settings != null ? settings : Resources.Load<SaveSystemSettings>("SaveSystemSettings");
        if (settings == null) settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
        if (!string.IsNullOrWhiteSpace(settings.saveFileName)) fileName = settings.saveFileName;
        encrypData = settings.encryptData;
        sessionStartTime = Time.unscaledTime;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }



    /// <summary>Starts a new game in the first empty slot, or the configured fallback when all are occupied.</summary>
    public int StartNewGameAutomatically()
    {
        int slotIndex = settings != null && settings.automaticallySelectFirstEmptySlot
            ? FindFirstEmptySlot()
            : Mathf.Clamp(settings != null ? settings.fallbackNewGameSlot : 0, 0, ConfiguredSlotCount - 1);
        NewGame(slotIndex);
        return slotIndex;
    }

    /// <summary>Creates a fresh game in one slot.</summary>
    public void NewGame(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return;
        ActiveSlot = slotIndex;
        dataHandler = CreateHandler(ActiveSlot);
        dataHandler.Delete();
        gameData = new GameData
        {
            slotNumber = ActiveSlot + 1,
            lastPlayedSceneName = settings.defaultGameplayScene,
            lastSavedAtUtc = DateTime.UtcNow.ToString("O")
        };
        sessionStartTime = Time.unscaledTime;
        dataHandler.Save(gameData);
        ApplyDataToRuntime();
    }

    /// <summary>Loads one complete save slot.</summary>
    public bool LoadGame(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return false;
        ActiveSlot = slotIndex;
        dataHandler = CreateHandler(ActiveSlot);
        gameData = dataHandler.Load();
        if (gameData == null)
        {
            gameData = new GameData { slotNumber = ActiveSlot + 1 };
            ApplyDataToRuntime();
            return false;
        }
        gameData.slotNumber = ActiveSlot + 1;
        sessionStartTime = Time.unscaledTime;
        ApplyDataToRuntime();
        return true;
    }

    /// <summary>Loads the active slot.</summary>
    public void LoadGame() => LoadGame(ActiveSlot);

    /// <summary>Saves all registered state and slot metadata.</summary>
    public void SaveGame()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        if (SceneManagement.Instance != null && !SceneManagement.Instance.IsGameplayScene(activeSceneName))
        {
            return;
        }

        if (gameData == null) InitialiseRuntime();
        if (dataHandler == null) dataHandler = CreateHandler(ActiveSlot);
        saveManagers = FindAllSaveManagers();
        foreach (ISaveManager saveManager in saveManagers) saveManager.SaveData(ref gameData);
        gameData.slotNumber = ActiveSlot + 1;
        gameData.totalPlayTime += Mathf.Max(0f, Time.unscaledTime - sessionStartTime);
        gameData.lastPlayedSceneName = activeSceneName;
        gameData.lastSavedAtUtc = DateTime.UtcNow.ToString("O");
        dataHandler.Save(gameData);
        ShowSaveIndicator();
        sessionStartTime = Time.unscaledTime;
    }

    private void ShowSaveIndicator()
    {
        if (saveIndicatorPrefab == null)
        {
            return;
        }

        Instantiate(saveIndicatorPrefab, saveIndicatorParent, false);
    }


    public void DeleteSavedData()
    {
        for (int slotIndex = 0; slotIndex < ConfiguredSlotCount; slotIndex++) CreateHandler(slotIndex).Delete();
        NewGame(Mathf.Clamp(ActiveSlot, 0, ConfiguredSlotCount - 1));
    }

    /// <summary>Deletes one slot without affecting the others.</summary>
    public void DeleteSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return;
        CreateHandler(slotIndex).Delete();
        if (slotIndex == ActiveSlot)
        {
            gameData = new GameData { slotNumber = ActiveSlot + 1 };
            ApplyDataToRuntime();
        }
    }

    /// <summary>Returns whether a slot has valid save data.</summary>
    public bool HasSavedData(int slotIndex) => IsValidSlot(slotIndex) && CreateHandler(slotIndex).Load() != null;

    /// <summary>Reads display metadata without changing the active slot.</summary>
    public SaveSlotInfo GetSlotInfo(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return new SaveSlotInfo(slotIndex, false, string.Empty, string.Empty, 0f);
        GameData slotData = CreateHandler(slotIndex).Load();
        if (slotData == null) return new SaveSlotInfo(slotIndex, false, string.Empty, string.Empty, 0f);
        return new SaveSlotInfo(slotIndex, true, slotData.lastPlayedSceneName, slotData.lastSavedAtUtc, slotData.totalPlayTime);
    }

    /// <summary>Saves the active checkpoint and all registered state.</summary>
    public void SaveCheckpoint(string checkpointId, string sceneName, Vector3 position)
    {
        if (gameData == null) InitialiseRuntime();
        gameData.hasCheckpoint = true;
        gameData.checkpointId = checkpointId;
        gameData.checkpointSceneName = sceneName;
        gameData.checkpointPosition = position;
        SaveGame();
    }

    /// <summary>Restores the player at the last activated checkpoint.</summary>
    public bool RestorePlayerAtCheckpoint()
    {
        if (gameData == null || !gameData.hasCheckpoint) return false;
        Player player = UnityEngine.Object.FindAnyObjectByType<Player>();
        if (player == null) return false;
        CheckpointSavePoint checkpoint = UnityEngine.Object.FindObjectsByType<CheckpointSavePoint>(FindObjectsInactive.Exclude).FirstOrDefault(candidate => candidate.CheckpointId == gameData.checkpointId);
        if (checkpoint != null) checkpoint.PlacePlayer(player);
        else player.transform.position = gameData.checkpointPosition;
        return true;
    }

    /// <summary>Returns whether the active save has a checkpoint in the requested scene.</summary>
    public bool ShouldRestoreCheckpoint(string sceneName)
    {
        return gameData != null
            && gameData.hasCheckpoint
            && string.Equals(gameData.checkpointSceneName, sceneName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns the active slot's saved gameplay scene.</summary>
    public string GetSavedSceneName()
    {
        if (gameData == null) return string.Empty;

        string checkpointScene = gameData.hasCheckpoint ? gameData.checkpointSceneName : string.Empty;
        if (IsGameplayScene(checkpointScene)) return checkpointScene;
        if (IsGameplayScene(gameData.lastPlayedSceneName)) return gameData.lastPlayedSceneName;
        return settings != null ? settings.defaultGameplayScene : string.Empty;
    }

    private bool IsGameplayScene(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName)
            && (SceneManagement.Instance == null || SceneManagement.Instance.IsGameplayScene(sceneName));
    }


    /// <summary>Returns whether at least one configured slot is empty.</summary>
    public bool HasEmptySlot()
    {
        for (int slotIndex = 0; slotIndex < ConfiguredSlotCount; slotIndex++)
        {
            if (!HasSavedData(slotIndex)) return true;
        }

        return false;
    }

    /// <summary>Returns the first empty configured slot.</summary>
    public int FindFirstEmptySlot()
    {
        for (int slotIndex = 0; slotIndex < ConfiguredSlotCount; slotIndex++) if (!HasSavedData(slotIndex)) return slotIndex;
        return Mathf.Clamp(settings != null ? settings.fallbackNewGameSlot : 0, 0, ConfiguredSlotCount - 1);
    }

    /// <summary>Validates that Sandbox scenes contain an authored checkpoint.</summary>
    public void ValidateSceneCheckpoint()
    {
        if (SceneManagement.Instance == null
            || !SceneManagement.Instance.IsGameplayScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        if (UnityEngine.Object.FindAnyObjectByType<CheckpointSavePoint>() == null)
        {
            Debug.LogWarning("SaveManager: No authored CheckpointSavePoint exists in the active gameplay scene.");
        }
    }

    private void InitialiseRuntime()
    {
        dataHandler = CreateHandler(ActiveSlot);
        gameData = new GameData { slotNumber = ActiveSlot + 1 };
        ApplyDataToRuntime();
    }

    private void ApplyDataToRuntime()
    {
        saveManagers = FindAllSaveManagers();
        foreach (ISaveManager saveManager in saveManagers) saveManager.LoadData(gameData);
    }

    private FileDataHandler CreateHandler(int slotIndex)
    {
        string slotFileName = Path.GetFileNameWithoutExtension(fileName) + "_Slot" + (slotIndex + 1) + Path.GetExtension(fileName);
        return new FileDataHandler(Application.persistentDataPath, slotFileName, encrypData);
    }

    private bool IsValidSlot(int slotIndex) => slotIndex >= 0 && slotIndex < ConfiguredSlotCount;
    private List<ISaveManager> FindAllSaveManagers() => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude).OfType<ISaveManager>().ToList();

    private void OnApplicationPause(bool paused)
    {
        if (paused && settings != null && settings.saveOnApplicationPause) SaveGame();
    }

    private void OnApplicationQuit()
    {
        if (settings == null || settings.saveOnApplicationQuit) SaveGame();
    }

}

/// <summary>Metadata displayed by a configured save slot.</summary>
public readonly struct SaveSlotInfo
{
    public readonly int SlotIndex;
    public readonly bool IsOccupied;
    public readonly string SceneName;
    public readonly string LastSavedAtUtc;
    public readonly float PlaytimeSeconds;

    public SaveSlotInfo(int slotIndex, bool isOccupied, string sceneName, string lastSavedAtUtc, float playtimeSeconds)
    {
        SlotIndex = slotIndex;
        IsOccupied = isOccupied;
        SceneName = sceneName;
        LastSavedAtUtc = lastSavedAtUtc;
        PlaytimeSeconds = playtimeSeconds;
    }
}
