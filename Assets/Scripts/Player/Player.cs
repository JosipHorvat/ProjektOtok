using System;
using UnityEngine;

/// <summary>Applies player movement to the physics body and exposes shared player data to states.</summary>
public class Player : MonoBehaviour
{
    public Animator anim { get; private set; }
    public Rigidbody2D rb { get; private set; }
    public PlayerInputReader InputReader { get; private set; }
    private PlayerSquash playerSquash;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float smoothMovement = 10f;
    public float acceleration = 120f;
    public float airDeceleration = 30f;

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
    private bool coyoteJumpAvailable;

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

    public event Action<Vector2> MoveInputChanged;

    public Vector2 MoveInput => InputReader == null ? Vector2.zero : InputReader.MoveInput;

    private void Awake()
    {
        stateMachine = new PlayerStateMachine();
        idleState = new PlayerIdleState(this, stateMachine, "Idle");
        moveState = new PlayerMoveState(this, stateMachine, "Move");
        jumpState = new PlayerJumpState(this, stateMachine, "Jump");
        airState = new PlayerAirState(this, stateMachine, "Jump");

        anim = GetComponentInChildren<Animator>();
        playerSquash = anim != null ? anim.GetComponent<PlayerSquash>() : null;
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        InputReader = GetComponent<PlayerInputReader>();

        if (InputReader == null)
            InputReader = gameObject.AddComponent<PlayerInputReader>();

        InputReader.MoveInputChanged += HandleMoveInputChanged;
    }

    private void Start()
    {
        stateMachine.Initialize(idleState);
    }

    private void Update()
    {
        stateMachine.CurrentState?.Update();
    }

    private void FixedUpdate()
    {
        stateMachine.CurrentState?.FixedUpdate();
    }

    // Gameplay input locking is owned by GameManager and enforced by PlayerInputReader.

    private void HandleMoveInputChanged(Vector2 nextInput)
    {
        MoveInputChanged?.Invoke(MoveInput);
    }

    public void AnimationTrigger() => stateMachine.CurrentState?.AnimationFinishTrigger();

    /// <summary>Triggers the visual squash effect when the player lands.</summary>
    public void TriggerLandingSquash()
    {
        playerSquash?.TriggerSquash();
    }

    public void SetHorizontalVelocity(float velocityX)
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(velocityX, rb.linearVelocity.y);
        }
    }

    public void SetVerticalVelocity(float velocityY)
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, velocityY);
        }
    }

    public void ApplyAirMovement(float horizontalInput)
    {
        float targetVelocityX = horizontalInput * moveSpeed;
        float rate = Mathf.Abs(horizontalInput) > 0f ? acceleration : airDeceleration;
        float velocityX = Mathf.MoveTowards(rb.linearVelocity.x, targetVelocityX, rate * Time.fixedDeltaTime);
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
        float velocityY = Mathf.MoveTowards(rb.linearVelocity.y, -maxFallSpeed, gravity * Time.fixedDeltaTime);
        SetVerticalVelocity(velocityY);
    }

    public void MarkGrounded()
    {
        lastGroundedTime = Time.time;
        coyoteJumpAvailable = true;
    }

    public bool CanUseCoyoteJump()
    {
        return coyoteJumpAvailable && Time.time <= lastGroundedTime + coyoteTime;
    }

    public void ConsumeCoyoteJump()
    {
        coyoteJumpAvailable = false;
    }

    public void SetZeroVelocity()
    {
        rb.linearVelocity = Vector2.zero;
    }

    public void CheckIfShouldFlip(int inputDirection)
    {
        if (inputDirection != 0 && inputDirection != facingDir)
        {
            Flip();
        }
    }

    public void Flip()
    {
        facingDir *= -1;
        transform.Rotate(0, 180, 0);
    }

    public bool IsGroundDetected()
    {
        Vector2 origin = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position;
        Vector2 size = new Vector2(groundCheckSize.x * 1.2f, groundCheckSize.y);
        return Physics2D.BoxCast(origin, size, 0f, Vector2.down, groundCheckDistance, whatIsGround);
    }

    public bool IsAnyWallDetected()
    {
        if (wallCheck == null)
        {
            return false;
        }

        return Physics2D.Raycast(wallCheck.position, Vector2.right * facingDir, wallCheckDistance, whatIsWall);
    }

    public void SetBusyFor(float duration)
    {
        isBusy = true;
        Invoke(nameof(ClearBusy), duration);
    }

    private void ClearBusy()
    {
        isBusy = false;
    }

    private void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Vector2 origin = groundCheck.position;
            Vector2 size = new Vector2(groundCheckSize.x * 1.2f, groundCheckSize.y);
            Gizmos.DrawWireCube(origin + Vector2.down * groundCheckDistance * 0.5f, size + new Vector2(0f, groundCheckDistance));
        }

        if (wallCheck != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(wallCheck.position, wallCheck.position + Vector3.right * facingDir * wallCheckDistance);
        }
    }
}
