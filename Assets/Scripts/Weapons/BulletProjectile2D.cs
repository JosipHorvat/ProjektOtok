using UnityEngine;

/// <summary>Moves a visible pistol bullet until it reaches range or its first world collider.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BulletProjectile2D : MonoBehaviour
{
    private const float MinimumDirectionSqrMagnitude = 0.0001f;
    private const float DefaultLifetime = 3f;
    private const string UnlitShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

    [Header("Projectile fallback values")]
    [Min(0.01f)] public float defaultSpeed = 28f;
    [Min(0.01f)] public float defaultRange = 25f;
    public LayerMask defaultCollisionLayers;

    [Header("Visibility")]
    public Color projectileColor = new Color(1f, 0.9f, 0.32f, 1f);
    [Min(0.1f)] public float visualLength = 0.42f;
    [Min(0.02f)] public float visualThickness = 0.14f;
    [Min(0.01f)] public float trailDuration = 0.14f;
    [Min(0.01f)] public float trailWidth = 0.12f;
    [Min(0.01f)] public float impactVisibleDuration = 0.35f;
    [Min(0f)] public float collisionGraceDistance = 0.5f;
    public bool logProjectileDebug;
    public string sortingLayerName = "Player";
    public int sortingOrder = 5;

    private Vector2 direction;
    private float speed;
    private float remainingDistance;
    private float remainingLifetime;
    private float travelledDistance;
    private LayerMask collisionLayers;
    private Transform ownerRoot;
    private SpriteRenderer spriteRenderer;
    private TrailRenderer trailRenderer;
    private Material runtimeUnlitMaterial;
    private bool isInitialized;

    /// <summary>Launches this projectile with normalized travel direction and collision filtering.</summary>
    public void Initialize(Vector2 travelDirection, float travelSpeed, float maximumDistance, LayerMask layers, Transform owner)
    {
        if (travelDirection.sqrMagnitude < MinimumDirectionSqrMagnitude)
        {
            travelDirection = Vector2.right;
        }

        EnsureVisuals();
        direction = travelDirection.normalized;
        speed = Mathf.Max(0.01f, travelSpeed > 0f ? travelSpeed : defaultSpeed);
        remainingDistance = Mathf.Max(0.01f, maximumDistance > 0f ? maximumDistance : defaultRange);
        remainingLifetime = Mathf.Min(DefaultLifetime, remainingDistance / speed + 0.25f);
        travelledDistance = 0f;
        collisionLayers = layers.value == 0 ? defaultCollisionLayers : layers;
        ownerRoot = owner;
        transform.right = direction;
        spriteRenderer.enabled = true;
        spriteRenderer.color = projectileColor;
        if (trailRenderer != null)
        {
            trailRenderer.Clear();
            trailRenderer.emitting = true;
        }

        isInitialized = true;
    }

    private void Awake()
    {
        if (defaultCollisionLayers.value == 0)
        {
            defaultCollisionLayers = LayerMask.GetMask("Ground", "Wall");
        }

        EnsureVisuals();
    }

    private void EnsureVisuals()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        transform.localScale = new Vector3(visualLength, visualThickness, 1f);
        spriteRenderer.color = projectileColor;
        spriteRenderer.sortingLayerName = sortingLayerName;
        spriteRenderer.sortingOrder = sortingOrder;
        spriteRenderer.enabled = true;

        Shader unlitShader = Shader.Find(UnlitShaderName);
        if (unlitShader != null && (runtimeUnlitMaterial == null || runtimeUnlitMaterial.shader != unlitShader))
        {
            if (runtimeUnlitMaterial != null)
            {
                Destroy(runtimeUnlitMaterial);
            }

            runtimeUnlitMaterial = new Material(unlitShader);
            spriteRenderer.material = runtimeUnlitMaterial;
        }

        if (trailRenderer == null)
        {
            trailRenderer = GetComponent<TrailRenderer>();
            if (trailRenderer == null)
            {
                trailRenderer = gameObject.AddComponent<TrailRenderer>();
            }
        }

        trailRenderer.time = trailDuration;
        trailRenderer.startWidth = trailWidth;
        trailRenderer.endWidth = trailWidth * 0.15f;
        trailRenderer.startColor = projectileColor;
        trailRenderer.endColor = new Color(projectileColor.r, projectileColor.g, projectileColor.b, 0f);
        trailRenderer.alignment = LineAlignment.View;
        trailRenderer.textureMode = LineTextureMode.Stretch;
        trailRenderer.sortingLayerName = sortingLayerName;
        trailRenderer.sortingOrder = sortingOrder + 1;
        trailRenderer.emitting = false;

        if (runtimeUnlitMaterial != null)
        {
            trailRenderer.material = runtimeUnlitMaterial;
        }
        else if (spriteRenderer.sharedMaterial != null)
        {
            trailRenderer.material = spriteRenderer.sharedMaterial;
        }
    }

    private void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f || remainingDistance <= 0f || speed <= 0f)
        {
            FinishProjectile();
            return;
        }

        Vector3 startPosition = transform.position;
        float stepDistance = Mathf.Min(speed * Time.deltaTime, remainingDistance);
        bool mayCollide = travelledDistance >= collisionGraceDistance;
        if (mayCollide)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(startPosition, direction, stepDistance, collisionLayers);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hitCollider = hits[i].collider;
                if (hitCollider == null || IsOwnedByShooter(hitCollider.transform))
                {
                    continue;
                }

                transform.position = hits[i].point;
                if (logProjectileDebug)
                {
                    Debug.Log($"[BulletProjectile2D] Projectile stopped by {hitCollider.name} after travelling {travelledDistance + hits[i].distance:F2} world units.", this);
                }

                FinishProjectile();
                return;
            }
        }

        transform.position = startPosition + (Vector3)(direction * stepDistance);
        travelledDistance += stepDistance;
        remainingDistance -= stepDistance;
        if (logProjectileDebug && travelledDistance <= stepDistance + 0.0001f)
        {
            Debug.Log($"[BulletProjectile2D] Projectile moving {direction} at {speed:F1} units/second.", this);
        }
    }

    private void FinishProjectile()
    {
        isInitialized = false;
        if (trailRenderer != null)
        {
            trailRenderer.emitting = false;
        }

        Destroy(gameObject, Mathf.Max(impactVisibleDuration, trailDuration));
    }

    private bool IsOwnedByShooter(Transform hitTransform)
    {
        return ownerRoot != null && (hitTransform == ownerRoot || hitTransform.IsChildOf(ownerRoot));
    }

    private void OnDestroy()
    {
        if (runtimeUnlitMaterial != null)
        {
            Destroy(runtimeUnlitMaterial);
        }
    }
}
