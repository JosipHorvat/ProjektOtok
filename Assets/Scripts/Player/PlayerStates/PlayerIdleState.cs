using UnityEngine;

/// <summary>Player state used while there is no horizontal movement input.</summary>
public class PlayerIdleState : PlayerGroundedState
{
    public PlayerIdleState(Player player, PlayerStateMachine stateMachine, string animBoolName)
        : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        player.SetHorizontalVelocity(0f);
    }

    public override void Update()
    {
        base.Update();

        if (Mathf.Abs(xInput) > 0.01f)
        {
            stateMachine.ChangeState(player.moveState);
        }
    }

    public override void FixedUpdate()
    {
       
        float movementStep = player.smoothMovement * player.moveSpeed * Time.fixedDeltaTime;
        float velocityX = Mathf.MoveTowards(rb.linearVelocity.x, 0f, movementStep);
        player.SetHorizontalVelocity(velocityX);
    }
}
