using UnityEngine;

/// <summary>Static identity and crafting-group data for stackable crafting materials.</summary>
[CreateAssetMenu(fileName = "CraftingMaterialDefinition", menuName = "Items/Crafting Material Definition")]
public sealed class CraftingMaterialDefinition : ItemDefinition
{
    [Header("Crafting material")]
    [Tooltip("Stable material group used by future recipes; empty means recipes match this definition ID only.")]
    [SerializeField] private string materialGroupId = string.Empty;
    [Tooltip("Optional quality or grade used by future recipe rules.")]
    [SerializeField] private int materialGrade;

    /// <summary>Gets the material category.</summary>
    public override ItemCategory Category => ItemCategory.CraftingMaterial;

    /// <summary>Gets the recipe material group ID.</summary>
    public string MaterialGroupId => string.IsNullOrWhiteSpace(materialGroupId) ? StableItemId : materialGroupId;

    /// <summary>Gets the material quality grade.</summary>
    public int MaterialGrade => materialGrade;

    /// <summary>Clamps authored material quality data.</summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        materialGrade = Mathf.Max(0, materialGrade);
    }
}
