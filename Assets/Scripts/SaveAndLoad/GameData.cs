using UnityEngine;

/// <summary>Serializable state for one independent save slot.</summary>
[System.Serializable]
public sealed class GameData
{
    public float totalPlayTime;
    public SerializableDictionary<string, bool> completedEventIds;
    public int extraLifeCount;
    public int slotNumber;
    public bool hasCheckpoint;
    public string checkpointId;
    public string checkpointSceneName;
    public Vector3 checkpointPosition;
    public string lastSavedAtUtc;
    public string lastPlayedSceneName;

    public GameData()
    {
        totalPlayTime = 0f;
        completedEventIds = new SerializableDictionary<string, bool>();
        extraLifeCount = 0;
        slotNumber = 1;
        hasCheckpoint = false;
        checkpointId = string.Empty;
        checkpointSceneName = string.Empty;
        checkpointPosition = Vector3.zero;
        lastSavedAtUtc = string.Empty;
        lastPlayedSceneName = string.Empty;
    }
}
