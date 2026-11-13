using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Serializable carry-grid model that owns item instances and validates placement transactions.</summary>
[Serializable]
public sealed class ItemInventory : ISerializationCallbackReceiver
{
    public const int CaseGridWidth = 8;
    public const int CaseStage1Height = 6;
    public const int CaseStage2Height = 7;
    public const int CaseStage3Height = 8;
    public const int CaseStage4Height = 10;
    public const int MaximumCaseHeight = CaseStage4Height;
    public const int DefaultGridWidth = CaseGridWidth;
    public const int DefaultGridHeight = CaseStage1Height;
    public const int DefaultPistolReserveRounds = 160;
    public const int DefaultRifleReserveRounds = 240;

    private const int MinimumGridDimension = 1;
    private const int FirstGridCoordinate = 0;
    private const int UnplacedGridCoordinate = -1;

    [Header("Configurable carry-grid dimensions")]
    [Tooltip("Current carry-grid width in cells. The standard case is eight columns wide.")]
    [Min(1)]
    [SerializeField] private int gridWidth = DefaultGridWidth;
    [Tooltip("Current carry-grid height in cells. Case stages contain six, seven, eight, or ten rows.")]
    [Min(1)]
    [SerializeField] private int gridHeight = DefaultGridHeight;

    [Header("Owned item instances")]
    [Tooltip("Runtime-owned items currently placed in this carry grid.")]
    [SerializeField] private List<ItemInstance> items = new List<ItemInstance>();

    [NonSerialized] private Action<ItemInventory> changed;
    [NonSerialized] private HashSet<string> subscribedInstanceIds;
    [NonSerialized] private bool suppressItemChangeNotifications;

    /// <summary>Creates the configured starting 8×6 carry inventory.</summary>
    public ItemInventory()
        : this(DefaultGridWidth, DefaultGridHeight)
    {
    }

    /// <summary>Creates a carry inventory with configurable positive dimensions.</summary>
    public ItemInventory(int initialGridWidth, int initialGridHeight)
    {
        gridWidth = Mathf.Max(MinimumGridDimension, initialGridWidth);
        gridHeight = Mathf.Max(MinimumGridDimension, initialGridHeight);
        items = new List<ItemInstance>();
        subscribedInstanceIds = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>Raised after a completed inventory operation or a contained item-state change.</summary>
    public event Action<ItemInventory> Changed
    {
        add
        {
            changed += value;
            EnsureItemSubscriptions();
        }
        remove => changed -= value;
    }

    /// <summary>Creates the agreed starter inventory locally without changing the current combat ammo owner.</summary>
    public static bool TryCreateStartingLoadout(
        WeaponDefinition pistolDefinition,
        WeaponDefinition assaultRifleDefinition,
        AmmoDefinition pistolAmmoDefinition,
        AmmoDefinition rifleAmmoDefinition,
        out ItemInventory starterInventory,
        out InventoryOperationFailure failure,
        int pistolReserveRounds = DefaultPistolReserveRounds,
        int rifleReserveRounds = DefaultRifleReserveRounds)
    {
        starterInventory = null;
        failure = InventoryOperationFailure.None;
        if (!IsValidDefinition(pistolDefinition) || !IsValidDefinition(assaultRifleDefinition) ||
            !IsValidDefinition(pistolAmmoDefinition) || !IsValidDefinition(rifleAmmoDefinition) ||
            pistolAmmoDefinition.AmmunitionType != pistolDefinition.AmmoType ||
            rifleAmmoDefinition.AmmunitionType != assaultRifleDefinition.AmmoType)
        {
            failure = InventoryOperationFailure.InvalidDefinition;
            return false;
        }

        if (pistolReserveRounds < 0 || rifleReserveRounds < 0)
        {
            failure = InventoryOperationFailure.InvalidQuantity;
            return false;
        }

        ItemInventory candidateInventory = new ItemInventory(DefaultGridWidth, DefaultGridHeight);
        if (!TryAddWeaponDefinition(candidateInventory, pistolDefinition, out failure) ||
            !TryAddWeaponDefinition(candidateInventory, assaultRifleDefinition, out failure))
        {
            return false;
        }

        if (pistolReserveRounds > 0)
        {
            InventoryOperationResult pistolAmmoResult = candidateInventory.TryAddQuantity(pistolAmmoDefinition, pistolReserveRounds);
            if (!pistolAmmoResult.Completed)
            {
                failure = pistolAmmoResult.Failure;
                return false;
            }
        }

        if (rifleReserveRounds > 0)
        {
            InventoryOperationResult rifleAmmoResult = candidateInventory.TryAddQuantity(rifleAmmoDefinition, rifleReserveRounds);
            if (!rifleAmmoResult.Completed)
            {
                failure = rifleAmmoResult.Failure;
                return false;
            }
        }

        starterInventory = candidateInventory;
        return true;
    }

    /// <summary>Gets the current grid width in cells.</summary>
    public int GridWidth => gridWidth;

    /// <summary>Gets the current grid height in cells.</summary>
    public int GridHeight => gridHeight;

    /// <summary>Checks whether the dimensions represent one of the four supported case stages.</summary>
    public static bool IsValidCaseDimensions(int width, int height)
    {
        return width == CaseGridWidth && height >= MinimumGridDimension &&
               (height == CaseStage1Height || height == CaseStage2Height || height == CaseStage3Height || height == CaseStage4Height);
    }

    internal static bool TryGetNextCaseDimensions(int currentWidth, int currentHeight, out int nextWidth, out int nextHeight)
    {
        nextWidth = CaseGridWidth;
        switch (currentHeight)
        {
            case CaseStage1Height:
                nextHeight = CaseStage2Height;
                break;
            case CaseStage2Height:
                nextHeight = CaseStage3Height;
                break;
            case CaseStage3Height:
                nextHeight = CaseStage4Height;
                break;
            default:
                nextHeight = currentHeight;
                return false;
        }

        return currentWidth == CaseGridWidth;
    }

    internal InventoryOperationResult TryAdvanceCaseStage()
    {
        EnsureItemSubscriptions();
        if (!TryGetNextCaseDimensions(gridWidth, gridHeight, out int nextWidth, out int nextHeight))
        {
            InventoryOperationFailure failure = IsValidCaseDimensions(gridWidth, gridHeight)
                ? InventoryOperationFailure.MaximumCapacity
                : InventoryOperationFailure.InvalidDimensions;
            return InventoryOperationResult.Rejected(failure, 1);
        }

        InventoryOperationResult resizeResult = TrySetGridDimensions(nextWidth, nextHeight);
        return resizeResult.Succeeded
            ? InventoryOperationResult.Accepted(1, 1)
            : InventoryOperationResult.Rejected(resizeResult.Failure, 1);
    }

    /// <summary>Gets the number of owned item instances in this carry inventory.</summary>
    public int Count => items == null ? 0 : items.Count;

    /// <summary>Gets a read-only view of the owned item instances.</summary>
    public IReadOnlyList<ItemInstance> Items
    {
        get
        {
            EnsureItemSubscriptions();
            return items.AsReadOnly();
        }
    }

    /// <summary>Finds an owned item instance by its unique instance identifier.</summary>
    public ItemInstance FindItem(string instanceId)
    {
        EnsureItemSubscriptions();
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        for (int index = 0; index < items.Count; index++)
        {
            if (string.Equals(items[index].InstanceId, instanceId, StringComparison.Ordinal))
            {
                return items[index];
            }
        }

        return null;
    }

    /// <summary>Returns the item occupying a grid cell, or null when the cell is empty or outside the grid.</summary>
    public ItemInstance GetItemAt(Vector2Int gridCell)
    {
        EnsureItemSubscriptions();
        if (!IsInsideGrid(gridCell.x, gridCell.y, gridWidth, gridHeight))
        {
            return null;
        }

        for (int index = 0; index < items.Count; index++)
        {
            ItemInstance item = items[index];
            GetFootprint(item, item.IsRotated, out int itemWidth, out int itemHeight);
            if (gridCell.x >= item.GridPosition.x && gridCell.x < item.GridPosition.x + itemWidth &&
                gridCell.y >= item.GridPosition.y && gridCell.y < item.GridPosition.y + itemHeight)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>Adds one existing owned item instance at an unrotated or quarter-turned grid position.</summary>
    public InventoryOperationResult TryAddItem(ItemInstance item, Vector2Int gridPosition, bool rotated)
    {
        return TryAddItem(item, gridPosition, rotated ? 1 : 0);
    }


    /// <summary>Adds an owned item using one of four clockwise quarter-turn orientations.</summary>
    public InventoryOperationResult TryAddItem(ItemInstance item, Vector2Int gridPosition, int rotationQuarterTurns)
    {
        EnsureItemSubscriptions();
        if (!IsValidItemInstance(item))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition);
        }

        if (IsKeyItem(item.Definition))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.KeyItemsUseSeparateCollection);
        }

        if (item.OwningContainer != null)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.DuplicateInstance);
        }

        if (FindItem(item.InstanceId) != null)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.DuplicateInstance);
        }

        int normalizedRotation = ((rotationQuarterTurns % 4) + 4) % 4;
        InventoryOperationFailure placementFailure = ValidatePlacement(
            item.Definition,
            gridPosition,
            normalizedRotation % 2 != 0,
            gridWidth,
            gridHeight,
            null,
            null);
        if (placementFailure != InventoryOperationFailure.None)
        {
            return InventoryOperationResult.Rejected(placementFailure);
        }

        item.SetGridPlacement(gridPosition, normalizedRotation);
        items.Add(item);
        SubscribeToItem(item);
        RaiseChanged();
        return InventoryOperationResult.Success();
    }

    /// <summary>Adds as much of a stackable quantity as the current grid can safely own.</summary>
    public InventoryOperationResult TryAddQuantity(ItemDefinition definition, int requestedQuantity, bool requireFullAcceptance = false)
    {
        EnsureItemSubscriptions();
        if (!IsValidDefinition(definition))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition, requestedQuantity);
        }

        if (requestedQuantity <= 0)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidQuantity, requestedQuantity);
        }

        if (IsKeyItem(definition))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.KeyItemsUseSeparateCollection, requestedQuantity);
        }

        int remainingQuantity = requestedQuantity;
        List<PlannedStackChange> stackChanges = new List<PlannedStackChange>();
        List<PlannedPlacement> newItemPlacements = new List<PlannedPlacement>();
        int maximumStackSize = definition.MaximumStackSize;

        if (maximumStackSize > 1)
        {
            for (int index = 0; index < items.Count && remainingQuantity > 0; index++)
            {
                ItemInstance existingItem = items[index];
                if (!existingItem.CanStackWith(definition))
                {
                    continue;
                }

                int availableStackSpace = existingItem.Definition.MaximumStackSize - existingItem.Quantity;
                int amountToAdd = Mathf.Min(Mathf.Max(0, availableStackSpace), remainingQuantity);
                if (amountToAdd <= 0)
                {
                    continue;
                }

                stackChanges.Add(new PlannedStackChange(existingItem, existingItem.Quantity + amountToAdd));
                remainingQuantity -= amountToAdd;
            }
        }

        while (remainingQuantity > 0)
        {
            int newStackQuantity = Mathf.Min(maximumStackSize, remainingQuantity);
            if (!TryFindPlacementForDefinition(
                    definition,
                    true,
                    null,
                    newItemPlacements,
                    out Vector2Int position,
                    out bool rotated))
            {
                break;
            }

            newItemPlacements.Add(new PlannedPlacement(definition, newStackQuantity, position, rotated));
            remainingQuantity -= newStackQuantity;
        }

        int acceptedQuantity = requestedQuantity - remainingQuantity;
        if (requireFullAcceptance && remainingQuantity > 0)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.NoSpace, requestedQuantity);
        }

        if (acceptedQuantity == 0)
        {
            InventoryOperationFailure failure = stackChanges.Count == 0 && newItemPlacements.Count == 0
                ? InventoryOperationFailure.NoSpace
                : InventoryOperationFailure.StackLimitReached;
            return InventoryOperationResult.Rejected(failure, requestedQuantity);
        }

        List<ItemInstance> newItemInstances = new List<ItemInstance>(newItemPlacements.Count);
        for (int index = 0; index < newItemPlacements.Count; index++)
        {
            PlannedPlacement placement = newItemPlacements[index];
            if (!ItemInstance.TryCreate(placement.Definition, placement.Quantity, out ItemInstance newItemInstance))
            {
                return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition, requestedQuantity);
            }

            newItemInstance.SetGridPlacement(placement.Position, placement.Rotated);
            newItemInstances.Add(newItemInstance);
        }

        suppressItemChangeNotifications = true;
        for (int index = 0; index < stackChanges.Count; index++)
        {
            stackChanges[index].Item.SetQuantity(stackChanges[index].NewQuantity);
        }

        for (int index = 0; index < newItemInstances.Count; index++)
        {
            ItemInstance newItemInstance = newItemInstances[index];
            items.Add(newItemInstance);
            SubscribeToItem(newItemInstance);
        }

        suppressItemChangeNotifications = false;
        RaiseChanged();
        return InventoryOperationResult.Accepted(requestedQuantity, acceptedQuantity);
    }
    /// <summary>Returns the total reserve rounds held in ammo item stacks for a weapon ammunition type.</summary>
    public int GetAmmoQuantity(WeaponDefinition.AmmunitionType ammunitionType)
    {
        EnsureItemSubscriptions();
        if (!IsValidAmmunitionType(ammunitionType))
        {
            return 0;
        }

        long totalRounds = 0;
        for (int index = 0; index < items.Count; index++)
        {
            if (items[index].Definition is AmmoDefinition ammoDefinition && ammoDefinition.AmmunitionType == ammunitionType)
            {
                totalRounds += items[index].Quantity;
            }
        }

        return (int)Math.Min(int.MaxValue, totalRounds);
    }

    /// <summary>Consumes reserve rounds only when the full requested amount exists across compatible ammo stacks.</summary>
    public bool TryConsumeAmmo(WeaponDefinition.AmmunitionType ammunitionType, int roundsToConsume)
    {
        EnsureItemSubscriptions();
        if (!IsValidAmmunitionType(ammunitionType) || roundsToConsume <= 0 || GetAmmoQuantity(ammunitionType) < roundsToConsume)
        {
            return false;
        }

        int remainingToConsume = roundsToConsume;
        List<PlannedStackChange> stackChanges = new List<PlannedStackChange>();
        for (int index = items.Count - 1; index >= 0 && remainingToConsume > 0; index--)
        {
            ItemInstance item = items[index];
            if (!(item.Definition is AmmoDefinition ammoDefinition) || ammoDefinition.AmmunitionType != ammunitionType)
            {
                continue;
            }

            int amountToConsume = Mathf.Min(item.Quantity, remainingToConsume);
            stackChanges.Add(new PlannedStackChange(item, item.Quantity - amountToConsume));
            remainingToConsume -= amountToConsume;
        }

        if (remainingToConsume != 0)
        {
            return false;
        }

        suppressItemChangeNotifications = true;
        ApplyAmmoStackChanges(stackChanges);
        suppressItemChangeNotifications = false;
        RaiseChanged();
        return true;
    }

    /// <summary>Returns an old magazine and withdraws up to the requested capacity as one stack-safe transaction.</summary>
    public bool TryExchangeAmmo(
        WeaponDefinition.AmmunitionType ammunitionType,
        int roundsReturned,
        int maximumRoundsToWithdraw,
        out int roundsWithdrawn)
    {
        EnsureItemSubscriptions();
        roundsWithdrawn = 0;
        if (!IsValidAmmunitionType(ammunitionType) || roundsReturned < 0 || maximumRoundsToWithdraw <= 0 ||
            roundsReturned > maximumRoundsToWithdraw)
        {
            return false;
        }

        int currentRounds = GetAmmoQuantity(ammunitionType);
        long availableRounds = (long)currentRounds + roundsReturned;
        roundsWithdrawn = (int)Math.Min(maximumRoundsToWithdraw, availableRounds);
        int targetReserveRounds = (int)(availableRounds - roundsWithdrawn);
        int roundsToRemove = currentRounds - targetReserveRounds;
        if (roundsToRemove < 0)
        {
            roundsWithdrawn = 0;
            return false;
        }

        if (roundsToRemove == 0)
        {
            return true;
        }

        List<PlannedStackChange> stackChanges = new List<PlannedStackChange>();
        int remainingToRemove = roundsToRemove;
        for (int index = items.Count - 1; index >= 0 && remainingToRemove > 0; index--)
        {
            ItemInstance item = items[index];
            if (!(item.Definition is AmmoDefinition ammoDefinition) || ammoDefinition.AmmunitionType != ammunitionType)
            {
                continue;
            }

            int amountToRemove = Mathf.Min(item.Quantity, remainingToRemove);
            stackChanges.Add(new PlannedStackChange(item, item.Quantity - amountToRemove));
            remainingToRemove -= amountToRemove;
        }

        if (remainingToRemove != 0)
        {
            roundsWithdrawn = 0;
            return false;
        }

        suppressItemChangeNotifications = true;
        ApplyAmmoStackChanges(stackChanges);
        suppressItemChangeNotifications = false;
        RaiseChanged();
        return true;
    }



    /// <summary>Consumes recipe ingredients and adds every output as one all-or-nothing carry-grid transaction.</summary>
    public InventoryOperationResult TryCraftRecipe(ItemRecipeDefinition recipe)
    {
        EnsureItemSubscriptions();
        if (recipe == null || recipe.Ingredients == null || recipe.Outputs == null ||
            recipe.Ingredients.Count == 0 || recipe.Outputs.Count == 0)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition);
        }

        Dictionary<ItemInstance, int> plannedQuantities = new Dictionary<ItemInstance, int>();
        for (int ingredientIndex = 0; ingredientIndex < recipe.Ingredients.Count; ingredientIndex++)
        {
            ItemRecipeEntry ingredient = recipe.Ingredients[ingredientIndex];
            if (!IsValidDefinition(ingredient.Definition) || IsKeyItem(ingredient.Definition) || ingredient.Quantity <= 0)
            {
                return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition);
            }

            int remainingQuantity = ingredient.Quantity;
            for (int itemIndex = 0; itemIndex < items.Count && remainingQuantity > 0; itemIndex++)
            {
                ItemInstance item = items[itemIndex];
                if (!AreSameRecipeDefinition(item.Definition, ingredient.Definition) || item.IsEquipped ||
                    (item.Definition is WeaponDefinition && item.InstalledAttachmentInstanceIds.Count > 0))
                {
                    continue;
                }

                int availableQuantity = plannedQuantities.TryGetValue(item, out int plannedQuantity)
                    ? plannedQuantity
                    : item.Quantity;
                int quantityToConsume = Mathf.Min(availableQuantity, remainingQuantity);
                if (quantityToConsume <= 0)
                {
                    continue;
                }

                plannedQuantities[item] = availableQuantity - quantityToConsume;
                remainingQuantity -= quantityToConsume;
            }

            if (remainingQuantity > 0)
            {
                return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidQuantity, ingredient.Quantity);
            }
        }

        List<PlannedPlacement> outputPlacements = new List<PlannedPlacement>();
        for (int outputIndex = 0; outputIndex < recipe.Outputs.Count; outputIndex++)
        {
            ItemRecipeEntry output = recipe.Outputs[outputIndex];
            if (!IsValidDefinition(output.Definition) || IsKeyItem(output.Definition) || output.Quantity <= 0)
            {
                return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition);
            }

            int remainingQuantity = output.Quantity;
            if (output.Definition.MaximumStackSize > 1)
            {
                for (int itemIndex = 0; itemIndex < items.Count && remainingQuantity > 0; itemIndex++)
                {
                    ItemInstance item = items[itemIndex];
                    int currentQuantity = plannedQuantities.TryGetValue(item, out int plannedQuantity)
                        ? plannedQuantity
                        : item.Quantity;
                    if (currentQuantity <= 0 || !item.CanStackWith(output.Definition))
                    {
                        continue;
                    }

                    int availableStackSpace = item.Definition.MaximumStackSize - currentQuantity;
                    int quantityToAdd = Mathf.Min(Mathf.Max(0, availableStackSpace), remainingQuantity);
                    if (quantityToAdd <= 0)
                    {
                        continue;
                    }

                    plannedQuantities[item] = currentQuantity + quantityToAdd;
                    remainingQuantity -= quantityToAdd;
                }

                for (int placementIndex = 0; placementIndex < outputPlacements.Count && remainingQuantity > 0; placementIndex++)
                {
                    PlannedPlacement placement = outputPlacements[placementIndex];
                    if (!CanStackDefinitions(placement.Definition, output.Definition))
                    {
                        continue;
                    }

                    int availableStackSpace = output.Definition.MaximumStackSize - placement.Quantity;
                    int quantityToAdd = Mathf.Min(Mathf.Max(0, availableStackSpace), remainingQuantity);
                    if (quantityToAdd <= 0)
                    {
                        continue;
                    }

                    outputPlacements[placementIndex] = new PlannedPlacement(
                        placement.Definition,
                        placement.Quantity + quantityToAdd,
                        placement.Position,
                        placement.Rotated);
                    remainingQuantity -= quantityToAdd;
                }
            }

            while (remainingQuantity > 0)
            {
                int stackQuantity = Mathf.Min(output.Definition.MaximumStackSize, remainingQuantity);
                if (!TryFindCraftPlacement(output.Definition, plannedQuantities, outputPlacements, out Vector2Int position, out bool rotated))
                {
                    return InventoryOperationResult.Rejected(InventoryOperationFailure.NoSpace, output.Quantity);
                }

                outputPlacements.Add(new PlannedPlacement(output.Definition, stackQuantity, position, rotated));
                remainingQuantity -= stackQuantity;
            }
        }

        List<ItemInstance> outputInstances = new List<ItemInstance>(outputPlacements.Count);
        for (int index = 0; index < outputPlacements.Count; index++)
        {
            PlannedPlacement placement = outputPlacements[index];
            if (!ItemInstance.TryCreate(placement.Definition, placement.Quantity, out ItemInstance itemInstance))
            {
                return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition);
            }

            itemInstance.SetGridPlacement(placement.Position, placement.Rotated);
            outputInstances.Add(itemInstance);
        }

        suppressItemChangeNotifications = true;
        for (int index = items.Count - 1; index >= 0; index--)
        {
            ItemInstance item = items[index];
            if (!plannedQuantities.TryGetValue(item, out int newQuantity) || newQuantity == item.Quantity)
            {
                continue;
            }

            if (newQuantity <= 0)
            {
                item.SetQuantity(0);
                item.SetGridPlacement(new Vector2Int(UnplacedGridCoordinate, UnplacedGridCoordinate), false);
                RemoveItemAt(index, item);
            }
            else
            {
                item.SetQuantity(newQuantity);
            }
        }

        for (int index = 0; index < outputInstances.Count; index++)
        {
            items.Add(outputInstances[index]);
            SubscribeToItem(outputInstances[index]);
        }

        suppressItemChangeNotifications = false;
        RaiseChanged();
        return InventoryOperationResult.Success();
    }

    /// <summary>Validates a proposed item move without mutation, using a clockwise quarter-turn orientation.</summary>
    public InventoryOperationFailure GetMoveItemFailure(string instanceId, Vector2Int newGridPosition, int rotationQuarterTurns)
    {
        EnsureItemSubscriptions();
        ItemInstance item = FindItem(instanceId);
        if (item == null)
        {
            return InventoryOperationFailure.ItemNotFound;
        }

        int normalizedRotation = ((rotationQuarterTurns % 4) + 4) % 4;
        return ValidatePlacement(
            item.Definition,
            newGridPosition,
            normalizedRotation % 2 != 0,
            gridWidth,
            gridHeight,
            item.InstanceId,
            null);
    }

    /// <summary>Validates a move and optionally allows picking up exactly one blocking item.</summary>
    public InventoryOperationFailure GetMoveItemFailureWithSingleBlockerPickup(
        string instanceId,
        Vector2Int newGridPosition,
        int rotationQuarterTurns,
        out string blockingItemInstanceId)
    {
        EnsureItemSubscriptions();
        blockingItemInstanceId = null;
        ItemInstance item = FindItem(instanceId);
        if (item == null)
        {
            return InventoryOperationFailure.ItemNotFound;
        }

        int normalizedRotation = ((rotationQuarterTurns % 4) + 4) % 4;
        InventoryOperationFailure failure = ValidatePlacement(
            item.Definition,
            newGridPosition,
            normalizedRotation % 2 != 0,
            gridWidth,
            gridHeight,
            item.InstanceId,
            null);
        if (failure != InventoryOperationFailure.Overlap)
        {
            return failure;
        }

        int overlapCount = CountOverlappingItems(item, newGridPosition, normalizedRotation, out ItemInstance blockingItem);
        if (overlapCount != 1 || blockingItem == null)
        {
            return InventoryOperationFailure.Overlap;
        }

        failure = ValidatePlacement(
            item.Definition,
            newGridPosition,
            normalizedRotation % 2 != 0,
            gridWidth,
            gridHeight,
            item.InstanceId,
            null,
            blockingItem.InstanceId);
        if (failure != InventoryOperationFailure.None)
        {
            return failure;
        }

        blockingItemInstanceId = blockingItem.InstanceId;
        return InventoryOperationFailure.None;
    }

    /// <summary>Moves an item into a free cell or picks up its single blocking item after placing it.</summary>
    public InventoryOperationResult TryMoveItemAndPickUpSingleBlocker(
        string instanceId,
        Vector2Int newGridPosition,
        int rotationQuarterTurns,
        out string pickedUpItemInstanceId)
    {
        pickedUpItemInstanceId = null;
        InventoryOperationFailure failure = GetMoveItemFailureWithSingleBlockerPickup(
            instanceId,
            newGridPosition,
            rotationQuarterTurns,
            out string blockingItemInstanceId);
        if (failure != InventoryOperationFailure.None)
        {
            return InventoryOperationResult.Rejected(failure);
        }

        if (string.IsNullOrWhiteSpace(blockingItemInstanceId))
        {
            return TryMoveItem(instanceId, newGridPosition, rotationQuarterTurns);
        }

        ItemInstance movingItem = FindItem(instanceId);
        ItemInstance blockingItem = FindItem(blockingItemInstanceId);
        if (movingItem == null || blockingItem == null)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ItemNotFound);
        }

        int normalizedRotation = ((rotationQuarterTurns % 4) + 4) % 4;
        suppressItemChangeNotifications = true;
        movingItem.SetGridPlacement(newGridPosition, normalizedRotation);
        blockingItem.SetGridPlacement(
            new Vector2Int(UnplacedGridCoordinate, UnplacedGridCoordinate),
            blockingItem.RotationQuarterTurns);
        suppressItemChangeNotifications = false;
        RaiseChanged();
        pickedUpItemInstanceId = blockingItemInstanceId;
        return InventoryOperationResult.Success();
    }

    /// <summary>Restores both items after cancelling a single-blocker pickup exchange.</summary>
    public InventoryOperationResult TryRollbackPickupExchange(
        string placedItemInstanceId,
        Vector2Int placedItemOriginalPosition,
        int placedItemOriginalRotationQuarterTurns,
        string pickedUpItemInstanceId,
        Vector2Int pickedUpItemOriginalPosition,
        int pickedUpItemOriginalRotationQuarterTurns)
    {
        EnsureItemSubscriptions();
        ItemInstance placedItem = FindItem(placedItemInstanceId);
        ItemInstance pickedUpItem = FindItem(pickedUpItemInstanceId);
        if (placedItem == null || pickedUpItem == null)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ItemNotFound);
        }

        int placedRotation = ((placedItemOriginalRotationQuarterTurns % 4) + 4) % 4;
        int pickedUpRotation = ((pickedUpItemOriginalRotationQuarterTurns % 4) + 4) % 4;
        InventoryOperationFailure failure = ValidatePlacement(
            placedItem.Definition,
            placedItemOriginalPosition,
            placedRotation % 2 != 0,
            gridWidth,
            gridHeight,
            placedItem.InstanceId,
            null,
            pickedUpItem.InstanceId);
        if (failure != InventoryOperationFailure.None)
        {
            return InventoryOperationResult.Rejected(failure);
        }

        failure = ValidatePlacement(
            pickedUpItem.Definition,
            pickedUpItemOriginalPosition,
            pickedUpRotation % 2 != 0,
            gridWidth,
            gridHeight,
            pickedUpItem.InstanceId,
            null,
            placedItem.InstanceId);
        if (failure != InventoryOperationFailure.None)
        {
            return InventoryOperationResult.Rejected(failure);
        }

        GetFootprint(placedItem.Definition, placedRotation % 2 != 0, out int placedWidth, out int placedHeight);
        GetFootprint(pickedUpItem.Definition, pickedUpRotation % 2 != 0, out int pickedUpWidth, out int pickedUpHeight);
        if (FootprintsOverlap(
                placedItemOriginalPosition,
                placedWidth,
                placedHeight,
                pickedUpItemOriginalPosition,
                pickedUpWidth,
                pickedUpHeight))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.Overlap);
        }

        suppressItemChangeNotifications = true;
        placedItem.SetGridPlacement(placedItemOriginalPosition, placedRotation);
        pickedUpItem.SetGridPlacement(pickedUpItemOriginalPosition, pickedUpRotation);
        suppressItemChangeNotifications = false;
        RaiseChanged();
        return InventoryOperationResult.Success();
    }

    /// <summary>Validates a proposed move using the legacy unrotated or quarter-turned state.</summary>
    public InventoryOperationFailure GetMoveItemFailure(string instanceId, Vector2Int newGridPosition, bool rotated)
    {
        return GetMoveItemFailure(instanceId, newGridPosition, rotated ? 1 : 0);
    }

    /// <summary>Moves or rotates an owned item to a valid grid position and orientation.</summary>
    public InventoryOperationResult TryMoveItem(string instanceId, Vector2Int newGridPosition, int rotationQuarterTurns)
    {
        EnsureItemSubscriptions();
        ItemInstance item = FindItem(instanceId);
        if (item == null)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ItemNotFound);
        }

        int normalizedRotation = ((rotationQuarterTurns % 4) + 4) % 4;
        InventoryOperationFailure placementFailure = ValidatePlacement(
            item.Definition,
            newGridPosition,
            normalizedRotation % 2 != 0,
            gridWidth,
            gridHeight,
            item.InstanceId,
            null);
        if (placementFailure != InventoryOperationFailure.None)
        {
            return InventoryOperationResult.Rejected(placementFailure);
        }

        suppressItemChangeNotifications = true;
        item.SetGridPlacement(newGridPosition, normalizedRotation);
        suppressItemChangeNotifications = false;
        RaiseChanged();
        return InventoryOperationResult.Success();
    }

    /// <summary>Moves or rotates an owned item using the legacy unrotated or quarter-turned state.</summary>
    public InventoryOperationResult TryMoveItem(string instanceId, Vector2Int newGridPosition, bool rotated)
    {
        return TryMoveItem(instanceId, newGridPosition, rotated ? 1 : 0);
    }

    /// <summary>Splits a stack into a new independently identified instance at a valid grid position.</summary>
    public InventoryOperationResult TrySplitStack(
        string instanceId,
        int quantityToSplit,
        Vector2Int newGridPosition,
        bool rotated,
        out ItemInstance splitItem)
    {
        EnsureItemSubscriptions();
        splitItem = null;
        ItemInstance sourceItem = FindItem(instanceId);
        if (sourceItem == null)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ItemNotFound);
        }

        if (sourceItem.Definition.MaximumStackSize <= 1 || quantityToSplit <= 0 || quantityToSplit >= sourceItem.Quantity)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.NotStackable, quantityToSplit);
        }

        InventoryOperationFailure placementFailure = ValidatePlacement(
            sourceItem.Definition,
            newGridPosition,
            rotated,
            gridWidth,
            gridHeight,
            null,
            null);
        if (placementFailure != InventoryOperationFailure.None)
        {
            return InventoryOperationResult.Rejected(placementFailure, quantityToSplit);
        }

        if (!ItemInstance.TryCreate(sourceItem.Definition, quantityToSplit, out ItemInstance newItemInstance))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition, quantityToSplit);
        }

        suppressItemChangeNotifications = true;
        sourceItem.SetQuantity(sourceItem.Quantity - quantityToSplit);
        newItemInstance.SetGridPlacement(newGridPosition, rotated);
        items.Add(newItemInstance);
        SubscribeToItem(newItemInstance);
        suppressItemChangeNotifications = false;

        splitItem = newItemInstance;
        RaiseChanged();
        return InventoryOperationResult.Accepted(quantityToSplit, quantityToSplit);
    }

    /// <summary>Merges compatible stacks up to the target definition's maximum and preserves any source remainder.</summary>
    public InventoryOperationResult TryMergeStacks(string targetInstanceId, string sourceInstanceId)
    {
        EnsureItemSubscriptions();
        if (string.Equals(targetInstanceId, sourceInstanceId, StringComparison.Ordinal))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.SameInstance);
        }

        ItemInstance targetItem = FindItem(targetInstanceId);
        ItemInstance sourceItem = FindItem(sourceInstanceId);
        if (targetItem == null || sourceItem == null)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ItemNotFound);
        }

        if (targetItem.Definition.MaximumStackSize <= 1 || !targetItem.CanStackWith(sourceItem.Definition))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.IncompatibleStack, sourceItem.Quantity);
        }

        int sourceQuantity = sourceItem.Quantity;
        int availableStackSpace = targetItem.Definition.MaximumStackSize - targetItem.Quantity;
        int quantityToMerge = Mathf.Min(Mathf.Max(0, availableStackSpace), sourceQuantity);
        if (quantityToMerge <= 0)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.StackLimitReached, sourceItem.Quantity);
        }

        suppressItemChangeNotifications = true;
        targetItem.SetQuantity(targetItem.Quantity + quantityToMerge);
        int sourceRemainder = sourceQuantity - quantityToMerge;
        if (sourceRemainder == 0)
        {
            sourceItem.SetQuantity(0);
            sourceItem.SetGridPlacement(new Vector2Int(UnplacedGridCoordinate, UnplacedGridCoordinate), false);
            RemoveItemAt(items.IndexOf(sourceItem), sourceItem);
        }
        else
        {
            sourceItem.SetQuantity(sourceRemainder);
        }

        suppressItemChangeNotifications = false;
        RaiseChanged();
        return InventoryOperationResult.Accepted(sourceQuantity, quantityToMerge);
    }

    /// <summary>Changes the configured grid dimensions only when all existing placements remain valid.</summary>
    public InventoryOperationResult TrySetGridDimensions(int newGridWidth, int newGridHeight)
    {
        EnsureItemSubscriptions();
        if (!IsValidCaseDimensions(newGridWidth, newGridHeight))
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDimensions);
        }

        if (gridWidth != newGridWidth || gridHeight != newGridHeight)
        {
            if (!TryGetNextCaseDimensions(gridWidth, gridHeight, out int expectedWidth, out int expectedHeight) ||
                expectedWidth != newGridWidth || expectedHeight != newGridHeight)
            {
                return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDimensions);
            }
        }

        InventoryOperationFailure placementFailure = ValidateAllPlacementsForDimensions(newGridWidth, newGridHeight);
        if (placementFailure != InventoryOperationFailure.None)
        {
            return InventoryOperationResult.Rejected(placementFailure);
        }

        if (gridWidth == newGridWidth && gridHeight == newGridHeight)
        {
            return InventoryOperationResult.Success();
        }

        gridWidth = newGridWidth;
        gridHeight = newGridHeight;
        RaiseChanged();
        return InventoryOperationResult.Success();
    }

    private InventoryOperationFailure ValidateAllPlacementsForDimensions(int targetWidth, int targetHeight)
    {
        for (int index = 0; index < items.Count; index++)
        {
            ItemInstance item = items[index];
            InventoryOperationFailure placementFailure = ValidatePlacement(
                item.Definition,
                item.GridPosition,
                item.IsRotated,
                targetWidth,
                targetHeight,
                item.InstanceId,
                null);
            if (placementFailure != InventoryOperationFailure.None)
            {
                return InventoryOperationFailure.ResizeWouldOrphanItems;
            }
        }

        return InventoryOperationFailure.None;
    }

    /// <summary>Applies one owned capacity-upgrade item as the next case stage and consumes exactly one unit.</summary>
    public InventoryOperationResult TryApplyCapacityUpgrade(string instanceId)
    {
        EnsureItemSubscriptions();
        ItemInstance upgradeItem = FindItem(instanceId);
        if (upgradeItem == null)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ItemNotFound);
        }

        if (!(upgradeItem.Definition is InventoryCapacityUpgradeDefinition) || upgradeItem.Quantity < 1)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidUpgrade);
        }

        if (!TryGetNextCaseDimensions(gridWidth, gridHeight, out int newWidth, out int newHeight))
        {
            InventoryOperationFailure stageFailure = IsValidCaseDimensions(gridWidth, gridHeight)
                ? InventoryOperationFailure.MaximumCapacity
                : InventoryOperationFailure.InvalidDimensions;
            return InventoryOperationResult.Rejected(stageFailure, 1);
        }

        InventoryOperationFailure placementFailure = ValidateAllPlacementsForDimensions(newWidth, newHeight);
        if (placementFailure != InventoryOperationFailure.None)
        {
            return InventoryOperationResult.Rejected(placementFailure, 1);
        }

        suppressItemChangeNotifications = true;
        gridWidth = newWidth;
        gridHeight = newHeight;
        if (upgradeItem.Quantity == 1)
        {
            int itemIndex = items.IndexOf(upgradeItem);
            upgradeItem.SetQuantity(0);
            upgradeItem.SetGridPlacement(new Vector2Int(UnplacedGridCoordinate, UnplacedGridCoordinate), false);
            RemoveItemAt(itemIndex, upgradeItem);
        }
        else
        {
            upgradeItem.SetQuantity(upgradeItem.Quantity - 1);
        }

        suppressItemChangeNotifications = false;
        RaiseChanged();
        return InventoryOperationResult.Success();
    }


    /// <summary>Plans and atomically applies a deterministic grid sort, keeping equipped items anchored in place.</summary>
    public InventoryOperationResult TryAutoSort()
    {
        EnsureItemSubscriptions();
        if (items.Count <= 1)
        {
            return InventoryOperationResult.Success();
        }

        List<PlannedPlacement> plannedPlacements = new List<PlannedPlacement>(items.Count);
        List<ItemInstance> movableItems = new List<ItemInstance>();
        for (int index = 0; index < items.Count; index++)
        {
            ItemInstance item = items[index];
            if (item.IsEquipped)
            {
                if (!CanUseSortPlacement(item.Definition, item.GridPosition, item.IsRotated, plannedPlacements))
                {
                    return InventoryOperationResult.Rejected(InventoryOperationFailure.ResizeWouldOrphanItems);
                }

                plannedPlacements.Add(new PlannedPlacement(item.Definition, item.Quantity, item.GridPosition, item.RotationQuarterTurns, item.InstanceId));
            }
            else
            {
                movableItems.Add(item);
            }
        }

        movableItems.Sort(CompareSortPriority);
        for (int index = 0; index < movableItems.Count; index++)
        {
            ItemInstance item = movableItems[index];
            if (!TryFindSortPlacement(item.Definition, plannedPlacements, out Vector2Int position, out bool rotated))
            {
                return InventoryOperationResult.Rejected(InventoryOperationFailure.NoSpace);
            }

            plannedPlacements.Add(new PlannedPlacement(item.Definition, item.Quantity, position, rotated, item.InstanceId));
        }

        if (plannedPlacements.Count != items.Count)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.NoSpace);
        }

        for (int index = 0; index < items.Count; index++)
        {
            if (!TryGetPlannedPlacement(items[index].InstanceId, plannedPlacements, out _))
            {
                return InventoryOperationResult.Rejected(InventoryOperationFailure.NoSpace);
            }
        }

        suppressItemChangeNotifications = true;
        for (int index = 0; index < items.Count; index++)
        {
            ItemInstance item = items[index];
            TryGetPlannedPlacement(item.InstanceId, plannedPlacements, out PlannedPlacement placement);
            item.SetGridPlacement(placement.Position, placement.RotationQuarterTurns);
        }

        suppressItemChangeNotifications = false;
        RaiseChanged();
        return InventoryOperationResult.Success();
    }

    private static int CompareSortPriority(ItemInstance left, ItemInstance right)
    {
        long leftArea = (long)left.Definition.InventoryWidth * left.Definition.InventoryHeight;
        long rightArea = (long)right.Definition.InventoryWidth * right.Definition.InventoryHeight;
        int areaComparison = rightArea.CompareTo(leftArea);
        if (areaComparison != 0)
        {
            return areaComparison;
        }

        int longestSideComparison = Mathf.Max(right.Definition.InventoryWidth, right.Definition.InventoryHeight)
            .CompareTo(Mathf.Max(left.Definition.InventoryWidth, left.Definition.InventoryHeight));
        if (longestSideComparison != 0)
        {
            return longestSideComparison;
        }

        int stableIdComparison = string.Compare(left.Definition.StableItemId, right.Definition.StableItemId, StringComparison.Ordinal);
        return stableIdComparison != 0
            ? stableIdComparison
            : string.Compare(left.InstanceId, right.InstanceId, StringComparison.Ordinal);
    }

    private bool TryFindSortPlacement(
        ItemDefinition definition,
        List<PlannedPlacement> plannedPlacements,
        out Vector2Int position,
        out bool rotated)
    {
        position = Vector2Int.zero;
        rotated = false;
        bool canRotate = definition != null && definition.CanRotate;
        for (int y = FirstGridCoordinate; y < gridHeight; y++)
        {
            for (int x = FirstGridCoordinate; x < gridWidth; x++)
            {
                Vector2Int candidatePosition = new Vector2Int(x, y);
                if (CanUseSortPlacement(definition, candidatePosition, false, plannedPlacements))
                {
                    position = candidatePosition;
                    rotated = false;
                    return true;
                }

                if (canRotate && CanUseSortPlacement(definition, candidatePosition, true, plannedPlacements))
                {
                    position = candidatePosition;
                    rotated = true;
                    return true;
                }
            }
        }

        return false;
    }

    private bool CanUseSortPlacement(
        ItemDefinition definition,
        Vector2Int position,
        bool rotated,
        List<PlannedPlacement> plannedPlacements)
    {
        if (!IsValidDefinition(definition))
        {
            return false;
        }

        GetFootprint(definition, rotated, out int width, out int height);
        if (position.x < FirstGridCoordinate || position.y < FirstGridCoordinate ||
            position.x + width > gridWidth || position.y + height > gridHeight)
        {
            return false;
        }

        for (int index = 0; index < plannedPlacements.Count; index++)
        {
            PlannedPlacement planned = plannedPlacements[index];
            GetFootprint(planned.Definition, planned.Rotated, out int plannedWidth, out int plannedHeight);
            if (FootprintsOverlap(position, width, height, planned.Position, plannedWidth, plannedHeight))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetPlannedPlacement(
        string instanceId,
        List<PlannedPlacement> plannedPlacements,
        out PlannedPlacement placement)
    {
        for (int index = 0; index < plannedPlacements.Count; index++)
        {
            if (string.Equals(plannedPlacements[index].InstanceId, instanceId, StringComparison.Ordinal))
            {
                placement = plannedPlacements[index];
                return true;
            }
        }

        placement = default;
        return false;
    }


    /// <summary>Marks an owned weapon or equipment item equipped without moving its carry-grid footprint.</summary>
    public InventoryOperationResult TrySetEquipped(string instanceId, bool equipped)
    {
        EnsureItemSubscriptions();
        ItemInstance item = FindItem(instanceId);
        if (item == null)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ItemNotFound);
        }

        if (equipped && item.Definition.Category != ItemCategory.Weapon && item.Definition.Category != ItemCategory.Equipment)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition);
        }

        suppressItemChangeNotifications = true;
        item.SetEquipped(equipped);
        suppressItemChangeNotifications = false;
        RaiseChanged();
        return InventoryOperationResult.Success();
    }

    /// <summary>Transfers an item out of this container without applying discard restrictions.</summary>
    public InventoryOperationResult TryTakeItem(string instanceId, out ItemInstance removedItem)
    {
        EnsureItemSubscriptions();
        removedItem = null;
        ItemInstance item = FindItem(instanceId);
        if (item == null)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ItemNotFound);
        }

        int itemIndex = items.IndexOf(item);
        RemoveItemAt(itemIndex, item);
        item.SetGridPlacement(new Vector2Int(UnplacedGridCoordinate, UnplacedGridCoordinate), false);
        item.SetEquipped(false);
        removedItem = item;
        RaiseChanged();
        return InventoryOperationResult.Success();
    }

    /// <summary>Discards an item only when its definition permits player discard.</summary>
    public InventoryOperationResult TryDiscardItem(string instanceId, out ItemInstance discardedItem)
    {
        discardedItem = null;
        ItemInstance item = FindItem(instanceId);
        if (item == null)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ItemNotFound);
        }

        if (item.Definition.Category == ItemCategory.Weapon || !item.Definition.CanBeDiscarded)
        {
            return InventoryOperationResult.Rejected(InventoryOperationFailure.ProtectedItem);
        }

        return TryTakeItem(instanceId, out discardedItem);
    }

    /// <summary>Checks whether an item footprint can be placed at a requested position.</summary>
    public InventoryOperationFailure CheckPlacement(
        ItemDefinition definition,
        Vector2Int gridPosition,
        bool rotated,
        string ignoredInstanceId = null)
    {
        EnsureItemSubscriptions();
        return ValidatePlacement(definition, gridPosition, rotated, gridWidth, gridHeight, ignoredInstanceId, null);
    }

    /// <summary>Finds the first valid free position, preferring unrotated placement and row-major grid order.</summary>
    public bool TryFindFirstFit(ItemDefinition definition, out Vector2Int gridPosition, out bool rotated)
    {
        EnsureItemSubscriptions();
        return TryFindPlacementForDefinition(definition, true, null, null, out gridPosition, out rotated);
    }

    /// <summary>Restores nonserialized events and ensures collections remain usable after Unity deserialization.</summary>
    public void OnAfterDeserialize()
    {
        if (items == null)
        {
            items = new List<ItemInstance>();
        }

        gridWidth = Mathf.Max(MinimumGridDimension, gridWidth);
        gridHeight = Mathf.Max(MinimumGridDimension, gridHeight);
        changed = null;
        subscribedInstanceIds = new HashSet<string>(StringComparer.Ordinal);
        suppressItemChangeNotifications = false;
    }

    /// <summary>Unity serialization callback; no state is transformed before serialization.</summary>
    public void OnBeforeSerialize()
    {
    }

    private void ApplyAmmoStackChanges(List<PlannedStackChange> stackChanges)
    {
        for (int changeIndex = 0; changeIndex < stackChanges.Count; changeIndex++)
        {
            PlannedStackChange stackChange = stackChanges[changeIndex];
            ItemInstance item = stackChange.Item;
            int itemIndex = items.IndexOf(item);
            if (itemIndex < 0)
            {
                continue;
            }

            if (stackChange.NewQuantity <= 0)
            {
                item.SetQuantity(0);
                item.SetGridPlacement(new Vector2Int(UnplacedGridCoordinate, UnplacedGridCoordinate), false);
                RemoveItemAt(itemIndex, item);
            }
            else
            {
                item.SetQuantity(stackChange.NewQuantity);
            }
        }
    }

    private static bool IsValidAmmunitionType(WeaponDefinition.AmmunitionType ammunitionType)
    {
        return Enum.IsDefined(typeof(WeaponDefinition.AmmunitionType), ammunitionType) &&
               ammunitionType != WeaponDefinition.AmmunitionType.None;
    }

    private static bool TryAddWeaponDefinition(
        ItemInventory inventory,
        WeaponDefinition definition,
        out InventoryOperationFailure failure)
    {
        failure = InventoryOperationFailure.None;
        if (definition == null || definition.MaximumStackSize != 1 || definition.Category != ItemCategory.Weapon)
        {
            failure = InventoryOperationFailure.InvalidDefinition;
            return false;
        }

        if (!ItemInstance.TryCreate(definition, 1, out ItemInstance itemInstance))
        {
            failure = InventoryOperationFailure.InvalidDefinition;
            return false;
        }

        if (!inventory.TryFindFirstFit(definition, out Vector2Int position, out bool rotated))
        {
            failure = InventoryOperationFailure.NoSpace;
            return false;
        }

        InventoryOperationResult addResult = inventory.TryAddItem(itemInstance, position, rotated);
        failure = addResult.Failure;
        return addResult.Succeeded;
    }

    private static bool IsValidDefinition(ItemDefinition definition)
    {
        return definition != null &&
               !string.IsNullOrWhiteSpace(definition.StableItemId) &&
               definition.InventoryWidth > 0 &&
               definition.InventoryHeight > 0 &&
               definition.MaximumStackSize > 0;
    }

    private static bool IsValidItemInstance(ItemInstance item)
    {
        return item != null &&
               !string.IsNullOrWhiteSpace(item.InstanceId) &&
               IsValidDefinition(item.Definition) &&
               item.Quantity > 0 &&
               item.Quantity <= item.Definition.MaximumStackSize;
    }

    private static bool IsKeyItem(ItemDefinition definition)
    {
        return definition.Category == ItemCategory.KeyItem || definition.Category == ItemCategory.QuestItem;
    }

    private int CountOverlappingItems(
        ItemInstance movingItem,
        Vector2Int position,
        int rotationQuarterTurns,
        out ItemInstance singleBlockingItem)
    {
        singleBlockingItem = null;
        bool rotated = rotationQuarterTurns % 2 != 0;
        GetFootprint(movingItem.Definition, rotated, out int itemWidth, out int itemHeight);
        int overlapCount = 0;
        for (int index = 0; index < items.Count; index++)
        {
            ItemInstance existingItem = items[index];
            if (string.Equals(existingItem.InstanceId, movingItem.InstanceId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!FootprintsOverlap(
                    position,
                    itemWidth,
                    itemHeight,
                    existingItem.GridPosition,
                    GetFootprintWidth(existingItem),
                    GetFootprintHeight(existingItem)))
            {
                continue;
            }

            overlapCount++;
            singleBlockingItem = existingItem;
            if (overlapCount > 1)
            {
                return overlapCount;
            }
        }

        return overlapCount;
    }

    private InventoryOperationFailure ValidatePlacement(
        ItemDefinition definition,
        Vector2Int position,
        bool rotated,
        int candidateGridWidth,
        int candidateGridHeight,
        string ignoredInstanceId,
        List<PlannedPlacement> plannedPlacements,
        string secondIgnoredInstanceId = null)
    {
        if (!IsValidDefinition(definition))
        {
            return InventoryOperationFailure.InvalidDefinition;
        }

        GetFootprint(definition, rotated, out int itemWidth, out int itemHeight);
        if (position.x < FirstGridCoordinate || position.y < FirstGridCoordinate ||
            position.x + itemWidth > candidateGridWidth || position.y + itemHeight > candidateGridHeight)
        {
            return InventoryOperationFailure.OutOfBounds;
        }

        for (int index = 0; index < items.Count; index++)
        {
            ItemInstance existingItem = items[index];
            if (string.Equals(existingItem.InstanceId, ignoredInstanceId, StringComparison.Ordinal) ||
                string.Equals(existingItem.InstanceId, secondIgnoredInstanceId, StringComparison.Ordinal))
            {
                continue;
            }

            if (FootprintsOverlap(
                    position,
                    itemWidth,
                    itemHeight,
                    existingItem.GridPosition,
                    GetFootprintWidth(existingItem),
                    GetFootprintHeight(existingItem)))
            {
                return InventoryOperationFailure.Overlap;
            }
        }

        if (plannedPlacements != null)
        {
            for (int index = 0; index < plannedPlacements.Count; index++)
            {
                PlannedPlacement plannedPlacement = plannedPlacements[index];
                GetFootprint(plannedPlacement.Definition, plannedPlacement.Rotated, out int plannedWidth, out int plannedHeight);
                if (FootprintsOverlap(position, itemWidth, itemHeight, plannedPlacement.Position, plannedWidth, plannedHeight))
                {
                    return InventoryOperationFailure.Overlap;
                }
            }
        }

        return InventoryOperationFailure.None;
    }

    private bool TryFindCraftPlacement(
        ItemDefinition definition,
        Dictionary<ItemInstance, int> plannedQuantities,
        List<PlannedPlacement> plannedPlacements,
        out Vector2Int position,
        out bool rotated)
    {
        position = Vector2Int.zero;
        rotated = false;
        bool canRotate = definition != null && definition.CanRotate;
        for (int y = FirstGridCoordinate; y < gridHeight; y++)
        {
            for (int x = FirstGridCoordinate; x < gridWidth; x++)
            {
                Vector2Int candidate = new Vector2Int(x, y);
                if (IsCraftPlacementFree(definition, candidate, false, plannedQuantities, plannedPlacements))
                {
                    position = candidate;
                    rotated = false;
                    return true;
                }

                if (canRotate && IsCraftPlacementFree(definition, candidate, true, plannedQuantities, plannedPlacements))
                {
                    position = candidate;
                    rotated = true;
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsCraftPlacementFree(
        ItemDefinition definition,
        Vector2Int position,
        bool rotated,
        Dictionary<ItemInstance, int> plannedQuantities,
        List<PlannedPlacement> plannedPlacements)
    {
        if (!IsValidDefinition(definition))
        {
            return false;
        }

        GetFootprint(definition, rotated, out int width, out int height);
        if (position.x < FirstGridCoordinate || position.y < FirstGridCoordinate ||
            position.x + width > gridWidth || position.y + height > gridHeight)
        {
            return false;
        }

        for (int index = 0; index < items.Count; index++)
        {
            ItemInstance item = items[index];
            if (plannedQuantities.TryGetValue(item, out int plannedQuantity) && plannedQuantity <= 0)
            {
                continue;
            }

            if (FootprintsOverlap(position, width, height, item.GridPosition, GetFootprintWidth(item), GetFootprintHeight(item)))
            {
                return false;
            }
        }

        for (int index = 0; index < plannedPlacements.Count; index++)
        {
            PlannedPlacement placement = plannedPlacements[index];
            GetFootprint(placement.Definition, placement.Rotated, out int placedWidth, out int placedHeight);
            if (FootprintsOverlap(position, width, height, placement.Position, placedWidth, placedHeight))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreSameRecipeDefinition(ItemDefinition first, ItemDefinition second)
    {
        return first != null && second != null && first.Category == second.Category &&
               string.Equals(first.StableItemId, second.StableItemId, StringComparison.Ordinal);
    }

    private static bool CanStackDefinitions(ItemDefinition first, ItemDefinition second)
    {
        if (first == null || second == null || first.MaximumStackSize <= 1 || second.MaximumStackSize <= 1)
        {
            return false;
        }

        if (first is AmmoDefinition firstAmmo && second is AmmoDefinition secondAmmo)
        {
            return firstAmmo.AmmunitionType == secondAmmo.AmmunitionType &&
                   string.Equals(firstAmmo.StackCompatibilityKey, secondAmmo.StackCompatibilityKey, StringComparison.Ordinal);
        }

        return first.Category == second.Category &&
               string.Equals(first.StableItemId, second.StableItemId, StringComparison.Ordinal);
    }

    private bool TryFindPlacementForDefinition(
        ItemDefinition definition,
        bool allowRotation,
        string ignoredInstanceId,
        List<PlannedPlacement> plannedPlacements,
        out Vector2Int position,
        out bool rotated)
    {
        position = Vector2Int.zero;
        rotated = false;
        if (!IsValidDefinition(definition))
        {
            return false;
        }

        bool canTryRotatedPlacement = allowRotation && definition.CanRotate;
        for (int y = FirstGridCoordinate; y < gridHeight; y++)
        {
            for (int x = FirstGridCoordinate; x < gridWidth; x++)
            {
                Vector2Int candidate = new Vector2Int(x, y);
                if (ValidatePlacement(definition, candidate, false, gridWidth, gridHeight, ignoredInstanceId, plannedPlacements) == InventoryOperationFailure.None)
                {
                    position = candidate;
                    rotated = false;
                    return true;
                }

                if (canTryRotatedPlacement &&
                    ValidatePlacement(definition, candidate, true, gridWidth, gridHeight, ignoredInstanceId, plannedPlacements) == InventoryOperationFailure.None)
                {
                    position = candidate;
                    rotated = true;
                    return true;
                }
            }
        }

        return false;
    }

    private static void GetFootprint(ItemInstance item, bool rotated, out int width, out int height)
    {
        GetFootprint(item.Definition, rotated, out width, out height);
    }

    private static void GetFootprint(ItemDefinition definition, bool rotated, out int width, out int height)
    {
        width = rotated ? definition.InventoryHeight : definition.InventoryWidth;
        height = rotated ? definition.InventoryWidth : definition.InventoryHeight;
    }

    private static int GetFootprintWidth(ItemInstance item)
    {
        GetFootprint(item, item.IsRotated, out int width, out _);
        return width;
    }

    private static int GetFootprintHeight(ItemInstance item)
    {
        GetFootprint(item, item.IsRotated, out _, out int height);
        return height;
    }

    private static bool IsInsideGrid(int x, int y, int candidateGridWidth, int candidateGridHeight)
    {
        return x >= FirstGridCoordinate && y >= FirstGridCoordinate && x < candidateGridWidth && y < candidateGridHeight;
    }

    private static bool FootprintsOverlap(
        Vector2Int firstPosition,
        int firstWidth,
        int firstHeight,
        Vector2Int secondPosition,
        int secondWidth,
        int secondHeight)
    {
        return firstPosition.x < secondPosition.x + secondWidth &&
               firstPosition.x + firstWidth > secondPosition.x &&
               firstPosition.y < secondPosition.y + secondHeight &&
               firstPosition.y + firstHeight > secondPosition.y;
    }

    private void RemoveItemAt(int index, ItemInstance item)
    {
        if (index < 0 || index >= items.Count)
        {
            return;
        }

        UnsubscribeFromItem(item);
        items.RemoveAt(index);
    }

    private void EnsureItemSubscriptions()
    {
        if (items == null)
        {
            items = new List<ItemInstance>();
        }

        if (subscribedInstanceIds == null)
        {
            subscribedInstanceIds = new HashSet<string>(StringComparer.Ordinal);
        }

        for (int index = 0; index < items.Count; index++)
        {
            SubscribeToItem(items[index]);
        }
    }

    private void SubscribeToItem(ItemInstance item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.InstanceId) || !subscribedInstanceIds.Add(item.InstanceId))
        {
            return;
        }

        item.StateChanged += HandleItemStateChanged;
        item.SetOwningInventory(this);
    }

    private void UnsubscribeFromItem(ItemInstance item)
    {
        if (item == null || subscribedInstanceIds == null || !subscribedInstanceIds.Remove(item.InstanceId))
        {
            return;
        }

        item.StateChanged -= HandleItemStateChanged;
        item.SetOwningInventory(null);
    }

    private void HandleItemStateChanged(ItemInstance item)
    {
        if (!suppressItemChangeNotifications)
        {
            RaiseChanged();
        }
    }

    private void RaiseChanged()
    {
        changed?.Invoke(this);
    }

    private struct PlannedStackChange
    {
        public PlannedStackChange(ItemInstance item, int newQuantity)
        {
            Item = item;
            NewQuantity = newQuantity;
        }

        public ItemInstance Item { get; }
        public int NewQuantity { get; }
    }

    private struct PlannedPlacement
    {
        public PlannedPlacement(ItemDefinition definition, int quantity, Vector2Int position, bool rotated, string instanceId = null)
            : this(definition, quantity, position, rotated ? 1 : 0, instanceId)
        {
        }

        public PlannedPlacement(ItemDefinition definition, int quantity, Vector2Int position, int rotationQuarterTurns, string instanceId = null)
        {
            Definition = definition;
            Quantity = quantity;
            Position = position;
            RotationQuarterTurns = ((rotationQuarterTurns % 4) + 4) % 4;
            InstanceId = instanceId;
        }

        public ItemDefinition Definition { get; }
        public int Quantity { get; }
        public Vector2Int Position { get; }
        public int RotationQuarterTurns { get; }
        public bool Rotated => RotationQuarterTurns % 2 != 0;
        public string InstanceId { get; }
    }
}
