using UnityEngine;

/// <summary>Detaches the enemy head when a critical head hit is resolved as lethal.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(HitRegionMarker))]
public sealed class EnemyHeadDismemberment : MonoBehaviour, IHitRegionDamageReceiver2D
{

    [Header("Dismemberment")]
    [Tooltip("Prefab spawned when a critical head hit detaches this head. It must include EnemyDetachedHead, SpriteRenderer, CircleCollider2D, and Rigidbody2D.")]
    [SerializeField] private EnemyDetachedHead detachedHeadPrefab;
    [Tooltip("Seconds before the spawned head is removed. Set to zero to keep it for the rest of the scene.")]
    [SerializeField, Min(0f)] private float detachedLifetime = 12f;

    private SpriteRenderer headRenderer;
    private CircleCollider2D headHitbox;
    private EnemyLimbCondition2D limbCondition;
    private bool isDetached;

    private void Awake()
    {
        headRenderer = GetComponent<SpriteRenderer>();
        headHitbox = GetComponent<CircleCollider2D>();
        limbCondition = GetComponentInParent<EnemyLimbCondition2D>();

        if (headHitbox != null)
        {
            headHitbox.isTrigger = true;
            headHitbox.enabled = !isDetached;
        }
    }

    /// <inheritdoc />
    public void ReceiveHitRegionDamage(CombatHitData hitData)
    {
        if (isDetached
            || hitData.HitRegion != CharacterHitRegion.Head
            || !hitData.IsCritical
            || !hitData.IsLethalHit)
        {
            return;
        }

        DetachHead(hitData.HitPoint, hitData.HitDirection);
    }

    private void DetachHead(Vector2 hitPoint, Vector2 hitDirection)
    {
        if (headRenderer == null || !headRenderer.enabled || headRenderer.sprite == null)
        {
            return;
        }

        if (detachedHeadPrefab == null)
        {
            Debug.LogError($"{nameof(EnemyHeadDismemberment)} on '{name}' has no detached head prefab assigned.", this);
            return;
        }

        isDetached = true;
        EnemyDetachedHead detachedHead = Instantiate(
            detachedHeadPrefab,
            headRenderer.transform.position,
            headRenderer.transform.rotation);
        detachedHead.Initialize(headRenderer, hitPoint, hitDirection, detachedLifetime);

        headRenderer.enabled = false;
        if (headHitbox != null)
        {
            headHitbox.enabled = false;
        }

        if (limbCondition != null)
        {
            limbCondition.RecordLimbLoss(EnemyLimbCondition2D.Limb.Head);
        }
    }
}
