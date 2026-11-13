using UnityEngine;

/// <summary>Static capability and durability data for tools used by future recipes or stations.</summary>
[CreateAssetMenu(fileName = "CraftingToolDefinition", menuName = "Items/Crafting Tool Definition")]
public sealed class CraftingToolDefinition : ItemDefinition
{
    [Header("Crafting tool")]
    [Tooltip("Stable capability identifier checked by future crafting recipes.")]
    [SerializeField] private string craftingCapabilityId = string.Empty;
    [Tooltip("Whether this tool loses durability when a recipe uses it.")]
    [SerializeField] private bool consumesDurability;
    [Tooltip("Maximum durability for one owned tool instance when durability is enabled.")]
    [Min(1)]
    [SerializeField] private int maximumDurability = 1;

    /// <summary>Gets the tool category.</summary>
    public override ItemCategory Category => ItemCategory.CraftingTool;

    /// <summary>Gets the recipe capability identifier.</summary>
    public string CraftingCapabilityId => craftingCapabilityId;

    /// <summary>Gets whether the tool loses durability on use.</summary>
    public bool ConsumesDurability => consumesDurability;

    /// <summary>Gets maximum durability when durability is enabled.</summary>
    public int MaximumDurability => maximumDurability;

    /// <summary>Clamps authored durability data.</summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        maximumDurability = Mathf.Max(1, maximumDurability);
    }
}
