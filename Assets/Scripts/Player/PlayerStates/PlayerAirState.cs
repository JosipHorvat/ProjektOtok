using UnityEngine;

/// <summary>Handles horizontal air control, coyote jumps, falling, and landing transitions.</summary>
public sealed class PlayerAirState : PlayerState
{
    public PlayerAirState(
        Player player,
        PlayerStateMachine stateMachine,
        string animBoolName)
        : base(
            player,
            stateMachine,
            animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();

        player.CheckIfShouldFlip(InputDirection);

        // A grounded result is finalized in FixedUpdate so buffered jumps
        // are handled by TryLand instead of competing with the landing transition.
        if (player.HasGroundContact)
        {
            return;
        }

        // A jump pressed shortly after leaving a ledge can still use coyote time.
        // A buffered press that misses coyote time remains available for landing.
        if (TryStartBufferedJump())
        {
            return;
        }
    }

    public override void FixedUpdate()
    {
        // Player.FixedUpdate() refreshes ground information immediately before
        // this method, so landing uses the freshest physics result.
        if (TryLand())
        {
            return;
        }

        player.ApplyAirMovement(xInput);
        player.ApplyGravity(false, false);
    }

    private bool TryLand()
    {
        if (!player.IsGroundDetected())
        {
            return false;
        }

        float impactSpeed = Mathf.Max(0f, -rb.linearVelocity.y);

        player.StopAtGround();
        player.MarkGrounded();
        player.TriggerLandingSquash(impactSpeed);

        if (TryStartBufferedJump())
        {
            return true;
        }

        bool hasMovementInput = player.HasLiveMovementInput;

        if (!hasMovementInput)
        {
            player.SetHorizontalVelocity(0f);
        }

        stateMachine.ChangeState(
            hasMovementInput
                ? player.moveState
                : player.idleState);

        return true;
    }
}
