using UnityEngine;

/// <summary>Persistent game-level services used by gameplay systems.</summary>
public sealed class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>Returns whether gameplay time is currently paused.</summary>
    public bool IsPaused => Time.timeScale <= 0f;

    /// <summary>Returns whether gameplay input is currently blocked.</summary>
    public bool IsGameplayInputLocked => gameplayInputLocked || Time.frameCount <= inputUnlockFrame;

    private bool gameplayInputLocked;
    private int inputUnlockFrame = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResumeGame();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Pauses gameplay and blocks all player gameplay input.</summary>
    public void PauseGame()
    {
        gameplayInputLocked = true;
        Time.timeScale = 0f;
    }

    /// <summary>Resumes gameplay and allows player input from the following frame.</summary>
    public void ResumeGame()
    {
        Time.timeScale = 1f;
        gameplayInputLocked = false;
        inputUnlockFrame = Time.frameCount;
    }

    /// <summary>Blocks all player gameplay input without changing time scale.</summary>
    public void LockGameplayInput()
    {
        gameplayInputLocked = true;
    }

    /// <summary>Allows player gameplay input from the following frame.</summary>
    public void UnlockGameplayInput()
    {
        gameplayInputLocked = false;
        inputUnlockFrame = Time.frameCount;
    }
}
