using UnityEngine;

/// <summary>Handles variable-height jumps, jump cutting, and the transition into falling.</summary>
public sealed class PlayerJumpState : PlayerState
{
    private float jumpTime;
    private bool jumpCut;

    public PlayerJumpState(
        Player player,
        PlayerStateMachine stateMachine,
        string animBoolName)
        : base(
            player,
            stateMachine,
            animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        player.ResetJumpApexControl();
        jumpTime = 0f;
        jumpCut = false;

        player.SetVerticalVelocity(player.jumpForce);
        player.CheckIfShouldFlip(InputDirection);
    }

    public override void Update()
    {
        base.Update();

        player.CheckIfShouldFlip(InputDirection);

        if (!jumpCut &&
            player.InputReader != null &&
            (!player.InputReader.JumpHeld ||
             player.InputReader.JumpReleasedThisFrame))
        {
            CutJumpShort();
        }

        // Keep JumpState responsible for the landing while ground contact is
        // already confirmed. This avoids an unnecessary AirState transition.
        if (rb.linearVelocity.y <= 0f &&
            !player.HasGroundContact)
        {
            stateMachine.ChangeState(player.airState);
        }
    }

    public override void FixedUpdate()
    {
        // Count jump time in physics steps so the configured hold duration
        // stays consistent with the velocity changes below.
        if (!jumpCut)
        {
            jumpTime += Time.fixedDeltaTime;

            if (jumpTime >= Mathf.Max(0f, player.jumpTimeMax))
            {
                CutJumpShort();
            }
        }

        // Only treat it as a landing when we are descending.
        if (player.IsGroundDetected() &&
            rb.linearVelocity.y <= 0f)
        {
            Land();
            return;
        }

        player.ApplyAirMovement(xInput);
        player.ApplyGravity(false, jumpCut);
    }

    private void Land()
    {
        player.StopAtGround();
        player.MarkGrounded();
        player.TriggerLandingSquash();

        if (TryStartBufferedJump())
        {
            return;
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
    }

    private void CutJumpShort()
    {
        jumpCut = true;

        if (rb.linearVelocity.y > 0f)
        {
            player.SetVerticalVelocity(
                rb.linearVelocity.y * player.JumpCutVelocityMultiplier);
        }
    }
}