using UnityEngine;

/// <summary>Static grid-dimension increments provided by a found bag, case, or other capacity upgrade.</summary>
[CreateAssetMenu(fileName = "InventoryCapacityUpgradeDefinition", menuName = "Items/Inventory Capacity Upgrade Definition")]
public sealed class InventoryCapacityUpgradeDefinition : ItemDefinition
{
    [Header("Carry-grid capacity increase")]
    [Tooltip("Number of columns added to the configured carry grid when this upgrade is applied.")]
    [Min(0)]
    [SerializeField] private int widthIncrease = 1;
    [Tooltip("Number of rows added to the configured carry grid when this upgrade is applied.")]
    [Min(0)]
    [SerializeField] private int heightIncrease;

    /// <summary>Gets the capacity-upgrade category.</summary>
    public override ItemCategory Category => ItemCategory.CapacityUpgrade;

    /// <summary>Gets the configured number of columns added by this upgrade.</summary>
    public int WidthIncrease => widthIncrease;

    /// <summary>Gets the configured number of rows added by this upgrade.</summary>
    public int HeightIncrease => heightIncrease;

    /// <summary>Ensures this definition increases at least one grid dimension.</summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        widthIncrease = Mathf.Max(0, widthIncrease);
        heightIncrease = Mathf.Max(0, heightIncrease);
        if (widthIncrease == 0 && heightIncrease == 0)
        {
            Debug.LogWarning($"Capacity upgrade '{name}' must increase grid width, height, or both.", this);
        }
    }
}
