using UnityEngine;

/// <summary>Static data for reusable utility tools that are neither crafting tools nor weapons.</summary>
[CreateAssetMenu(fileName = "UtilityToolDefinition", menuName = "Items/Utility Tool Definition")]
public sealed class UtilityToolDefinition : ItemDefinition
{
    [Header("Utility tool")]
    [Tooltip("Stable behavior identifier resolved by a future utility-item handler.")]
    [SerializeField] private string utilityActionId = string.Empty;
    [Tooltip("Optional maximum charges per owned tool instance; zero means uncharged/reusable.")]
    [Min(0)]
    [SerializeField] private int maximumCharges;
    [Tooltip("Whether using the utility consumes one charge.")]
    [SerializeField] private bool consumesCharge;

    /// <summary>Gets the tool category.</summary>
    public override ItemCategory Category => ItemCategory.UtilityTool;

    /// <summary>Gets the stable utility behavior identifier.</summary>
    public string UtilityActionId => utilityActionId;

    /// <summary>Gets the maximum charge count.</summary>
    public int MaximumCharges => maximumCharges;

    /// <summary>Gets whether the utility consumes charges.</summary>
    public bool ConsumesCharge => consumesCharge;

    /// <summary>Clamps authored utility charge data.</summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        maximumCharges = Mathf.Max(0, maximumCharges);
    }
}
