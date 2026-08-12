using UnityEngine;

/// <summary>
/// Serializable scene reference that exposes a Scene asset picker in the Inspector
/// instead of a raw string. Stores the scene name for runtime use.
/// In the editor, the scene name is always resolved from the actual asset to prevent
/// stale names after file renames.
/// </summary>
[System.Serializable]
public class SceneReference : ISerializationCallbackReceiver
{
#if UNITY_EDITOR
    [SerializeField] private UnityEditor.SceneAsset sceneAsset;
#endif

    [SerializeField] private string sceneName = "";

    /// <summary>The name of the referenced scene (matches the asset file name without extension).</summary>
    public string SceneName
    {
        get
        {
#if UNITY_EDITOR
            // Always resolve from the asset in the editor to catch renames.
            if (sceneAsset != null)
                return sceneAsset.name;
#endif
            return sceneName;
        }
    }

    /// <summary>Returns true when a valid scene has been assigned.</summary>
    public bool IsValid => !string.IsNullOrEmpty(SceneName);

    /// <summary>Implicit conversion to string so SceneReference can be passed directly to SceneManager calls.</summary>
    public static implicit operator string(SceneReference sceneRef)
    {
        return sceneRef != null ? sceneRef.SceneName : "";
    }

    public override string ToString() => SceneName;

    public void OnBeforeSerialize()
    {
#if UNITY_EDITOR
        sceneName = sceneAsset != null ? sceneAsset.name : "";
#endif
    }

    public void OnAfterDeserialize() { }
}
