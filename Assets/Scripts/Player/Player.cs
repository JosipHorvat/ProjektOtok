using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(PlayerInputReader))]
public class Player : MonoBehaviour
{
    #region Constants

    private const float MinimumInput = PlayerState.HorizontalInputThreshold;
    private const float MinimumTangentHorizontal = 0.01f;
    private const float GroundedVerticalVelocityThreshold = 0.1f;
    private const float JumpApexVelocityThreshold = 2.5f;
    private const float LadderGrabInputDeadZone = 0.1f;
    private const float MinimumQuickStepTravelDuration = 0.01f;
    private const string OnLadderAnimatorParameter = "OnLadder";
    private const string ClimbAnimatorParameter = "Climb";
    private const string LadderAnimatorLayerName = "Ladder";
    private const string LadderFacingRightAnimatorParameter = "LadderFacingRight";
    private const string QuickStepAnimatorTrigger = "QuickStep";
    private const string RunSpeedAnimatorParameter = "RunSpeed";

    #endregion

    #region Component References

    public Animator anim { get; private set; }
    public Rigidbody2D rb { get; private set; }
    public PlayerInputReader InputReader { get; private set; }

    private CapsuleCollider2D capsuleCollider;
    private PlayerSquash playerSquash;
    private WeaponHoldController weaponHoldController;
    private bool hasQuickStepAnimatorTrigger;
    private bool hasRunSpeedAnimatorParameter;
    private LadderClimbVolume availableLadder;
    private LadderClimbVolume activeLadder;
    private readonly List<Collider2D> ladderIgnoredGroundColliders = new List<Collider2D>();

    #endregion

    #region Movement Settings

    [Header("Movement")]
    public float moveSpeed = 5f;
    [SerializeField, Range(0.1f, 1f)] private float aimingMoveSpeedMultiplier = 0.75f;
    public float smoothMovement = 10f;
    public float acceleration = 120f;
    public float airDeceleration = 30f;

    [Header("Quick Step and Run")]
    [SerializeField, Min(0f)] private float quickStepDistance = 1.7f;
    [SerializeField, Min(MinimumQuickStepTravelDuration)] private float quickStepTravelDuration = 0.18f;
    [SerializeField, Min(0f)] private float quickStepRecoveryDuration = 0.08f;
    [SerializeField, Min(0f)] private float quickStepRunHoldThreshold = 0.12f;
    [SerializeField, Range(1f, 2f)] private float runSpeedMultiplier = 1.35f;

    #endregion

    #region Slope and Grounding Settings

    [Header("Slope Movement")]
    [SerializeField, Range(0f, 89f)]
    private float maxSlopeAngle = 50f;

    [SerializeField]
    private float slopeStickSpeed = 6f;

    [SerializeField]
    private float maximumGroundSnapSpeed = 25f;

    [Tooltip("Extra cast distance used to keep ground contact stable over small gaps and slopes.")]
    [SerializeField]
    private float groundProbeSkin = 0.05f;

    #endregion

    #region Jump Settings

    [Header("Jump")]
    public float jumpForce = 10f;

    [Tooltip("Maximum time the player can hold jump before the upward phase is cut.")]
    public float jumpTimeMax = 0.18f;

    public float fallAcceleration = 110f;
    public float maxFallSpeed = 40f;
    public float jumpEndEarlyGravityModifier = 3f;

    [Header("Jump Apex Control")]
    [Tooltip("Gravity multiplier used briefly around the top of a full jump. Lower values create a softer apex.")]
    [Range(0.1f, 1f)]
    public float jumpApexGravityMultiplier = 0.75f;

    [Tooltip("Duration of the gentle gravity phase around the top of a full jump.")]
    [Min(0f)]
    public float jumpApexHangTime = 0.03f;

    public float groundingForce = -1.5f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;

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
    private float jumpApexTimeRemaining;
    private bool jumpApexUsed;
    private bool jumpApexControlAvailable;
    private bool suppressAirborneLadderGrabUntilClimbReleased;
    private readonly ContactPoint2D[] groundContacts = new ContactPoint2D[16];

    private GroundInfo currentGroundInfo;

    private GroundInfo lastWalkableGroundInfo = new GroundInfo(
        false,
        Vector2.up,
        Vector2.right,
        Vector2.zero,
        0f,
        0f);

    public int facingDir { get; private set; } = 1;

    #endregion

    #region State Machine

    public PlayerStateMachine stateMachine { get; private set; }

    public PlayerIdleState idleState { get; private set; }
    public PlayerMoveState moveState { get; private set; }
    public PlayerJumpState jumpState { get; private set; }
    public PlayerAirState airState { get; private set; }
    public PlayerLadderState ladderState { get; private set; }
    public PlayerQuickStepState quickStepState { get; private set; }

    #endregion

    #region Public State Data

    public Vector2 MoveInput => InputReader.MoveInput;

    public float QuickStepSpeed =>
        quickStepDistance / Mathf.Max(quickStepTravelDuration, MinimumQuickStepTravelDuration);

    public float QuickStepTravelDuration =>
        Mathf.Max(quickStepTravelDuration, MinimumQuickStepTravelDuration);

    public float QuickStepRecoveryDuration =>
        Mathf.Max(quickStepRecoveryDuration, 0f);

    public float QuickStepRunHoldThreshold =>
        Mathf.Max(quickStepRunHoldThreshold, 0f);

    public float QuickStepTotalDuration =>
        QuickStepTravelDuration + QuickStepRecoveryDuration;

    public bool IsForwardRunRequested =>
        InputReader != null &&
        InputReader.QuickStepHeld &&
        !InputReader.FocusAimHeld &&
        MoveInput.x * facingDir > MinimumInput;

    public GroundInfo CurrentGroundInfo =>
        currentGroundInfo.HasGround
            ? currentGroundInfo
            : lastWalkableGroundInfo;

    public bool HasGroundContact => currentGroundInfo.HasGround;
    public bool IsOnLadder => stateMachine != null && stateMachine.CurrentState == ladderState;
    public bool CanGrabAvailableLadder =>
        availableLadder != null &&
        stateMachine != null &&
        (stateMachine.CurrentState == idleState ||
         stateMachine.CurrentState == moveState ||
         stateMachine.CurrentState == jumpState ||
         stateMachine.CurrentState == airState);
    public CapsuleCollider2D BodyCollider => capsuleCollider;
    public LadderClimbVolume ActiveLadder => activeLadder;

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

    private void Update()
    {
        stateMachine.CurrentState?.Update();
        UpdateRunAnimationSpeed();
        TryGrabNearbyLadder();
        TryEnterNearbyLadder();
    }

    private void FixedUpdate()
    {
        // Ground information is refreshed before any state physics runs.
        RefreshGroundInfo();
        ResolveGroundedState();

        stateMachine.CurrentState?.FixedUpdate();
    }

    #endregion

    #region Movement

    /// <summary>Transitions airborne states to a grounded state as soon as contact is confirmed.</summary>
    private void ResolveGroundedState()
    {
        if (!HasGroundContact ||
            rb.linearVelocity.y > GroundedVerticalVelocityThreshold ||
            (stateMachine.CurrentState != jumpState &&
             stateMachine.CurrentState != airState))
        {
            return;
        }

        float impactSpeed = Mathf.Max(0f, -rb.linearVelocity.y);

        StopAtGround();
        MarkGrounded();
        TriggerLandingSquash(impactSpeed);

        bool hasMovementInput = HasLiveMovementInput;

        if (!hasMovementInput)
        {
            SetHorizontalVelocity(0f);
        }

        stateMachine.ChangeState(
            hasMovementInput
                ? moveState
                : idleState);
    }

    /// <summary>Begins a grounded quick-step in the current input direction or backward when input is neutral.</summary>
    public bool TryStartQuickStep(float horizontalInput)
    {
        if (InputReader == null ||
            !InputReader.QuickStepPressedThisFrame ||
            !HasGroundContact ||
            (stateMachine.CurrentState != idleState && stateMachine.CurrentState != moveState))
        {
            return false;
        }

        int movementDirection =
            Mathf.Abs(horizontalInput) > MinimumInput
                ? (horizontalInput > 0f ? 1 : -1)
                : -facingDir;

        if (Mathf.Abs(horizontalInput) > MinimumInput)
        {
            CheckIfShouldFlip(movementDirection);
        }

        quickStepState.SetMovementDirection(movementDirection);
        stateMachine.ChangeState(quickStepState);
        return true;
    }

    public void SetHorizontalVelocity(float velocityX) =>
        rb.linearVelocity = new Vector2(
            velocityX,
            rb.linearVelocity.y);

    public void SetVerticalVelocity(float velocityY) =>
        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            velocityY);

    /// <summary>Registers the ladder trigger currently overlapping this player.</summary>
    public void SetAvailableLadder(LadderClimbVolume ladder)
    {
        availableLadder = ladder;
    }

    /// <summary>Clears a ladder trigger when its volume no longer overlaps this player.</summary>
    public void ClearAvailableLadder(LadderClimbVolume ladder)
    {
        if (availableLadder == ladder)
        {
            availableLadder = null;
        }
    }

    /// <summary>Aligns the player with the active ladder's climb lane.</summary>
    public void AlignToLadder(LadderClimbVolume ladder)
    {
        if (ladder == null || rb == null)
        {
            return;
        }

        Vector2 limits = ladder.GetRootYLimits(this);
        Vector2 position = rb.position;
        position.x = ladder.GetPlayerRootX(this, facingDir);
        position.y = ladder.GetNearestRungRootY(this);
        position.y = Mathf.Clamp(position.y, limits.x, limits.y);
        rb.position = position;
    }

    /// <summary>Enables or restores animation, IK, and weapon behavior for ladder traversal.</summary>
    public void SetLadderVisualMode(bool isClimbing)
    {
        if (anim != null)
        {
            anim.SetBool(OnLadderAnimatorParameter, isClimbing);
            anim.SetBool(LadderFacingRightAnimatorParameter, facingDir > 0);
            anim.SetFloat(ClimbAnimatorParameter, 0f);

            int ladderLayerIndex = anim.GetLayerIndex(LadderAnimatorLayerName);
            if (ladderLayerIndex >= 0)
            {
                anim.SetLayerWeight(ladderLayerIndex, isClimbing ? 1f : 0f);
            }
        }

        weaponHoldController?.SetLadderMode(isClimbing);
        playerSquash?.SetLadderClimbing(isClimbing);
    }

    /// <summary>Ignores only ground colliders crossing the ladder lane while attached.</summary>
    public void SetLadderGroundCollisionMode(bool ignoreGroundInLane)
    {
        if (capsuleCollider == null)
        {
            return;
        }

        if (ignoreGroundInLane)
        {
            ladderIgnoredGroundColliders.Clear();
            if (activeLadder == null || whatIsGround.value == 0)
            {
                return;
            }

            Bounds ladderBounds = activeLadder.TriggerBounds;
            Vector2 querySize = new Vector2(
                ladderBounds.size.x + capsuleCollider.bounds.size.x,
                ladderBounds.size.y);
            Collider2D[] groundColliders = Physics2D.OverlapBoxAll(
                ladderBounds.center,
                querySize,
                0f,
                whatIsGround.value);

            foreach (Collider2D groundCollider in groundColliders)
            {
                if (groundCollider == null || groundCollider == capsuleCollider || groundCollider.isTrigger)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(capsuleCollider, groundCollider, true);
                ladderIgnoredGroundColliders.Add(groundCollider);
            }

            return;
        }

        foreach (Collider2D groundCollider in ladderIgnoredGroundColliders)
        {
            if (groundCollider != null)
            {
                Physics2D.IgnoreCollision(capsuleCollider, groundCollider, false);
            }
        }

        ladderIgnoredGroundColliders.Clear();
    }

    /// <summary>Prevents a still-held climb input from immediately reattaching after dismount.</summary>
    public void SuppressAirborneLadderGrabUntilClimbReleased()
    {
        suppressAirborneLadderGrabUntilClimbReleased = true;
    }

    /// <summary>Releases the ladder reference after exiting or jumping away.</summary>
    public void ReleaseActiveLadder()
    {
        activeLadder = null;
    }

    /// <summary>Dismounts at a ladder endpoint, clears ignored collisions, and resumes ground or air movement.</summary>
    public void ExitLadderAtEndpoint(LadderClimbVolume ladder, float horizontalDirection)
    {
        if (ladder == null || rb == null || !IsOnLadder)
        {
            return;
        }

        SetLadderGroundCollisionMode(false);

        Vector2 dismountPosition = rb.position;
        dismountPosition.x = ladder.GetPlayerDismountRootX(this, horizontalDirection);
        rb.position = dismountPosition;
        rb.linearVelocity = Vector2.zero;
        stateMachine.ChangeState(airState);
    }

    private void TryGrabNearbyLadder()
    {
        if (InputReader == null)
        {
            return;
        }

        bool climbUpHeld = InputReader.ClimbInput > LadderGrabInputDeadZone;
        if (!climbUpHeld)
        {
            suppressAirborneLadderGrabUntilClimbReleased = false;
        }

        if (!CanGrabAvailableLadder)
        {
            return;
        }

        bool explicitGrabPressed = InputReader.ExplicitLadderGrabPressedThisFrame;
        bool climbUpPressed = InputReader.ClimbUpPressedThisFrame &&
                              !suppressAirborneLadderGrabUntilClimbReleased;
        bool heldWhileAirborne =
            (stateMachine.CurrentState == jumpState || stateMachine.CurrentState == airState) &&
            climbUpHeld &&
            !suppressAirborneLadderGrabUntilClimbReleased;

        if (!explicitGrabPressed && !climbUpPressed && !heldWhileAirborne)
        {
            return;
        }

        AttachToLadder(availableLadder);
    }

    private void TryEnterNearbyLadder()
    {
        if (IsOnLadder || !HasGroundContact || availableLadder == null || InputReader == null ||
            (stateMachine.CurrentState != idleState && stateMachine.CurrentState != moveState))
        {
            return;
        }

        float climbInput = InputReader.ClimbInput;
        if (Mathf.Abs(climbInput) <= PlayerState.HorizontalInputThreshold ||
            !availableLadder.CanEnter(this, climbInput))
        {
            return;
        }

        AttachToLadder(availableLadder);
    }

    private void AttachToLadder(LadderClimbVolume ladder)
    {
        if (ladder == null)
        {
            return;
        }

        activeLadder = ladder;
        int ladderDirection = ladder.GetFacingDirectionToLadder(this);
        if (ladderDirection != 0)
        {
            SetFacingDirection(ladderDirection);
        }

        stateMachine.ChangeState(ladderState);
    }

    public void SetZeroVelocity() =>
        rb.linearVelocity = Vector2.zero;

    /// <summary>Returns true when the latest movement input contains horizontal intent.</summary>
    public bool HasLiveMovementInput =>
        Mathf.Abs(MoveInput.x) > MinimumInput;

    /// <summary>Stops vertical jump or fall motion when a landing is confirmed.</summary>
    public void StopAtGround()
    {
        SetVerticalVelocity(groundingForce);
    }

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
        float currentMoveSpeed = GetGroundMoveSpeed();

        float targetHorizontalVelocity =
            horizontalInput * currentMoveSpeed;

        float horizontalVelocity = Mathf.MoveTowards(
            rb.linearVelocity.x,
            targetHorizontalVelocity,
            smoothMovement * currentMoveSpeed * Time.fixedDeltaTime);

        float tangentHorizontal = Mathf.Max(
            Mathf.Abs(tangent.x),
            MinimumTangentHorizontal);

        float slopeVerticalVelocity =
            horizontalVelocity *
            tangent.y /
            tangentHorizontal;

        if (slopeVerticalVelocity < 0f)
            slopeVerticalVelocity -= slopeStickSpeed;

        slopeVerticalVelocity -= GetGroundSnapSpeed();

        rb.linearVelocity = new Vector2(
            horizontalVelocity,
            slopeVerticalVelocity);
    }

    /// <summary>Applies the committed quick-step movement while following the current ground slope.</summary>
    public void ApplyQuickStepMovement(float horizontalInput, float quickStepSpeed)
    {
        if (!HasGroundContact)
        {
            ApplyAirMovement(horizontalInput);
            ApplyGravity(false, false);
            return;
        }

        Vector2 tangent = CurrentGroundInfo.Tangent;
        float movementSpeed = Mathf.Max(0f, quickStepSpeed);
        float targetHorizontalVelocity = horizontalInput * movementSpeed;
        float horizontalVelocity = Mathf.MoveTowards(
            rb.linearVelocity.x,
            targetHorizontalVelocity,
            smoothMovement * Mathf.Max(movementSpeed, moveSpeed) * Time.fixedDeltaTime);

        float tangentHorizontal = Mathf.Max(
            Mathf.Abs(tangent.x),
            MinimumTangentHorizontal);

        float slopeVerticalVelocity =
            horizontalVelocity * tangent.y / tangentHorizontal;

        if (slopeVerticalVelocity < 0f)
        {
            slopeVerticalVelocity -= slopeStickSpeed;
        }

        slopeVerticalVelocity -= GetGroundSnapSpeed();
        rb.linearVelocity = new Vector2(horizontalVelocity, slopeVerticalVelocity);
    }

    private float GetGroundMoveSpeed()
    {
        bool isAiming = InputReader != null && InputReader.FocusAimHeld;
        float speedMultiplier = isAiming ? aimingMoveSpeedMultiplier : 1f;
        if (IsForwardRunRequested)
        {
            speedMultiplier *= runSpeedMultiplier;
        }

        return moveSpeed * speedMultiplier;
    }

    public void ApplyAirMovement(float horizontalInput)
    {
        float targetVelocityX =
            horizontalInput * moveSpeed;

        float movementRate =
            Mathf.Abs(horizontalInput) > MinimumInput
                ? acceleration
                : airDeceleration;

        float velocityX = Mathf.MoveTowards(
            rb.linearVelocity.x,
            targetVelocityX,
            movementRate * Time.fixedDeltaTime);

        SetHorizontalVelocity(velocityX);
    }

    #endregion

 
    #region Gravity

    /// <summary>Resets the one-shot apex control for a newly started jump.</summary>
    public void ResetJumpApexControl()
    {
        jumpApexTimeRemaining = 0f;
        jumpApexUsed = false;
        jumpApexControlAvailable = true;
    }

    public void ApplyGravity(
        bool grounded,
        bool jumpEndedEarly)
    {
        if (grounded && rb.linearVelocity.y <= 0f)
        {
            SetVerticalVelocity(groundingForce);
            return;
        }

        if (jumpEndedEarly)
        {
            jumpApexTimeRemaining = 0f;
            jumpApexUsed = true;
            jumpApexControlAvailable = false;
        }

        float gravity =
            jumpEndedEarly &&
            rb.linearVelocity.y > 0f
                ? fallAcceleration *
                  jumpEndEarlyGravityModifier
                : fallAcceleration;

        if (!jumpEndedEarly &&
            (jumpApexTimeRemaining > 0f || TryStartJumpApex()))
        {
            gravity *= jumpApexGravityMultiplier;
            jumpApexTimeRemaining =
                Mathf.Max(
                    0f,
                    jumpApexTimeRemaining - Time.fixedDeltaTime);
        }

        float velocityY = Mathf.MoveTowards(
            rb.linearVelocity.y,
            -maxFallSpeed,
            gravity * Time.fixedDeltaTime);

        SetVerticalVelocity(velocityY);
    }

    private bool TryStartJumpApex()
    {
        if (jumpApexUsed ||
            !jumpApexControlAvailable ||
            jumpApexHangTime <= 0f ||
            Mathf.Abs(rb.linearVelocity.y) > JumpApexVelocityThreshold)
        {
            return false;
        }

        jumpApexUsed = true;
        jumpApexTimeRemaining = jumpApexHangTime;
        return true;
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

        if (Time.time >
            lastGroundedTime + coyoteTime)
        {
            coyoteJumpAvailable = false;
        }

        return coyoteJumpAvailable;
    }

    public void ConsumeCoyoteJump() =>
        coyoteJumpAvailable = false;

    #endregion

    #region Facing

    public void CheckIfShouldFlip(int inputDirection)
    {
        if (InputReader != null && InputReader.FocusAimHeld)
        {
            return;
        }

        SetFacingDirection(inputDirection);
    }

    /// <summary>Turns the player toward a nonzero horizontal direction.</summary>
    public void SetFacingDirection(int direction)
    {
        if (direction != 0 && direction != facingDir)
        {
            Flip();
        }
    }

    public void Flip()
    {
        facingDir *= -1;

        transform.Rotate(
            0f,
            180f,
            0f);
    }

    #endregion

    #region Animation and Interaction

    public void TriggerJumpTakeoffStretch()
    {
        playerSquash?.TriggerJumpTakeoff();
    }

    /// <summary>Plays the landing squash, scaled by how fast the player hit the ground.</summary>
    public void TriggerLandingSquash(float impactSpeed)
    {
        playerSquash?.TriggerSquash(impactSpeed);
    }

    public float GetAnimationVerticalVelocity() =>
        HasGroundContact
            ? 0f
            : rb.linearVelocity.y;

    /// <summary>Triggers the authored quick-step animation when this Animator supports it.</summary>
    public void TriggerQuickStepAnimation()
    {
        if (anim != null && hasQuickStepAnimatorTrigger)
        {
            anim.ResetTrigger(QuickStepAnimatorTrigger);
            anim.SetTrigger(QuickStepAnimatorTrigger);
        }
    }

    private void UpdateRunAnimationSpeed()
    {
        if (anim != null && hasRunSpeedAnimatorParameter)
        {
            anim.SetFloat(
                RunSpeedAnimatorParameter,
                IsForwardRunRequested ? runSpeedMultiplier : 1f);
        }
    }

    private static bool HasAnimatorParameter(
        Animator animator,
        string parameterName,
        AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Initialization

    private void InitializeStates()
    {
        stateMachine =
            new PlayerStateMachine();

        idleState =
            new PlayerIdleState(
                this,
                stateMachine,
                "Idle");

        moveState =
            new PlayerMoveState(
                this,
                stateMachine,
                "Move");

        jumpState =
            new PlayerJumpState(
                this,
                stateMachine,
                "Jump");

        airState =
            new PlayerAirState(
                this,
                stateMachine,
                "Jump");

        ladderState =
            new PlayerLadderState(
                this,
                stateMachine);

        quickStepState =
            new PlayerQuickStepState(
                this,
                stateMachine);
    }

    private void CacheComponents()
    {
        anim =
            GetComponentInChildren<Animator>();
        hasQuickStepAnimatorTrigger = HasAnimatorParameter(
            anim,
            QuickStepAnimatorTrigger,
            AnimatorControllerParameterType.Trigger);
        hasRunSpeedAnimatorParameter = HasAnimatorParameter(
            anim,
            RunSpeedAnimatorParameter,
            AnimatorControllerParameterType.Float);

        playerSquash =
            GetComponentInChildren<PlayerSquash>();

        weaponHoldController =
            GetComponentInChildren<WeaponHoldController>();

        if (playerSquash == null)
        {
            Debug.LogWarning("Player has no PlayerSquash in its children; squash and stretch is disabled.", this);
        }

        rb =
            GetComponent<Rigidbody2D>();

        capsuleCollider =
            GetComponent<CapsuleCollider2D>();

        InputReader =
            GetComponent<PlayerInputReader>();
    }

    #endregion

    #region Collision Checks

    public bool IsGroundDetected() =>
        HasGroundContact;

    public bool IsAnyWallDetected() =>
        wallCheck != null &&
        Physics2D.Raycast(
            wallCheck.position,
            Vector2.right * facingDir,
            wallCheckDistance,
            whatIsWall);

    private void RefreshGroundInfo()
    {
        Bounds bounds =
            capsuleCollider.bounds;

        Vector2 origin =
            GetGroundCastOrigin(bounds);

        RaycastHit2D hit =
            Physics2D.BoxCast(
                origin,
                GetGroundCastSize(bounds),
                0f,
                Vector2.down,
                GetGroundCastDistance(),
                whatIsGround);

        if (IsGroundHitValid(hit))
        {
            SetGroundContactFromHit(hit, bounds);
            return;
        }

        if (TrySetGroundContactFromPhysicsContacts())
        {
            return;
        }

        LoseGroundContact();
    }

    private void SetGroundContactFromHit(
        RaycastHit2D hit,
        Bounds bounds)
    {
        Vector2 normal =
            hit.normal.normalized;

        Vector2 tangent =
            new Vector2(
                normal.y,
                -normal.x).normalized;

        float distance =
            Mathf.Max(
                0f,
                bounds.min.y - hit.point.y);

        float angle =
            Vector2.SignedAngle(
                Vector2.right,
                tangent);

        SetGroundContact(
            normal,
            tangent,
            hit.point,
            distance,
            angle);
    }

    private bool TrySetGroundContactFromPhysicsContacts()
    {
        int contactCount =
            capsuleCollider.GetContacts(groundContacts);

        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint2D contact =
                groundContacts[i];

            if (contact.otherCollider == null ||
                !IsLayerInGroundMask(
                    contact.otherCollider.gameObject.layer))
            {
                continue;
            }

            Vector2 normal =
                contact.normal.normalized;

            if (normal.y < 0f)
            {
                normal = -normal;
            }

            if (normal.y <= 0f ||
                Vector2.Angle(normal, Vector2.up) > maxSlopeAngle)
            {
                continue;
            }

            Vector2 tangent =
                new Vector2(
                    normal.y,
                    -normal.x).normalized;

            float angle =
                Vector2.SignedAngle(
                    Vector2.right,
                    tangent);

            SetGroundContact(
                normal,
                tangent,
                contact.point,
                0f,
                angle);

            return true;
        }

        return false;
    }

    private bool IsLayerInGroundMask(int layer) =>
        (whatIsGround.value & (1 << layer)) != 0;

    private void SetGroundContact(
        Vector2 normal,
        Vector2 tangent,
        Vector2 point,
        float distance,
        float angle)
    {
        currentGroundInfo =
            new GroundInfo(
                true,
                normal,
                tangent,
                point,
                distance,
                angle);

        lastWalkableGroundInfo =
            currentGroundInfo;
    }

    private void LoseGroundContact()
    {
        // IMPORTANT:
        // Losing ground contact does NOT mean we are grounded.
        // Coyote time should only be started when we actually land.
        currentGroundInfo =
            new GroundInfo(
                false,
                lastWalkableGroundInfo.Normal,
                lastWalkableGroundInfo.Tangent,
                lastWalkableGroundInfo.Point,
                lastWalkableGroundInfo.Distance,
                lastWalkableGroundInfo.Angle);
    }

    private bool IsGroundHitValid(RaycastHit2D hit) =>
        IsWalkable(hit);

    private bool IsWalkable(
        RaycastHit2D hit) =>
        hit.collider != null &&
        Vector2.Angle(
            hit.normal,
            Vector2.up) <= maxSlopeAngle;

    #endregion

    #region Helpers

    private float GetGroundSnapSpeed()
    {
        float gap =
            CurrentGroundInfo.Distance;

        if (gap <= 0f)
            return 0f;

        return Mathf.Min(
            gap / Time.fixedDeltaTime,
            maximumGroundSnapSpeed);
    }

    private float GetGroundCastDistance() =>
        groundCheckDistance +
        groundProbeSkin;

    private Vector2 GetGroundCastOrigin(
        Bounds bounds)
    {
        Vector2 castSize =
            GetGroundCastSize(bounds);

        float castOriginX =
            groundCheck != null
                ? groundCheck.position.x
                : bounds.center.x;

        float castOriginY =
            bounds.min.y -
            castSize.y * 0.5f;

        return new Vector2(
            castOriginX,
            castOriginY);
    }

    private Vector2 GetGroundCastSize(
        Bounds bounds) =>
        new Vector2(
            Mathf.Min(
                groundCheckSize.x,
                bounds.size.x),
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
        CapsuleCollider2D capsule =
            capsuleCollider != null
                ? capsuleCollider
                : GetComponent<CapsuleCollider2D>();

        if (capsule == null)
            return;

        Bounds bounds =
            capsule.bounds;

        Vector2 origin =
            GetGroundCastOrigin(bounds);

        Vector2 size =
            GetGroundCastSize(bounds);

        float distance =
            GetGroundCastDistance();

        Gizmos.color =
            Color.red;

        Gizmos.DrawWireCube(
            origin,
            size);

        Gizmos.DrawWireCube(
            origin +
            Vector2.down * distance,
            size);

        DrawGizmoCastEdge(
            origin,
            size.x,
            distance,
            -0.5f);

        DrawGizmoCastEdge(
            origin,
            size.x,
            distance,
            0.5f);

        if (wallCheck != null)
        {
            Gizmos.DrawLine(
                wallCheck.position,
                wallCheck.position +
                new Vector3(
                    wallCheckDistance *
                    facingDir,
                    0));
        }
    }

    private void DrawGizmoCastEdge(
        Vector2 origin,
        float width,
        float distance,
        float side)
    {
        Vector2 edge =
            origin +
            Vector2.right *
            width *
            side;

        Gizmos.DrawLine(
            edge,
            edge +
            Vector2.down *
            distance);
    }

    #endregion
}