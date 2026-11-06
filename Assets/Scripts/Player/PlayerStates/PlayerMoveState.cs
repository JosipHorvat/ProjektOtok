using UnityEngine;

/// <summary>Player state used while horizontal movement input is active.</summary>
public sealed class PlayerMoveState : PlayerGroundedState
{
    public PlayerMoveState(Player player, PlayerStateMachine stateMachine, string animBoolName)
        : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        player.CheckIfShouldFlip(InputDirection);
    }

    public override void Update()
    {
        base.Update();

        if (stateMachine.CurrentState != this)
        {
            return;
        }

        bool movingTowardFacingWall =
           InputDirection == player.facingDir &&
           player.IsAnyWallDetected();

        if (movingTowardFacingWall)
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }

        if (Mathf.Abs(xInput) <= PlayerState.HorizontalInputThreshold)
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }

        player.CheckIfShouldFlip(InputDirection);
    }

    public override void FixedUpdate()
    {
        player.ApplyGroundMovement(xInput);
    }
}
