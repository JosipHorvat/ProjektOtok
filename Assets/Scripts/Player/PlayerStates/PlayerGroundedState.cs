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

        if (!player.ShouldRemainGrounded)
        {
            stateMachine.ChangeState(player.airState);
            return;
        }

        if (player.HasGroundContact)
        {
            player.MarkGrounded();
            TryStartBufferedJump();
        }
    }
}
