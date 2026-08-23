using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(PlayerInputReader))]
public class Player : MonoBehaviour
{
    #region Constants
    private const float MinimumInput = PlayerState.HorizontalInputThreshold;
    private const float MinimumTangentHorizontal = 0.01f;
    private const float GroundContactHeightTolerance = 0.01f;
    #endregion

    #region Component References
    public Animator anim { get; private set; }
    public Rigidbody2D rb { get; private set; }
    public PlayerInputReader InputReader { get; private set; }

    private CapsuleCollider2D capsuleCollider;
    private PlayerSquash playerSquash;
    #endregion

    #region Movement Settings
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float smoothMovement = 10f;
    public float acceleration = 120f;
    public float airDeceleration = 30f;
    #endregion

    #region Slope and Grounding Settings
    [Header("Slope Movement")]
    [SerializeField, Range(0f, 89f)] private float maxSlopeAngle = 50f;
    [SerializeField] private float slopeStickSpeed = 6f;
    [SerializeField] private float maximumGroundSnapSpeed = 25f;

    [Tooltip("Extra cast distance used to keep ground contact stable over small gaps and slopes.")]
    [SerializeField] private float groundProbeSkin = 0.05f;
    #endregion

    #region Jump Settings
    [Header("Jump")]
    public float jumpForce = 10f;

    [Tooltip("Maximum time the player can hold jump before the upward phase is cut.")]
    public float jumpTimeMax = 0.18f;

    public float fallAcceleration = 110f;
    public float maxFallSpeed = 40f;
    public float jumpEndEarlyGravityModifier = 3f;
    public float groundingForce = -1.5f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;
    public bool hasJumped { get; set; }

    [SerializeField, Range(0f, 1f)]
    private float jumpCutVelocityMultiplier = 0.4f;

    public float JumpCutVelocityMultiplier => jumpCutVelocityMultiplier;
    #endregion

    #region Collision Check Settings
    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckDistance = 0.2f;
    public Vector2 groundCheckSize = new Vector2(0.8f, 0.1f);
    public LayerMask whatIsGround;

    [Header("Wall Check")]
    public Transform wallCheck;
    public float wallCheckDistance = 0.5f;
    public LayerMask whatIsWall;
    #endregion

    #region Runtime State
    private float lastGroundedTime = float.NegativeInfinity;
    private bool coyoteJumpAvailable;

    private GroundInfo currentGroundInfo;

    private GroundInfo lastWalkableGroundInfo = new GroundInfo(
        false,
        Vector2.up,
        Vector2.right,
        Vector2.zero,
        0f,
        0f);

    public int facingDir { get; private set; } = 1;
    public bool isBusy { get; private set; }
    #endregion

    #region State Machine
    public PlayerStateMachine stateMachine { get; private set; }
    public PlayerIdleState idleState { get; private set; }
    public PlayerMoveState moveState { get; private set; }
    public PlayerJumpState jumpState { get; private set; }
    public PlayerAirState airState { get; private set; }
    #endregion

    #region Public State Data
    public Vector2 MoveInput => InputReader.MoveInput;

    public GroundInfo CurrentGroundInfo =>
        currentGroundInfo.HasGround ? currentGroundInfo : lastWalkableGroundInfo;

    public bool HasGroundContact => currentGroundInfo.HasGround;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        InitializeStates();
        CacheComponents();
        rb.gravityScale = 0f;
    }

    private void Start()
    {
        RefreshGroundInfo();
        stateMachine.Initialize(idleState);
    }

    private void Update() =>
        stateMachine.CurrentState?.Update();

    private void FixedUpdate()
    {
        RefreshGroundInfo();
        stateMachine.CurrentState?.FixedUpdate();
    }
    #endregion

    #region Movement
    public void SetHorizontalVelocity(float velocityX) =>
        rb.linearVelocity = new Vector2(velocityX, rb.linearVelocity.y);

    public void SetVerticalVelocity(float velocityY) =>
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, velocityY);

    public void SetZeroVelocity() =>
        rb.linearVelocity = Vector2.zero;

    public void ApplyGroundMovement(float horizontalInput)
    {
        if (!HasGroundContact)
        {
            ApplyAirMovement(horizontalInput);
            ApplyGravity(false, false);
            return;
        }

        if (Mathf.Abs(horizontalInput) <= MinimumInput)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 tangent = CurrentGroundInfo.Tangent;
        float targetHorizontalVelocity = horizontalInput * moveSpeed;

        float horizontalVelocity = Mathf.MoveTowards(
            rb.linearVelocity.x,
            targetHorizontalVelocity,
            smoothMovement * moveSpeed * Time.fixedDeltaTime);

        float tangentHorizontal = Mathf.Max(
            Mathf.Abs(tangent.x),
            MinimumTangentHorizontal);

        float slopeVerticalVelocity =
            horizontalVelocity * tangent.y / tangentHorizontal;

        if (slopeVerticalVelocity < 0f)
            slopeVerticalVelocity -= slopeStickSpeed;

        slopeVerticalVelocity -= GetGroundSnapSpeed();

        rb.linearVelocity = new Vector2(
            horizontalVelocity,
            slopeVerticalVelocity);
    }

    public void ApplyAirMovement(float horizontalInput)
    {
        float targetVelocityX = horizontalInput * moveSpeed;

        float movementRate = Mathf.Abs(horizontalInput) > MinimumInput
            ? acceleration
            : airDeceleration;

        float velocityX = Mathf.MoveTowards(
            rb.linearVelocity.x,
            targetVelocityX,
            movementRate * Time.fixedDeltaTime);

        SetHorizontalVelocity(velocityX);
    }

    public void ApplyGravity(bool grounded, bool jumpEndedEarly)
    {
        if (grounded && rb.linearVelocity.y <= 0f)
        {
            SetVerticalVelocity(groundingForce);
            return;
        }

        float gravity = jumpEndedEarly && rb.linearVelocity.y > 0f
            ? fallAcceleration * jumpEndEarlyGravityModifier
            : fallAcceleration;

        float velocityY = Mathf.MoveTowards(
            rb.linearVelocity.y,
            -maxFallSpeed,
            gravity * Time.fixedDeltaTime);

        SetVerticalVelocity(velocityY);
    }
    #endregion

    #region Grounding and Jump Timing
    public void MarkGrounded()
    {
        lastGroundedTime = Time.time;
        coyoteJumpAvailable = true;
    }

    public bool CanUseCoyoteJump()
    {
        if (!coyoteJumpAvailable)
            return false;

        if (Time.time > lastGroundedTime + coyoteTime)
            coyoteJumpAvailable = false;

        return coyoteJumpAvailable;
    }

    public void ConsumeCoyoteJump() =>
        coyoteJumpAvailable = false;
    #endregion

    #region Facing
    public void CheckIfShouldFlip(int inputDirection)
    {
        if (inputDirection != 0 && inputDirection != facingDir)
            Flip();
    }

    public void Flip()
    {
        facingDir *= -1;
        transform.Rotate(0f, 180f, 0f);
    }
    #endregion

    #region Animation and Interaction
    public void TriggerLandingSquash() =>
        playerSquash?.TriggerSquash();

    public float GetAnimationVerticalVelocity() =>
        HasGroundContact ? 0f : rb.linearVelocity.y;

    public void SetBusyFor(float duration)
    {
        isBusy = true;
        Invoke(nameof(ClearBusy), duration);
    }

    private void ClearBusy() =>
        isBusy = false;
    #endregion

    #region Initialization
    private void InitializeStates()
    {
        stateMachine = new PlayerStateMachine();

        idleState = new PlayerIdleState(this, stateMachine, "Idle");
        moveState = new PlayerMoveState(this, stateMachine, "Move");
        jumpState = new PlayerJumpState(this, stateMachine, "Jump");
        airState = new PlayerAirState(this, stateMachine, "Jump");
    }

    private void CacheComponents()
    {
        anim = GetComponentInChildren<Animator>();
        playerSquash = GetComponentInChildren<PlayerSquash>();
        rb = GetComponent<Rigidbody2D>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
        InputReader = GetComponent<PlayerInputReader>();
    }
    #endregion

    #region Collision Checks
    public bool IsGroundDetected() => HasGroundContact;

    public bool IsAnyWallDetected() =>
        wallCheck != null &&
        Physics2D.Raycast(
            wallCheck.position,
            Vector2.right * facingDir,
            wallCheckDistance,
            whatIsWall);

    private void RefreshGroundInfo()
    {
        bool wasGrounded = currentGroundInfo.HasGround;

        Bounds bounds = capsuleCollider.bounds;
        Vector2 origin = GetGroundCastOrigin(bounds);

        RaycastHit2D hit = Physics2D.BoxCast(
            origin,
            GetGroundCastSize(bounds),
            0f,
            Vector2.down,
            GetGroundCastDistance(),
            whatIsGround);

        if (!IsGroundHitValid(hit, bounds, wasGrounded))
        {
            LoseGroundContact();
            return;
        }

        Vector2 normal = hit.normal.normalized;
        Vector2 tangent = new Vector2(normal.y, -normal.x).normalized;

        float distance = Mathf.Max(
            0f,
            bounds.min.y - hit.point.y);

        float angle = Vector2.SignedAngle(
            Vector2.right,
            tangent);

        SetGroundContact(
            normal,
            tangent,
            hit.point,
            distance,
            angle);

        if (!wasGrounded && rb.linearVelocity.y > 0f)
        {
            Debug.Log(
                $"SUSPICIOUS GROUND HIT | " +
                $"State: {stateMachine.CurrentState.GetType().Name} | " +
                $"Y Velocity: {rb.linearVelocity.y:F2} | " +
                $"Distance: {distance:F3} | " +
                $"Hit: {hit.collider.name} | " +
                $"Normal: {normal}");
        }
    }

    private void SetGroundContact(
        Vector2 normal,
        Vector2 tangent,
        Vector2 point,
        float distance,
        float angle)
    {
        currentGroundInfo = new GroundInfo(
            true,
            normal,
            tangent,
            point,
            distance,
            angle);

        lastWalkableGroundInfo = currentGroundInfo;
    }

    private void LoseGroundContact()
    {
        if (currentGroundInfo.HasGround)
        {
            Debug.Log(
                $"LOST GROUND | State: {stateMachine.CurrentState.GetType().Name} | " +
                $"Y Velocity: {rb.linearVelocity.y:F2}");

            MarkGrounded();
        }

        currentGroundInfo = new GroundInfo(
            false,
            lastWalkableGroundInfo.Normal,
            lastWalkableGroundInfo.Tangent,
            lastWalkableGroundInfo.Point,
            lastWalkableGroundInfo.Distance,
            lastWalkableGroundInfo.Angle);
    }

    private bool IsGroundHitValid(
        RaycastHit2D hit,
        Bounds bounds,
        bool wasGrounded)
    {
        if (!IsWalkable(hit))
            return false;

        bool hitIsAboveFootLine =
            hit.point.y > bounds.min.y + GroundContactHeightTolerance;

        return wasGrounded || !hitIsAboveFootLine || rb.linearVelocity.y <= 0f;
    }
    private bool IsWalkable(RaycastHit2D hit) =>
        hit.collider != null &&
        Vector2.Angle(hit.normal, Vector2.up) <= maxSlopeAngle;
    #endregion

    #region Helpers
    private float GetGroundSnapSpeed()
    {
        float gap = CurrentGroundInfo.Distance;

        if (gap <= 0f)
            return 0f;

        return Mathf.Min(
            gap / Time.fixedDeltaTime,
            maximumGroundSnapSpeed);
    }

    private float GetGroundCastDistance() =>
        groundCheckDistance + groundProbeSkin;

    private Vector2 GetGroundCastOrigin(Bounds bounds)
    {
        Vector2 castSize = GetGroundCastSize(bounds);
        float castOriginX = groundCheck != null
            ? groundCheck.position.x
            : bounds.center.x;
        float castOriginY = bounds.min.y - castSize.y * 0.5f;

        return new Vector2(castOriginX, castOriginY);
    }

    private Vector2 GetGroundCastSize(Bounds bounds) =>
        new Vector2(
            Mathf.Min(groundCheckSize.x, bounds.size.x),
            groundCheckSize.y);
    #endregion

    #region Data Types
    public readonly struct GroundInfo
    {
        public GroundInfo(
            bool hasGround,
            Vector2 normal,
            Vector2 tangent,
            Vector2 point,
            float distance,
            float angle)
        {
            HasGround = hasGround;
            Normal = normal;
            Tangent = tangent;
            Point = point;
            Distance = distance;
            Angle = angle;
        }

        public bool HasGround { get; }
        public Vector2 Normal { get; }
        public Vector2 Tangent { get; }
        public Vector2 Point { get; }
        public float Distance { get; }
        public float Angle { get; }
    }
    #endregion

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        CapsuleCollider2D capsule = capsuleCollider != null
            ? capsuleCollider
            : GetComponent<CapsuleCollider2D>();

        if (capsule == null)
            return;

        Bounds bounds = capsule.bounds;
        Vector2 origin = GetGroundCastOrigin(bounds);
        Vector2 size = GetGroundCastSize(bounds);
        float distance = GetGroundCastDistance();

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(origin, size);
        Gizmos.DrawWireCube(
            origin + Vector2.down * distance,
            size);

        DrawGizmoCastEdge(origin, size.x, distance, -0.5f);
        DrawGizmoCastEdge(origin, size.x, distance, 0.5f);
    }

    private void DrawGizmoCastEdge(
        Vector2 origin,
        float width,
        float distance,
        float side)
    {
        Vector2 edge = origin + Vector2.right * width * side;

        Gizmos.DrawLine(
            edge,
            edge + Vector2.down * distance);
    }
    #endregion
}