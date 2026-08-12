using UnityEngine;

/// <summary>Project-level configuration for the authoritative save system.</summary>
[CreateAssetMenu(fileName = "SaveSystemSettings", menuName = "Game/Save System Settings")]
public sealed class SaveSystemSettings : ScriptableObject
{
    [Min(1)] public int maxSaveSlots = 6;
    public bool automaticallySelectFirstEmptySlot = true;
    public int fallbackNewGameSlot;
    public string defaultGameplayScene = "Sandbox";
    public string saveFileName = "GameData.json";
    public bool encryptData;
    public bool showSlotNumber = true;
    public bool showSceneName = true;
    public bool showSaveTime = true;
    public bool showPlaytime = true;
    public bool showDeleteButton = true;
    public bool saveOnApplicationQuit = true;
    public bool saveOnApplicationPause = true;
}
