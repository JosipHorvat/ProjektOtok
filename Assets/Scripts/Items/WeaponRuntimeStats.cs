using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Resolved combat values for one owned weapon, calculated without changing its shared definition.</summary>
public readonly struct WeaponRuntimeStats
{
    private const float MinimumShotsPerSecond = 0.01f;
    private const string AccuracyModifierId = "Accuracy";
    private const string FireRateModifierId = "FireRate";

    private WeaponRuntimeStats(float shotsPerSecond, float hipFireSpread, float aimedSpread, float maximumAimedSpread)
    {
        ShotsPerSecond = shotsPerSecond;
        HipFireSpread = hipFireSpread;
        AimedSpread = aimedSpread;
        MaximumAimedSpread = maximumAimedSpread;
    }

    /// <summary>Gets the final shots-per-second value after supported upgrades and attachments.</summary>
    public float ShotsPerSecond { get; }

    /// <summary>Gets the final hip-fire spread in degrees after Accuracy modifiers.</summary>
    public float HipFireSpread { get; }

    /// <summary>Gets the final aimed spread in degrees after Accuracy modifiers.</summary>
    public float AimedSpread { get; }

    /// <summary>Gets the final maximum aimed spread in degrees after Accuracy modifiers.</summary>
    public float MaximumAimedSpread { get; }

    /// <summary>Resolves Accuracy and FireRate for an owned weapon and its installed attachment instances.</summary>
    /// <param name="definition">The shared weapon definition.</param>
    /// <param name="weaponInstance">The owned weapon instance whose upgrade levels and attachment references apply.</param>
    /// <param name="installedAttachments">Owned attachment instances resolved from the player's installed-attachment container.</param>
    /// <returns>Resolved combat values; base definition values are returned when the owned instance is not a match.</returns>
    public static WeaponRuntimeStats Resolve(
        WeaponDefinition definition,
        ItemInstance weaponInstance,
        IReadOnlyList<ItemInstance> installedAttachments)
    {
        if (definition == null)
        {
            return default;
        }

        float fireRateAdjustment = 0f;
        float accuracySpreadReduction = 0f;
        bool isMatchingWeapon = weaponInstance != null && ReferenceEquals(weaponInstance.Definition, definition);
        if (isMatchingWeapon)
        {
            if (TryGetUniqueUpgradeOption(definition, WeaponUpgradeType.FireRate, out WeaponUpgradeOption fireRateOption))
            {
                fireRateAdjustment += fireRateOption.valuePerLevel * weaponInstance.GetUpgradeLevel(WeaponUpgradeType.FireRate);
            }

            if (TryGetUniqueUpgradeOption(definition, WeaponUpgradeType.Accuracy, out WeaponUpgradeOption accuracyOption))
            {
                accuracySpreadReduction += accuracyOption.valuePerLevel * weaponInstance.GetUpgradeLevel(WeaponUpgradeType.Accuracy);
            }

            if (installedAttachments != null)
            {
                for (int index = 0; index < installedAttachments.Count; index++)
                {
                    ItemInstance attachmentInstance = installedAttachments[index];
                    if (attachmentInstance == null ||
                        !ContainsAttachmentId(weaponInstance.InstalledAttachmentInstanceIds, attachmentInstance.InstanceId) ||
                        !(attachmentInstance.Definition is WeaponAttachmentDefinition attachmentDefinition))
                    {
                        continue;
                    }

                    if (string.Equals(attachmentDefinition.ModifierId, FireRateModifierId, StringComparison.OrdinalIgnoreCase))
                    {
                        fireRateAdjustment += attachmentDefinition.ModifierValue;
                    }
                    else if (string.Equals(attachmentDefinition.ModifierId, AccuracyModifierId, StringComparison.OrdinalIgnoreCase))
                    {
                        accuracySpreadReduction += attachmentDefinition.ModifierValue;
                    }
                }
            }
        }

        float aimedSpread = Mathf.Max(0f, definition.AimedSpread - accuracySpreadReduction);
        float maximumAimedSpread = Mathf.Max(aimedSpread, definition.MaximumAimedSpread - accuracySpreadReduction);
        return new WeaponRuntimeStats(
            Mathf.Max(MinimumShotsPerSecond, definition.ShotsPerSecond + fireRateAdjustment),
            Mathf.Max(0f, definition.HipFireSpread - accuracySpreadReduction),
            aimedSpread,
            maximumAimedSpread);
    }

    private static bool ContainsAttachmentId(IReadOnlyList<string> attachmentIds, string targetId)
    {
        if (attachmentIds == null || string.IsNullOrWhiteSpace(targetId))
        {
            return false;
        }

        for (int index = 0; index < attachmentIds.Count; index++)
        {
            if (string.Equals(attachmentIds[index], targetId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetUniqueUpgradeOption(
        WeaponDefinition definition,
        WeaponUpgradeType upgradeType,
        out WeaponUpgradeOption option)
    {
        option = default;
        WeaponUpgradeOption[] authoredOptions = definition != null ? definition.upgradeOptions : null;
        if (authoredOptions == null)
        {
            return false;
        }

        bool found = false;
        for (int index = 0; index < authoredOptions.Length; index++)
        {
            if (authoredOptions[index].upgradeType != upgradeType)
            {
                continue;
            }

            if (found)
            {
                return false;
            }

            option = authoredOptions[index];
            found = true;
        }

        return found && option.maximumLevel > 0;
    }
}
