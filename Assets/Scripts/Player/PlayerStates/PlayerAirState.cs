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

        bool hasBufferedJump = player.InputReader.HasBufferedJump(player.jumpBufferTime);
        if (hasBufferedJump && player.CanUseCoyoteJump())
        {
            player.InputReader.ConsumeJumpBuffer();
            player.ConsumeCoyoteJump();
            stateMachine.ChangeState(player.jumpState);
            return;
        }

        if (player.HasGroundContact)
        {
            player.MarkGrounded();
            player.TriggerLandingSquash();

            if (TryStartBufferedJump())
            {
                return;
            }

            stateMachine.ChangeState(HasMovementInput ? player.moveState : player.idleState);
        }
    }

    public override void FixedUpdate()
    {
        player.ApplyAirMovement(xInput);
        player.ApplyGravity(false, false);
    }
}
