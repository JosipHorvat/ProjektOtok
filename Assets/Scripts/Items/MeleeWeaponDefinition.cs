using UnityEngine;

/// <summary>Static melee-specific data kept separate from ranged weapon firing configuration.</summary>
[CreateAssetMenu(fileName = "MeleeWeaponDefinition", menuName = "Items/Melee Weapon Definition")]
public sealed class MeleeWeaponDefinition : ItemDefinition
{
    [Header("Melee combat")]
    [Tooltip("Damage applied by a successful melee hit.")]
    [Min(0f)]
    [SerializeField] private float damage = 10f;
    [Tooltip("Maximum effective melee reach in world units.")]
    [Min(0.01f)]
    [SerializeField] private float attackRange = 1.5f;
    [Tooltip("Time between permitted attacks, in seconds.")]
    [Min(0.01f)]
    [SerializeField] private float attackCooldown = 0.6f;
    [Tooltip("Optional world-space hit volume prefab used by a future melee handler.")]
    [SerializeField] private GameObject hitVolumePrefab;
    [Tooltip("Optional animation trigger used by a future melee handler.")]
    [SerializeField] private string attackAnimationTrigger = "Attack";

    /// <summary>Gets the weapon category.</summary>
    public override ItemCategory Category => ItemCategory.Weapon;

    /// <summary>Gets melee hit damage.</summary>
    public float Damage => damage;

    /// <summary>Gets melee attack reach.</summary>
    public float AttackRange => attackRange;

    /// <summary>Gets the minimum time between attacks.</summary>
    public float AttackCooldown => attackCooldown;

    /// <summary>Gets the optional hit-volume prefab.</summary>
    public GameObject HitVolumePrefab => hitVolumePrefab;

    /// <summary>Gets the optional melee animation trigger.</summary>
    public string AttackAnimationTrigger => attackAnimationTrigger;

    /// <summary>Clamps authored melee combat values.</summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        damage = Mathf.Max(0f, damage);
        attackRange = Mathf.Max(0.01f, attackRange);
        attackCooldown = Mathf.Max(0.01f, attackCooldown);
    }
}
