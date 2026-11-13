#if UNITY_EDITOR
using System.Reflection;

using System;
using UnityEditor;
using UnityEngine;

/// <summary>Provides repeatable editor checks for item-instance and carry-grid behavior.</summary>
internal static class ItemInventoryModelValidationMenu
{
    [MenuItem("Tools/Items/Run Inventory Model Checks")]
    private static void RunInventoryModelChecks()
    {
        AmmoDefinition stackableAmmo = null;
        AmmoDefinition rotatableAmmo = null;
        WeaponDefinition weaponDefinition = null;

        try
        {
            stackableAmmo = CreateAmmoDefinition(
                "test.ammo.stackable",
                WeaponDefinition.AmmunitionType.Pistol,
                "test-ammo-pistol",
                1,
                1,
                false,
                120);
            rotatableAmmo = CreateAmmoDefinition(
                "test.ammo.rotatable",
                WeaponDefinition.AmmunitionType.Rifle,
                "test-ammo-rifle",
                2,
                1,
                true,
                1);
            weaponDefinition = CreateWeaponDefinition();

            ValidateStackingSplitMergeAndPlacement(stackableAmmo);
            ValidatePartialAcceptanceIsSafe(stackableAmmo);
            ValidateRotationAndResize(stackableAmmo, rotatableAmmo);
            ValidateAutoSort(stackableAmmo, rotatableAmmo, weaponDefinition);
            ValidateIndependentWeaponMagazineState(weaponDefinition);
            ValidateChangeNotifications(stackableAmmo);
            ValidateStarterLoadoutFactory();
            ValidateReloadAmmoBridge();
            ValidatePlayerAmmoCompatibilityFacade();
            ValidatePickupTransactions();

            Debug.Log("Inventory model checks passed: stacking, split/merge, partial pickup acceptance, placement, rotation, resize protection, per-instance magazine state, notifications, starter loadout, reload transfers, compatibility facade, and pickup transactions.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
        finally
        {
            if (stackableAmmo != null)
            {
                UnityEngine.Object.DestroyImmediate(stackableAmmo);
            }

            if (rotatableAmmo != null)
            {
                UnityEngine.Object.DestroyImmediate(rotatableAmmo);
            }

            if (weaponDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(weaponDefinition);
            }
        }
    }

    private static void ValidateStackingSplitMergeAndPlacement(AmmoDefinition ammoDefinition)
    {
        ItemInventory inventory = new ItemInventory(ItemInventory.DefaultGridWidth, ItemInventory.DefaultGridHeight);
        InventoryOperationResult addResult = inventory.TryAddQuantity(ammoDefinition, 240);
        Require(addResult.Completed && addResult.AcceptedQuantity == 240, "An 8×6 grid should accept two full ammo stacks.");
        Require(inventory.Count == 2, "240 rounds at a 120-round stack cap should create two item instances.");
        Require(inventory.Items[0].Quantity == 120 && inventory.Items[1].Quantity == 120, "New ammo stacks should not exceed their definition cap.");

        ItemInstance sourceItem = inventory.Items[0];
        Require(inventory.TryFindFirstFit(ammoDefinition, out Vector2Int splitPosition, out bool splitRotated), "The remaining grid should contain a free cell for a split stack.");
        InventoryOperationResult splitResult = inventory.TrySplitStack(sourceItem.InstanceId, 40, splitPosition, splitRotated, out ItemInstance splitItem);
        Require(splitResult.Completed && splitItem != null, "A legal stack split should create a new instance.");
        Require(sourceItem.Quantity == 80 && splitItem.Quantity == 40, "Splitting should preserve the requested quantity total.");

        InventoryOperationResult mergeResult = inventory.TryMergeStacks(sourceItem.InstanceId, splitItem.InstanceId);
        Require(mergeResult.Completed && mergeResult.RequestedQuantity == 40, "Merging a complete source stack should report the source quantity once.");
        Require(sourceItem.Quantity == 120 && inventory.Count == 2, "A completed merge should restore the target stack and remove the empty source instance.");

        ItemInstance otherStack = inventory.Items[1];
        InventoryOperationResult overlapResult = inventory.TryMoveItem(sourceItem.InstanceId, otherStack.GridPosition, false);
        Require(!overlapResult.Succeeded && overlapResult.Failure == InventoryOperationFailure.Overlap, "Overlapping placement must be rejected.");
        Require(inventory.Items[0].GridPosition != otherStack.GridPosition, "A rejected move must preserve the original layout.");
        InventoryOperationResult outOfBoundsResult = inventory.TryMoveItem(sourceItem.InstanceId, new Vector2Int(-1, 0), false);
        Require(!outOfBoundsResult.Succeeded && outOfBoundsResult.Failure == InventoryOperationFailure.OutOfBounds, "Negative grid coordinates must be rejected.");
        InventoryOperationResult duplicateOwnerResult = new ItemInventory().TryAddItem(sourceItem, Vector2Int.zero, false);
        Require(!duplicateOwnerResult.Succeeded && duplicateOwnerResult.Failure == InventoryOperationFailure.DuplicateInstance, "An item instance must not be owned by two containers at once.");
    }

    private static void ValidatePartialAcceptanceIsSafe(AmmoDefinition ammoDefinition)
    {
        ItemInventory oneCellInventory = new ItemInventory(1, 1);
        InventoryOperationResult partialResult = oneCellInventory.TryAddQuantity(ammoDefinition, 240);
        Require(partialResult.Succeeded && !partialResult.Completed, "A pickup larger than a one-cell stack should report partial acceptance.");
        Require(partialResult.AcceptedQuantity == 120 && partialResult.RemainingQuantity == 120, "Partial pickup results should accurately report accepted and remaining rounds.");

        InventoryOperationResult fullResult = oneCellInventory.TryAddQuantity(ammoDefinition, 10);
        Require(!fullResult.Succeeded && fullResult.AcceptedQuantity == 0, "A full one-cell grid should reject an additional stack without partial mutation.");
        Require(oneCellInventory.Count == 1 && oneCellInventory.Items[0].Quantity == 120, "A rejected full-grid pickup must preserve inventory state.");

        InventoryOperationResult allOrNothingResult = oneCellInventory.TryAddQuantity(ammoDefinition, 30, true);
        Require(!allOrNothingResult.Succeeded && allOrNothingResult.Failure == InventoryOperationFailure.NoSpace,
            "An all-or-nothing shop transaction must reject when only part of the requested amount fits.");
        Require(oneCellInventory.Count == 1 && oneCellInventory.Items[0].Quantity == 120,
            "A rejected all-or-nothing purchase must leave the existing stack unchanged.");

        ItemInventory twoCellInventory = new ItemInventory(2, 1);
        Require(twoCellInventory.TryAddQuantity(ammoDefinition, 120).Completed,
            "The purchase-capacity test should fill one stack before testing remaining capacity.");
        InventoryOperationResult completePurchaseResult = twoCellInventory.TryAddQuantity(ammoDefinition, 30, true);
        Require(completePurchaseResult.Completed && twoCellInventory.GetAmmoQuantity(WeaponDefinition.AmmunitionType.Pistol) == 150,
            "An all-or-nothing shop transaction should accept the full amount when capacity is available.");
    }

    private static void ValidateRotationAndResize(AmmoDefinition nonRotatableFlagDefinition, AmmoDefinition rotatableDefinition)
    {
        Require(nonRotatableFlagDefinition.CanRotate, "Every item should allow inventory rotation even when its legacy authoring flag is disabled.");
        ItemInventory anyItemInventory = new ItemInventory(1, 1);
        Require(anyItemInventory.TryAddQuantity(nonRotatableFlagDefinition, 1).Completed, "A default one-cell item should fit the rotation check grid.");
        ItemInstance anyItem = anyItemInventory.Items[0];
        Require(anyItemInventory.TryMoveItem(anyItem.InstanceId, Vector2Int.zero, 3).Succeeded && anyItem.RotationQuarterTurns == 3,
            "Items authored with rotation disabled should still accept three quarter turns.");

        ItemInventory narrowInventory = new ItemInventory(1, 2);
        InventoryOperationResult addResult = narrowInventory.TryAddQuantity(rotatableDefinition, 1);
        Require(addResult.Completed && narrowInventory.Count == 1, "An eligible footprint should fit when rotated into a 1×2 grid.");
        Require(narrowInventory.Items[0].IsRotated, "Automatic placement should rotate an eligible item only when needed.");

        InventoryOperationResult unsupportedTransition = narrowInventory.TrySetGridDimensions(ItemInventory.CaseGridWidth, ItemInventory.CaseStage2Height);
        Require(!unsupportedTransition.Succeeded && unsupportedTransition.Failure == InventoryOperationFailure.InvalidDimensions,
            "A case stage change from unsupported legacy dimensions must be rejected.");
        Require(narrowInventory.GridWidth == 1 && narrowInventory.GridHeight == 2,
            "A rejected stage transition must preserve the existing inventory dimensions.");

        ItemInventory caseInventory = new ItemInventory(ItemInventory.DefaultGridWidth, ItemInventory.DefaultGridHeight);
        Require(caseInventory.TryAddQuantity(rotatableDefinition, 1).Completed,
            "A rotatable test item should fit the starting case.");
        ItemInstance caseItem = caseInventory.Items[0];
        Require(caseInventory.TryMoveItem(caseItem.InstanceId, caseItem.GridPosition, 1).Completed,
            "The test item should rotate to 90 degrees in the starting case.");
        Require(caseItem.RotationQuarterTurns == 1 && caseItem.IsRotated,
            "A clockwise quarter turn should update the item's persisted rotation orientation.");
        Require(caseInventory.TryMoveItem(caseItem.InstanceId, caseItem.GridPosition, 2).Completed && caseItem.RotationQuarterTurns == 2 && !caseItem.IsRotated,
            "A 180-degree turn should preserve the footprint while retaining its distinct visual orientation.");
        Require(caseInventory.TryMoveItem(caseItem.InstanceId, caseItem.GridPosition, 3).Completed && caseItem.RotationQuarterTurns == 3 && caseItem.IsRotated,
            "A 270-degree turn should remain a distinct valid orientation.");
        Vector2Int originalPosition = caseItem.GridPosition;
        InventoryOperationResult expansion = caseInventory.TrySetGridDimensions(ItemInventory.CaseGridWidth, ItemInventory.CaseStage2Height);
        Require(expansion.Completed && caseInventory.GridWidth == ItemInventory.CaseGridWidth && caseInventory.GridHeight == ItemInventory.CaseStage2Height,
            "A valid case-stage expansion should advance to the next supported height.");
        Require(caseItem.GridPosition == originalPosition && caseItem.RotationQuarterTurns == 3,
            "A valid expansion should preserve existing item placement and its full rotation orientation.");
    }

    private static void ValidateAutoSort(
        AmmoDefinition stackableAmmo,
        AmmoDefinition rotatableAmmo,
        WeaponDefinition weaponDefinition)
    {
        ItemInventory inventory = new ItemInventory(4, 3);
        Require(ItemInstance.TryCreate(stackableAmmo, 1, out ItemInstance smallAmmo), "The sort check should create its small ammo item.");
        Require(ItemInstance.TryCreate(rotatableAmmo, 1, out ItemInstance wideAmmo), "The sort check should create its wide ammo item.");
        Require(ItemInstance.TryCreate(weaponDefinition, 1, out ItemInstance equippedWeapon), "The sort check should create its weapon item.");
        Require(inventory.TryAddItem(smallAmmo, new Vector2Int(3, 0), false).Succeeded, "The small ammo item should be added to the sort check grid.");
        Require(inventory.TryAddItem(wideAmmo, new Vector2Int(0, 1), false).Succeeded, "The wide ammo item should be added to the sort check grid.");
        Require(inventory.TryAddItem(equippedWeapon, new Vector2Int(3, 2), false).Succeeded, "The weapon should be added to the sort check grid.");
        Require(inventory.TrySetEquipped(equippedWeapon.InstanceId, true).Succeeded, "The weapon should be marked equipped before sorting.");

        string smallAmmoId = smallAmmo.InstanceId;
        string wideAmmoId = wideAmmo.InstanceId;
        string equippedWeaponId = equippedWeapon.InstanceId;
        InventoryOperationResult sortResult = inventory.TryAutoSort();
        Require(sortResult.Succeeded, "Auto-sort should plan and apply a valid layout.");
        Require(equippedWeapon.GridPosition == new Vector2Int(3, 2) && equippedWeapon.IsEquipped,
            "Auto-sort must keep equipped items anchored and equipped.");
        Require(wideAmmo.GridPosition == Vector2Int.zero && !wideAmmo.IsRotated,
            "Auto-sort should place the larger unrotated footprint first in row-major order.");
        Require(smallAmmo.GridPosition == new Vector2Int(2, 0),
            "Auto-sort should place smaller items deterministically after larger footprints.");
        Require(inventory.FindItem(smallAmmoId) == smallAmmo && inventory.FindItem(wideAmmoId) == wideAmmo && inventory.FindItem(equippedWeaponId) == equippedWeapon,
            "Auto-sort must preserve item instance identities and ownership.");

        Vector2Int smallPositionAfterFirstSort = smallAmmo.GridPosition;
        Vector2Int widePositionAfterFirstSort = wideAmmo.GridPosition;
        Require(inventory.TryAutoSort().Succeeded, "Repeated auto-sort should remain successful.");
        Require(smallAmmo.GridPosition == smallPositionAfterFirstSort && wideAmmo.GridPosition == widePositionAfterFirstSort,
            "Repeated auto-sort should be idempotent.");
    }


    private static void ValidateIndependentWeaponMagazineState(WeaponDefinition weaponDefinition)
    {
        Require(ItemInstance.TryCreate(weaponDefinition, 1, out ItemInstance firstWeapon), "The first weapon instance should be created.");
        Require(ItemInstance.TryCreate(weaponDefinition, 1, out ItemInstance secondWeapon), "The second weapon instance should be created.");
        Require(firstWeapon.LoadedRounds == weaponDefinition.MagazineCapacity && secondWeapon.LoadedRounds == weaponDefinition.MagazineCapacity, "New weapon instances should start with full magazines by default.");
        ItemInventory inventory = new ItemInventory(2, 2);
        int changeCount = 0;
        inventory.Changed += _ => changeCount++;
        InventoryOperationResult addFirstResult = inventory.TryAddItem(firstWeapon, Vector2Int.zero, false);
        Require(addFirstResult.Succeeded && changeCount == 1, "Adding a weapon instance should notify observers once.");
        Require(firstWeapon.TrySpendLoadedRound(), "The inventory-owned weapon should spend a loaded round.");
        Require(changeCount == 2, "A loaded-round change should notify inventory observers exactly once.");
        Require(firstWeapon.LoadedRounds == weaponDefinition.MagazineCapacity - 1, "The first weapon should have one fewer round after firing.");
        Require(secondWeapon.LoadedRounds == weaponDefinition.MagazineCapacity, "Spending a round must not change another instance's magazine.");
    }

    private static void ValidateChangeNotifications(AmmoDefinition ammoDefinition)
    {
        ItemInventory inventory = new ItemInventory(2, 2);
        int changeCount = 0;
        inventory.Changed += _ => changeCount++;
        InventoryOperationResult addResult = inventory.TryAddQuantity(ammoDefinition, 10);
        Require(addResult.Completed && changeCount == 1, "A stack add should notify observers once after the transaction.");

        InventoryOperationResult splitResult = inventory.TrySplitStack(
            inventory.Items[0].InstanceId,
            3,
            new Vector2Int(1, 0),
            false,
            out ItemInstance splitItem);
        Require(splitResult.Completed && splitItem != null && changeCount == 2, "A split should notify observers once after the transaction.");
        Require(splitItem.TrySetLoadedRounds(0) == false, "Ammunition instances must not expose weapon magazine state.");
    }

    private static void ValidateStarterLoadoutFactory()
    {
        WeaponDefinition pistolDefinition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Weapons/Definitions/Pistol.asset");
        WeaponDefinition rifleDefinition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Weapons/Definitions/AssaultRifle.asset");
        AmmoDefinition pistolAmmoDefinition = AssetDatabase.LoadAssetAtPath<AmmoDefinition>("Assets/Items/Definitions/Ammo_Pistol.asset");
        AmmoDefinition rifleAmmoDefinition = AssetDatabase.LoadAssetAtPath<AmmoDefinition>("Assets/Items/Definitions/Ammo_Rifle.asset");

        Require(ItemInventory.TryCreateStartingLoadout(
                pistolDefinition,
                rifleDefinition,
                pistolAmmoDefinition,
                rifleAmmoDefinition,
                out ItemInventory starterInventory,
                out InventoryOperationFailure failure),
            $"The configured starter loadout should fit the default grid. Failure: {failure}.");

        Require(starterInventory.GridWidth == ItemInventory.CaseGridWidth && starterInventory.GridHeight == ItemInventory.CaseStage1Height, "The starter inventory should use 8×6 default dimensions.");
        Require(starterInventory.Count == 6, "The starter loadout should contain two weapons and four ammo stacks.");

        int pistolAmmoTotal = 0;
        int rifleAmmoTotal = 0;
        int weaponCount = 0;
        for (int index = 0; index < starterInventory.Items.Count; index++)
        {
            ItemInstance item = starterInventory.Items[index];
            if (item.Definition is AmmoDefinition ammoDefinition)
            {
                if (ammoDefinition.AmmunitionType == WeaponDefinition.AmmunitionType.Pistol)
                {
                    pistolAmmoTotal += item.Quantity;
                }
                else if (ammoDefinition.AmmunitionType == WeaponDefinition.AmmunitionType.Rifle)
                {
                    rifleAmmoTotal += item.Quantity;
                }
            }
            else if (item.Definition is WeaponDefinition weaponDefinition)
            {
                weaponCount++;
                Require(item.LoadedRounds == weaponDefinition.MagazineCapacity, "Each starter weapon should have an independent full magazine.");
            }

            Require(starterInventory.GetItemAt(item.GridPosition) == item, "Each starter item should occupy its recorded grid position.");
        }

        Require(pistolAmmoTotal == ItemInventory.DefaultPistolReserveRounds, "The starter Pistol ammo total should be 160 rounds.");
        Require(rifleAmmoTotal == ItemInventory.DefaultRifleReserveRounds, "The starter Rifle ammo total should be 240 rounds.");
        Require(weaponCount == 2, "The starter loadout should contain exactly two production weapon instances.");
    }
    private static void ValidateReloadAmmoBridge()
    {
        WeaponDefinition pistolDefinition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Weapons/Definitions/Pistol.asset");
        WeaponDefinition rifleDefinition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Weapons/Definitions/AssaultRifle.asset");
        AmmoDefinition pistolAmmoDefinition = AssetDatabase.LoadAssetAtPath<AmmoDefinition>("Assets/Items/Definitions/Ammo_Pistol.asset");
        AmmoDefinition rifleAmmoDefinition = AssetDatabase.LoadAssetAtPath<AmmoDefinition>("Assets/Items/Definitions/Ammo_Rifle.asset");

        Require(ItemInventory.TryCreateStartingLoadout(
                pistolDefinition,
                rifleDefinition,
                pistolAmmoDefinition,
                rifleAmmoDefinition,
                out ItemInventory inventory,
                out InventoryOperationFailure failure),
            $"The reload bridge starter inventory should initialize. Failure: {failure}.");

        ItemInstance pistolItem = FindWeaponItem(inventory, pistolDefinition);
        ItemInstance rifleItem = FindWeaponItem(inventory, rifleDefinition);
        Require(pistolItem != null && rifleItem != null, "Both production weapon item instances must be present for reload checks.");

        WeaponAmmoState pistolAmmoState = new WeaponAmmoState(pistolItem);
        Require(pistolAmmoState.TrySpendRound(pistolDefinition), "Firing should spend a round from the owned Pistol instance.");
        Require(pistolItem.LoadedRounds == pistolDefinition.MagazineCapacity - 1, "The Pistol magazine state should be stored on its item instance.");
        Require(inventory.TryExchangeAmmo(pistolDefinition.AmmoType, pistolAmmoState.LoadedRounds, pistolDefinition.MagazineCapacity, out int roundsWithdrawn), "The Pistol magazine exchange should atomically use inventory ammo stacks.");
        Require(roundsWithdrawn == pistolDefinition.MagazineCapacity, "The Pistol should withdraw a full magazine when reserve permits.");
        Require(pistolAmmoState.ReplaceMagazine(pistolDefinition, roundsWithdrawn) == pistolDefinition.MagazineCapacity, "The Pistol instance should receive the exchanged magazine.");
        Require(inventory.GetAmmoQuantity(pistolDefinition.AmmoType) == ItemInventory.DefaultPistolReserveRounds - 1, "The reserve total should reflect returned old rounds minus withdrawn magazine rounds.");

        WeaponAmmoState rifleAmmoState = new WeaponAmmoState(rifleItem);
        Require(rifleAmmoState.ReplaceMagazine(rifleDefinition, rifleDefinition.MagazineCapacity - 2) == rifleDefinition.MagazineCapacity - 2, "The Rifle test magazine should set on its instance.");
        Require(inventory.TryConsumeAmmo(rifleDefinition.AmmoType, 1), "Round-by-round reload should consume reserve from Rifle ammo stacks.");
        Require(rifleAmmoState.AddRounds(rifleDefinition, 1) == 1, "Round-by-round reload should add a round to the owned Rifle magazine.");
        Require(inventory.TryConsumeAmmo(rifleDefinition.AmmoType, 1), "A second round-by-round transfer should consume reserve exactly once.");
        Require(rifleAmmoState.AddRounds(rifleDefinition, 1) == 1, "A second round-by-round transfer should fill the owned Rifle magazine.");
        Require(rifleAmmoState.LoadedRounds == rifleDefinition.MagazineCapacity, "Round-by-round transfers should fill only this Rifle instance.");
        Require(inventory.GetAmmoQuantity(rifleDefinition.AmmoType) == ItemInventory.DefaultRifleReserveRounds - 2, "Rifle reserve total should drop exactly for rounds inserted.");
        Require(pistolAmmoState.LoadedRounds == pistolDefinition.MagazineCapacity, "Reloading the Rifle must not change the Pistol instance magazine.");
    }

    private static ItemInstance FindWeaponItem(ItemInventory inventory, WeaponDefinition definition)
    {
        for (int index = 0; index < inventory.Items.Count; index++)
        {
            if (inventory.Items[index].Definition == definition)
            {
                return inventory.Items[index];
            }
        }

        return null;
    }


    private static void ValidatePlayerAmmoCompatibilityFacade()
    {
        GameObject temporaryPlayer = new GameObject("InventoryAmmoFacadeCheck");
        temporaryPlayer.SetActive(false);
        try
        {
            PlayerItemInventory playerItemInventory = temporaryPlayer.AddComponent<PlayerItemInventory>();
            SerializedObject serializedInventory = new SerializedObject(playerItemInventory);
            serializedInventory.FindProperty("startingPistolDefinition").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Weapons/Definitions/Pistol.asset");
            serializedInventory.FindProperty("startingAssaultRifleDefinition").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Weapons/Definitions/AssaultRifle.asset");
            serializedInventory.FindProperty("pistolAmmoDefinition").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AmmoDefinition>("Assets/Items/Definitions/Ammo_Pistol.asset");
            serializedInventory.FindProperty("rifleAmmoDefinition").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AmmoDefinition>("Assets/Items/Definitions/Ammo_Rifle.asset");
            serializedInventory.FindProperty("startingPistolReserveRounds").intValue = ItemInventory.DefaultPistolReserveRounds;
            serializedInventory.FindProperty("startingRifleReserveRounds").intValue = ItemInventory.DefaultRifleReserveRounds;
            serializedInventory.ApplyModifiedPropertiesWithoutUndo();
            temporaryPlayer.SetActive(true);
            ItemInventory initializedInventory = playerItemInventory.Inventory;
            Require(initializedInventory != null, "The test PlayerItemInventory should initialize through its public Inventory property.");

            PlayerAmmoInventory ammoCompatibilityFacade = temporaryPlayer.AddComponent<PlayerAmmoInventory>();
            Require(ammoCompatibilityFacade.GetReserveRounds(WeaponDefinition.AmmunitionType.Pistol) == 160, "The existing reserve-ammo API should read the Pistol ammo stacks.");
            Require(ammoCompatibilityFacade.GetReserveRounds(WeaponDefinition.AmmunitionType.Rifle) == 240, "The existing reserve-ammo API should read the Rifle ammo stacks.");

            Require(ammoCompatibilityFacade.TryConsumeRounds(WeaponDefinition.AmmunitionType.Pistol, 1), "The compatibility facade should consume from inventory-owned ammo stacks.");
            Require(ammoCompatibilityFacade.GetReserveRounds(WeaponDefinition.AmmunitionType.Pistol) == 159, "Facade consumption should update the single item-stack reserve owner.");
            Require(ammoCompatibilityFacade.TryAddRounds(WeaponDefinition.AmmunitionType.Pistol, 1), "The compatibility facade should return a round to inventory-owned ammo stacks.");
            Require(ammoCompatibilityFacade.GetReserveRounds(WeaponDefinition.AmmunitionType.Pistol) == 160, "Facade addition should update the same ammo stacks without a second reserve cache.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(temporaryPlayer);
        }
    }

    private static void ValidatePickupTransactions()
    {
        GameObject temporaryPlayer = new GameObject("InventoryPickupTransactionCheck");
        temporaryPlayer.SetActive(false);
        GameObject pickupObject = null;
        GameObject disabledPickupObject = null;
        KeyItemDefinition keyItemDefinition = null;
        InventoryCapacityUpgradeDefinition capacityUpgradeDefinition = null;
        try
        {
            Player testPlayer = temporaryPlayer.AddComponent<Player>();
            PlayerItemInventory playerItemInventory = temporaryPlayer.AddComponent<PlayerItemInventory>();
            SerializedObject serializedInventory = new SerializedObject(playerItemInventory);
            serializedInventory.FindProperty("startingPistolDefinition").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Weapons/Definitions/Pistol.asset");
            serializedInventory.FindProperty("startingAssaultRifleDefinition").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Weapons/Definitions/AssaultRifle.asset");
            serializedInventory.FindProperty("pistolAmmoDefinition").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AmmoDefinition>("Assets/Items/Definitions/Ammo_Pistol.asset");
            serializedInventory.FindProperty("rifleAmmoDefinition").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AmmoDefinition>("Assets/Items/Definitions/Ammo_Rifle.asset");
            serializedInventory.FindProperty("startingPistolReserveRounds").intValue = ItemInventory.DefaultPistolReserveRounds;
            serializedInventory.FindProperty("startingRifleReserveRounds").intValue = ItemInventory.DefaultRifleReserveRounds;
            serializedInventory.ApplyModifiedPropertiesWithoutUndo();
            temporaryPlayer.SetActive(true);

            InventoryOperationResult partialAmmoPickup = playerItemInventory.TryAddPickupQuantity(
                AssetDatabase.LoadAssetAtPath<AmmoDefinition>("Assets/Items/Definitions/Ammo_Pistol.asset"),
                10000);
            Require(partialAmmoPickup.AcceptedQuantity > 0 && partialAmmoPickup.RemainingQuantity > 0,
                "A full 8×6 starter loadout should accept only the amount that fits and leave the remainder on the pickup.");

            InventoryOperationResult fullPickup = playerItemInventory.TryAddPickupQuantity(
                AssetDatabase.LoadAssetAtPath<AmmoDefinition>("Assets/Items/Definitions/Ammo_Pistol.asset"),
                1);
            Require(!fullPickup.Succeeded && fullPickup.AcceptedQuantity == 0 && fullPickup.Failure == InventoryOperationFailure.NoSpace,
                "A full carry grid should reject a pickup without changing its item quantity.");

            capacityUpgradeDefinition = ScriptableObject.CreateInstance<InventoryCapacityUpgradeDefinition>();
            SerializedObject serializedCapacityDefinition = new SerializedObject(capacityUpgradeDefinition);
            serializedCapacityDefinition.FindProperty("stableItemId").stringValue = "test.capacity.case-stage";
            serializedCapacityDefinition.FindProperty("displayName").stringValue = "Case Expansion";
            serializedCapacityDefinition.ApplyModifiedPropertiesWithoutUndo();
            int itemCountBeforeCapacityPickups = playerItemInventory.Inventory.Count;
            int[] expectedCaseHeights = { ItemInventory.CaseStage2Height, ItemInventory.CaseStage3Height, ItemInventory.CaseStage4Height };
            for (int index = 0; index < expectedCaseHeights.Length; index++)
            {
                InventoryOperationResult capacityPickup = playerItemInventory.TryAddPickupQuantity(capacityUpgradeDefinition, 1);
                Require(capacityPickup.Completed && playerItemInventory.Inventory.GridWidth == ItemInventory.CaseGridWidth &&
                        playerItemInventory.Inventory.GridHeight == expectedCaseHeights[index],
                    "A capacity pickup should advance exactly one supported case stage.");
                Require(playerItemInventory.Inventory.Count == itemCountBeforeCapacityPickups,
                    "A capacity pickup should apply immediately without creating an inventory item.");
            }

            InventoryOperationResult maximumCapacityPickup = playerItemInventory.TryAddPickupQuantity(capacityUpgradeDefinition, 1);
            Require(!maximumCapacityPickup.Succeeded && maximumCapacityPickup.Failure == InventoryOperationFailure.MaximumCapacity &&
                    playerItemInventory.Inventory.GridHeight == ItemInventory.CaseStage4Height &&
                    playerItemInventory.Inventory.Count == itemCountBeforeCapacityPickups,
                "A pickup at maximum capacity should be rejected without changing the case or item count.");

            keyItemDefinition = ScriptableObject.CreateInstance<KeyItemDefinition>();
            SerializedObject serializedKeyDefinition = new SerializedObject(keyItemDefinition);
            serializedKeyDefinition.FindProperty("stableItemId").stringValue = "test.keyitem.pickup-check";
            serializedKeyDefinition.FindProperty("displayName").stringValue = "Pickup Test Key";
            serializedKeyDefinition.FindProperty("questOrKeyId").stringValue = "test.lock.pickup-check";
            serializedKeyDefinition.ApplyModifiedPropertiesWithoutUndo();

            int carryCountBeforeKey = playerItemInventory.Inventory.Count;
            InventoryOperationResult keyPickup = playerItemInventory.TryAddPickupQuantity(keyItemDefinition, 1);
            Require(keyPickup.Completed && playerItemInventory.KeyItems.Count == 1,
                "Key pickups should be accepted into the separate protected key-item list.");
            Require(playerItemInventory.Inventory.Count == carryCountBeforeKey,
                "Key pickups must not consume carry-grid cells.");

            pickupObject = new GameObject("InventoryPickupSuccessCheck");
            pickupObject.AddComponent<CircleCollider2D>();
            WorldItemPickup worldItemPickup = pickupObject.AddComponent<WorldItemPickup>();
            SerializedObject serializedPickup = new SerializedObject(worldItemPickup);
            serializedPickup.FindProperty("itemDefinition").objectReferenceValue = keyItemDefinition;
            serializedPickup.FindProperty("quantity").intValue = 1;
            serializedPickup.ApplyModifiedPropertiesWithoutUndo();
            typeof(WorldItemPickup).GetField("playerInRange", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(worldItemPickup, testPlayer);

            int keyItemsBeforeWorldPickup = playerItemInventory.KeyItems.Count;
            worldItemPickup.Interact();
            Require(!pickupObject.activeSelf && playerItemInventory.KeyItems.Count == keyItemsBeforeWorldPickup + 1,
                "A successful world pickup should transfer exactly one key item and deactivate itself.");
            worldItemPickup.Interact();
            Require(playerItemInventory.KeyItems.Count == keyItemsBeforeWorldPickup + 1,
                "Repeated interaction callbacks after collection must not duplicate ownership.");

            disabledPickupObject = new GameObject("InventoryDisabledPickupCheck");
            disabledPickupObject.AddComponent<CircleCollider2D>();
            WorldItemPickup disabledPickup = disabledPickupObject.AddComponent<WorldItemPickup>();
            SerializedObject serializedDisabledPickup = new SerializedObject(disabledPickup);
            serializedDisabledPickup.FindProperty("itemDefinition").objectReferenceValue = keyItemDefinition;
            serializedDisabledPickup.FindProperty("quantity").intValue = 1;
            serializedDisabledPickup.ApplyModifiedPropertiesWithoutUndo();
            typeof(WorldItemPickup).GetField("playerInRange", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(disabledPickup, testPlayer);
            disabledPickup.enabled = false;
            disabledPickup.Interact();
            Require(playerItemInventory.KeyItems.Count == keyItemsBeforeWorldPickup + 1,
                "A disabled world pickup must not be collected by a direct or stale interaction callback.");
        }
        finally
        {
            if (disabledPickupObject != null)
            {
                UnityEngine.Object.DestroyImmediate(disabledPickupObject);
            }

            if (pickupObject != null)
            {
                UnityEngine.Object.DestroyImmediate(pickupObject);
            }

            if (keyItemDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(keyItemDefinition);
            }

            if (capacityUpgradeDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(capacityUpgradeDefinition);
            }

            UnityEngine.Object.DestroyImmediate(temporaryPlayer);
        }
    }

    private static AmmoDefinition CreateAmmoDefinition(
        string stableItemId,
        WeaponDefinition.AmmunitionType ammunitionType,
        string compatibilityKey,
        int width,
        int height,
        bool canRotate,
        int maximumStackSize)
    {
        AmmoDefinition definition = ScriptableObject.CreateInstance<AmmoDefinition>();
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("stableItemId").stringValue = stableItemId;
        serializedDefinition.FindProperty("displayName").stringValue = stableItemId;
        serializedDefinition.FindProperty("inventoryWidth").intValue = width;
        serializedDefinition.FindProperty("inventoryHeight").intValue = height;
        serializedDefinition.FindProperty("canRotate").boolValue = canRotate;
        serializedDefinition.FindProperty("maximumStackSize").intValue = maximumStackSize;
        serializedDefinition.FindProperty("itemAmmunitionType").enumValueIndex = (int)ammunitionType;
        serializedDefinition.FindProperty("ammunitionStackCompatibilityKey").stringValue = compatibilityKey;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static WeaponDefinition CreateWeaponDefinition()
    {
        WeaponDefinition definition = ScriptableObject.CreateInstance<WeaponDefinition>();
        SerializedObject serializedDefinition = new SerializedObject(definition);
        serializedDefinition.FindProperty("stableItemId").stringValue = "test.weapon.independent-magazines";
        serializedDefinition.FindProperty("displayName").stringValue = "Inventory Model Test Weapon";
        serializedDefinition.FindProperty("magazineCapacity").intValue = 20;
        serializedDefinition.FindProperty("upgradeOptions").arraySize = 0;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static void Require(bool condition, string failureMessage)
    {
        if (!condition)
        {
            throw new InvalidOperationException(failureMessage);
        }
    }
}
#endif
