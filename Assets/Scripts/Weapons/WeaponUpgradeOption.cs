using UnityEngine;

/// <summary>Identifies a weapon stat that may be adjusted by a later permanent upgrade.</summary>
public enum WeaponUpgradeType
{
    Damage = 0,
    MagazineCapacity = 1,
    FireRate = 2,
    ReloadSpeed = 3,
    Accuracy = 4,
    Recoil = 5,
    Range = 6,
    Pierce = 7,
}

/// <summary>Static upgrade capability authored on a weapon definition; owned upgrade level is saved per item instance.</summary>
[System.Serializable]
public struct WeaponUpgradeOption
{
    [Tooltip("Weapon stat this definition permits to be upgraded.")]
    public WeaponUpgradeType upgradeType;
    [Tooltip("Maximum level supported by this weapon definition.")]
    [Min(1)]
    public int maximumLevel;
    [Tooltip("Configured value change per level; interpretation and units belong to the future upgrade system.")]
    public float valuePerLevel;
}
