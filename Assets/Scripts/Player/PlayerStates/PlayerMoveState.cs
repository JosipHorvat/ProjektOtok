using UnityEngine;

/// <summary>Player state used while horizontal movement input is active.</summary>
public class PlayerMoveState : PlayerGroundedState
{
    public PlayerMoveState(Player player, PlayerStateMachine stateMachine, string animBoolName)
        : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        player.CheckIfShouldFlip(Mathf.RoundToInt(xInput));
    }

    public override void Update()
    {
        base.Update();

        if (stateMachine.CurrentState != this)
        {
            return;
        }

        if (Mathf.Abs(xInput) <= 0.01f)
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }

        player.CheckIfShouldFlip(Mathf.RoundToInt(xInput));
    }

    public override void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        float targetVelocityX = xInput * player.moveSpeed;
        float movementStep = player.smoothMovement * player.moveSpeed * Time.fixedDeltaTime;
        float velocityX = Mathf.MoveTowards(rb.linearVelocity.x, targetVelocityX, movementStep);
        player.SetHorizontalVelocity(velocityX);
    }
}
