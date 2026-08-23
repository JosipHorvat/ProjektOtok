using UnityEngine;

/// <summary>Base behavior shared by all player states.</summary>
public abstract class PlayerState
{
    private const string VerticalVelocityParameter = "yVelocity";

    public const float HorizontalInputThreshold = 0.01f;

    protected readonly PlayerStateMachine stateMachine;
    protected readonly Player player;
    protected Rigidbody2D rb;
    protected float xInput;
    protected readonly string animBoolName;
    protected bool HasMovementInput => Mathf.Abs(xInput) > HorizontalInputThreshold;
    protected int InputDirection => Mathf.RoundToInt(xInput);
    protected bool IsCurrentState => stateMachine.CurrentState == this;

    protected PlayerState(Player player, PlayerStateMachine stateMachine, string animBoolName)
    {
        this.player = player;
        this.stateMachine = stateMachine;
        this.animBoolName = animBoolName;
    }

    /// <summary>Initializes references and activates this state's animation flag.</summary>
    public virtual void Enter()
    {
        rb = player.rb;

        if (player.anim != null && !string.IsNullOrEmpty(animBoolName))
        {
            player.anim.SetBool(animBoolName, true);
        }
    }

    /// <summary>Reads shared player input and updates common animation values.</summary>
    public virtual void Update()
    {
        xInput = player.MoveInput.x;

        if (player.anim != null && rb != null)
        {
            player.anim.SetFloat(VerticalVelocityParameter, player.GetAnimationVerticalVelocity());
        }
    }

    protected bool TryStartBufferedJump()
    {
        if (player.InputReader == null || !player.InputReader.HasBufferedJump(player.jumpBufferTime))
        {
            return false;
        }

        bool hasGroundContact = player.HasGroundContact;
        bool canUseCoyoteJump = !hasGroundContact && player.CanUseCoyoteJump();
       
        if (!hasGroundContact && !canUseCoyoteJump)
        {
            return false;
        }

        player.InputReader.ConsumeJumpBuffer();
        player.ConsumeCoyoteJump();
        stateMachine.ChangeState(player.jumpState);
        return true;
    }

    /// <summary>Applies state-specific physics movement.</summary>
    public virtual void FixedUpdate()
    {
    }

    /// <summary>Deactivates this state's animation flag.</summary>
    public virtual void Exit()
    {
        if (player.anim != null && !string.IsNullOrEmpty(animBoolName))
        {
            player.anim.SetBool(animBoolName, false);
        }
    }
}
