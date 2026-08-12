using UnityEngine;

/// <summary>Shared behavior for player states that require ground contact.</summary>
public class PlayerGroundedState : PlayerState
{
    public PlayerGroundedState(Player player, PlayerStateMachine stateMachine, string animBoolName)
        : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        player.hasJumped = false;
    }

    public override void Update()
    {
        base.Update();

        bool grounded = player.IsGroundDetected();
        if (!grounded)
        {
            stateMachine.ChangeState(player.airState);
            return;
        }

        player.MarkGrounded();

        if (player.InputReader.HasBufferedJump(player.jumpBufferTime))
        {
            player.InputReader.ConsumeJumpBuffer();
            player.ConsumeCoyoteJump();
            stateMachine.ChangeState(player.jumpState);
        }
    }
}
