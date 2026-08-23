using UnityEngine;

public sealed class PlayerJumpState : PlayerState
{
    private float jumpTime;
    private bool jumpCut;

    public PlayerJumpState(Player player, PlayerStateMachine stateMachine, string animBoolName)
        : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        player.hasJumped = true;
        jumpTime = 0f;
        jumpCut = false;

        player.SetVerticalVelocity(player.jumpForce);
        player.CheckIfShouldFlip(InputDirection);
    }

    public override void Update()
    {
        base.Update();

        player.CheckIfShouldFlip(InputDirection);

        // If the player has landed, immediately leave the jump state.
        if (player.HasGroundContact && rb.linearVelocity.y <= 0f)
        {
            player.MarkGrounded();

            player.TriggerLandingSquash();

            stateMachine.ChangeState(
                HasMovementInput
                    ? player.moveState
                    : player.idleState);

            return;
        }

        if (!jumpCut && rb.linearVelocity.y > 0f)
        {
            jumpTime += Time.deltaTime;

            bool jumpTimeExpired = jumpTime >= player.jumpTimeMax;

            if (jumpTimeExpired || player.InputReader.JumpReleasedThisFrame)
            {
                CutJumpShort();
            }
        }

        if (rb.linearVelocity.y <= 0f)
        {
            stateMachine.ChangeState(player.airState);
        }
    }

    public override void FixedUpdate()
    {
        player.ApplyAirMovement(xInput);
        player.ApplyGravity(false, jumpCut);
    }

    private void CutJumpShort()
    {
        jumpCut = true;

        player.SetVerticalVelocity(
            rb.linearVelocity.y * player.JumpCutVelocityMultiplier);
    }
}