using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that tracks which one-time game events have been completed.
/// Events are identified by a unique string ID. Persists across scenes and save files.
/// </summary>
public class OneTimeGameEventManager : MonoBehaviour, ISaveManager
{
    public static OneTimeGameEventManager Instance { get; private set; }

    private HashSet<string> completedEventIds = new HashSet<string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>Checks whether the event with the given UID has already been completed.</summary>
    public bool IsEventCompleted(string uid)
    {
        return !string.IsNullOrEmpty(uid) && completedEventIds.Contains(uid);
    }

    /// <summary>Marks the event with the given UID as completed and saves immediately.</summary>
    public void CompleteEvent(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return;

        if (completedEventIds.Add(uid))
        {
            Debug.Log($"[OneTimeGameEventManager] Event completed: {uid}");

            if (SaveManager.Instance != null)
                SaveManager.Instance.SaveGame();
        }
    }

    public void LoadData(GameData _data)
    {
        completedEventIds.Clear();

        if (_data.completedEventIds != null)
        {
            foreach (var kvp in _data.completedEventIds)
            {
                if (kvp.Value)
                    completedEventIds.Add(kvp.Key);
            }
        }

        Debug.Log($"[OneTimeGameEventManager] Loaded {completedEventIds.Count} completed events. Extra lives: {_data.extraLifeCount}");
    }

    public void SaveData(ref GameData _data)
    {
        if (_data.completedEventIds == null)
            _data.completedEventIds = new SerializableDictionary<string, bool>();

        _data.completedEventIds.Clear();

        foreach (string uid in completedEventIds)
            _data.completedEventIds[uid] = true;
    }
}
