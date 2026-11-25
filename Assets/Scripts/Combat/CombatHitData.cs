using UnityEngine;

/// <summary>Resolved damage and context for one weapon hit, including its region multiplier and critical result.</summary>
public readonly struct CombatHitData
{
    /// <summary>Gets the weapon's unmodified base damage.</summary>
    public float BaseDamage { get; }

    /// <summary>Gets damage after the hit-region and critical multipliers.</summary>
    public float FinalDamage { get; }

    /// <summary>Gets the body region struck.</summary>
    public CharacterHitRegion HitRegion { get; }

    /// <summary>Gets whether this hit was critical.</summary>
    public bool IsCritical { get; }

    /// <summary>Gets the world-space contact point.</summary>
    public Vector2 HitPoint { get; }

    /// <summary>Gets the direction the attack was travelling.</summary>
    public Vector2 HitDirection { get; }

    /// <summary>Gets the target's health fraction immediately before this hit, or 1 when health is unavailable.</summary>
    public float TargetHealthFractionBeforeHit { get; }

    /// <summary>Gets whether this hit will reduce the known target health to zero.</summary>
    public bool IsLethalHit { get; }

    /// <summary>Creates a resolved hit result and rolls its critical chance once.</summary>
    public CombatHitData(
        float baseDamage,
        float regionDamageMultiplier,
        float criticalChance,
        float criticalMultiplier,
        CharacterHitRegion hitRegion,
        Vector2 hitPoint,
        Vector2 hitDirection,
        float targetCurrentHealth,
        float targetMaximumHealth)
    {
        BaseDamage = Mathf.Max(0f, baseDamage);
        HitRegion = hitRegion;
        HitPoint = hitPoint;
        HitDirection = hitDirection;
        TargetHealthFractionBeforeHit = targetMaximumHealth > 0f
            ? Mathf.Clamp01(targetCurrentHealth / targetMaximumHealth)
            : 1f;
        IsCritical = BaseDamage > 0f && Random.value < Mathf.Clamp01(criticalChance);
        float safeRegionMultiplier = Mathf.Max(0f, regionDamageMultiplier);
        float safeCriticalMultiplier = Mathf.Max(1f, criticalMultiplier);
        bool criticalHeadHit = IsCritical
            && HitRegion == CharacterHitRegion.Head
            && targetCurrentHealth > 0f;
        FinalDamage = criticalHeadHit
            ? targetCurrentHealth
            : BaseDamage * safeRegionMultiplier * (IsCritical ? safeCriticalMultiplier : 1f);
        IsLethalHit = targetCurrentHealth > 0f && FinalDamage >= targetCurrentHealth;
    }
}
