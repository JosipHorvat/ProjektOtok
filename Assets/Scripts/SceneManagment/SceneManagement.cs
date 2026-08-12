using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent singleton that owns scene transitions, fades, player placement, and input locking.
/// </summary>
public class SceneManagement : MonoBehaviour
{
    public static SceneManagement Instance { get; private set; }

    [Header("Scenes")]
    [SerializeField] private SceneReference mainMenuScene;
    [SerializeField] private SceneReference sandboxScene;

 
    public bool IsLoading { get; private set; }
    public string MainMenuSceneName => mainMenuScene != null ? mainMenuScene.SceneName : string.Empty;
    public SceneLocation PendingEntrance { get; private set; } = SceneLocation.A;

    /// <summary>Raised immediately before the transition fade-out starts.</summary>
    public event Action OnFadeOutStarted;

    private Coroutine activeLoad;
    private bool needsEntrancePlacement;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }


    public void StartNewGame(int slotIndex = -1)
    {
        if (SaveManager.Instance != null)
        {
            if (slotIndex < 0)
            {
                SaveManager.Instance.StartNewGameAutomatically();
            }
            else
            {
                SaveManager.Instance.NewGame(slotIndex);
            }
        }

        LoadScene(sandboxScene);
    }

    /// <summary>Loads a saved slot and returns the player to its checkpoint scene and position.</summary>
    public bool LoadSavedGame(int slotIndex)
    {
        if (SaveManager.Instance == null || !SaveManager.Instance.LoadGame(slotIndex))
        {
            Debug.LogWarning($"SceneManagement: Save slot {slotIndex + 1} is empty or unavailable.");
            return false;
        }

        string savedSceneName = SaveManager.Instance.GetSavedSceneName();
        if (string.IsNullOrWhiteSpace(savedSceneName))
        {
            Debug.LogWarning($"SceneManagement: Save slot {slotIndex + 1} has no saved checkpoint scene.");
            return false;
        }

        LoadScene(savedSceneName);
        return true;
    }

    /// <summary>Returns to the title menu after saving the active slot.</summary>
    public void LoadMainMenu()
    {
        SaveManager.Instance?.SaveGame();
        IsLoading = false;
        LoadScene(mainMenuScene);
    }

    /// <summary>Loads a room and places the player at the requested entrance.</summary>
    public void LoadRoom(string sceneName, SceneLocation entrance)
    {
        if (IsLoading)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("SceneManagement: Scene name is empty.");
            return;
        }

        SaveManager.Instance?.SaveGame();
        PendingEntrance = entrance;
        needsEntrancePlacement = true;
        IsLoading = true;
        LockCurrentPlayerInput();
        BeginLoad(sceneName);
    }

    /// <summary>Reloads the active scene and locks the current player's input during the load.</summary>
    public void ReloadCurrentScene()
    {
        if (IsLoading)
        {
            return;
        }

        SaveManager.Instance?.SaveGame();
        LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>Loads a named scene without entrance placement.</summary>
    public void LoadScene(string sceneName)
    {
        needsEntrancePlacement = false;
        BeginLoad(sceneName);
    }

    /// <summary>Loads a scene from a SceneReference without entrance placement.</summary>
    public void LoadScene(SceneReference sceneReference)
    {
        LoadScene(sceneReference != null ? sceneReference.SceneName : string.Empty);
    }

    /// <summary>Returns whether the named scene is a gameplay scene rather than the main menu.</summary>
    public bool IsGameplayScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)
            || string.Equals(sceneName, "Bootstrap", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sceneName, "Credits", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return mainMenuScene == null || !string.Equals(sceneName, mainMenuScene.SceneName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Quits the application or exits Play mode in the Unity Editor.</summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BeginLoad(string sceneName)
    {
        if (activeLoad != null)
        {
            StopCoroutine(activeLoad);
        }

        IsLoading = true;
        activeLoad = StartCoroutine(LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("SceneManagement: Cannot load — scene name is empty.");
            FinishFailedLoad();
            yield break;
        }

        OnFadeOutStarted?.Invoke();

        if (FadeCanvas.Instance != null)
        {
            FadeCanvas.Instance.FadeOut();
            yield return null;

            while (FadeCanvas.Instance != null && FadeCanvas.Instance.IsFading)
            {
                yield return null;
            }
        }

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        if (load == null)
        {
            Debug.LogError($"SceneManagement: Failed to load '{sceneName}'. Check Build Settings.");
            FinishFailedLoad();
            yield break;
        }

        yield return load;

        SaveManager.Instance?.ValidateSceneCheckpoint();

        if (SaveManager.Instance != null && SaveManager.Instance.ShouldRestoreCheckpoint(sceneName))
        {
            SaveManager.Instance.RestorePlayerAtCheckpoint();
        }

        if (needsEntrancePlacement)
        {
            PlacePlayerAtEntrance(PendingEntrance);
            needsEntrancePlacement = false;
        }

        if (FadeCanvas.Instance != null)
        {
            while (FadeCanvas.Instance.IsFading)
            {
                yield return null;
            }
        }

        IsLoading = false;
        activeLoad = null;
        UnlockCurrentPlayerInput();
    }

    private void LockCurrentPlayerInput()
    {
        GameManager.Instance?.LockGameplayInput();
    }

    private void UnlockCurrentPlayerInput()
    {
        GameManager.Instance?.UnlockGameplayInput();
    }

    private void FinishFailedLoad()
    {
        IsLoading = false;
        activeLoad = null;
        UnlockCurrentPlayerInput();
    }

    private void PlacePlayerAtEntrance(SceneLocation entrance)
    {
        foreach (SceneTransitionDestination destination in UnityEngine.Object.FindObjectsByType<SceneTransitionDestination>(FindObjectsInactive.Exclude))
        {
            if (destination.Location == entrance)
            {
                destination.PlacePlayer();
                return;
            }
        }

        Debug.LogWarning($"SceneManagement: No entrance '{entrance}' found in '{SceneManager.GetActiveScene().name}'.");
    }
}
