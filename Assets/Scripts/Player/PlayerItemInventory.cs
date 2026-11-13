using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns the player's live item-instance inventory and provides ammunition APIs over its ammo stacks.</summary>
[DisallowMultipleComponent]
public sealed class PlayerItemInventory : MonoBehaviour, ISaveManager
{
    private const string EquippedSlotLabel = "Equipped";
    public const int PrimaryWeaponSlotIndex = 0;
    public const int SecondaryWeaponSlotIndex = 1;
    private const string PrimaryWeaponSlotLabel = "PrimaryWeapon";
    private const string SecondaryWeaponSlotLabel = "SecondaryWeapon";
    public const int DefaultStartingCredits = 500;

    [Header("Starter weapon definitions")]
    [Tooltip("Existing Pistol definition placed in the starter carry grid.")]
    [SerializeField] private WeaponDefinition startingPistolDefinition;
    [Tooltip("Existing Assault Rifle definition placed in the starter carry grid.")]
    [SerializeField] private WeaponDefinition startingAssaultRifleDefinition;

    [Header("Starter ammunition definitions")]
    [Tooltip("Ammo item definition used for the player's Pistol reserve rounds.")]
    [SerializeField] private AmmoDefinition pistolAmmoDefinition;
    [Tooltip("Ammo item definition used for the player's Assault Rifle reserve rounds.")]
    [SerializeField] private AmmoDefinition rifleAmmoDefinition;
    [Tooltip("Starting Pistol reserve ammunition. This is placed into inventory stacks at runtime.")]
    [Min(0)]
    [SerializeField] private int startingPistolReserveRounds = ItemInventory.DefaultPistolReserveRounds;
    [Tooltip("Starting Rifle reserve ammunition. This is placed into inventory stacks at runtime.")]
    [Min(0)]
    [SerializeField] private int startingRifleReserveRounds = ItemInventory.DefaultRifleReserveRounds;
    [Tooltip("Temporary Sandbox layout test: start or restore the case at its maximum 8x10 size.")]
    [SerializeField] private bool useMaximumCaseSizeForLayoutTesting;

    [Header("Save definition catalog")]
    [Tooltip("Additional item definitions required to resolve saved stable IDs after their source scene is unloaded. Add every production definition here.")]
    [SerializeField] private List<ItemDefinition> definitionCatalog = new List<ItemDefinition>();

    [Header("Upgrade purchase prices")]
    [Tooltip("Exact Credits cost for each purchased upgrade level, keyed by weapon stable ID and upgrade type.")]
    [SerializeField] private List<WeaponUpgradePurchaseOption> weaponUpgradePurchaseOptions = new List<WeaponUpgradePurchaseOption>();

    [Header("Key item ownership")]
    [Tooltip("Owned key and quest item instances. These do not occupy the carry grid and are protected by their definitions.")]
    [SerializeField] private List<ItemInstance> keyItems = new List<ItemInstance>();

    [Header("Safe-room stash")]
    [SerializeField] private List<ItemInstance> stashItems = new List<ItemInstance>();
    [Tooltip("Owned removable attachment instances, separate from carry, key, and stash containers.")]
    [SerializeField] private List<ItemInstance> installedAttachmentItems = new List<ItemInstance>();
    [SerializeField] private string primaryWeaponInstanceId = string.Empty;
    [SerializeField] private string secondaryWeaponInstanceId = string.Empty;

    [NonSerialized] private ItemInventory inventory;
    [NonSerialized] private bool hasInitialized;
    [NonSerialized] private Action<PlayerItemInventory> changed;
    [NonSerialized] private bool suppressInventoryChanged;
    [NonSerialized] private List<ItemInstanceSaveData> unresolvedCarryItems = new List<ItemInstanceSaveData>();
    [NonSerialized] private List<ItemInstanceSaveData> unresolvedKeyItems = new List<ItemInstanceSaveData>();
    [NonSerialized] private List<ItemInstanceSaveData> unresolvedStashItems = new List<ItemInstanceSaveData>();
    [NonSerialized] private List<ItemInstanceSaveData> unresolvedInstalledAttachmentItems = new List<ItemInstanceSaveData>();
    [NonSerialized] private string equippedWeaponInstanceId = string.Empty;
    [NonSerialized] private bool unsupportedFutureInventoryVersion;
    [NonSerialized] private int credits = DefaultStartingCredits;

    /// <summary>Gets the player's current persistent Credits balance.</summary>
    public int Credits => credits;

    /// <summary>Gets the saved active weapon instance ID used by the weapon rig after inventory restoration.</summary>
    public string EquippedWeaponInstanceId => equippedWeaponInstanceId;

    /// <summary>Raised when carry inventory or the separate key-item collection changes.</summary>
    public event Action<PlayerItemInventory> Changed
    {
        add
        {
            changed += value;
            EnsureInitialized();
        }
        remove => changed -= value;
    }

    /// <summary>Gets a read-only view of protected key and quest item instances.</summary>
    public IReadOnlyList<ItemInstance> KeyItems
    {
        get
        {
            EnsureInitialized();
            if (keyItems == null)
            {
                keyItems = new List<ItemInstance>();
            }

            return keyItems.AsReadOnly();
        }
    }


    /// <summary>Gets the player's current carry inventory, initializing the configured starter loadout when needed.</summary>
    public ItemInventory Inventory
    {
        get
        {
            EnsureInitialized();
            return inventory;
        }
    }

    /// <summary>Gets items in the persistent unlimited safe-room stash.</summary>
    public IReadOnlyList<ItemInstance> StashItems
    {
        get
        {
            EnsureInitialized();
            return stashItems.AsReadOnly();
        }
    }

    /// <summary>Attempts a pickup transaction, immediately applying capacity upgrades and placing other pickups in their owning containers.</summary>
    public InventoryOperationResult TryAddPickupQuantity(ItemDefinition definition, int requestedQuantity)
    {
        EnsureInitialized();
        if (definition == null || string.IsNullOrWhiteSpace(definition.StableItemId))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition, requestedQuantity);
        }

        if (requestedQuantity <= 0)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidQuantity, requestedQuantity);
        }

        if (definition is InventoryCapacityUpgradeDefinition)
        {
            if (requestedQuantity != 1)
            {
                return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidQuantity, requestedQuantity);
            }

            return TryApplyCapacityPickup();
        }

        if (definition.Category != ItemCategory.KeyItem && definition.Category != ItemCategory.QuestItem)
        {
            return inventory != null
                ? inventory.TryAddQuantity(definition, requestedQuantity)
                : InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition, requestedQuantity);
        }

        if (keyItems == null)
        {
            keyItems = new List<ItemInstance>();
        }

        int maximumStackSize = Mathf.Max(1, definition.MaximumStackSize);
        int remainingQuantity = requestedQuantity;
        List<ItemInstance> newKeyItemStacks = new List<ItemInstance>();
        while (remainingQuantity > 0)
        {
            int stackQuantity = Mathf.Min(maximumStackSize, remainingQuantity);
            if (!ItemInstance.TryCreate(definition, stackQuantity, out ItemInstance keyItemInstance))
            {
                return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition, requestedQuantity);
            }

            keyItemInstance.SetOwningPlayerContainer(this);
            newKeyItemStacks.Add(keyItemInstance);
            remainingQuantity -= stackQuantity;
        }

        keyItems.AddRange(newKeyItemStacks);
        changed?.Invoke(this);
        return InventoryOperationResult.Accepted(requestedQuantity, requestedQuantity);
    }

    /// <summary>Advances the carry case by exactly one supported stage without creating an inventory item.</summary>
    public InventoryOperationResult TryApplyCapacityPickup()
    {
        EnsureInitialized();
        if (inventory == null)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition, 1);
        }

        InventoryOperationResult stageResult = inventory.TryAdvanceCaseStage();
        return stageResult.Succeeded
            ? InventoryOperationResult.Accepted(1, 1)
            : InventoryOperationResult.Rejected(stageResult.Failure, 1);
    }

    /// <summary>Finds a separate protected key or quest item instance by its unique instance identifier.</summary>
    public ItemInstance FindKeyItem(string instanceId)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        for (int index = 0; index < keyItems.Count; index++)
        {
            if (string.Equals(keyItems[index].InstanceId, instanceId, StringComparison.Ordinal))
            {
                return keyItems[index];
            }
        }

        return null;
    }

    /// <summary>Gets or creates the owned weapon item matching a shared weapon definition.</summary>
    public ItemInstance FindOwnedWeaponItem(WeaponDefinition definition)
    {
        EnsureInitialized();
        if (inventory == null || definition == null)
        {
            return null;
        }

        IReadOnlyList<ItemInstance> ownedItems = inventory.Items;
        for (int index = 0; index < ownedItems.Count; index++)
        {
            if (ReferenceEquals(ownedItems[index].Definition, definition))
            {
                return ownedItems[index];
            }
        }

        return null;
    }

    /// <summary>Gets the separately owned attachment instances currently installed on weapons.</summary>
    public IReadOnlyList<ItemInstance> InstalledAttachmentItems
    {
        get
        {
            EnsureInitialized();
            return installedAttachmentItems.AsReadOnly();
        }
    }

    /// <summary>Finds an installed attachment instance by its unique owned-item identifier.</summary>
    public ItemInstance FindInstalledAttachment(string instanceId)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        for (int index = 0; index < installedAttachmentItems.Count; index++)
        {
            ItemInstance item = installedAttachmentItems[index];
            if (item != null && string.Equals(item.InstanceId, instanceId, StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>Gets the owned attachment instances referenced by one weapon instance.</summary>
    public IReadOnlyList<ItemInstance> GetInstalledAttachmentsForWeapon(string weaponInstanceId)
    {
        EnsureInitialized();
        List<ItemInstance> resolvedAttachments = new List<ItemInstance>();
        ItemInstance weapon = inventory != null ? inventory.FindItem(weaponInstanceId) : null;
        if (!(weapon?.Definition is WeaponDefinition))
        {
            return resolvedAttachments.AsReadOnly();
        }

        IReadOnlyList<string> attachmentIds = weapon.InstalledAttachmentInstanceIds;
        for (int index = 0; index < attachmentIds.Count; index++)
        {
            ItemInstance attachment = FindInstalledAttachment(attachmentIds[index]);
            if (attachment != null)
            {
                resolvedAttachments.Add(attachment);
            }
        }

        return resolvedAttachments.AsReadOnly();
    }

    /// <summary>Installs an owned carried attachment on a carried weapon after compatibility and slot validation.</summary>
    public InventoryOperationResult TryInstallWeaponAttachment(string weaponInstanceId, string attachmentInstanceId)
    {
        EnsureInitialized();
        ItemInstance weapon = inventory != null ? inventory.FindItem(weaponInstanceId) : null;
        ItemInstance attachment = inventory != null ? inventory.FindItem(attachmentInstanceId) : null;
        if (!(weapon?.Definition is WeaponDefinition weaponDefinition) ||
            !(attachment?.Definition is WeaponAttachmentDefinition attachmentDefinition) || attachment.Quantity != 1)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition);
        }

        if (string.Equals(weaponInstanceId, attachmentInstanceId, StringComparison.Ordinal) ||
            !IsAttachmentCompatibleWithWeapon(weaponDefinition, attachmentDefinition))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition);
        }

        IReadOnlyList<string> installedIds = weapon.InstalledAttachmentInstanceIds;
        for (int index = 0; index < installedIds.Count; index++)
        {
            ItemInstance installedAttachment = FindInstalledAttachment(installedIds[index]);
            if (installedAttachment != null && installedAttachment.Definition is WeaponAttachmentDefinition installedDefinition &&
                string.Equals(installedDefinition.AttachmentSlotId, attachmentDefinition.AttachmentSlotId, StringComparison.OrdinalIgnoreCase))
            {
                return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidUpgrade);
            }
        }

        Vector2Int originalPosition = attachment.GridPosition;
        int originalRotationQuarterTurns = attachment.RotationQuarterTurns;
        suppressInventoryChanged = true;
        InventoryOperationResult takeResult = inventory.TryTakeItem(attachmentInstanceId, out ItemInstance removedAttachment);
        if (!takeResult.Succeeded || removedAttachment != attachment || !weapon.AddInstalledAttachment(attachmentInstanceId))
        {
            if (takeResult.Succeeded && removedAttachment != null)
            {
                inventory.TryAddItem(removedAttachment, originalPosition, originalRotationQuarterTurns);
            }

            suppressInventoryChanged = false;
            return InventoryOperationResult.Rejected(takeResult.Succeeded
                ? InventoryOperationFailure.InvalidDefinition
                : takeResult.Failure);
        }

        attachment.SetOwningPlayerContainer(this);
        installedAttachmentItems.Add(attachment);
        suppressInventoryChanged = false;
        changed?.Invoke(this);
        return InventoryOperationResult.Success();
    }

    /// <summary>Removes an installed attachment to the carry grid, leaving ownership unchanged if no placement fits.</summary>
    public InventoryOperationResult TryRemoveWeaponAttachment(string weaponInstanceId, string attachmentInstanceId)
    {
        EnsureInitialized();
        ItemInstance weapon = inventory != null ? inventory.FindItem(weaponInstanceId) : null;
        ItemInstance attachment = FindInstalledAttachment(attachmentInstanceId);
        if (!(weapon?.Definition is WeaponDefinition weaponDefinition) ||
            !(attachment?.Definition is WeaponAttachmentDefinition attachmentDefinition) ||
            !ContainsInstanceId(weapon.InstalledAttachmentInstanceIds, attachmentInstanceId) ||
            !IsAttachmentCompatibleWithWeapon(weaponDefinition, attachmentDefinition))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition);
        }

        if (inventory == null || !inventory.TryFindFirstFit(attachment.Definition, out Vector2Int position, out bool rotated))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.NoSpace);
        }

        int attachmentIndex = installedAttachmentItems.IndexOf(attachment);
        if (attachmentIndex < 0 || !weapon.RemoveInstalledAttachment(attachmentInstanceId))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ItemNotFound);
        }

        suppressInventoryChanged = true;
        installedAttachmentItems.RemoveAt(attachmentIndex);
        attachment.SetOwningPlayerContainer(null);
        InventoryOperationResult addResult = inventory.TryAddItem(attachment, position, rotated);
        if (!addResult.Succeeded)
        {
            attachment.SetOwningPlayerContainer(this);
            installedAttachmentItems.Insert(attachmentIndex, attachment);
            weapon.AddInstalledAttachment(attachmentInstanceId);
            suppressInventoryChanged = false;
            return addResult;
        }

        suppressInventoryChanged = false;
        changed?.Invoke(this);
        return InventoryOperationResult.Success();
    }

    /// <summary>Returns the exact authored Credits cost for the next level of an owned weapon upgrade.</summary>
    public bool TryGetWeaponUpgradeCost(string weaponInstanceId, WeaponUpgradeType upgradeType, out int exactCost)
    {
        EnsureInitialized();
        exactCost = 0;
        ItemInstance weapon = inventory != null ? inventory.FindItem(weaponInstanceId) : null;
        if (!(weapon?.Definition is WeaponDefinition weaponDefinition) ||
            !TryGetNextUpgradeLevel(weapon, weaponDefinition, upgradeType, out int nextLevel))
        {
            return false;
        }

        return TryGetAuthoredUpgradeCost(weaponDefinition, upgradeType, nextLevel, out exactCost);
    }

    /// <summary>Buys the next authored level for one owned weapon instance and debits its exact configured Credits cost.</summary>
    public InventoryOperationResult TryPurchaseWeaponUpgrade(string weaponInstanceId, WeaponUpgradeType upgradeType, int exactCost)
    {
        EnsureInitialized();
        ItemInstance weapon = inventory != null ? inventory.FindItem(weaponInstanceId) : null;
        if (!(weapon?.Definition is WeaponDefinition weaponDefinition))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ItemNotFound);
        }

        if (!TryGetNextUpgradeLevel(weapon, weaponDefinition, upgradeType, out int nextLevel))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidUpgrade);
        }

        if (!TryGetAuthoredUpgradeCost(weaponDefinition, upgradeType, nextLevel, out int authoredCost) ||
            exactCost != authoredCost || exactCost < 0)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidQuantity);
        }

        if (credits < exactCost)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidQuantity);
        }

        suppressInventoryChanged = true;
        if (!weapon.TrySetUpgradeLevel(upgradeType, nextLevel))
        {
            suppressInventoryChanged = false;
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidUpgrade);
        }

        credits -= exactCost;
        suppressInventoryChanged = false;
        changed?.Invoke(this);
        return InventoryOperationResult.Success();
    }

    /// <summary>Crafts a data-driven recipe using carry-grid ingredients and atomically places all outputs.</summary>
    public InventoryOperationResult TryCraftRecipe(ItemRecipeDefinition recipe)
    {
        EnsureInitialized();
        return inventory != null
            ? inventory.TryCraftRecipe(recipe)
            : InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition);
    }

    /// <summary>Returns reserve ammunition counted from owned ammo item instances.</summary>
    public int GetReserveRounds(WeaponDefinition.AmmunitionType ammunitionType)
    {
        EnsureInitialized();
        return inventory != null ? inventory.GetAmmoQuantity(ammunitionType) : 0;
    }

    /// <summary>Adds ammunition as inventory-owned stacks and succeeds only when every requested round fits.</summary>
    public bool TryAddRounds(WeaponDefinition.AmmunitionType ammunitionType, int roundsToAdd)
    {
        EnsureInitialized();
        AmmoDefinition ammoDefinition = GetAmmoDefinition(ammunitionType);
        if (inventory == null || ammoDefinition == null || roundsToAdd <= 0)
        {
            return false;
        }

        InventoryOperationResult result = inventory.TryAddQuantity(ammoDefinition, roundsToAdd);
        return result.Completed;
    }

    /// <summary>Consumes ammunition only when the complete requested amount exists in item stacks.</summary>
    public bool TryConsumeRounds(WeaponDefinition.AmmunitionType ammunitionType, int roundsToConsume)
    {
        EnsureInitialized();
        return inventory != null && inventory.TryConsumeAmmo(ammunitionType, roundsToConsume);
    }

    /// <summary>Atomically exchanges the loaded magazine with reserve ammo item instances.</summary>
    public bool TryExchangeRounds(
        WeaponDefinition.AmmunitionType ammunitionType,
        int roundsReturned,
        int maximumRoundsToWithdraw,
        out int roundsWithdrawn)
    {
        EnsureInitialized();
        roundsWithdrawn = 0;
        return inventory != null && inventory.TryExchangeAmmo(
            ammunitionType,
            roundsReturned,
            maximumRoundsToWithdraw,
            out roundsWithdrawn);
    }

    /// <summary>Debits Credits only when the complete nonnegative amount is available.</summary>
    public bool TrySpendCredits(int amount)
    {
        if (amount < 0 || credits < amount)
        {
            return false;
        }

        credits -= amount;
        changed?.Invoke(this);
        return true;
    }

    /// <summary>Credits a nonnegative amount when the balance can represent the result.</summary>
    public bool TryAddCredits(int amount)
    {
        if (amount < 0 || (long)credits + amount > int.MaxValue)
        {
            return false;
        }

        credits += amount;
        changed?.Invoke(this);
        return true;
    }

    /// <summary>Buys an all-or-nothing quantity, rolling back the debit when carry capacity rejects it.</summary>
    public bool TryPurchaseItem(ItemDefinition definition, int quantity, int totalCost, out InventoryOperationFailure failure)
    {
        EnsureInitialized();
        failure = InventoryOperationFailure.None;
        if (definition == null || quantity <= 0 || totalCost < 0 || inventory == null ||
            !definition.CanBePurchased || definition.BuyPrice <= 0)
        {
            failure = InventoryOperationFailure.InvalidDefinition;
            return false;
        }

        long expectedCost = (long)definition.BuyPrice * quantity;
        if (expectedCost > int.MaxValue || totalCost != (int)expectedCost)
        {
            failure = InventoryOperationFailure.InvalidQuantity;
            return false;
        }

        if (credits < totalCost)
        {
            failure = InventoryOperationFailure.InvalidQuantity;
            return false;
        }

        credits -= totalCost;
        suppressInventoryChanged = true;
        InventoryOperationResult addResult = inventory.TryAddQuantity(definition, quantity, true);
        suppressInventoryChanged = false;
        if (!addResult.Completed)
        {
            credits += totalCost;
            failure = addResult.Failure;
            return false;
        }

        changed?.Invoke(this);
        return true;
    }

    /// <summary>Sells one complete eligible carried stack and Credits it as one inventory transaction.</summary>
    public bool TrySellCarryItem(string instanceId, int sellPricePerUnit, out int quantitySold, out int creditsEarned, out string failureReason)
    {
        EnsureInitialized();
        quantitySold = 0;
        creditsEarned = 0;
        failureReason = string.Empty;
        ItemInstance item = inventory != null ? inventory.FindItem(instanceId) : null;
        if (item == null)
        {
            failureReason = "This item is no longer in the carry grid.";
            return false;
        }

        if (!item.Definition.CanBeSold || item.Definition.SellPrice <= 0 || sellPricePerUnit != item.Definition.SellPrice)
        {
            failureReason = $"{item.Definition.DisplayName} is protected, not eligible for sale, or has an invalid sale price.";
            return false;
        }

        if (item.Definition.Category == ItemCategory.Weapon || item.Definition.Category == ItemCategory.Equipment ||
            item.Definition.Category == ItemCategory.Attachment || item.Definition.Category == ItemCategory.CapacityUpgrade)
        {
            failureReason = $"{item.Definition.DisplayName} has per-instance state the shop does not preserve yet, so it cannot be sold.";
            return false;
        }

        if (item.IsEquipped)
        {
            failureReason = $"Unequip {item.Definition.DisplayName} before selling it.";
            return false;
        }

        long totalValue = (long)sellPricePerUnit * item.Quantity;
        if (totalValue <= 0 || totalValue > int.MaxValue || (long)credits + totalValue > int.MaxValue)
        {
            failureReason = "The sale value exceeds the supported Credits balance.";
            return false;
        }

        quantitySold = item.Quantity;
        suppressInventoryChanged = true;
        InventoryOperationResult takeResult = inventory.TryTakeItem(instanceId, out _);
        suppressInventoryChanged = false;
        if (!takeResult.Succeeded)
        {
            quantitySold = 0;
            failureReason = $"Sale rejected: {takeResult.Failure}.";
            return false;
        }

        creditsEarned = (int)totalValue;
        credits += creditsEarned;
        changed?.Invoke(this);
        return true;
    }


    /// <summary>Finds an item in the player's unlimited safe-room stash by stable instance identity.</summary>
    public ItemInstance FindStashItem(string instanceId)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        for (int index = 0; index < stashItems.Count; index++)
        {
            if (stashItems[index] != null && string.Equals(stashItems[index].InstanceId, instanceId, StringComparison.Ordinal))
            {
                return stashItems[index];
            }
        }

        return null;
    }

    /// <summary>Moves a storable, unequipped carry item into the unlimited safe-room stash.</summary>
    public InventoryOperationResult TryStoreCarryItem(string instanceId)
    {
        EnsureInitialized();
        ItemInstance item = inventory != null ? inventory.FindItem(instanceId) : null;
        if (item == null)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ItemNotFound);
        }

        if (!item.Definition.CanBeStored)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.NotStorable);
        }

        if (item.IsEquipped)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ItemEquipped);
        }

        if (inventory.TryTakeItem(instanceId, out ItemInstance removedItem).Succeeded)
        {
            ClearWeaponSlotAssignments(instanceId);
            stashItems.Add(removedItem);
            removedItem.SetOwningPlayerContainer(this);
            changed?.Invoke(this);
            return InventoryOperationResult.Success();
        }

        return InventoryOperationResult.Rejected(InventoryOperationFailure.ItemNotFound);
    }

    /// <summary>Moves a stashed item into the first legal carry-grid placement without losing it on failure.</summary>
    public InventoryOperationResult TryWithdrawStashItem(string instanceId)
    {
        EnsureInitialized();
        ItemInstance item = FindStashItem(instanceId);
        if (item == null || inventory == null)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ItemNotFound);
        }

        if (!item.Definition.CanBeStored)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.NotStorable);
        }

        if (!inventory.TryFindFirstFit(item.Definition, out Vector2Int position, out bool rotated))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.NoSpace);
        }

        int stashIndex = stashItems.IndexOf(item);
        stashItems.RemoveAt(stashIndex);
        item.SetOwningPlayerContainer(null);
        InventoryOperationResult addResult = inventory.TryAddItem(item, position, rotated);
        if (!addResult.Succeeded)
        {
            item.SetOwningPlayerContainer(this);
            stashItems.Insert(stashIndex, item);
            return addResult;
        }

        if (item.Definition is WeaponDefinition)
        {
            AssignWeaponToFirstAvailableSlot(item.InstanceId);
        }

        changed?.Invoke(this);
        return InventoryOperationResult.Success();
    }

    /// <summary>Assigns a carried ranged weapon to primary (0) or secondary (1) without duplicating ownership.</summary>
    public InventoryOperationResult AssignWeaponEquipmentSlot(string instanceId, int slotIndex)
    {
        EnsureInitialized();
        ItemInstance item = inventory != null ? inventory.FindItem(instanceId) : null;
        if (item == null || !(item.Definition is WeaponDefinition) || (slotIndex != PrimaryWeaponSlotIndex && slotIndex != SecondaryWeaponSlotIndex))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition);
        }

        if (slotIndex == PrimaryWeaponSlotIndex)
        {
            if (string.Equals(secondaryWeaponInstanceId, instanceId, StringComparison.Ordinal))
            {
                secondaryWeaponInstanceId = string.Empty;
            }

            primaryWeaponInstanceId = instanceId;
        }
        else
        {
            if (string.Equals(primaryWeaponInstanceId, instanceId, StringComparison.Ordinal))
            {
                primaryWeaponInstanceId = string.Empty;
            }

            secondaryWeaponInstanceId = instanceId;
        }

        changed?.Invoke(this);
        return InventoryOperationResult.Success();
    }

    /// <summary>Gets the weapon instance assigned to primary (0) or secondary (1), or null for an empty slot.</summary>
    public ItemInstance GetWeaponInEquipmentSlot(int slotIndex)
    {
        EnsureInitialized();
        string instanceId = slotIndex == PrimaryWeaponSlotIndex
            ? primaryWeaponInstanceId
            : slotIndex == SecondaryWeaponSlotIndex
                ? secondaryWeaponInstanceId
                : string.Empty;
        return inventory != null && !string.IsNullOrWhiteSpace(instanceId)
            ? inventory.FindItem(instanceId)
            : null;
    }

    /// <summary>Returns the saved equipment-slot label assigned to this weapon instance.</summary>
    public string GetEquipmentSlotLabel(string instanceId)
    {
        if (string.Equals(primaryWeaponInstanceId, instanceId, StringComparison.Ordinal))
        {
            return PrimaryWeaponSlotLabel;
        }

        if (string.Equals(secondaryWeaponInstanceId, instanceId, StringComparison.Ordinal))
        {
            return SecondaryWeaponSlotLabel;
        }

        return string.Equals(equippedWeaponInstanceId, instanceId, StringComparison.Ordinal)
            ? EquippedSlotLabel
            : string.Empty;
    }

    /// <summary>Applies a carried capacity-upgrade item exactly once and consumes one stack unit.</summary>
    public InventoryOperationResult TryApplyCapacityUpgrade(string instanceId)
    {
        EnsureInitialized();
        ItemInstance item = inventory != null ? inventory.FindItem(instanceId) : null;
        if (item == null || !(item.Definition is InventoryCapacityUpgradeDefinition))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidUpgrade);
        }

        return inventory.TryApplyCapacityUpgrade(instanceId);
    }

    /// <summary>Discards a carried non-weapon item only when its definition allows it and it is not equipped.</summary>
    public InventoryOperationResult TryDiscardCarryItem(string instanceId)
    {
        EnsureInitialized();
        ItemInstance item = inventory != null ? inventory.FindItem(instanceId) : null;
        if (item == null)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ItemNotFound);
        }

        if (item.Definition.Category == ItemCategory.Weapon)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ProtectedItem);
        }

        if (item.IsEquipped)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ItemEquipped);
        }

        if (item.InstalledAttachmentInstanceIds.Count > 0)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidUpgrade);
        }

        InventoryOperationResult result = inventory.TryDiscardItem(instanceId, out _);
        if (result.Succeeded)
        {
            ClearWeaponSlotAssignments(instanceId);
            changed?.Invoke(this);
        }

        return result;
    }

    private static bool ContainsInstanceId(IReadOnlyList<string> instanceIds, string targetInstanceId)
    {
        if (instanceIds == null || string.IsNullOrWhiteSpace(targetInstanceId))
        {
            return false;
        }

        for (int index = 0; index < instanceIds.Count; index++)
        {
            if (string.Equals(instanceIds[index], targetInstanceId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAttachmentCompatibleWithWeapon(
        WeaponDefinition weaponDefinition,
        WeaponAttachmentDefinition attachmentDefinition)
    {
        if (weaponDefinition == null || attachmentDefinition == null ||
            string.IsNullOrWhiteSpace(attachmentDefinition.AttachmentSlotId))
        {
            return false;
        }

        string[] compatibleTags = attachmentDefinition.CompatibleWeaponTags;
        string[] weaponTags = weaponDefinition.Tags;
        if (compatibleTags == null || weaponTags == null || compatibleTags.Length == 0)
        {
            return false;
        }

        for (int requiredIndex = 0; requiredIndex < compatibleTags.Length; requiredIndex++)
        {
            string requiredTag = compatibleTags[requiredIndex];
            if (string.IsNullOrWhiteSpace(requiredTag))
            {
                continue;
            }

            for (int weaponTagIndex = 0; weaponTagIndex < weaponTags.Length; weaponTagIndex++)
            {
                if (string.Equals(requiredTag, weaponTags[weaponTagIndex], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetNextUpgradeLevel(
        ItemInstance weapon,
        WeaponDefinition weaponDefinition,
        WeaponUpgradeType upgradeType,
        out int nextLevel)
    {
        nextLevel = 0;
        if (weapon == null || weaponDefinition == null || !Enum.IsDefined(typeof(WeaponUpgradeType), upgradeType))
        {
            return false;
        }

        WeaponUpgradeOption[] options = weaponDefinition.upgradeOptions;
        if (options == null)
        {
            return false;
        }

        bool found = false;
        int maximumLevel = 0;
        for (int index = 0; index < options.Length; index++)
        {
            if (options[index].upgradeType != upgradeType)
            {
                continue;
            }

            if (found || options[index].maximumLevel <= 0)
            {
                return false;
            }

            maximumLevel = options[index].maximumLevel;
            found = true;
        }

        int currentLevel = weapon.GetUpgradeLevel(upgradeType);
        if (!found || currentLevel < 0 || currentLevel >= maximumLevel)
        {
            return false;
        }

        nextLevel = currentLevel + 1;
        return true;
    }

    private bool TryGetAuthoredUpgradeCost(
        WeaponDefinition weaponDefinition,
        WeaponUpgradeType upgradeType,
        int level,
        out int exactCost)
    {
        exactCost = 0;
        if (weaponDefinition == null || level < 1 || weaponUpgradePurchaseOptions == null)
        {
            return false;
        }

        bool found = false;
        for (int index = 0; index < weaponUpgradePurchaseOptions.Count; index++)
        {
            WeaponUpgradePurchaseOption option = weaponUpgradePurchaseOptions[index];
            if (!string.Equals(option.WeaponStableItemId, weaponDefinition.StableItemId, StringComparison.Ordinal) ||
                option.UpgradeType != upgradeType)
            {
                continue;
            }

            if (found)
            {
                return false;
            }

            int[] levelCosts = option.LevelCosts;
            if (levelCosts == null || level > levelCosts.Length || levelCosts[level - 1] < 0)
            {
                return false;
            }

            exactCost = levelCosts[level - 1];
            found = true;
        }

        return found;
    }

    private void AssignWeaponToFirstAvailableSlot(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(primaryWeaponInstanceId) || GetWeaponInEquipmentSlot(PrimaryWeaponSlotIndex) == null)
        {
            primaryWeaponInstanceId = instanceId;
        }
        else if (string.IsNullOrWhiteSpace(secondaryWeaponInstanceId) || GetWeaponInEquipmentSlot(SecondaryWeaponSlotIndex) == null)
        {
            secondaryWeaponInstanceId = instanceId;
        }
    }

    private void ClearWeaponSlotAssignments(string instanceId)
    {
        if (string.Equals(primaryWeaponInstanceId, instanceId, StringComparison.Ordinal))
        {
            primaryWeaponInstanceId = string.Empty;
        }

        if (string.Equals(secondaryWeaponInstanceId, instanceId, StringComparison.Ordinal))
        {
            secondaryWeaponInstanceId = string.Empty;
        }
    }

    private void EnsureDefaultWeaponSlots()
    {
        if (inventory == null)
        {
            return;
        }

        if (GetWeaponInEquipmentSlot(PrimaryWeaponSlotIndex) == null)
        {
            primaryWeaponInstanceId = string.Empty;
        }

        if (GetWeaponInEquipmentSlot(SecondaryWeaponSlotIndex) == null)
        {
            secondaryWeaponInstanceId = string.Empty;
        }

        IReadOnlyList<ItemInstance> items = inventory.Items;
        for (int index = 0; index < items.Count; index++)
        {
            ItemInstance item = items[index];
            if (!(item.Definition is WeaponDefinition) ||
                string.Equals(item.InstanceId, primaryWeaponInstanceId, StringComparison.Ordinal) ||
                string.Equals(item.InstanceId, secondaryWeaponInstanceId, StringComparison.Ordinal))
            {
                continue;
            }

            AssignWeaponToFirstAvailableSlot(item.InstanceId);
        }
    }

    private void ValidateWeaponEquipmentSlots()
    {
        ItemInstance primaryWeapon = GetWeaponInEquipmentSlot(PrimaryWeaponSlotIndex);
        ItemInstance secondaryWeapon = GetWeaponInEquipmentSlot(SecondaryWeaponSlotIndex);
        if (primaryWeapon != null && !(primaryWeapon.Definition is WeaponDefinition))
        {
            primaryWeaponInstanceId = string.Empty;
        }

        if (secondaryWeapon != null && !(secondaryWeapon.Definition is WeaponDefinition))
        {
            secondaryWeaponInstanceId = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(primaryWeaponInstanceId) &&
            string.Equals(primaryWeaponInstanceId, secondaryWeaponInstanceId, StringComparison.Ordinal))
        {
            Debug.LogWarning($"Player item inventory on '{name}' found one weapon assigned to both equipment slots; the secondary assignment was cleared.", this);
            secondaryWeaponInstanceId = string.Empty;
        }
    }

    /// <summary>Updates the active held-weapon identity that is persisted with the inventory.</summary>
    public void SetEquippedWeaponInstanceId(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            equippedWeaponInstanceId = string.Empty;
            return;
        }

        ItemInstance item = Inventory != null ? Inventory.FindItem(instanceId) : null;
        if (item == null || !(item.Definition is WeaponDefinition))
        {
            Debug.LogWarning($"Cannot record equipped weapon instance '{instanceId}' because it is not an owned weapon.", this);
            return;
        }

        equippedWeaponInstanceId = instanceId;
        AssignWeaponToFirstAvailableSlot(instanceId);
    }

    /// <summary>Applies a supported inventory snapshot while preserving invalid records for future recovery.</summary>
    public void LoadData(GameData data)
    {
        EnsureInitialized();
        InventorySaveData savedState = data != null ? data.inventoryState : null;
        if (savedState == null || !savedState.hasInventoryPayload)
        {
            if (!TryCreateConfiguredStarterInventory(out ItemInventory starterInventory, out InventoryOperationFailure failure))
            {
                Debug.LogError($"Could not reset player item inventory to the configured starter loadout. Failure: {failure}. Existing inventory state was left unchanged.", this);
                return;
            }

            WeaponHoldController resetWeaponHoldController = GetComponent<WeaponHoldController>();
            if (resetWeaponHoldController != null)
            {
                resetWeaponHoldController.Unequip();
            }

            unsupportedFutureInventoryVersion = false;
            ReplaceInventory(starterInventory);
            keyItems = new List<ItemInstance>();
            stashItems = new List<ItemInstance>();
            installedAttachmentItems = new List<ItemInstance>();
            equippedWeaponInstanceId = string.Empty;
            unresolvedCarryItems.Clear();
            unresolvedKeyItems.Clear();
            unresolvedStashItems.Clear();
            unresolvedInstalledAttachmentItems.Clear();
            primaryWeaponInstanceId = string.Empty;
            secondaryWeaponInstanceId = string.Empty;
            credits = DefaultStartingCredits;
            EnsureDefaultWeaponSlots();
            changed?.Invoke(this);
            return;
        }

        if (savedState.schemaVersion > InventorySaveData.CurrentSchemaVersion)
        {
            unsupportedFutureInventoryVersion = true;
            Debug.LogWarning(
                $"Player item inventory on '{name}' found unsupported inventory save version {savedState.schemaVersion}; the stored snapshot will be left untouched.",
                this);
            return;
        }

        if (!TryGetRestoredGridDimensions(savedState, out int gridWidth, out int gridHeight))
        {
            Debug.LogWarning($"Player item inventory on '{name}' left the current inventory unchanged because saved dimensions {savedState.gridWidth}x{savedState.gridHeight} cannot be mapped to a supported case stage.", this);
            return;
        }

        unsupportedFutureInventoryVersion = false;
        EnsureInitialized();
        credits = savedState.schemaVersion < 3
            ? DefaultStartingCredits
            : Mathf.Max(0, savedState.credits);
        unresolvedCarryItems.Clear();
        unresolvedKeyItems.Clear();
        unresolvedStashItems.Clear();
        unresolvedInstalledAttachmentItems.Clear();

        ItemInventory restoredInventory = new ItemInventory(gridWidth, gridHeight);
        List<ItemInstance> restoredKeyItems = new List<ItemInstance>();
        List<ItemInstance> restoredStashItems = new List<ItemInstance>();
        List<ItemInstance> restoredInstalledAttachmentItems = new List<ItemInstance>();
        HashSet<string> restoredInstanceIds = new HashSet<string>(StringComparer.Ordinal);
        RestoreItemRecords(savedState.carryItems, false, restoredInventory, restoredKeyItems, restoredInstanceIds, savedState.schemaVersion < InventorySaveData.DirectCaseDimensionsSchemaVersion);
        RestoreItemRecords(savedState.keyItems, true, restoredInventory, restoredKeyItems, restoredInstanceIds, false);
        RestoreStashItemRecords(savedState.stashItems, restoredStashItems, restoredInstanceIds);
        RestoreInstalledAttachmentRecords(savedState.installedAttachmentItems, restoredInstalledAttachmentItems, restoredInstanceIds);
        if (savedState.schemaVersion < InventorySaveData.InstalledAttachmentCollectionSchemaVersion)
        {
            RestoreLegacyInstalledAttachments(restoredInventory, restoredInstalledAttachmentItems);
        }

        ValidateInstalledAttachmentReferences(restoredInventory, restoredInstalledAttachmentItems);

        string requestedEquippedWeaponId = savedState.equippedWeaponInstanceId ?? string.Empty;
        ItemInstance selectedWeapon = string.IsNullOrWhiteSpace(requestedEquippedWeaponId)
            ? null
            : restoredInventory.FindItem(requestedEquippedWeaponId);
        if (!string.IsNullOrWhiteSpace(requestedEquippedWeaponId) &&
            (selectedWeapon == null || !(selectedWeapon.Definition is WeaponDefinition)))
        {
            Debug.LogWarning($"Player item inventory on '{name}' could not resolve equipped weapon instance '{requestedEquippedWeaponId}'; the saved ID is retained until a valid weapon is equipped.", this);
            selectedWeapon = null;
        }

        if (selectedWeapon == null)
        {
            IReadOnlyList<ItemInstance> restoredItems = restoredInventory.Items;
            for (int index = 0; index < restoredItems.Count; index++)
            {
                if (restoredItems[index].IsEquipped && restoredItems[index].Definition is WeaponDefinition)
                {
                    selectedWeapon = restoredItems[index];
                    break;
                }
            }
        }

        IReadOnlyList<ItemInstance> allRestoredItems = restoredInventory.Items;
        for (int index = 0; index < allRestoredItems.Count; index++)
        {
            ItemInstance item = allRestoredItems[index];
            if (item.Definition is WeaponDefinition)
            {
                restoredInventory.TrySetEquipped(item.InstanceId, item == selectedWeapon);
            }
        }

        equippedWeaponInstanceId = selectedWeapon != null
            ? selectedWeapon.InstanceId
            : requestedEquippedWeaponId;
        primaryWeaponInstanceId = savedState.primaryWeaponInstanceId ?? string.Empty;
        secondaryWeaponInstanceId = savedState.secondaryWeaponInstanceId ?? string.Empty;
        if (!TryExpandInventoryForLayoutTest(restoredInventory, out InventoryOperationFailure layoutTestFailure))
        {
            Debug.LogWarning($"Player item inventory on '{name}' could not expand to the maximum layout-test case size: {layoutTestFailure}.", this);
        }

        ReplaceInventory(restoredInventory);
        keyItems = restoredKeyItems;
        stashItems = restoredStashItems;
        installedAttachmentItems = restoredInstalledAttachmentItems;
        for (int index = 0; index < keyItems.Count; index++)
        {
            keyItems[index].SetOwningPlayerContainer(this);
        }

        for (int index = 0; index < stashItems.Count; index++)
        {
            stashItems[index].SetOwningPlayerContainer(this);
        }

        for (int index = 0; index < installedAttachmentItems.Count; index++)
        {
            installedAttachmentItems[index].SetOwningPlayerContainer(this);
        }

        ValidateWeaponEquipmentSlots();
        EnsureDefaultWeaponSlots();
        changed?.Invoke(this);
        WeaponHoldController weaponHoldController = GetComponent<WeaponHoldController>();
        if (weaponHoldController != null && weaponHoldController.isActiveAndEnabled)
        {
            weaponHoldController.RestoreSavedWeaponSelection();
        }
    }

    /// <summary>Copies carry, key-item, equipment, and weapon-instance state into the active save slot.</summary>
    public void SaveData(ref GameData data)
    {
        if (unsupportedFutureInventoryVersion)
        {
            return;
        }

        EnsureInitialized();
        if (data == null)
        {
            data = new GameData();
        }

        if (data.inventoryState == null)
        {
            data.inventoryState = new InventorySaveData();
        }

        InventorySaveData savedState = data.inventoryState;
        savedState.hasInventoryPayload = true;
        savedState.schemaVersion = InventorySaveData.CurrentSchemaVersion;
        savedState.gridWidth = inventory != null ? inventory.GridWidth : ItemInventory.DefaultGridWidth;
        savedState.gridHeight = inventory != null ? inventory.GridHeight : ItemInventory.DefaultGridHeight;
        savedState.carryItems = new List<ItemInstanceSaveData>();
        savedState.stashItems = new List<ItemInstanceSaveData>();
        savedState.keyItems = new List<ItemInstanceSaveData>();
        savedState.installedAttachmentItems = new List<ItemInstanceSaveData>();

        if (inventory != null)
        {
            IReadOnlyList<ItemInstance> ownedItems = inventory.Items;
            for (int index = 0; index < ownedItems.Count; index++)
            {
                savedState.carryItems.Add(CreateSaveData(ownedItems[index]));
            }
        }

        for (int index = 0; index < stashItems.Count; index++)
        {
            if (stashItems[index] != null)
            {
                savedState.stashItems.Add(CreateSaveData(stashItems[index]));
            }
        }

        for (int index = 0; index < keyItems.Count; index++)
        {
            if (keyItems[index] != null)
            {
                savedState.keyItems.Add(CreateSaveData(keyItems[index]));
            }
        }

        for (int index = 0; index < installedAttachmentItems.Count; index++)
        {
            if (installedAttachmentItems[index] != null)
            {
                savedState.installedAttachmentItems.Add(CreateSaveData(installedAttachmentItems[index]));
            }
        }

        savedState.carryItems.AddRange(unresolvedCarryItems);
        savedState.keyItems.AddRange(unresolvedKeyItems);
        savedState.stashItems.AddRange(unresolvedStashItems);
        savedState.installedAttachmentItems.AddRange(unresolvedInstalledAttachmentItems);

        WeaponHoldController weaponHoldController = GetComponent<WeaponHoldController>();
        if (weaponHoldController != null)
        {
            equippedWeaponInstanceId = weaponHoldController.CurrentWeaponItem != null
                ? weaponHoldController.CurrentWeaponItem.InstanceId
                : string.Empty;
        }
        else if (inventory != null)
        {
            IReadOnlyList<ItemInstance> ownedItems = inventory.Items;
            for (int index = 0; index < ownedItems.Count; index++)
            {
                if (ownedItems[index].Definition is WeaponDefinition && ownedItems[index].IsEquipped)
                {
                    equippedWeaponInstanceId = ownedItems[index].InstanceId;
                    break;
                }
            }
        }

        savedState.equippedWeaponInstanceId = equippedWeaponInstanceId ?? string.Empty;
        savedState.primaryWeaponInstanceId = primaryWeaponInstanceId ?? string.Empty;
        savedState.secondaryWeaponInstanceId = secondaryWeaponInstanceId ?? string.Empty;
        savedState.credits = credits;
    }

    private bool TryGetRestoredGridDimensions(InventorySaveData savedState, out int gridWidth, out int gridHeight)
    {
        gridWidth = ItemInventory.DefaultGridWidth;
        gridHeight = ItemInventory.DefaultGridHeight;
        if (savedState.schemaVersion >= InventorySaveData.DirectCaseDimensionsSchemaVersion)
        {
            if (!ItemInventory.IsValidCaseDimensions(savedState.gridWidth, savedState.gridHeight))
            {
                return false;
            }

            gridWidth = savedState.gridWidth;
            gridHeight = savedState.gridHeight;
            return true;
        }

        if (savedState.gridWidth < 1 || savedState.gridHeight < 1)
        {
            return false;
        }

        long savedCapacity = (long)savedState.gridWidth * savedState.gridHeight;
        if (savedCapacity > (long)ItemInventory.CaseGridWidth * ItemInventory.MaximumCaseHeight)
        {
            return false;
        }

        long resolvedItemCells = 0;
        int requiredSavedRowCount = 0;
        HashSet<string> countedInstanceIds = new HashSet<string>(StringComparer.Ordinal);
        List<ItemInstanceSaveData> carryRecords = savedState.carryItems;
        if (carryRecords != null)
        {
            for (int index = 0; index < carryRecords.Count; index++)
            {
                ItemInstanceSaveData savedItem = carryRecords[index];
                if (savedItem == null || string.IsNullOrWhiteSpace(savedItem.instanceId) || !countedInstanceIds.Add(savedItem.instanceId))
                {
                    continue;
                }

                ItemDefinition definition = ResolveDefinition(savedItem.stableItemId);
                if (definition == null || definition.Category == ItemCategory.KeyItem || definition.Category == ItemCategory.QuestItem)
                {
                    continue;
                }

                int itemWidth = Mathf.Max(1, definition.InventoryWidth);
                int itemHeight = Mathf.Max(1, definition.InventoryHeight);
                if (savedItem.RotationQuarterTurns % 2 != 0)
                {
                    int swap = itemWidth;
                    itemWidth = itemHeight;
                    itemHeight = swap;
                }

                resolvedItemCells += (long)itemWidth * itemHeight;
                long right = (long)savedItem.gridX + itemWidth;
                long bottom = (long)savedItem.gridY + itemHeight;
                if (savedItem.gridX >= 0 && savedItem.gridY >= 0 && right <= ItemInventory.CaseGridWidth && bottom <= ItemInventory.MaximumCaseHeight)
                {
                    requiredSavedRowCount = Mathf.Max(requiredSavedRowCount, (int)bottom);
                }
            }
        }

        long maximumCaseCapacity = (long)ItemInventory.CaseGridWidth * ItemInventory.MaximumCaseHeight;
        if (resolvedItemCells > maximumCaseCapacity)
        {
            return false;
        }

        long requiredCapacity = Math.Max(
            (long)ItemInventory.DefaultGridWidth * ItemInventory.DefaultGridHeight,
            Math.Max(savedCapacity, resolvedItemCells));
        int[] supportedHeights =
        {
            ItemInventory.CaseStage1Height,
            ItemInventory.CaseStage2Height,
            ItemInventory.CaseStage3Height,
            ItemInventory.CaseStage4Height
        };
        for (int index = 0; index < supportedHeights.Length; index++)
        {
            int candidateHeight = supportedHeights[index];
            long candidateCapacity = (long)ItemInventory.CaseGridWidth * candidateHeight;
            if (candidateCapacity >= requiredCapacity && candidateHeight >= requiredSavedRowCount)
            {
                gridWidth = ItemInventory.CaseGridWidth;
                gridHeight = candidateHeight;
                return true;
            }
        }

        return false;
    }


    private void RestoreItemRecords(
        List<ItemInstanceSaveData> savedItems,
        bool isKeyItemCollection,
        ItemInventory restoredInventory,
        List<ItemInstance> restoredKeyItems,
        HashSet<string> restoredInstanceIds,
        bool allowLegacyPlacementMigration)
    {
        if (savedItems == null)
        {
            return;
        }

        List<ItemInstanceSaveData> unresolvedItems = isKeyItemCollection ? unresolvedKeyItems : unresolvedCarryItems;
        for (int index = 0; index < savedItems.Count; index++)
        {
            ItemInstanceSaveData savedItem = savedItems[index];
            if (savedItem == null)
            {
                Debug.LogWarning($"Player item inventory on '{name}' skipped a null saved item record.", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(savedItem.instanceId) || !restoredInstanceIds.Add(savedItem.instanceId))
            {
                Debug.LogWarning($"Player item inventory on '{name}' preserved an item with an empty or duplicate instance ID '{savedItem.instanceId}'.", this);
                unresolvedItems.Add(savedItem);
                continue;
            }

            ItemDefinition definition = ResolveDefinition(savedItem.stableItemId);
            if (definition == null)
            {
                Debug.LogWarning($"Player item inventory on '{name}' preserved item instance '{savedItem.instanceId}' because stable definition ID '{savedItem.stableItemId}' is missing or ambiguous in the definition catalog.", this);
                unresolvedItems.Add(savedItem);
                continue;
            }

            bool isKeyDefinition = definition.Category == ItemCategory.KeyItem || definition.Category == ItemCategory.QuestItem;
            if (isKeyDefinition != isKeyItemCollection)
            {
                Debug.LogWarning($"Player item inventory on '{name}' preserved item instance '{savedItem.instanceId}' because its saved container does not match definition '{definition.StableItemId}'.", this);
                unresolvedItems.Add(savedItem);
                continue;
            }

            if (!ItemInstance.TryRestoreFromSave(definition, savedItem, out ItemInstance restoredItem, out string failureReason))
            {
                Debug.LogWarning($"Player item inventory on '{name}' preserved item instance '{savedItem.instanceId}': {failureReason}", this);
                unresolvedItems.Add(savedItem);
                continue;
            }

            if (isKeyItemCollection)
            {
                if (savedItem.gridX != -1 || savedItem.gridY != -1 || savedItem.RotationQuarterTurns != 0)
                {
                    Debug.LogWarning($"Player item inventory on '{name}' preserved key-item instance '{savedItem.instanceId}' because it contains carry-grid placement state.", this);
                    unresolvedItems.Add(savedItem);
                    continue;
                }

                restoredItem.SetOwningPlayerContainer(this);
                restoredKeyItems.Add(restoredItem);
                continue;
            }

            InventoryOperationResult addResult = restoredInventory.TryAddItem(
                restoredItem,
                new Vector2Int(savedItem.gridX, savedItem.gridY),
                savedItem.RotationQuarterTurns);
            if (!addResult.Succeeded && allowLegacyPlacementMigration &&
                restoredInventory.TryFindFirstFit(definition, out Vector2Int migratedPosition, out bool migratedRotation))
            {
                addResult = restoredInventory.TryAddItem(restoredItem, migratedPosition, migratedRotation);
                if (addResult.Succeeded)
                {
                    Debug.LogWarning($"Player item inventory on '{name}' moved legacy item instance '{savedItem.instanceId}' to a valid case-grid placement during migration.", this);
                }
            }

            if (!addResult.Succeeded)
            {
                Debug.LogWarning($"Player item inventory on '{name}' preserved item instance '{savedItem.instanceId}' because its saved grid placement is invalid ({addResult.Failure}).", this);
                unresolvedItems.Add(savedItem);
            }
        }
    }

    private void RestoreInstalledAttachmentRecords(
        List<ItemInstanceSaveData> savedItems,
        List<ItemInstance> restoredAttachments,
        HashSet<string> restoredInstanceIds)
    {
        if (savedItems == null)
        {
            return;
        }

        for (int index = 0; index < savedItems.Count; index++)
        {
            ItemInstanceSaveData savedItem = savedItems[index];
            if (savedItem == null || string.IsNullOrWhiteSpace(savedItem.instanceId) ||
                !restoredInstanceIds.Add(savedItem.instanceId))
            {
                Debug.LogWarning($"Player item inventory on '{name}' preserved an invalid or duplicate installed-attachment record.", this);
                if (savedItem != null)
                {
                    unresolvedInstalledAttachmentItems.Add(savedItem);
                }

                continue;
            }

            ItemDefinition definition = ResolveDefinition(savedItem.stableItemId);
            if (!(definition is WeaponAttachmentDefinition) || savedItem.quantity != 1 ||
                savedItem.gridX != -1 || savedItem.gridY != -1 || savedItem.RotationQuarterTurns != 0 ||
                savedItem.isEquipped || !string.IsNullOrWhiteSpace(savedItem.equippedSlot))
            {
                Debug.LogWarning($"Player item inventory on '{name}' preserved installed attachment '{savedItem.instanceId}' because its definition or container state is invalid.", this);
                unresolvedInstalledAttachmentItems.Add(savedItem);
                continue;
            }

            if (!ItemInstance.TryRestoreFromSave(definition, savedItem, out ItemInstance restoredItem, out string failureReason))
            {
                Debug.LogWarning($"Player item inventory on '{name}' preserved installed attachment '{savedItem.instanceId}': {failureReason}", this);
                unresolvedInstalledAttachmentItems.Add(savedItem);
                continue;
            }

            restoredItem.SetOwningPlayerContainer(this);
            restoredAttachments.Add(restoredItem);
        }
    }

    private void RestoreLegacyInstalledAttachments(ItemInventory restoredInventory, List<ItemInstance> restoredAttachments)
    {
        if (restoredInventory == null || restoredAttachments == null)
        {
            return;
        }

        IReadOnlyList<ItemInstance> restoredWeapons = restoredInventory.Items;
        for (int weaponIndex = 0; weaponIndex < restoredWeapons.Count; weaponIndex++)
        {
            ItemInstance weapon = restoredWeapons[weaponIndex];
            if (!(weapon.Definition is WeaponDefinition weaponDefinition))
            {
                continue;
            }

            IReadOnlyList<string> attachmentIds = weapon.InstalledAttachmentInstanceIds;
            for (int attachmentIndex = 0; attachmentIndex < attachmentIds.Count; attachmentIndex++)
            {
                string attachmentId = attachmentIds[attachmentIndex];
                if (FindAttachmentInList(restoredAttachments, attachmentId) != null)
                {
                    continue;
                }

                ItemInstance attachment = restoredInventory.FindItem(attachmentId);
                if (!(attachment?.Definition is WeaponAttachmentDefinition attachmentDefinition) ||
                    !IsAttachmentCompatibleWithWeapon(weaponDefinition, attachmentDefinition))
                {
                    continue;
                }

                bool slotAlreadyUsed = false;
                for (int existingIndex = 0; existingIndex < attachmentIndex; existingIndex++)
                {
                    ItemInstance existingAttachment = FindAttachmentInList(restoredAttachments, attachmentIds[existingIndex]);
                    if (existingAttachment?.Definition is WeaponAttachmentDefinition existingDefinition &&
                        string.Equals(existingDefinition.AttachmentSlotId, attachmentDefinition.AttachmentSlotId, StringComparison.OrdinalIgnoreCase))
                    {
                        slotAlreadyUsed = true;
                        break;
                    }
                }

                if (slotAlreadyUsed || !restoredInventory.TryTakeItem(attachmentId, out ItemInstance removedAttachment).Succeeded)
                {
                    continue;
                }

                removedAttachment.SetOwningPlayerContainer(this);
                restoredAttachments.Add(removedAttachment);
            }
        }
    }

    private void ValidateInstalledAttachmentReferences(ItemInventory restoredInventory, List<ItemInstance> restoredAttachments)
    {
        if (restoredInventory == null || restoredAttachments == null)
        {
            return;
        }

        HashSet<string> globallyReferencedAttachmentIds = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<ItemInstance> restoredItems = restoredInventory.Items;
        for (int weaponIndex = 0; weaponIndex < restoredItems.Count; weaponIndex++)
        {
            ItemInstance weapon = restoredItems[weaponIndex];
            if (!(weapon.Definition is WeaponDefinition weaponDefinition))
            {
                continue;
            }

            HashSet<string> usedSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<string> attachmentIds = weapon.InstalledAttachmentInstanceIds;
            for (int attachmentIndex = attachmentIds.Count - 1; attachmentIndex >= 0; attachmentIndex--)
            {
                string attachmentId = attachmentIds[attachmentIndex];
                ItemInstance attachment = FindAttachmentInList(restoredAttachments, attachmentId);
                WeaponAttachmentDefinition attachmentDefinition = attachment != null
                    ? attachment.Definition as WeaponAttachmentDefinition
                    : null;
                if (attachmentDefinition == null || !IsAttachmentCompatibleWithWeapon(weaponDefinition, attachmentDefinition) ||
                    !usedSlots.Add(attachmentDefinition.AttachmentSlotId) || !globallyReferencedAttachmentIds.Add(attachmentId))
                {
                    Debug.LogWarning($"Player item inventory on '{name}' removed invalid installed attachment reference '{attachmentId}' from weapon '{weapon.InstanceId}'.", this);
                    weapon.RemoveInstalledAttachment(attachmentId);
                }
            }
        }
    }

    private static ItemInstance FindAttachmentInList(List<ItemInstance> attachments, string instanceId)
    {
        if (attachments == null || string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        for (int index = 0; index < attachments.Count; index++)
        {
            ItemInstance attachment = attachments[index];
            if (attachment != null && string.Equals(attachment.InstanceId, instanceId, StringComparison.Ordinal))
            {
                return attachment;
            }
        }

        return null;
    }

    private void RestoreStashItemRecords(
        List<ItemInstanceSaveData> savedItems,
        List<ItemInstance> restoredStashItems,
        HashSet<string> restoredInstanceIds)
    {
        if (savedItems == null)
        {
            return;
        }

        for (int index = 0; index < savedItems.Count; index++)
        {
            ItemInstanceSaveData savedItem = savedItems[index];
            if (savedItem == null || string.IsNullOrWhiteSpace(savedItem.instanceId) ||
                !restoredInstanceIds.Add(savedItem.instanceId))
            {
                Debug.LogWarning($"Player item inventory on '{name}' preserved an invalid or duplicate stash record.", this);
                if (savedItem != null)
                {
                    unresolvedStashItems.Add(savedItem);
                }

                continue;
            }

            ItemDefinition definition = ResolveDefinition(savedItem.stableItemId);
            bool isKeyDefinition = definition != null &&
                (definition.Category == ItemCategory.KeyItem || definition.Category == ItemCategory.QuestItem);
            if (definition == null || isKeyDefinition || !definition.CanBeStored ||
                savedItem.gridX != -1 || savedItem.gridY != -1 || savedItem.RotationQuarterTurns != 0 ||
                savedItem.isEquipped || !string.IsNullOrWhiteSpace(savedItem.equippedSlot))
            {
                Debug.LogWarning($"Player item inventory on '{name}' preserved stash item '{savedItem.instanceId}' because its definition or container/equipment state is invalid.", this);
                unresolvedStashItems.Add(savedItem);
                continue;
            }

            if (!ItemInstance.TryRestoreFromSave(definition, savedItem, out ItemInstance restoredItem, out string failureReason))
            {
                Debug.LogWarning($"Player item inventory on '{name}' preserved stash item '{savedItem.instanceId}': {failureReason}", this);
                unresolvedStashItems.Add(savedItem);
                continue;
            }

            restoredItem.SetOwningPlayerContainer(this);
            restoredStashItems.Add(restoredItem);
        }
    }

    private ItemDefinition ResolveDefinition(string stableItemId)
    {
        if (string.IsNullOrWhiteSpace(stableItemId))
        {
            return null;
        }

        ItemDefinition resolvedDefinition = null;
        ItemDefinition[] loadedDefinitions = Resources.FindObjectsOfTypeAll<ItemDefinition>();
        for (int index = 0; index < loadedDefinitions.Length; index++)
        {
            if (!TryMatchDefinition(loadedDefinitions[index], stableItemId, ref resolvedDefinition))
            {
                return null;
            }
        }

        if (definitionCatalog != null)
        {
            for (int index = 0; index < definitionCatalog.Count; index++)
            {
                if (!TryMatchDefinition(definitionCatalog[index], stableItemId, ref resolvedDefinition))
                {
                    return null;
                }
            }
        }

        ItemDefinition[] configuredDefinitions =
        {
            startingPistolDefinition,
            startingAssaultRifleDefinition,
            pistolAmmoDefinition,
            rifleAmmoDefinition
        };
        for (int index = 0; index < configuredDefinitions.Length; index++)
        {
            if (!TryMatchDefinition(configuredDefinitions[index], stableItemId, ref resolvedDefinition))
            {
                return null;
            }
        }

        return resolvedDefinition;
    }

    private bool TryMatchDefinition(ItemDefinition candidate, string stableItemId, ref ItemDefinition resolvedDefinition)
    {
        if (candidate == null || !string.Equals(candidate.StableItemId, stableItemId, StringComparison.Ordinal))
        {
            return true;
        }

        if (resolvedDefinition != null && resolvedDefinition != candidate)
        {
            Debug.LogError($"Player item inventory on '{name}' found duplicate loaded definitions for stable item ID '{stableItemId}'.", this);
            return false;
        }

        resolvedDefinition = candidate;
        return true;
    }

    private void ReplaceInventory(ItemInventory replacement)
    {
        if (inventory != null)
        {
            inventory.Changed -= HandleInventoryChanged;
        }

        inventory = replacement;
        if (inventory != null)
        {
            inventory.Changed += HandleInventoryChanged;
        }
    }

    private ItemInstanceSaveData CreateSaveData(ItemInstance item)
    {
        return new ItemInstanceSaveData
        {
            stableItemId = item.Definition != null ? item.Definition.StableItemId : string.Empty,
            instanceId = item.InstanceId,
            quantity = item.Quantity,
            gridX = item.GridPosition.x,
            gridY = item.GridPosition.y,
            isRotated = item.IsRotated,
            rotationQuarterTurns = item.RotationQuarterTurns,
            isEquipped = item.IsEquipped,
            equippedSlot = GetEquipmentSlotLabel(item.InstanceId),
            loadedRounds = item.LoadedRounds,
            upgradeLevels = new List<ItemUpgradeLevel>(item.UpgradeLevels),
            installedAttachmentInstanceIds = new List<string>(item.InstalledAttachmentInstanceIds)
        };
    }

    private bool TryCreateConfiguredStarterInventory(
        out ItemInventory starterInventory,
        out InventoryOperationFailure failure)
    {
        if (!ItemInventory.TryCreateStartingLoadout(
                startingPistolDefinition,
                startingAssaultRifleDefinition,
                pistolAmmoDefinition,
                rifleAmmoDefinition,
                out starterInventory,
                out failure,
                startingPistolReserveRounds,
                startingRifleReserveRounds))
        {
            return false;
        }

        return TryExpandInventoryForLayoutTest(starterInventory, out failure);
    }

    private bool TryExpandInventoryForLayoutTest(ItemInventory targetInventory, out InventoryOperationFailure failure)
    {
        failure = InventoryOperationFailure.None;
        if (!useMaximumCaseSizeForLayoutTesting || targetInventory == null)
        {
            return true;
        }

        while (targetInventory.GridHeight < ItemInventory.MaximumCaseHeight)
        {
            if (!ItemInventory.TryGetNextCaseDimensions(
                    targetInventory.GridWidth,
                    targetInventory.GridHeight,
                    out int nextWidth,
                    out int nextHeight))
            {
                failure = InventoryOperationFailure.InvalidDimensions;
                return false;
            }

            InventoryOperationResult resizeResult = targetInventory.TrySetGridDimensions(nextWidth, nextHeight);
            if (!resizeResult.Succeeded)
            {
                failure = resizeResult.Failure;
                return false;
            }
        }

        return true;
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (definitionCatalog == null)
        {
            definitionCatalog = new List<ItemDefinition>();
        }

        if (weaponUpgradePurchaseOptions == null)
        {
            weaponUpgradePurchaseOptions = new List<WeaponUpgradePurchaseOption>();
        }

        if (keyItems == null)
        {
            keyItems = new List<ItemInstance>();
        }

        if (stashItems == null)
        {
            stashItems = new List<ItemInstance>();
        }

        if (installedAttachmentItems == null)
        {
            installedAttachmentItems = new List<ItemInstance>();
        }

        if (unresolvedCarryItems == null)
        {
            unresolvedCarryItems = new List<ItemInstanceSaveData>();
        }

        if (unresolvedKeyItems == null)
        {
            unresolvedKeyItems = new List<ItemInstanceSaveData>();
        }

        if (unresolvedStashItems == null)
        {
            unresolvedStashItems = new List<ItemInstanceSaveData>();
        }

        if (unresolvedInstalledAttachmentItems == null)
        {
            unresolvedInstalledAttachmentItems = new List<ItemInstanceSaveData>();
        }

        if (hasInitialized)
        {
            return;
        }

        hasInitialized = true;
        if (keyItems == null)
        {
            keyItems = new List<ItemInstance>();
        }

        startingPistolReserveRounds = Mathf.Max(0, startingPistolReserveRounds);
        startingRifleReserveRounds = Mathf.Max(0, startingRifleReserveRounds);

        if (!TryCreateConfiguredStarterInventory(out inventory, out InventoryOperationFailure failure))
        {
            inventory = null;
            Debug.LogError($"Could not create the starter item inventory. Failure: {failure}. Assign valid Pistol, Assault Rifle, and ammo item definitions to {nameof(PlayerItemInventory)}.", this);
            return;
        }

        inventory.Changed += HandleInventoryChanged;
        for (int index = 0; index < keyItems.Count; index++)
        {
            if (keyItems[index] != null)
            {
                keyItems[index].SetOwningPlayerContainer(this);
            }
        }

        for (int index = 0; index < stashItems.Count; index++)
        {
            if (stashItems[index] != null)
            {
                stashItems[index].SetOwningPlayerContainer(this);
            }
        }

        for (int index = 0; index < installedAttachmentItems.Count; index++)
        {
            if (installedAttachmentItems[index] != null)
            {
                installedAttachmentItems[index].SetOwningPlayerContainer(this);
            }
        }

        EnsureDefaultWeaponSlots();
    }

    private void HandleInventoryChanged(ItemInventory changedInventory)
    {
        if (!suppressInventoryChanged)
        {
            changed?.Invoke(this);
        }
    }

    private AmmoDefinition GetAmmoDefinition(WeaponDefinition.AmmunitionType ammunitionType)
    {
        if (pistolAmmoDefinition != null && pistolAmmoDefinition.AmmunitionType == ammunitionType)
        {
            return pistolAmmoDefinition;
        }

        if (rifleAmmoDefinition != null && rifleAmmoDefinition.AmmunitionType == ammunitionType)
        {
            return rifleAmmoDefinition;
        }

        return null;
    }

    private void OnValidate()
    {
        startingPistolReserveRounds = Mathf.Max(0, startingPistolReserveRounds);
        startingRifleReserveRounds = Mathf.Max(0, startingRifleReserveRounds);
    }
}

/// <summary>Authored Credits prices indexed by the level being purchased for one weapon upgrade type.</summary>
[Serializable]
public struct WeaponUpgradePurchaseOption
{
    [SerializeField] private string weaponStableItemId;
    [SerializeField] private WeaponUpgradeType upgradeType;
    [SerializeField] private int[] levelCosts;

    /// <summary>Gets the stable weapon definition ID this price table applies to.</summary>
    public string WeaponStableItemId => weaponStableItemId;

    /// <summary>Gets the supported upgrade type for this price table.</summary>
    public WeaponUpgradeType UpgradeType => upgradeType;

    /// <summary>Gets exact Credits costs indexed by purchased level minus one.</summary>
    public int[] LevelCosts => levelCosts;
}
