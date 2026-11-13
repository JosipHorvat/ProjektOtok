using UnityEngine;

/// <summary>Static throw configuration for throwable weapons such as grenades.</summary>
[CreateAssetMenu(fileName = "ThrowableWeaponDefinition", menuName = "Items/Throwable Weapon Definition")]
public sealed class ThrowableWeaponDefinition : ItemDefinition
{
    [Header("Throwable combat")]
    [Tooltip("Projectile or thrown-object prefab spawned by a future throwable handler.")]
    [SerializeField] private GameObject thrownPrefab;
    [Tooltip("Damage applied by the throwable on impact or detonation.")]
    [Min(0f)]
    [SerializeField] private float damage;
    [Tooltip("Time between throwing requests, in seconds.")]
    [Min(0.01f)]
    [SerializeField] private float useCooldown = 0.5f;
    [Tooltip("Optional fuse duration; zero means the prefab or handler controls detonation timing.")]
    [Min(0f)]
    [SerializeField] private float fuseDuration;
    [Tooltip("Optional effect radius for the throwable.")]
    [Min(0f)]
    [SerializeField] private float effectRadius;

    /// <summary>Gets the weapon category.</summary>
    public override ItemCategory Category => ItemCategory.Weapon;

    /// <summary>Gets the thrown-object prefab.</summary>
    public GameObject ThrownPrefab => thrownPrefab;

    /// <summary>Gets throwable damage.</summary>
    public float Damage => damage;

    /// <summary>Gets the minimum time between throws.</summary>
    public float UseCooldown => useCooldown;

    /// <summary>Gets the configured fuse duration.</summary>
    public float FuseDuration => fuseDuration;

    /// <summary>Gets the effect radius.</summary>
    public float EffectRadius => effectRadius;

    /// <summary>Clamps authored throwable values.</summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        damage = Mathf.Max(0f, damage);
        useCooldown = Mathf.Max(0.01f, useCooldown);
        fuseDuration = Mathf.Max(0f, fuseDuration);
        effectRadius = Mathf.Max(0f, effectRadius);
    }
}
