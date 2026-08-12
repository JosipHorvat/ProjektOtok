using UnityEngine;

/// <summary>Handles horizontal air control, falling, and landing transitions.</summary>
public class PlayerAirState : PlayerState
{
    public PlayerAirState(Player player, PlayerStateMachine stateMachine, string animBoolName)
        : base(player, stateMachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();
        player.CheckIfShouldFlip(Mathf.RoundToInt(xInput));

        bool hasBufferedJump = player.InputReader.HasBufferedJump(player.jumpBufferTime);
        if (hasBufferedJump && player.CanUseCoyoteJump())
        {
            player.InputReader.ConsumeJumpBuffer();
            player.ConsumeCoyoteJump();
            stateMachine.ChangeState(player.jumpState);
            return;
        }

        if (player.IsGroundDetected())
        {
            player.MarkGrounded();
            player.TriggerLandingSquash();

            if (hasBufferedJump)
            {
                player.InputReader.ConsumeJumpBuffer();
                player.ConsumeCoyoteJump();
                stateMachine.ChangeState(player.jumpState);
                return;
            }

            stateMachine.ChangeState(Mathf.Abs(xInput) > 0.01f ? player.moveState : player.idleState);
        }
    }

    public override void FixedUpdate()
    {
        player.ApplyAirMovement(xInput);
        player.ApplyGravity(false, false);
    }
}
