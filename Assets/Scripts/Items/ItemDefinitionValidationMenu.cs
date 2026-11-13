#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Provides an editor command for validating authored item definitions and cross-asset links.</summary>
internal static class ItemDefinitionValidationMenu
{
    private static readonly string[] ItemDefinitionTypes =
    {
        "ItemDefinition",
        "WeaponDefinition",
        "MeleeWeaponDefinition",
        "AmmoDefinition",
        "HealingItemDefinition",
        "ThrowableWeaponDefinition",
        "DeployableDefinition",
        "CraftingMaterialDefinition",
        "CraftingToolDefinition",
        "UtilityToolDefinition",
        "WeaponAttachmentDefinition",
        "ValuableDefinition",
        "KeyItemDefinition",
        "InventoryCapacityUpgradeDefinition",
    };

    [MenuItem("Tools/Items/Validate Item Definitions")]
    private static void ValidateAllDefinitions()
    {
        HashSet<string> guids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string typeName in ItemDefinitionTypes)
        {
            string[] typeGuids = AssetDatabase.FindAssets($"t:{typeName}");
            foreach (string guid in typeGuids)
            {
                guids.Add(guid);
            }
        }

        Dictionary<string, ItemDefinition> definitionsById = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        HashSet<WeaponDefinition> weaponDefinitionsWithPrefabs = FindWeaponDefinitionsUsedByPrefabs();
        int checkedCount = 0;
        int errorCount = 0;

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
            if (definition == null)
            {
                continue;
            }

            checkedCount++;
            ValidateDefinitionType(definition, assetPath, ref errorCount);
            ValidateCommonData(definition, assetPath, definitionsById, ref errorCount);
            ValidateSpecializedData(definition, assetPath, weaponDefinitionsWithPrefabs, ref errorCount);
        }

        ValidateRangedWeaponAmmoCompatibility(definitionsById, ref errorCount);
        if (errorCount == 0)
        {
            Debug.Log($"Item definition validation passed: {checkedCount} asset(s) checked, no errors.");
        }
        else
        {
            Debug.LogError($"Item definition validation found {errorCount} error(s) across {checkedCount} asset(s).");
        }
    }

    private static void ValidateDefinitionType(ItemDefinition definition, string assetPath, ref int errorCount)
    {
        bool validType =
            (definition is WeaponDefinition && definition.Category == ItemCategory.Weapon) ||
            (definition is MeleeWeaponDefinition && definition.Category == ItemCategory.Weapon) ||
            (definition is ThrowableWeaponDefinition && definition.Category == ItemCategory.Weapon) ||
            (definition is AmmoDefinition && definition.Category == ItemCategory.Ammunition) ||
            (definition is HealingItemDefinition && definition.Category == ItemCategory.Consumable) ||
            (definition is DeployableDefinition && definition.Category == ItemCategory.Deployable) ||
            (definition is CraftingMaterialDefinition && definition.Category == ItemCategory.CraftingMaterial) ||
            (definition is CraftingToolDefinition && definition.Category == ItemCategory.CraftingTool) ||
            (definition is UtilityToolDefinition && definition.Category == ItemCategory.UtilityTool) ||
            (definition is WeaponAttachmentDefinition && definition.Category == ItemCategory.Attachment) ||
            (definition is ValuableDefinition && definition.Category == ItemCategory.Valuable) ||
            (definition is KeyItemDefinition && (definition.Category == ItemCategory.KeyItem || definition.Category == ItemCategory.QuestItem)) ||
            (definition is InventoryCapacityUpgradeDefinition && definition.Category == ItemCategory.CapacityUpgrade);

        if (!validType)
        {
            Debug.LogError($"Item definition '{assetPath}' has an unsupported definition type/category combination.", definition);
            errorCount++;
        }
    }

    private static void ValidateCommonData(
        ItemDefinition definition,
        string assetPath,
        Dictionary<string, ItemDefinition> definitionsById,
        ref int errorCount)
    {
        if (string.IsNullOrWhiteSpace(definition.StableItemId))
        {
            Debug.LogError($"Item definition '{assetPath}' has no stable item ID.", definition);
            errorCount++;
        }
        else if (definitionsById.TryGetValue(definition.StableItemId, out ItemDefinition duplicate))
        {
            Debug.LogError(
                $"Duplicate item ID '{definition.StableItemId}' is used by '{AssetDatabase.GetAssetPath(duplicate)}' and '{assetPath}'.",
                definition);
            errorCount++;
        }
        else
        {
            definitionsById.Add(definition.StableItemId, definition);
        }

        if (definition.InventoryIcon == null)
        {
            Debug.LogError($"Item definition '{assetPath}' has no inventory icon.", definition);
            errorCount++;
        }

        if (definition.InventoryWidth < 1 || definition.InventoryHeight < 1 || definition.MaximumStackSize < 1)
        {
            Debug.LogError($"Item definition '{assetPath}' has an invalid footprint or stack limit.", definition);
            errorCount++;
        }
    }

    private static void ValidateSpecializedData(
        ItemDefinition definition,
        string assetPath,
        HashSet<WeaponDefinition> weaponDefinitionsWithPrefabs,
        ref int errorCount)
    {
        if (definition is WeaponDefinition weaponDefinition)
        {
            if (!Enum.IsDefined(typeof(WeaponDefinition.AmmunitionType), weaponDefinition.AmmoType) ||
                weaponDefinition.AmmoType == WeaponDefinition.AmmunitionType.None)
            {
                Debug.LogError($"Weapon definition '{assetPath}' has no valid ammunition type.", definition);
                errorCount++;
            }

            if (weaponDefinition.ProjectilePrefab == null)
            {
                Debug.LogError($"Weapon definition '{assetPath}' has no projectile prefab.", definition);
                errorCount++;
            }

            if (!IsTestOnly(definition) && !weaponDefinitionsWithPrefabs.Contains(weaponDefinition))
            {
                Debug.LogError($"Production weapon definition '{assetPath}' is not referenced by a HeldWeapon prefab under Assets/Prefabs/Weapons.", definition);
                errorCount++;
            }

            ValidateUpgradeOptions(weaponDefinition, assetPath, ref errorCount);
        }

        if (definition is AmmoDefinition ammoDefinition)
        {
            if (!Enum.IsDefined(typeof(WeaponDefinition.AmmunitionType), ammoDefinition.AmmunitionType) ||
                ammoDefinition.AmmunitionType == WeaponDefinition.AmmunitionType.None)
            {
                Debug.LogError($"Ammo definition '{assetPath}' has no valid ammunition type.", definition);
                errorCount++;
            }

            if (ammoDefinition.MaximumStackSize <= 1 || string.IsNullOrWhiteSpace(ammoDefinition.StackCompatibilityKey))
            {
                Debug.LogError($"Ammo definition '{assetPath}' needs a stack limit greater than one and a compatibility key.", definition);
                errorCount++;
            }
        }

        if (definition is HealingItemDefinition healingDefinition && healingDefinition.HealthRestored <= 0)
        {
            Debug.LogError($"Healing item '{assetPath}' must restore a positive health amount.", definition);
            errorCount++;
        }

        if (definition is KeyItemDefinition keyItemDefinition && string.IsNullOrWhiteSpace(keyItemDefinition.QuestOrKeyId))
        {
            Debug.LogError($"Key/quest item '{assetPath}' needs a stable quest/key identifier.", definition);
            errorCount++;
        }

        if (definition is CraftingToolDefinition craftingToolDefinition &&
            string.IsNullOrWhiteSpace(craftingToolDefinition.CraftingCapabilityId))
        {
            Debug.LogError($"Crafting tool '{assetPath}' needs a capability ID.", definition);
            errorCount++;
        }

        if (definition is UtilityToolDefinition utilityToolDefinition &&
            string.IsNullOrWhiteSpace(utilityToolDefinition.UtilityActionId))
        {
            Debug.LogError($"Utility tool '{assetPath}' needs a utility action ID.", definition);
            errorCount++;
        }

        if (definition is WeaponAttachmentDefinition attachmentDefinition &&
            string.IsNullOrWhiteSpace(attachmentDefinition.AttachmentSlotId))
        {
            Debug.LogError($"Weapon attachment '{assetPath}' needs an attachment slot ID.", definition);
            errorCount++;
        }

        if (definition is InventoryCapacityUpgradeDefinition capacityUpgrade &&
            capacityUpgrade.WidthIncrease == 0 && capacityUpgrade.HeightIncrease == 0)
        {
            Debug.LogError($"Capacity upgrade '{assetPath}' must increase width, height, or both.", definition);
            errorCount++;
        }
    }

    private static void ValidateUpgradeOptions(WeaponDefinition weaponDefinition, string assetPath, ref int errorCount)
    {
        WeaponUpgradeOption[] options = weaponDefinition.upgradeOptions;
        if (options == null)
        {
            return;
        }

        for (int optionIndex = 0; optionIndex < options.Length; optionIndex++)
        {
            for (int otherIndex = optionIndex + 1; otherIndex < options.Length; otherIndex++)
            {
                if (options[optionIndex].upgradeType == options[otherIndex].upgradeType)
                {
                    Debug.LogError($"Weapon definition '{assetPath}' has duplicate '{options[optionIndex].upgradeType}' upgrade options.", weaponDefinition);
                    errorCount++;
                    break;
                }
            }
        }
    }

    private static void ValidateRangedWeaponAmmoCompatibility(
        Dictionary<string, ItemDefinition> definitionsById,
        ref int errorCount)
    {
        foreach (ItemDefinition definition in definitionsById.Values)
        {
            if (!(definition is WeaponDefinition weaponDefinition) || IsTestOnly(definition))
            {
                continue;
            }

            bool hasCompatibleAmmo = false;
            foreach (ItemDefinition candidate in definitionsById.Values)
            {
                if (candidate is AmmoDefinition ammoDefinition && ammoDefinition.AmmunitionType == weaponDefinition.AmmoType)
                {
                    hasCompatibleAmmo = true;
                    break;
                }
            }

            if (!hasCompatibleAmmo)
            {
                Debug.LogError($"Production weapon '{definition.StableItemId}' has no AmmoDefinition for '{weaponDefinition.AmmoType}'.", definition);
                errorCount++;
            }
        }
    }

    private static HashSet<WeaponDefinition> FindWeaponDefinitionsUsedByPrefabs()
    {
        HashSet<WeaponDefinition> weaponDefinitions = new HashSet<WeaponDefinition>();
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Weapons" });
        foreach (string prefabGuid in prefabGuids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                continue;
            }

            HeldWeapon[] heldWeapons = prefab.GetComponentsInChildren<HeldWeapon>(true);
            foreach (HeldWeapon heldWeapon in heldWeapons)
            {
                if (heldWeapon != null && heldWeapon.Definition != null)
                {
                    weaponDefinitions.Add(heldWeapon.Definition);
                }
            }
        }

        return weaponDefinitions;
    }

    private static bool IsTestOnly(ItemDefinition definition)
    {
        string[] tags = definition.Tags;
        if (tags == null)
        {
            return false;
        }

        foreach (string tag in tags)
        {
            if (string.Equals(tag, "test-only", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
#endif
