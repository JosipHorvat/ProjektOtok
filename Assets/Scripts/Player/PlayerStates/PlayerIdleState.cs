using UnityEngine;

/// <summary>Player state used while there is no horizontal movement input.</summary>
public sealed class PlayerIdleState : PlayerGroundedState
{
    public PlayerIdleState(Player player, PlayerStateMachine stateMachine, string animBoolName)
        : base(player, stateMachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();

        if (!IsCurrentState)
        {
            return;
        }

        if (HasMovementInput)
        {
            stateMachine.ChangeState(player.moveState);
        }
    }

    public override void FixedUpdate()
    {
        player.ApplyGroundMovement(0f);
    }
}
