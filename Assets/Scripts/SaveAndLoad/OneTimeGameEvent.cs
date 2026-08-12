using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Attach to any GameObject that should only exist once (e.g. a collectible).
/// On Start, if this event's UID has already been completed, the GameObject is disabled.
/// Call MarkCompleted() when the event is consumed (pickup, trigger, etc.).
/// </summary>
public class OneTimeGameEvent : MonoBehaviour
{
    [Tooltip("Unique identifier for this event. Auto-generated automatically — do not edit by hand.")]
    public string uID;

    private void Awake()
    {
        if (string.IsNullOrEmpty(uID))
            uID = $"{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}/{gameObject.name}";
    }

    private void Start()
    {
        if (OneTimeGameEventManager.Instance != null &&
            OneTimeGameEventManager.Instance.IsEventCompleted(uID))
        {
            Debug.Log($"[OneTimeGameEvent] Already completed: {uID}");
        }
    }

    /// <summary>Marks this event as completed so it won't appear again on future loads.</summary>
    public void MarkCompleted()
    {
        if (OneTimeGameEventManager.Instance != null)
            OneTimeGameEventManager.Instance.CompleteEvent(uID);
    }

#if UNITY_EDITOR
    /// <summary>Called by Unity when the component is first added or the GameObject is reset.
    /// Always generates a fresh GUID so every new instance starts unique.</summary>
    private void Reset()
    {
        GenerateNewUID();
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(uID))
            GenerateNewUID();
    }

    /// <summary>Assigns a fresh GUID and marks the object dirty so the scene saves it.</summary>
    public void GenerateNewUID()
    {
        uID = GUID.Generate().ToString();
        EditorUtility.SetDirty(this);
    }
#endif
}
