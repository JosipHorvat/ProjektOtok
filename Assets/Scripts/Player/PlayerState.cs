using UnityEngine;

/// <summary>Base behavior shared by player states.</summary>
public class PlayerState
{
    protected PlayerStateMachine stateMachine;
    protected Player player;
    protected Rigidbody2D rb;
    protected float xInput;
    protected float yInput;
    protected string animBoolName;
    protected float stateTimer;
    protected bool triggerCalled;

    public PlayerState(Player player, PlayerStateMachine stateMachine, string animBoolName)
    {
        this.player = player;
        this.stateMachine = stateMachine;
        this.animBoolName = animBoolName;
    }

    /// <summary>Initializes references and activates this state's animation flag.</summary>
    public virtual void Enter()
    {
        rb = player.rb;
        triggerCalled = false;

        if (player.anim != null && !string.IsNullOrEmpty(animBoolName))
        {
            player.anim.SetBool(animBoolName, true);
        }
    }

    /// <summary>Reads shared player input for the current state.</summary>
    public virtual void Update()
    {
        stateTimer -= Time.deltaTime;

        Vector2 moveInput = player.MoveInput;
        xInput = moveInput.x;
        yInput = moveInput.y;

        if (player.anim != null && rb != null)
        {
            player.anim.SetFloat("yVelocity", rb.linearVelocity.y);
        }
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

    /// <summary>Marks the state's animation trigger as complete.</summary>
    public virtual void AnimationFinishTrigger()
    {
        triggerCalled = true;
    }
}
