using UnityEngine;

/// <summary>Shared static data for an ammunition item and its reserve-ammo compatibility.</summary>
[CreateAssetMenu(fileName = "AmmoDefinition", menuName = "Items/Ammo Definition")]
public sealed class AmmoDefinition : ItemDefinition
{
    [Header("Ammunition")]
    [Tooltip("Reserve-ammo pool used by compatible weapon definitions.")]
    [SerializeField] private WeaponDefinition.AmmunitionType itemAmmunitionType;
    [Tooltip("Optional stack group for compatible ammo variants. Empty uses the ammo type name.")]
    [SerializeField] private string ammunitionStackCompatibilityKey = string.Empty;

    /// <summary>Gets the ammunition category.</summary>
    public override ItemCategory Category => ItemCategory.Ammunition;

    /// <summary>Gets the reserve-ammo type used by weapon reloads.</summary>
    public WeaponDefinition.AmmunitionType AmmunitionType => itemAmmunitionType;

    /// <summary>Gets the effective key used to decide whether ammo stacks may merge.</summary>
    public string StackCompatibilityKey => string.IsNullOrWhiteSpace(ammunitionStackCompatibilityKey)
        ? itemAmmunitionType.ToString()
        : ammunitionStackCompatibilityKey;

    /// <summary>Validates the authored ammunition type and stack configuration.</summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        if (!System.Enum.IsDefined(typeof(WeaponDefinition.AmmunitionType), itemAmmunitionType) ||
            itemAmmunitionType == WeaponDefinition.AmmunitionType.None)
        {
            Debug.LogWarning($"Ammo definition '{name}' needs a valid ammunition type.", this);
        }

        if (MaximumStackSize <= 1)
        {
            Debug.LogWarning($"Ammo definition '{name}' should have a stack limit greater than one.", this);
        }
    }
}
