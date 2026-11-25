using UnityEngine;

/// <summary>Labels a hurtbox with its body region without depending on the visual rig hierarchy.</summary>
[DisallowMultipleComponent]
public sealed class HitRegionMarker : MonoBehaviour
{
    [Tooltip("Body region represented by the collider on this GameObject. Leave the enemy root collider as Torso.")]
    [SerializeField] private CharacterHitRegion region = CharacterHitRegion.Torso;
    [Tooltip("Damage multiplier applied to weapon hits that strike this region.")]
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;

    /// <summary>The body region assigned to this hurtbox.</summary>
    public CharacterHitRegion Region => region;

    /// <summary>Damage multiplier applied to hits on this hurtbox.</summary>
    public float DamageMultiplier => damageMultiplier;

    /// <summary>Resolves the most specific marked region covering a contact point on a damageable character.</summary>
    /// <param name="hitCollider">The collider returned by the physics query.</param>
    /// <param name="damageable">The damageable character hit by the query.</param>
    /// <param name="hitPoint">The world-space contact point.</param>
    /// <returns>The direct region marker or an overlapping specific body-part marker; null when none exists.</returns>
    public static HitRegionMarker FindAtHitPoint(Collider2D hitCollider, IDamageable2D damageable, Vector2 hitPoint)
    {
        if (hitCollider == null)
        {
            return null;
        }

        HitRegionMarker directMarker = hitCollider.GetComponentInParent<HitRegionMarker>();
        if (directMarker != null && directMarker.region != CharacterHitRegion.Torso)
        {
            return directMarker;
        }

        Component damageableComponent = damageable as Component;
        if (damageableComponent == null)
        {
            return directMarker;
        }

        Collider2D[] targetColliders = damageableComponent.GetComponentsInChildren<Collider2D>(true);
        for (int index = 0; index < targetColliders.Length; index++)
        {
            Collider2D candidate = targetColliders[index];
            if (candidate == null || candidate == hitCollider || !candidate.OverlapPoint(hitPoint))
            {
                continue;
            }

            HitRegionMarker candidateMarker = candidate.GetComponent<HitRegionMarker>();
            if (candidateMarker != null && candidateMarker.region != CharacterHitRegion.Torso)
            {
                return candidateMarker;
            }
        }

        return directMarker;
    }
}
