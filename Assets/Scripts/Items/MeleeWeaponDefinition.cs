using UnityEngine;

/// <summary>Static melee-specific data kept separate from ranged weapon firing configuration.</summary>
[CreateAssetMenu(fileName = "MeleeWeaponDefinition", menuName = "Items/Melee Weapon Definition")]
public sealed class MeleeWeaponDefinition : ItemDefinition
{
    [Header("Melee combat")]
    [Tooltip("Damage applied by a successful melee hit.")]
    [Min(0f)]
    [SerializeField] private float damage = 10f;
    [Tooltip("Chance for a successful melee hit to critically strike, from 0 to 1.")]
    [Range(0f, 1f)]
    [SerializeField] private float criticalChance = 0.08f;
    [Tooltip("Damage multiplier applied when a melee hit is critical.")]
    [Min(1f)]
    [SerializeField] private float criticalMultiplier = 1.5f;
    [Tooltip("Poise added by a successful hit. Stronger melee weapons can stagger in fewer hits.")]
    [Min(0f)]
    [SerializeField] private float staggerImpact = 18f;
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

    /// <summary>Gets the probability that a successful hit is critical.</summary>
    public float CriticalChance => criticalChance;

    /// <summary>Gets the damage multiplier applied to a critical hit.</summary>
    public float CriticalMultiplier => criticalMultiplier;
    public float Damage => damage;

    /// <summary>Gets poise impact from a successful melee hit.</summary>
    public float StaggerImpact => staggerImpact;

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
        criticalChance = Mathf.Clamp01(criticalChance);
        criticalMultiplier = Mathf.Max(1f, criticalMultiplier);
        staggerImpact = Mathf.Max(0f, staggerImpact);
        attackRange = Mathf.Max(0.01f, attackRange);
        attackCooldown = Mathf.Max(0.01f, attackCooldown);
    }
}
