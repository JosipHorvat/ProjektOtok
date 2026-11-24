using UnityEngine;

/// <summary>Applies horizontal patrol movement and probes the path ahead.</summary>
public sealed class EnemyPatrolMovement : MonoBehaviour, IHitReactionMovement2D
{
    private const int ProbeHitCapacity = 16;
    private const float MinimumVisualScaleMagnitude = 0.0001f;
    private const string DefaultWallLayerName = "Wall";

    [Header("References")]
    [Tooltip("Rigidbody2D whose horizontal velocity this component controls. Automatically cached from this GameObject when left empty.")]
    [SerializeField] private Rigidbody2D body;
    [Tooltip("Visual child whose local X scale is flipped to face the patrol direction. Automatically cached from a child when left empty.")]
    [SerializeField] private Transform visualRoot;

    [Header("Patrol")]
    [Tooltip("Horizontal movement speed in world units per second.")]
    [SerializeField, Min(0f)] private float walkSpeed = 2f;
    [Tooltip("Distance at which the enemy stops horizontal movement while chasing the player.")]
    [SerializeField, Min(0f)] private float chaseStoppingDistance = 0.8f;
    [Tooltip("When enabled, patrol begins moving right; otherwise it begins moving left.")]
    [SerializeField] private bool startMovingRight;

    [Header("Ground Check")]
    [Tooltip("Optional marker positioned at the enemy's feet. Ground rays start here; if empty, the collider's bottom is used.")]
    [SerializeField] private Transform groundCheckPoint;
    [Tooltip("Layers that count as ground for slope walking and ledge checks.")]
    [SerializeField] private LayerMask groundLayers;
    [Tooltip("How far ahead of the feet to sample the slope and check for a ledge.")]
    [SerializeField, Min(0f)] private float groundProbeForwardDistance = 0.38f;
    [Tooltip("How far downward the ground ray searches below its raised start point.")]
    [SerializeField, Min(0f)] private float groundProbeDistance = 0.45f;

    [Header("Slope Handling")]
    [Tooltip("Maximum ground angle the enemy can walk on. Steeper ground is treated as an obstacle.")]
    [SerializeField, Range(0f, 89f)] private float maximumWalkableSlopeAngle = 60f;
    [Tooltip("How high above the collider's feet the ground sample begins, allowing uphill detection.")]
    [SerializeField, Min(0f)] private float slopeProbeHeight = 0.9f;
    [Tooltip("Vertical difference allowed when checking that the forward sample belongs to the same slope.")]
    [SerializeField, Min(0f)] private float maximumGroundStepHeight = 0.35f;
    [Tooltip("Maximum separation between the feet and ground for slope-following to be active.")]
    [SerializeField, Min(0f)] private float groundedProbeTolerance = 0.18f;

    [Header("Wall Check")]
    [Tooltip("Optional marker where the horizontal wall ray begins. If empty, the collider's center is used.")]
    [SerializeField] private Transform wallCheckPoint;
    [Tooltip("Layers that the wall ray should treat as obstacles. Set this to Wall for the project's wall geometry.")]
    [SerializeField] private LayerMask wallLayers;
    [Tooltip("Length of the horizontal ray cast in the direction the enemy is facing.")]
    [SerializeField, Min(0f)] private float wallProbeDistance = 0.45f;

    private readonly RaycastHit2D[] probeHits = new RaycastHit2D[ProbeHitCapacity];
    private Collider2D groundCollider;
    private int directionSign;
    private bool isWalking;
    private bool isChasing;
    private bool isStaggered;
    private Transform chaseTarget;
    private float movementSlowMultiplier = 1f;
    private float movementSlowRemaining;
    private bool hasWarnedMissingBody;
    private bool hasWarnedMissingVisualRoot;

    /// <summary>The enemy's current horizontal facing direction.</summary>
    public int FacingDirection => directionSign;

    private void Awake()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        groundCollider = GetComponent<Collider2D>();

        if (body == null && !hasWarnedMissingBody)
        {
            Debug.LogWarning($"{nameof(EnemyPatrolMovement)} on '{name}' requires a Rigidbody2D to move.", this);
            hasWarnedMissingBody = true;
        }

        if (visualRoot == null)
        {
            Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < childTransforms.Length; i++)
            {
                if (childTransforms[i] != transform)
                {
                    visualRoot = childTransforms[i];
                    break;
                }
            }
        }

        if (visualRoot == null && !hasWarnedMissingVisualRoot)
        {
            Debug.LogWarning($"{nameof(EnemyPatrolMovement)} on '{name}' has no visual child assigned; facing will not be flipped.", this);
            hasWarnedMissingVisualRoot = true;
        }

        directionSign = startMovingRight ? 1 : -1;
        ApplyFacing();
    }

    private void Update()
    {
        if (movementSlowRemaining <= 0f)
        {
            return;
        }

        movementSlowRemaining -= Time.deltaTime;
        if (movementSlowRemaining <= 0f)
        {
            movementSlowMultiplier = 1f;
        }
    }

    private void FixedUpdate()
    {
        if (body == null)
        {
            return;
        }

        if (isStaggered)
        {
            Vector2 stoppedVelocity = body.linearVelocity;
            stoppedVelocity.x = 0f;
            if (TryGetGroundHit(0f, out RaycastHit2D groundHit) && IsGroundedAt(groundHit))
            {
                stoppedVelocity.y = 0f;
            }

            body.linearVelocity = stoppedVelocity;
            return;
        }

        if (!isWalking && !isChasing)
        {
            return;
        }

        Vector2 velocity = body.linearVelocity;
        if (isChasing)
        {
            if (chaseTarget == null)
            {
                SetWalking(false);
                return;
            }

            float horizontalDistanceToTarget = chaseTarget.position.x - body.position.x;
            if (Mathf.Abs(horizontalDistanceToTarget) <= chaseStoppingDistance)
            {
                velocity.x = 0f;
                body.linearVelocity = velocity;
                return;
            }

            int targetDirection = horizontalDistanceToTarget > 0f ? 1 : -1;
            if (targetDirection != directionSign)
            {
                directionSign = targetDirection;
                ApplyFacing();
            }
        }

        float currentWalkSpeed = walkSpeed * movementSlowMultiplier;
        if (TryGetGroundHit(0f, out RaycastHit2D currentGroundHit)
            && IsGroundedAt(currentGroundHit)
            && TryGetGroundHit(groundProbeForwardDistance, out RaycastHit2D slopeHit)
            && IsWalkableSlope(currentGroundHit.normal)
            && IsWalkableSlope(slopeHit.normal))
        {
            Vector2 slopeTangent = new Vector2(slopeHit.normal.y, -slopeHit.normal.x).normalized;
            velocity = slopeTangent * (directionSign * currentWalkSpeed);
        }
        else
        {
            velocity.x = directionSign * currentWalkSpeed;
        }

        body.linearVelocity = velocity;
    }

    /// <summary>Returns whether a wall, ledge, or unwalkable slope is ahead.</summary>
    public bool HasObstacleAhead()
    {
        if (HasWallInFront())
        {
            return true;
        }

        if (!TryGetGroundHit(0f, out RaycastHit2D currentGroundHit)
            || !IsGroundedAt(currentGroundHit))
        {
            return false;
        }

        if (!TryGetGroundHit(groundProbeForwardDistance, out RaycastHit2D forwardGroundHit))
        {
            return true;
        }

        return !IsWalkableGroundTransition(currentGroundHit, forwardGroundHit, groundProbeForwardDistance);
    }

    /// <summary>Enables or stops patrol movement, preserving vertical velocity while airborne.</summary>
    public void SetWalking(bool walking)
    {
        isWalking = walking;
        isChasing = false;
        chaseTarget = null;
        if (!walking && body != null)
        {
            Vector2 velocity = body.linearVelocity;
            velocity.x = 0f;
            if (TryGetGroundHit(0f, out RaycastHit2D groundHit) && IsGroundedAt(groundHit))
            {
                velocity.y = 0f;
            }

            body.linearVelocity = velocity;
        }
    }

    /// <summary>Starts pursuing a target while preserving vertical and slope movement.</summary>
    public void SetChasing(Transform target)
    {
        isWalking = false;
        chaseTarget = target;
        isChasing = target != null;
    }

    /// <summary>Applies a temporary movement slow, retaining the strongest active multiplier.</summary>
    public void ApplyTemporaryMovementSlow(float speedMultiplier, float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        float clampedMultiplier = Mathf.Clamp(speedMultiplier, 0.1f, 1f);
        movementSlowMultiplier = movementSlowRemaining > 0f
            ? Mathf.Min(movementSlowMultiplier, clampedMultiplier)
            : clampedMultiplier;
        movementSlowRemaining = Mathf.Max(movementSlowRemaining, duration);
    }

    /// <summary>Stops or resumes patrol/chase movement during a stagger without changing AI state.</summary>
    public void SetStaggered(bool staggered)
    {
        isStaggered = staggered;
        if (!staggered || body == null)
        {
            return;
        }

        Vector2 velocity = body.linearVelocity;
        velocity.x = 0f;
        if (TryGetGroundHit(0f, out RaycastHit2D groundHit) && IsGroundedAt(groundHit))
        {
            velocity.y = 0f;
        }

        body.linearVelocity = velocity;
    }

    /// <summary>Reverses patrol direction and flips only the assigned visual root.</summary>
    public void TurnAround()
    {
        directionSign = -directionSign;
        ApplyFacing();
    }

    private bool TryGetGroundHit(float distanceAhead, out RaycastHit2D groundHit)
    {
        return TryGetGroundHit(distanceAhead, directionSign, out groundHit);
    }

    private bool TryGetGroundHit(float distanceAhead, int facingDirection, out RaycastHit2D groundHit)
    {
        groundHit = default;
        if (groundLayers.value == 0 || groundCollider == null)
        {
            return false;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(groundLayers);
        filter.useTriggers = false;

        Bounds colliderBounds = groundCollider.bounds;
        float groundBaseY = groundCheckPoint != null ? groundCheckPoint.position.y : colliderBounds.min.y;
        float groundBaseX = groundCheckPoint != null ? groundCheckPoint.position.x : colliderBounds.center.x;
        Vector2 origin = new Vector2(
            groundBaseX + facingDirection * distanceAhead,
            groundBaseY + slopeProbeHeight);
        float rayDistance = slopeProbeHeight + groundProbeDistance + maximumGroundStepHeight;
        int hitCount = Physics2D.Raycast(origin, Vector2.down, filter, probeHits, rayDistance);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = probeHits[i];
            if (hit.collider == null || IsOwnCollider(hit.collider))
            {
                continue;
            }

            groundHit = hit;
            return true;
        }

        return false;
    }

    private bool IsGroundedAt(RaycastHit2D groundHit)
    {
        return groundHit.distance <= slopeProbeHeight + groundedProbeTolerance;
    }

    private bool IsWalkableGroundTransition(
        RaycastHit2D currentGroundHit,
        RaycastHit2D forwardGroundHit,
        float horizontalDistance)
    {
        if (!IsWalkableSlope(currentGroundHit.normal) || !IsWalkableSlope(forwardGroundHit.normal))
        {
            return false;
        }

        Vector2 averageNormal = (currentGroundHit.normal + forwardGroundHit.normal).normalized;
        if (averageNormal.y <= 0f)
        {
            return false;
        }

        float expectedHeightChange = -averageNormal.x / averageNormal.y * directionSign * horizontalDistance;
        float actualHeightChange = forwardGroundHit.point.y - currentGroundHit.point.y;
        return Mathf.Abs(actualHeightChange - expectedHeightChange) <= maximumGroundStepHeight;
    }

    private bool IsWalkableSlope(Vector2 surfaceNormal)
    {
        return Vector2.Angle(surfaceNormal, Vector2.up) <= maximumWalkableSlopeAngle;
    }

    private bool HasWallInFront()
    {
        int facingDirection = directionSign == 0 ? (startMovingRight ? 1 : -1) : directionSign;
        return TryGetWallHit(facingDirection, out _);
    }

    private bool TryGetWallHit(int facingDirection, out RaycastHit2D wallHit)
    {
        wallHit = default;
        LayerMask layersToCheck = GetWallLayerMask();
        if (layersToCheck.value == 0)
        {
            return false;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(layersToCheck);
        filter.useTriggers = false;

        Vector2 origin = GetWallRayOrigin();
        Vector2 rayDirection = Vector2.right * facingDirection;
        int hitCount = Physics2D.Raycast(origin, rayDirection, filter, probeHits, wallProbeDistance);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = probeHits[i];
            if (hit.collider == null || IsOwnCollider(hit.collider))
            {
                continue;
            }

            wallHit = hit;
            return true;
        }

        return false;
    }

    private LayerMask GetWallLayerMask()
    {
        return wallLayers.value != 0 ? wallLayers : LayerMask.GetMask(DefaultWallLayerName);
    }

    private Vector2 GetWallRayOrigin()
    {
        if (wallCheckPoint != null)
        {
            return wallCheckPoint.position;
        }

        if (groundCollider != null)
        {
            return groundCollider.bounds.center;
        }

        return transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        int facingDirection = directionSign == 0
            ? (startMovingRight ? 1 : -1)
            : directionSign;

        Vector2 wallOrigin = GetWallRayOrigin();
        Vector2 wallDirection = Vector2.right * facingDirection;
        bool hasWall = TryGetWallHit(facingDirection, out RaycastHit2D wallHit);
        Vector2 wallRayEnd = hasWall ? wallHit.point : wallOrigin + wallDirection * wallProbeDistance;

        Gizmos.color = hasWall ? Color.red : Color.green;
        Gizmos.DrawLine(wallOrigin, wallRayEnd);
        Gizmos.DrawWireSphere(wallRayEnd, 0.05f);

        DrawGroundProbeGizmo(0f, facingDirection, Color.cyan);
        DrawGroundProbeGizmo(groundProbeForwardDistance, facingDirection, Color.yellow);
    }

    private void DrawGroundProbeGizmo(float distanceAhead, int facingDirection, Color color)
    {
        if (groundCollider == null || groundLayers.value == 0)
        {
            return;
        }

        bool hasGround = TryGetGroundHit(distanceAhead, facingDirection, out RaycastHit2D groundHit);

        Bounds colliderBounds = groundCollider.bounds;
        float groundBaseY = groundCheckPoint != null ? groundCheckPoint.position.y : colliderBounds.min.y;
        float groundBaseX = groundCheckPoint != null ? groundCheckPoint.position.x : colliderBounds.center.x;
        Vector2 rayOrigin = new Vector2(
            groundBaseX + facingDirection * distanceAhead,
            groundBaseY + slopeProbeHeight);
        Vector2 rayEnd = hasGround
            ? groundHit.point
            : rayOrigin + Vector2.down * (slopeProbeHeight + groundProbeDistance + maximumGroundStepHeight);

        Gizmos.color = hasGround ? color : Color.red;
        Gizmos.DrawLine(rayOrigin, rayEnd);
        Gizmos.DrawWireSphere(rayEnd, 0.04f);
    }

    private bool IsOwnCollider(Collider2D candidate)
    {
        if (body != null && candidate.attachedRigidbody == body)
        {
            return true;
        }

        Transform candidateTransform = candidate.transform;
        return candidateTransform == transform || candidateTransform.IsChildOf(transform);
    }

    private void ApplyFacing()
    {
        if (visualRoot == null)
        {
            return;
        }

        Vector3 scale = visualRoot.localScale;
        float magnitude = Mathf.Abs(scale.x);
        if (magnitude < MinimumVisualScaleMagnitude)
        {
            magnitude = 1f;
        }

        scale.x = magnitude * directionSign;
        visualRoot.localScale = scale;
    }
}
