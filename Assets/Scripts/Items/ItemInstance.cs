using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Mutable runtime state for one owned item, separate from its shared ScriptableObject definition.</summary>
[Serializable]
public sealed class ItemInstance : ISerializationCallbackReceiver{
    private const int DefaultUnplacedCoordinate = -1;

    [Header("Owned item identity")]
    [Tooltip("Unique instance identifier used for ownership and future save references.")]
    [SerializeField] private string instanceId = string.Empty;
    [Tooltip("Shared immutable definition for this owned item.")]
    [SerializeField] private ItemDefinition definition;
    [Tooltip("Current quantity in this instance. Quantity is capped by the definition's stack limit.")]
    [Min(1)]
    [SerializeField] private int quantity = 1;

    [Header("Carry-grid state")]
    [Tooltip("Top-left cell position in the owning carry grid; negative coordinates mean not currently placed in a carry grid.")]
    [SerializeField] private Vector2Int gridPosition = new Vector2Int(DefaultUnplacedCoordinate, DefaultUnplacedCoordinate);
    [Tooltip("Legacy 90-degree rotation state retained for serialized compatibility.")]
    [SerializeField] private bool isRotated;
    [Tooltip("Clockwise quarter-turn count used to render the item's full rotation orientation.")]
    [SerializeField] private int rotationQuarterTurns;
    [Tooltip("Whether this owned item is currently assigned as equipped.")]
    [SerializeField] private bool isEquipped;

    [Header("Weapon-instance state")]
    [Tooltip("Rounds loaded in this weapon instance's magazine. This value is not stored on the shared weapon definition.")]
    [Min(0)]
    [SerializeField] private int loadedRounds;
    [Tooltip("Permanent upgrade levels owned by this weapon instance.")]
    [SerializeField] private List<ItemUpgradeLevel> upgradeLevels = new List<ItemUpgradeLevel>();
    [Tooltip("Instance IDs of removable attachment items installed on this weapon instance.")]
    [SerializeField] private List<string> installedAttachmentInstanceIds = new List<string>();

    [NonSerialized] private Action<ItemInstance> stateChanged;
    [NonSerialized] private ItemInventory owningInventory;
    [NonSerialized] private object owningContainer;

    private ItemInstance()
    {
    }

    /// <summary>Gets the unique identity of this owned item instance.</summary>
    public string InstanceId => instanceId;

    /// <summary>Gets the immutable shared item definition.</summary>
    public ItemDefinition Definition => definition;

    /// <summary>Gets the current quantity owned by this instance.</summary>
    public int Quantity => quantity;

    /// <summary>Gets the item's carry-grid top-left cell position.</summary>
    public Vector2Int GridPosition => gridPosition;

    /// <summary>Gets whether the item uses a quarter-turned carry-grid footprint.</summary>
    public bool IsRotated => RotationQuarterTurns % 2 != 0;

    /// <summary>Gets the item's normalized clockwise quarter-turn count.</summary>
    public int RotationQuarterTurns => ((rotationQuarterTurns % 4) + 4) % 4;

    /// <summary>Gets whether the item is currently equipped.</summary>
    public bool IsEquipped => isEquipped;

    /// <summary>Gets the rounds loaded in this weapon instance.</summary>
    public int LoadedRounds => loadedRounds;

    /// <summary>Gets a read-only view of this weapon instance's permanent upgrade levels.</summary>
    public IReadOnlyList<ItemUpgradeLevel> UpgradeLevels => upgradeLevels;

    /// <summary>Gets a read-only view of installed attachment instance IDs.</summary>
    public IReadOnlyList<string> InstalledAttachmentInstanceIds => installedAttachmentInstanceIds;

    internal ItemInventory OwningInventory => owningInventory;
    internal object OwningContainer => owningContainer;

    internal event Action<ItemInstance> StateChanged
    {
        add => stateChanged += value;
        remove => stateChanged -= value;
    }

    /// <summary>Creates a new owned item instance without modifying its shared definition.</summary>
    public static bool TryCreate(
        ItemDefinition itemDefinition,
        int initialQuantity,
        out ItemInstance itemInstance,
        int initialLoadedRounds = -1)
    {
        itemInstance = null;
        if (itemDefinition == null || string.IsNullOrWhiteSpace(itemDefinition.StableItemId) ||
            itemDefinition.InventoryWidth < 1 || itemDefinition.InventoryHeight < 1 || itemDefinition.MaximumStackSize < 1)
        {
            return false;
        }

        if (initialQuantity < 1 || initialQuantity > itemDefinition.MaximumStackSize)
        {
            return false;
        }

        ItemInstance newItemInstance = new ItemInstance
        {
            instanceId = Guid.NewGuid().ToString("N"),
            definition = itemDefinition,
            quantity = initialQuantity,
            gridPosition = new Vector2Int(DefaultUnplacedCoordinate, DefaultUnplacedCoordinate),
            isRotated = false,
            rotationQuarterTurns = 0,
            isEquipped = false,
            loadedRounds = GetInitialLoadedRounds(itemDefinition, initialLoadedRounds),
            upgradeLevels = new List<ItemUpgradeLevel>(),
            installedAttachmentInstanceIds = new List<string>(),
        };

        itemInstance = newItemInstance;
        return true;
    }

    /// <summary>Reconstructs an owned item from a validated stable-ID save record.</summary>
    internal static bool TryRestoreFromSave(
        ItemDefinition itemDefinition,
        ItemInstanceSaveData saveData,
        out ItemInstance restoredItem,
        out string failureReason)
    {
        restoredItem = null;
        failureReason = string.Empty;
        if (itemDefinition == null || saveData == null ||
            string.IsNullOrWhiteSpace(itemDefinition.StableItemId) ||
            !string.Equals(itemDefinition.StableItemId, saveData.stableItemId, StringComparison.Ordinal))
        {
            failureReason = "The item definition is missing or its stable ID does not match the save record.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(saveData.instanceId))
        {
            failureReason = "The saved item instance ID is empty.";
            return false;
        }

        if (saveData.quantity < 1 || saveData.quantity > itemDefinition.MaximumStackSize)
        {
            failureReason = $"Saved quantity {saveData.quantity} is outside the definition's stack limit.";
            return false;
        }

        WeaponDefinition weaponDefinition = itemDefinition as WeaponDefinition;
        if (weaponDefinition == null && saveData.loadedRounds != 0)
        {
            failureReason = "A non-weapon item contains saved magazine rounds.";
            return false;
        }

        if (weaponDefinition != null &&
            (saveData.loadedRounds < 0 || saveData.loadedRounds > weaponDefinition.MagazineCapacity))
        {
            failureReason = $"Saved magazine rounds {saveData.loadedRounds} exceed the weapon's valid capacity.";
            return false;
        }

        if (saveData.isEquipped && itemDefinition.Category != ItemCategory.Weapon &&
            itemDefinition.Category != ItemCategory.Equipment)
        {
            failureReason = "The save marks an item category that cannot be equipped as equipped.";
            return false;
        }

        if (!TryCreate(itemDefinition, saveData.quantity, out ItemInstance item, saveData.loadedRounds))
        {
            failureReason = "The item instance could not be constructed from its definition.";
            return false;
        }

        item.instanceId = saveData.instanceId;
        item.gridPosition = new Vector2Int(saveData.gridX, saveData.gridY);
        item.rotationQuarterTurns = saveData.RotationQuarterTurns;
        item.isRotated = item.RotationQuarterTurns % 2 != 0;
        item.isEquipped = saveData.isEquipped;

        if (saveData.upgradeLevels != null)
        {
            HashSet<WeaponUpgradeType> restoredUpgradeTypes = new HashSet<WeaponUpgradeType>();
            for (int index = 0; index < saveData.upgradeLevels.Count; index++)
            {
                ItemUpgradeLevel upgrade = saveData.upgradeLevels[index];
                if (!restoredUpgradeTypes.Add(upgrade.UpgradeType) ||
                    !item.TrySetUpgradeLevel(upgrade.UpgradeType, upgrade.Level))
                {
                    failureReason = $"Saved weapon upgrade '{upgrade.UpgradeType}' is duplicated or unsupported.";
                    return false;
                }
            }
        }

        if (saveData.installedAttachmentInstanceIds != null)
        {
            HashSet<string> attachmentIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < saveData.installedAttachmentInstanceIds.Count; index++)
            {
                string attachmentInstanceId = saveData.installedAttachmentInstanceIds[index];
                if (string.IsNullOrWhiteSpace(attachmentInstanceId) ||
                    !attachmentIds.Add(attachmentInstanceId) ||
                    !item.AddInstalledAttachment(attachmentInstanceId))
                {
                    failureReason = "Saved attachment references contain an empty or duplicate instance ID.";
                    return false;
                }
            }
        }

        restoredItem = item;
        return true;
    }

    /// <summary>Consumes one loaded round when this instance is a ranged weapon with ammunition available.</summary>
    public bool TrySpendLoadedRound()
    {
        if (!(definition is WeaponDefinition) || loadedRounds <= 0)
        {
            return false;
        }

        loadedRounds--;
        NotifyStateChanged();
        return true;
    }

    /// <summary>Sets the loaded round count, clamped to this weapon's magazine capacity.</summary>
    public bool TrySetLoadedRounds(int requestedRounds)
    {
        if (!(definition is WeaponDefinition weaponDefinition) || requestedRounds < 0)
        {
            return false;
        }

        int clampedRounds = Mathf.Clamp(requestedRounds, 0, weaponDefinition.MagazineCapacity);
        if (loadedRounds == clampedRounds)
        {
            return true;
        }

        loadedRounds = clampedRounds;
        NotifyStateChanged();
        return true;
    }

    /// <summary>Returns how many rounds can be transferred into this weapon's remaining magazine space.</summary>
    public int GetLoadedTransferCapacity(WeaponDefinition weaponDefinition)
    {
        if (!(definition is WeaponDefinition ownedWeaponDefinition) || ownedWeaponDefinition != weaponDefinition)
        {
            return 0;
        }

        return Mathf.Max(0, weaponDefinition.MagazineCapacity - loadedRounds);
    }


    /// <summary>Adds no more than the remaining magazine capacity and returns the number actually loaded.</summary>
    public int AddLoadedRounds(int requestedRounds)
    {
        if (!(definition is WeaponDefinition weaponDefinition) || requestedRounds <= 0)
        {
            return 0;
        }

        int addedRounds = Mathf.Min(requestedRounds, Mathf.Max(0, weaponDefinition.MagazineCapacity - loadedRounds));
        if (addedRounds > 0)
        {
            loadedRounds += addedRounds;
            NotifyStateChanged();
        }

        return addedRounds;
    }

    /// <summary>Replaces the magazine while clamping the loaded count to this weapon's capacity.</summary>
    public bool TryReplaceMagazine(int requestedRounds)
    {
        return TrySetLoadedRounds(requestedRounds);
    }

    /// <summary>Gets an instance's current permanent level for a supported weapon upgrade.</summary>
    public int GetUpgradeLevel(WeaponUpgradeType upgradeType)
    {
        for (int index = 0; index < upgradeLevels.Count; index++)
        {
            if (upgradeLevels[index].UpgradeType == upgradeType)
            {
                return upgradeLevels[index].Level;
            }
        }

        return 0;
    }

    /// <summary>Sets a permanent upgrade level only when the shared weapon definition supports that upgrade.</summary>
    public bool TrySetUpgradeLevel(WeaponUpgradeType upgradeType, int requestedLevel)
    {
        if (!(definition is WeaponDefinition weaponDefinition) || requestedLevel < 0 ||
            !Enum.IsDefined(typeof(WeaponUpgradeType), upgradeType) || upgradeLevels == null)
        {
            return false;
        }

        int maximumLevel = GetMaximumUpgradeLevel(weaponDefinition, upgradeType);
        if (maximumLevel <= 0 || requestedLevel > maximumLevel)
        {
            return false;
        }

        int existingIndex = FindUpgradeLevelIndex(upgradeType);
        if (requestedLevel == 0)
        {
            if (existingIndex >= 0)
            {
                upgradeLevels.RemoveAt(existingIndex);
                NotifyStateChanged();
            }

            return true;
        }

        ItemUpgradeLevel state = new ItemUpgradeLevel(upgradeType, requestedLevel);
        if (existingIndex >= 0)
        {
            if (upgradeLevels[existingIndex].Level != requestedLevel)
            {
                upgradeLevels[existingIndex] = state;
                NotifyStateChanged();
            }
        }
        else
        {
            upgradeLevels.Add(state);
            NotifyStateChanged();
        }

        return true;
    }

    internal bool CanStackWith(ItemDefinition candidateDefinition)
    {
        if (definition == null || candidateDefinition == null || quantity < 1 ||
            definition.MaximumStackSize <= 1 || candidateDefinition.MaximumStackSize <= 1)
        {
            return false;
        }

        if (definition is AmmoDefinition currentAmmo && candidateDefinition is AmmoDefinition candidateAmmo)
        {
            return currentAmmo.AmmunitionType == candidateAmmo.AmmunitionType &&
                   string.Equals(currentAmmo.StackCompatibilityKey, candidateAmmo.StackCompatibilityKey, StringComparison.Ordinal);
        }

        return definition.Category == candidateDefinition.Category &&
               string.Equals(definition.StableItemId, candidateDefinition.StableItemId, StringComparison.Ordinal);
    }

    internal void SetQuantity(int newQuantity)
    {
        quantity = newQuantity;
        NotifyStateChanged();
    }

    internal void SetGridPlacement(Vector2Int newGridPosition, bool rotated)
    {
        SetGridPlacement(newGridPosition, rotated ? 1 : 0);
    }

    internal void SetGridPlacement(Vector2Int newGridPosition, int newRotationQuarterTurns)
    {
        gridPosition = newGridPosition;
        rotationQuarterTurns = ((newRotationQuarterTurns % 4) + 4) % 4;
        isRotated = rotationQuarterTurns % 2 != 0;
        NotifyStateChanged();
    }

    internal void SetEquipped(bool equipped)
    {
        if (isEquipped == equipped)
        {
            return;
        }

        isEquipped = equipped;
        NotifyStateChanged();
    }

    internal bool AddInstalledAttachment(string attachmentInstanceId)
    {
        if (string.IsNullOrWhiteSpace(attachmentInstanceId) || installedAttachmentInstanceIds.Contains(attachmentInstanceId))
        {
            return false;
        }

        installedAttachmentInstanceIds.Add(attachmentInstanceId);
        NotifyStateChanged();
        return true;
    }

    internal bool RemoveInstalledAttachment(string attachmentInstanceId)
    {
        if (!installedAttachmentInstanceIds.Remove(attachmentInstanceId))
        {
            return false;
        }

        NotifyStateChanged();
        return true;
    }

    internal void SetOwningInventory(ItemInventory newOwningInventory)
    {
        owningInventory = newOwningInventory;
        owningContainer = newOwningInventory;
    }

    /// <summary>Marks the item as owned by the player's non-grid stash or protected key-item collection.</summary>
    internal void SetOwningPlayerContainer(PlayerItemInventory newOwningContainer)
    {
        owningInventory = null;
        owningContainer = newOwningContainer;
    }

    internal void NotifyStateChanged()
    {
        stateChanged?.Invoke(this);
    }

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        stateChanged = null;
        owningInventory = null;
        owningContainer = null;
        if (rotationQuarterTurns == 0 && isRotated)
        {
            rotationQuarterTurns = 1;
        }

        rotationQuarterTurns = RotationQuarterTurns;
        isRotated = rotationQuarterTurns % 2 != 0;
        if (upgradeLevels == null)
        {
            upgradeLevels = new List<ItemUpgradeLevel>();
        }

        if (installedAttachmentInstanceIds == null)
        {
            installedAttachmentInstanceIds = new List<string>();
        }
    }

    private int FindUpgradeLevelIndex(WeaponUpgradeType upgradeType)
    {
        for (int index = 0; index < upgradeLevels.Count; index++)
        {
            if (upgradeLevels[index].UpgradeType == upgradeType)
            {
                return index;
            }
        }

        return -1;
    }

    private static int GetMaximumUpgradeLevel(WeaponDefinition weaponDefinition, WeaponUpgradeType upgradeType)
    {
        WeaponUpgradeOption[] options = weaponDefinition.upgradeOptions;
        if (options == null)
        {
            return 0;
        }

        for (int index = 0; index < options.Length; index++)
        {
            if (options[index].upgradeType == upgradeType)
            {
                return Mathf.Max(0, options[index].maximumLevel);
            }
        }

        return 0;
    }

    private static int GetInitialLoadedRounds(ItemDefinition itemDefinition, int requestedRounds)
    {
        if (!(itemDefinition is WeaponDefinition weaponDefinition))
        {
            return 0;
        }

        int magazineCapacity = Mathf.Max(0, weaponDefinition.MagazineCapacity);
        return requestedRounds < 0 ? magazineCapacity : Mathf.Clamp(requestedRounds, 0, magazineCapacity);
    }
}

/// <summary>Serializable per-instance permanent upgrade level.</summary>
[Serializable]
public struct ItemUpgradeLevel
{
    [Tooltip("The upgrade supported by this weapon definition.")]
    [SerializeField] private WeaponUpgradeType upgradeType;
    [Tooltip("Current permanent level on this specific weapon instance.")]
    [Min(0)]
    [SerializeField] private int level;

    /// <summary>Creates an owned upgrade-level record.</summary>
    public ItemUpgradeLevel(WeaponUpgradeType type, int currentLevel)
    {
        upgradeType = type;
        level = currentLevel;
    }

    /// <summary>Gets the upgrade type.</summary>
    public WeaponUpgradeType UpgradeType => upgradeType;

    /// <summary>Gets the current level.</summary>
    public int Level => level;
}
