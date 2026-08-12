using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lives only in Bootstrap.unity. Initializes all DontDestroyOnLoad managers (via their own Awake),
/// then immediately loads the correct scene - MainMenu in builds, or the scene that was
/// open in the Editor when Play was pressed.
/// </summary>
public class Bootstrapper : MonoBehaviour
{
    private const string PreviousSceneKey = "BootstrapAutoload.PreviousScene";

    [Tooltip("Scene to load in production builds.")]
    [SerializeField] private SceneReference productionStartScene;

    private void Start()
    {
#if UNITY_EDITOR
        string previousScene = UnityEditor.EditorPrefs.GetString(PreviousSceneKey, "");
        if (!string.IsNullOrEmpty(previousScene))
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(previousScene);
            if (sceneName != "Bootstrap")
            {
                SceneManager.LoadScene(sceneName);
                return;
            }
        }
#endif
        SceneManager.LoadScene(productionStartScene);
    }
}
