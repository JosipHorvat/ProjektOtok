using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// When Play is pressed in the Editor, automatically opens Bootstrap.unity first
/// so all DontDestroyOnLoad managers are always initialized — regardless of which
/// scene is currently open. Returns to the original scene when Play stops.
/// </summary>
[InitializeOnLoad]
public static class BootstrapAutoload
{
    public const string PreviousSceneKey  = "BootstrapAutoload.PreviousScene";
    private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
    private const string EnabledPrefKey   = "BootstrapAutoload.Enabled";

    static BootstrapAutoload()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    /// <summary>
    /// Toggle via Edit menu so you can temporarily disable auto-bootstrap if needed.
    /// </summary>
    [MenuItem("Edit/Bootstrap Autoload/Enable")]
    private static void Enable()
    {
        EditorPrefs.SetBool(EnabledPrefKey, true);
        EditorUtility.DisplayDialog("Bootstrap Autoload", "Bootstrap autoload enabled.", "OK");
    }

    [MenuItem("Edit/Bootstrap Autoload/Disable")]
    private static void Disable()
    {
        EditorPrefs.SetBool(EnabledPrefKey, false);
        EditorUtility.DisplayDialog("Bootstrap Autoload", "Bootstrap autoload disabled.\nPlay will start from the currently open scene.", "OK");
    }

    private static bool IsEnabled => EditorPrefs.GetBool(EnabledPrefKey, true);

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!IsEnabled) return;

        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // Save which scene the developer was working on
            EditorPrefs.SetString(PreviousSceneKey, EditorSceneManager.GetActiveScene().path);

            // If already in Bootstrap, nothing to do
            if (EditorSceneManager.GetActiveScene().path == BootstrapScenePath) return;

            // Prompt save if the open scene has unsaved changes
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorApplication.isPlaying = false;
                return;
            }

            EditorSceneManager.OpenScene(BootstrapScenePath);
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            // After stopping play, reopen the scene the developer was working on
            string previousScene = EditorPrefs.GetString(PreviousSceneKey, "");
            if (!string.IsNullOrEmpty(previousScene) && previousScene != BootstrapScenePath)
            {
                EditorSceneManager.OpenScene(previousScene);
            }
        }
    }
}
