using UnityEngine;

/// <summary>Static use data for a healing or other health-recovery consumable.</summary>
[CreateAssetMenu(fileName = "HealingItemDefinition", menuName = "Items/Healing Item Definition")]
public sealed class HealingItemDefinition : ItemDefinition
{
    [Header("Healing")]
    [Tooltip("Amount of health restored by using one item.")]
    [Min(0)]
    [SerializeField] private int healthRestored = 25;
    [Tooltip("Whether one unit is consumed when use succeeds.")]
    [SerializeField] private bool consumedOnUse = true;
    [Tooltip("Whether the item may be used while the player is at full health.")]
    [SerializeField] private bool usableAtFullHealth;

    /// <summary>Gets the consumable category.</summary>
    public override ItemCategory Category => ItemCategory.Consumable;

    /// <summary>Gets the amount of health restored.</summary>
    public int HealthRestored => healthRestored;

    /// <summary>Gets whether a successful use consumes one unit.</summary>
    public bool ConsumedOnUse => consumedOnUse;

    /// <summary>Gets whether the item may be used at full health.</summary>
    public bool UsableAtFullHealth => usableAtFullHealth;

    /// <summary>Clamps the authored healing amount.</summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        healthRestored = Mathf.Max(0, healthRestored);
    }
}
