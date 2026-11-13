using System;
using System.Collections.Generic;

/// <summary>Versioned player inventory snapshot stored inside one save slot.</summary>
[Serializable]
public sealed class InventorySaveData
{
    public const int CurrentSchemaVersion = 6;
    public const int DirectCaseDimensionsSchemaVersion = 5;
    public const int InstalledAttachmentCollectionSchemaVersion = 4;

    public bool hasInventoryPayload;
    public int schemaVersion = CurrentSchemaVersion;
    public int gridWidth = ItemInventory.DefaultGridWidth;
    public int gridHeight = ItemInventory.DefaultGridHeight;
    public string equippedWeaponInstanceId = string.Empty;
    public string primaryWeaponInstanceId = string.Empty;
    public string secondaryWeaponInstanceId = string.Empty;
    public int credits = PlayerItemInventory.DefaultStartingCredits;
    public List<ItemInstanceSaveData> carryItems = new List<ItemInstanceSaveData>();
    public List<ItemInstanceSaveData> stashItems = new List<ItemInstanceSaveData>();
    public List<ItemInstanceSaveData> keyItems = new List<ItemInstanceSaveData>();
    public List<ItemInstanceSaveData> installedAttachmentItems = new List<ItemInstanceSaveData>();
}

/// <summary>Definition-ID-based snapshot of one owned item instance.</summary>
[Serializable]
public sealed class ItemInstanceSaveData
{
    public string stableItemId = string.Empty;
    public string instanceId = string.Empty;
    public int quantity;
    public int gridX = -1;
    public int gridY = -1;
    public bool isRotated;
    public int rotationQuarterTurns;
    public bool isEquipped;
    public string equippedSlot = string.Empty;
    public int loadedRounds;
    public List<ItemUpgradeLevel> upgradeLevels = new List<ItemUpgradeLevel>();
    public List<string> installedAttachmentInstanceIds = new List<string>();

    /// <summary>Gets the normalized rotation, preserving the legacy two-state save field.</summary>
    public int RotationQuarterTurns => rotationQuarterTurns == 0 && isRotated
        ? 1
        : ((rotationQuarterTurns % 4) + 4) % 4;
}
