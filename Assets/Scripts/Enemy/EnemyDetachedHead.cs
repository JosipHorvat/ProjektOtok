using UnityEngine;

/// <summary>Copies an animated enemy head into a restrained, squashy physics prop that bounces and settles.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D))]
public sealed class EnemyDetachedHead : MonoBehaviour
{
    [Header("Launch")]
    [Tooltip("Horizontal speed imparted away from the side hit. The sign follows the incoming hit direction.")]
    [SerializeField, Min(0f)] private float outwardLaunchSpeed = 2.1f;
    [Tooltip("Upward speed so the head lifts away before falling back down.")]
    [SerializeField, Min(0f)] private float verticalLaunchSpeed = 1.35f;
    [Tooltip("Minimum spin in degrees per second, with its direction tied to the hit side.")]
    [SerializeField, Min(0f)] private float minimumInitialSpin = 180f;
    [Tooltip("Maximum spin in degrees per second, with its direction tied to the hit side.")]
    [SerializeField, Min(0f)] private float maximumInitialSpin = 360f;
    [Tooltip("Squash-and-stretch pulse applied as the head separates from the body.")]
    [SerializeField, Range(0f, 1f)] private float launchStretchAmount = 0.65f;

    [Header("Ground Bounce")]
    [Tooltip("Layers that count as ground for squash and bounce behavior.")]
    [SerializeField] private LayerMask groundLayers;
    [Tooltip("Vertical speed retained after the first landing.")]
    [SerializeField, Range(0f, 1f)] private float firstBounceMultiplier = 0.38f;
    [Tooltip("Vertical speed retained after the second landing.")]
    [SerializeField, Range(0f, 1f)] private float secondBounceMultiplier = 0.16f;
    [Tooltip("Minimum vertical impact speed required to bounce. Softer landings settle the head.")]
    [SerializeField, Min(0f)] private float minimumBounceImpactSpeed = 0.55f;
    [Tooltip("Horizontal speed retained after each landing.")]
    [SerializeField, Range(0f, 1f)] private float horizontalSpeedRetention = 0.55f;
    [Tooltip("Spin retained after each landing.")]
    [SerializeField, Range(0f, 1f)] private float bounceSpinRetention = 0.42f;

    [Header("Squash and Lifetime")]
    [Tooltip("Maximum amount the head flattens during each landing response.")]
    [SerializeField, Range(0f, 0.8f)] private float landingSquashAmount = 0.42f;
    [Tooltip("Duration of the squash-and-recover response in seconds.")]
    [SerializeField, Min(0.01f)] private float squashDuration = 0.2f;
    [Tooltip("Seconds before the detached head is removed. Set to zero to keep it for the scene lifetime.")]
    [SerializeField, Min(0f)] private float lifetime = 12f;

    private SpriteRenderer headRenderer;
    private CircleCollider2D headCollider;
    private Rigidbody2D body;
    private Vector3 restingScale;
    private float squashTimer;
    private float currentSquashStrength;
    private int groundBounceCount;
    private bool isInitialized;
    private bool isSettled;

    private void Awake()
    {
        headRenderer = GetComponent<SpriteRenderer>();
        headCollider = GetComponent<CircleCollider2D>();
        body = GetComponent<Rigidbody2D>();

        if (groundLayers.value == 0)
        {
            groundLayers = LayerMask.GetMask("Ground");
        }
    }

    /// <summary>Copies the source head's appearance and launches it away from the incoming hit.</summary>
    /// <param name="sourceRenderer">The animated head renderer to copy.</param>
    /// <param name="hitPoint">World-space contact point of the hit.</param>
    /// <param name="hitDirection">The direction the hit was travelling toward the head.</param>
    /// <param name="headLifetime">How long the spawned head remains before cleanup.</param>
    public void Initialize(SpriteRenderer sourceRenderer, Vector2 hitPoint, Vector2 hitDirection, float headLifetime)
    {
        if (sourceRenderer == null || headRenderer == null || headCollider == null || body == null)
        {
            Destroy(gameObject);
            return;
        }

        headRenderer.sprite = sourceRenderer.sprite;
        headRenderer.color = sourceRenderer.color;
        headRenderer.flipX = sourceRenderer.flipX;
        headRenderer.flipY = sourceRenderer.flipY;
        headRenderer.drawMode = sourceRenderer.drawMode;
        headRenderer.size = sourceRenderer.size;
        headRenderer.maskInteraction = sourceRenderer.maskInteraction;
        headRenderer.spriteSortPoint = sourceRenderer.spriteSortPoint;
        headRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
        headRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        headRenderer.sortingOrder = sourceRenderer.sortingOrder + 1;
        headRenderer.enabled = true;

        transform.position = sourceRenderer.transform.position;
        transform.rotation = sourceRenderer.transform.rotation;
        transform.localScale = sourceRenderer.transform.lossyScale;
        restingScale = transform.localScale;

        Vector2 incomingDirection = hitDirection.sqrMagnitude > 0.001f
            ? hitDirection.normalized
            : Vector2.zero;
        float horizontalDirection = Mathf.Abs(incomingDirection.x) > 0.15f
            ? Mathf.Sign(incomingDirection.x)
            : 0f;

        if (Mathf.Approximately(horizontalDirection, 0f))
        {
            float impactSide = hitPoint.x - sourceRenderer.bounds.center.x;
            horizontalDirection = Mathf.Abs(impactSide) > 0.02f
                ? -Mathf.Sign(impactSide)
                : (sourceRenderer.flipX ? -1f : 1f);
        }

        body.linearDamping = 0.15f;
        body.angularDamping = 0.85f;
        body.linearVelocity = new Vector2(horizontalDirection * outwardLaunchSpeed, verticalLaunchSpeed);
        float spinSpeed = Random.Range(minimumInitialSpin, Mathf.Max(minimumInitialSpin, maximumInitialSpin));
        body.angularVelocity = -horizontalDirection * spinSpeed;
        StartSquash(-launchStretchAmount);
        isInitialized = true;

        if (headLifetime > 0f)
        {
            Destroy(gameObject, headLifetime);
        }
        else if (lifetime > 0f)
        {
            Destroy(gameObject, lifetime);
        }
    }

    private void Update()
    {
        if (!isInitialized || squashTimer <= 0f)
        {
            return;
        }

        squashTimer = Mathf.Max(0f, squashTimer - Time.deltaTime);
        float progress = 1f - squashTimer / squashDuration;
        float squashPulse = Mathf.Sin(progress * Mathf.PI) * currentSquashStrength;
        transform.localScale = new Vector3(
            restingScale.x * (1f + squashPulse * 0.65f),
            restingScale.y * (1f - squashPulse * landingSquashAmount),
            restingScale.z);

        if (squashTimer <= 0f)
        {
            transform.localScale = restingScale;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isInitialized || isSettled || collision == null || !IsGroundLayer(collision.gameObject.layer))
        {
            return;
        }

        float impactSpeed = Mathf.Max(0f, -collision.relativeVelocity.y);
        StartSquash(Mathf.Clamp01(impactSpeed / 2.5f));

        if (impactSpeed < minimumBounceImpactSpeed || groundBounceCount >= 2)
        {
            Settle();
            return;
        }

        float bounceMultiplier = groundBounceCount == 0 ? firstBounceMultiplier : secondBounceMultiplier;
        groundBounceCount++;

        Vector2 velocity = body.linearVelocity;
        velocity.x *= horizontalSpeedRetention;
        velocity.y = impactSpeed * bounceMultiplier;
        body.linearVelocity = velocity;
        body.angularVelocity *= bounceSpinRetention;
    }

    private bool IsGroundLayer(int layer)
    {
        return (groundLayers.value & (1 << layer)) != 0;
    }

    private void StartSquash(float strength)
    {
        currentSquashStrength = Mathf.Clamp(strength, -1f, 1f);
        squashTimer = squashDuration;
    }

    private void Settle()
    {
        isSettled = true;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.Sleep();
    }
}
