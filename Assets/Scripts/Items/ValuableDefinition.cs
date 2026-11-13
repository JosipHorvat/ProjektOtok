using UnityEngine;

/// <summary>Static appraisal and merchant data for valuable items.</summary>
[CreateAssetMenu(fileName = "ValuableDefinition", menuName = "Items/Valuable Definition")]
public sealed class ValuableDefinition : ItemDefinition
{
    [Header("Valuable")]
    [Tooltip("Base appraisal amount for future merchant rules.")]
    [Min(0)]
    [SerializeField] private int baseValue;
    [Tooltip("Stable appraisal category ID used for future price modifiers.")]
    [SerializeField] private string appraisalCategoryId = string.Empty;

    /// <summary>Gets the valuable category.</summary>
    public override ItemCategory Category => ItemCategory.Valuable;

    /// <summary>Gets the base appraisal amount.</summary>
    public int BaseValue => baseValue;

    /// <summary>Gets the appraisal category identifier.</summary>
    public string AppraisalCategoryId => appraisalCategoryId;

    /// <summary>Clamps the authored appraisal value.</summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        baseValue = Mathf.Max(0, baseValue);
    }
}
