using UnityEngine;

/// <summary>Runs a short committed grounded evade, then returns to normal locomotion.</summary>
public sealed class PlayerQuickStepState : PlayerState
{
    private int movementDirection = -1;
    private float elapsedTime;

    public PlayerQuickStepState(Player player, PlayerStateMachine stateMachine)
        : base(player, stateMachine, string.Empty)
    {
    }

    /// <summary>Sets the horizontal direction used by the next quick-step.</summary>
    public void SetMovementDirection(int direction)
    {
        movementDirection = direction < 0 ? -1 : 1;
    }

    /// <summary>Starts the quick-step animation and resets its committed movement timer.</summary>
    public override void Enter()
    {
        base.Enter();
        elapsedTime = 0f;
        player.TriggerQuickStepAnimation();
    }

    /// <summary>Transitions to running when held forward, otherwise exits after the step and recovery.</summary>
    public override void Update()
    {
        base.Update();
        elapsedTime += Time.deltaTime;

        if (!player.HasGroundContact)
        {
            stateMachine.ChangeState(player.airState);
            return;
        }

        if (elapsedTime >= player.QuickStepRunHoldThreshold && player.IsForwardRunRequested)
        {
            stateMachine.ChangeState(player.moveState);
            return;
        }

        if (elapsedTime < player.QuickStepTotalDuration)
        {
            return;
        }

        if (TryStartBufferedJump())
        {
            return;
        }

        stateMachine.ChangeState(
            Mathf.Abs(player.MoveInput.x) > HorizontalInputThreshold
                ? player.moveState
                : player.idleState);
    }

    /// <summary>Applies the quick-step impulse and controlled recovery along the current ground.</summary>
    public override void FixedUpdate()
    {
        if (!player.HasGroundContact)
        {
            return;
        }

        float movementBlend = 1f;
        if (elapsedTime >= player.QuickStepTravelDuration)
        {
            float totalDuration = player.QuickStepTotalDuration;
            movementBlend = totalDuration > player.QuickStepTravelDuration
                ? 1f - Mathf.InverseLerp(
                    player.QuickStepTravelDuration,
                    totalDuration,
                    elapsedTime)
                : 0f;
        }

        player.ApplyQuickStepMovement(
            movementDirection * Mathf.Clamp01(movementBlend),
            player.QuickStepSpeed);
    }
}
