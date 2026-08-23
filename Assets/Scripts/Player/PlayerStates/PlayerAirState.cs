using UnityEngine;

/// <summary>Handles horizontal air control, falling, and landing transitions.</summary>
public sealed class PlayerAirState : PlayerState
{
    public PlayerAirState(Player player, PlayerStateMachine stateMachine, string animBoolName)
        : base(player, stateMachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();
        player.CheckIfShouldFlip(InputDirection);

        if (!player.IsGroundDetected() && TryStartBufferedJump())
        {
            return;
        }

        TryLand();
    }

    public override void FixedUpdate()
    {
        if (TryLand())
        {
            stateMachine.CurrentState?.FixedUpdate();
            return;
        }

        player.ApplyAirMovement(xInput);
        player.ApplyGravity(false, false);
    }

    private bool TryLand()
    {
        if (!player.IsGroundDetected())
        {
            return false;
        }

        player.MarkGrounded();
        player.TriggerLandingSquash();

        if (TryStartBufferedJump())
        {
            return true;
        }

        stateMachine.ChangeState(HasMovementInput ? player.moveState : player.idleState);
        return true;
    }
}
