using UnityEngine;

public class PlayerJumpState : PlayerState
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
        player.CheckIfShouldFlip(Mathf.RoundToInt(xInput));
    }

    public override void Update()
    {
        base.Update();

        player.CheckIfShouldFlip(Mathf.RoundToInt(xInput));

        if (jumpTime < player.jumpTimeMax)
        {
            jumpTime += Time.deltaTime;
        }

        if (!jumpCut &&
            jumpTime < player.jumpTimeMax &&
            player.InputReader.JumpReleasedThisFrame &&
            rb.linearVelocity.y > 0f)
        {
            CutJumpShort();
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
        player.SetVerticalVelocity(rb.linearVelocity.y * player.JumpCutVelocityMultiplier);
    }
}
