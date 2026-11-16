using UnityEngine;

/// <summary>Shared behavior for player states that require ground contact.</summary>
public class PlayerGroundedState : PlayerState
{
    public PlayerGroundedState(Player player, PlayerStateMachine stateMachine, string animBoolName)
        : base(player, stateMachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();

        if (!player.HasGroundContact)
        {
            if (TryStartBufferedJump())
            {
                return;
            }

            // Ground contact is sampled per physics step. Wait through one missed
            // sample so a transient probe gap cannot bounce the state machine.
            if (!player.IsGroundContactLossConfirmed)
            {
                return;
            }

            stateMachine.ChangeState(player.airState);
            return;
        }

        player.MarkGrounded();
        if (TryStartBufferedJump())
        {
            return;
        }

        player.TryStartQuickStep(xInput);
    }
}
