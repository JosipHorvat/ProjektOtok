using UnityEngine;

/// <summary>Applies player movement to the physics body and exposes shared player data to states.</summary>
public class Player : MonoBehaviour
{
    /// <summary>Describes the most recent walkable surface detected below the player.</summary>
    public readonly struct GroundInfo
    {
        public GroundInfo(bool hasGround, Vector2 normal, Vector2 tangent, Vector2 point, float distance, float angle)
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

        /// <summary>Vertical gap between the bottom of the collider and the surface.</summary>
        public float Distance { get; }
        public float Angle { get; }
    }

    private const float MinimumInput = PlayerState.HorizontalInputThreshold;
    private const float MinimumTangentHorizontal = 0.01f;
    private const float GroundProbeDrawDuration = 0f;
    private const string IdleAnimation = "Idle";
    private const string MoveAnimation = "Move";
    private const string JumpAnimation = "Jump";

    private struct GroundProbeResult
    {
        public bool Found;
        public int SampleCount;
        public Vector2 NormalSum;
        public Vector2 PointSum;
        public float ClosestDistance;

        public Vector2 AverageNormal => SampleCount > 0 ? (NormalSum / SampleCount).normalized : Vector2.up;
        public Vector2 AveragePoint => SampleCount > 0 ? PointSum / SampleCount : Vector2.zero;

        public void Add(RaycastHit2D hit)
        {
            if (!Found)
            {
                ClosestDistance = hit.distance;
            }

            Found = true;
            SampleCount++;
            NormalSum += hit.normal;
            PointSum += hit.point;
            ClosestDistance = Mathf.Min(ClosestDistance, hit.distance);
        }
    }

    public Animator anim { get; private set; }
    public Rigidbody2D rb { get; private set; }
    public PlayerInputReader InputReader { get; private set; }
    private PlayerSquash playerSquash;
    private CapsuleCollider2D capsuleCollider;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float smoothMovement = 10f;
    public float acceleration = 120f;
    public float airDeceleration = 30f;

    [Header("Slope Movement")]
    [SerializeField, Range(0f, 89f)] private float maxSlopeAngle = 50f;
    [SerializeField] private float slopeStickSpeed = 6f;
    [SerializeField] private float maximumGroundSnapSpeed = 25f;
    [SerializeField] private float groundLossGraceTime = 0.08f;
    [Tooltip("Total horizontal span of the ground rays, centred under the feet. Keep it narrower than the collider so ledges are detected as air.")]
    [SerializeField] private float groundProbeWidth = 0.5f;
    [SerializeField] private float groundProbeSkin = 0.05f;
    [SerializeField] private float stepForwardDistance = 0.08f;
    [SerializeField] private float maximumStepDownDistance = 0.25f;

    [Header("Jump")]
    public float jumpForce = 10f;
    public float jumpTimeMax = 0.2f;
    public float fallAcceleration = 110f;
    public float maxFallSpeed = 40f;
    public float jumpEndEarlyGravityModifier = 3f;
    public float groundingForce = -1.5f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;
    public bool hasJumped { get; set; }
    [SerializeField, Range(0f, 1f)] private float jumpCutVelocityMultiplier = 0.4f;
    public float JumpCutVelocityMultiplier => jumpCutVelocityMultiplier;

    private float lastGroundedTime = float.NegativeInfinity;
    private float groundLossTimer = float.PositiveInfinity;
    private bool coyoteJumpAvailable;
    private GroundInfo currentGroundInfo;
    private GroundInfo lastWalkableGroundInfo = new GroundInfo(false, Vector2.up, Vector2.right, Vector2.zero, 0f, 0f);

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckDistance = 0.2f;
    public Vector2 groundCheckSize = new Vector2(0.8f, 0.1f);
    public LayerMask whatIsGround;

    [Header("Wall Check")]
    public Transform wallCheck;
    public float wallCheckDistance = 0.5f;
    public LayerMask whatIsWall;

    public int facingDir { get; private set; } = 1;
    public bool isBusy { get; private set; }

    public PlayerStateMachine stateMachine { get; private set; }
    public PlayerIdleState idleState { get; private set; }
    public PlayerMoveState moveState { get; private set; }
    public PlayerJumpState jumpState { get; private set; }
    public PlayerAirState airState { get; private set; }

    public Vector2 MoveInput => InputReader == null ? Vector2.zero : InputReader.MoveInput;
    public GroundInfo CurrentGroundInfo => currentGroundInfo.HasGround ? currentGroundInfo : lastWalkableGroundInfo;
    public bool HasGroundContact => currentGroundInfo.HasGround;
    public bool ShouldRemainGrounded => !hasJumped && (HasGroundContact || groundLossTimer <= groundLossGraceTime);

    private void Awake()
    {
        stateMachine = new PlayerStateMachine();
        idleState = new PlayerIdleState(this, stateMachine, IdleAnimation);
        moveState = new PlayerMoveState(this, stateMachine, MoveAnimation);
        jumpState = new PlayerJumpState(this, stateMachine, JumpAnimation);
        airState = new PlayerAirState(this, stateMachine, JumpAnimation);

        anim = GetComponentInChildren<Animator>();
        playerSquash = anim != null ? anim.GetComponent<PlayerSquash>() : null;
        rb = GetComponent<Rigidbody2D>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
        rb.gravityScale = 0f;
        InputReader = GetComponent<PlayerInputReader>();

        if (InputReader == null)
            InputReader = gameObject.AddComponent<PlayerInputReader>();
    }

    private void Start()
    {
        RefreshGroundInfo();
        stateMachine.Initialize(idleState);
    }

    private void Update()
    {
        stateMachine.CurrentState?.Update();
    }

    private void FixedUpdate()
    {
        RefreshGroundInfo();
        stateMachine.CurrentState?.FixedUpdate();
    }

    /// <summary>Triggers the visual squash effect when the player lands.</summary>
    public void TriggerLandingSquash()
    {
        playerSquash?.TriggerSquash();
    }

    /// <summary>Sets horizontal velocity without replacing vertical velocity.</summary>
    public void SetHorizontalVelocity(float velocityX)
    {
        if (rb != null)
            rb.linearVelocity = new Vector2(velocityX, rb.linearVelocity.y);
    }

    /// <summary>Sets vertical velocity without replacing horizontal velocity.</summary>
    public void SetVerticalVelocity(float velocityY)
    {
        if (rb != null)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, velocityY);
    }

    /// <summary>Maintains constant horizontal speed while holding the body against the current surface.</summary>
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

        float tangentHorizontal = Mathf.Max(Mathf.Abs(tangent.x), MinimumTangentHorizontal);
        float slopeVerticalVelocity = horizontalVelocity * tangent.y / tangentHorizontal;

        if (slopeVerticalVelocity < 0f)
        {
            slopeVerticalVelocity -= slopeStickSpeed;
        }

        slopeVerticalVelocity -= GetGroundSnapSpeed();
        rb.linearVelocity = new Vector2(horizontalVelocity, slopeVerticalVelocity);
    }

    /// <summary>Returns the extra downward speed needed to close the gap to the surface this step.</summary>
    private float GetGroundSnapSpeed()
    {
        float gap = CurrentGroundInfo.Distance;
        if (gap <= 0f)
        {
            return 0f;
        }

        return Mathf.Min(gap / Time.fixedDeltaTime, maximumGroundSnapSpeed);
    }

    /// <summary>Applies horizontal air control.</summary>
    public void ApplyAirMovement(float horizontalInput)
    {
        float targetVelocityX = horizontalInput * moveSpeed;
        float rate = Mathf.Abs(horizontalInput) > 0f ? acceleration : airDeceleration;
        float velocityX = Mathf.MoveTowards(rb.linearVelocity.x, targetVelocityX, rate * Time.fixedDeltaTime);
        SetHorizontalVelocity(velocityX);
    }

    /// <summary>Applies custom vertical gravity or a downward grounding velocity.</summary>
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
        float velocityY = Mathf.MoveTowards(rb.linearVelocity.y, -maxFallSpeed, gravity * Time.fixedDeltaTime);
        SetVerticalVelocity(velocityY);
    }

    /// <summary>Records confirmed ground contact for coyote jumping.</summary>
    public void MarkGrounded()
    {
        lastGroundedTime = Time.time;
        coyoteJumpAvailable = true;
    }

    /// <summary>Returns whether the current coyote jump window remains available.</summary>
    public bool CanUseCoyoteJump()
    {
        return coyoteJumpAvailable && Time.time <= lastGroundedTime + coyoteTime;
    }

    /// <summary>Consumes the current coyote jump opportunity.</summary>
    public void ConsumeCoyoteJump()
    {
        coyoteJumpAvailable = false;
    }

    /// <summary>Stops all Rigidbody2D movement.</summary>
    public void SetZeroVelocity()
    {
        rb.linearVelocity = Vector2.zero;
    }

    /// <summary>Flips the visual facing direction when input changes direction.</summary>
    public void CheckIfShouldFlip(int inputDirection)
    {
        if (inputDirection != 0 && inputDirection != facingDir)
            Flip();
    }

    /// <summary>Reverses the player's facing direction.</summary>
    public void Flip()
    {
        facingDir *= -1;
        transform.Rotate(0f, 180f, 0f);
    }

    /// <summary>Returns whether a walkable surface is currently detected.</summary>
    public bool IsGroundDetected()
    {
        return HasGroundContact;
    }

    /// <summary>Returns zero while grounded so downhill movement does not trigger falling animation.</summary>
    public float GetAnimationVerticalVelocity()
    {
        return ShouldRemainGrounded ? 0f : rb.linearVelocity.y;
    }

    /// <summary>Checks for a wall in the current facing direction.</summary>
    public bool IsAnyWallDetected()
    {
        if (wallCheck == null)
            return false;

        return Physics2D.Raycast(wallCheck.position, Vector2.right * facingDir, wallCheckDistance, whatIsWall);
    }

    /// <summary>Locks player-controlled interactions for a short duration.</summary>
    public void SetBusyFor(float duration)
    {
        isBusy = true;
        Invoke(nameof(ClearBusy), duration);
    }

    private void ClearBusy()
    {
        isBusy = false;
    }

    private void RefreshGroundInfo()
    {
        if (capsuleCollider == null)
        {
            currentGroundInfo = new GroundInfo(false, Vector2.up, Vector2.right, transform.position, 0f, 0f);
            groundLossTimer += Time.fixedDeltaTime;
            return;
        }

        Bounds bounds = capsuleCollider.bounds;
        float halfWidth = GetProbeHalfWidth(bounds);
        float probeY = bounds.min.y + groundProbeSkin;
        float leftX = bounds.center.x - halfWidth;
        float rightX = bounds.center.x + halfWidth;
        float centerX = bounds.center.x;
        float standardDistance = groundCheckDistance + groundProbeSkin;
        RaycastHit2D broadHit = GetBroadGroundHit(standardDistance);
        GroundProbeResult standardProbes = new GroundProbeResult();

        EvaluateGroundProbe(new Vector2(centerX, probeY), standardDistance, ref standardProbes);
        EvaluateGroundProbe(new Vector2(leftX, probeY), standardDistance, ref standardProbes);
        EvaluateGroundProbe(new Vector2(rightX, probeY), standardDistance, ref standardProbes);

        GroundProbeResult forwardProbe = new GroundProbeResult();
        if (!hasJumped && Mathf.Abs(MoveInput.x) > MinimumInput)
        {
            float inputDirection = Mathf.Sign(MoveInput.x);
            float forwardX = bounds.center.x + inputDirection * (bounds.extents.x + stepForwardDistance);
            float stepDistance = maximumStepDownDistance + groundProbeSkin;
            EvaluateGroundProbe(new Vector2(forwardX, probeY), stepDistance, ref forwardProbe);
        }

        GroundProbeResult selectedProbe = standardProbes.Found ? standardProbes : forwardProbe;
        bool foundGround = broadHit.collider != null || selectedProbe.Found;
        if (!foundGround)
        {
            LoseGroundContact();
            return;
        }

        Vector2 normal = selectedProbe.Found ? selectedProbe.AverageNormal : broadHit.normal.normalized;
        Vector2 tangent = new Vector2(normal.y, -normal.x).normalized;
        Vector2 point = selectedProbe.Found ? selectedProbe.AveragePoint : broadHit.point;
        float gap = selectedProbe.Found
            ? Mathf.Max(0f, selectedProbe.ClosestDistance - groundProbeSkin)
            : 0f;
        float angle = Vector2.SignedAngle(Vector2.right, tangent);
        SetGroundContact(normal, tangent, point, gap, angle);
    }

    private void SetGroundContact(Vector2 normal, Vector2 tangent, Vector2 point, float gap, float angle)
    {
        currentGroundInfo = new GroundInfo(true, normal, tangent, point, gap, angle);
        lastWalkableGroundInfo = currentGroundInfo;
        groundLossTimer = 0f;
    }

    private void LoseGroundContact()
    {
        currentGroundInfo = new GroundInfo(
            false,
            lastWalkableGroundInfo.Normal,
            lastWalkableGroundInfo.Tangent,
            lastWalkableGroundInfo.Point,
            lastWalkableGroundInfo.Distance,
            lastWalkableGroundInfo.Angle);
        groundLossTimer += Time.fixedDeltaTime;
    }

    /// <summary>Returns half of the effective ground probe span, never wider than the collider.</summary>
    private float GetProbeHalfWidth(Bounds bounds)
    {
        return Mathf.Min(groundProbeWidth, bounds.size.x) * 0.5f;
    }

    private RaycastHit2D GetBroadGroundHit(float distance)
    {
        Bounds bounds = capsuleCollider.bounds;
        Vector2 origin = groundCheck != null
            ? groundCheck.position
            : new Vector2(bounds.center.x, bounds.min.y + groundProbeSkin);
        Vector2 size = new Vector2(GetProbeHalfWidth(bounds) * 2f, groundCheckSize.y);
        RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, distance, whatIsGround);
        return IsWalkable(hit) ? hit : default;
    }

    private void EvaluateGroundProbe(Vector2 origin, float distance, ref GroundProbeResult result)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, distance, whatIsGround);
        Debug.DrawRay(origin, Vector2.down * distance, hit.collider != null ? Color.green : Color.red, GroundProbeDrawDuration);

        if (IsWalkable(hit))
        {
            result.Add(hit);
        }
    }

    private bool IsWalkable(RaycastHit2D hit)
    {
        return hit.collider != null && Vector2.Angle(hit.normal, Vector2.up) <= maxSlopeAngle;
    }

    private void OnDrawGizmosSelected()
    {
        CapsuleCollider2D capsule = capsuleCollider != null ? capsuleCollider : GetComponent<CapsuleCollider2D>();
        if (capsule == null)
            return;

        Bounds bounds = capsule.bounds;
        float halfWidth = GetProbeHalfWidth(bounds);
        float probeY = bounds.min.y + groundProbeSkin;
        float distance = groundCheckDistance + groundProbeSkin;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector2(bounds.center.x - halfWidth, probeY), new Vector2(bounds.center.x - halfWidth, probeY - distance));
        Gizmos.DrawLine(new Vector2(bounds.center.x, probeY), new Vector2(bounds.center.x, probeY - distance));
        Gizmos.DrawLine(new Vector2(bounds.center.x + halfWidth, probeY), new Vector2(bounds.center.x + halfWidth, probeY - distance));
    }
}
