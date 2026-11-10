using UnityEngine;

/// <summary>Owns player movement and controls while attached to a ladder.</summary>
public sealed class PlayerLadderState : PlayerState
{
    private const float ClimbInputDeadZone = 0.1f;
    private const float LadderJumpAwaySpeed = 2.5f;
    private const string OnLadderParameter = "OnLadder";
    private const string ClimbParameter = "Climb";

    private LadderClimbVolume ladder;
    private int dismountDirection;

    public PlayerLadderState(Player player, PlayerStateMachine stateMachine)
        : base(player, stateMachine, string.Empty)
    {
    }

    /// <summary>Snaps to the ladder and activates ladder-only visuals and equipment handling.</summary>
    public override void Enter()
    {
        base.Enter();
        ladder = player.ActiveLadder;

        if (ladder == null)
        {
            return;
        }

        player.AlignToLadder(ladder);
        dismountDirection = -player.facingDir;
        rb.linearVelocity = Vector2.zero;
        player.SetLadderVisualMode(true);
        player.SetLadderGroundCollisionMode(true);
    }

    /// <summary>Handles jump-off, ladder endpoint exits, and the ladder animation direction.</summary>
    public override void Update()
    {
        base.Update();

        if (ladder == null)
        {
            return;
        }

        if (player.InputReader != null && player.InputReader.HasBufferedJump(player.jumpBufferTime))
        {
            player.InputReader.ConsumeJumpBuffer();
            player.SetFacingDirection(dismountDirection);
            player.SetHorizontalVelocity(dismountDirection * LadderJumpAwaySpeed);
            stateMachine.ChangeState(player.jumpState);
            return;
        }

        float climbInput = ReadClimbInput();
        if (player.anim != null)
        {
            player.anim.SetFloat(ClimbParameter, climbInput);
        }

        if (climbInput > 0f && ladder.IsAtEndpoint(player, true))
        {
            ExitAtEndpoint();
            return;
        }

        if (climbInput < 0f && ladder.IsAtEndpoint(player, false))
        {
            ExitAtEndpoint();
            return;
        }
    }

    /// <summary>Moves vertically along the trigger bounds and holds horizontal alignment.</summary>
    public override void FixedUpdate()
    {
        if (ladder == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float climbInput = ReadClimbInput();
        Vector2 limits = ladder.GetRootYLimits(player);
        Vector2 currentPosition = rb.position;
        float nextY = Mathf.Clamp(
            currentPosition.y + climbInput * ladder.ClimbSpeed * Time.fixedDeltaTime,
            limits.x,
            limits.y);

        rb.linearVelocity = Vector2.zero;
        rb.MovePosition(new Vector2(ladder.GetPlayerRootX(player, player.facingDir), nextY));
    }

    private void ExitAtEndpoint()
    {
        player.SetFacingDirection(dismountDirection);
        player.ExitLadderAtEndpoint(ladder, dismountDirection);
    }

    private float ReadClimbInput()
    {
        float climbInput = player.InputReader != null ? player.InputReader.ClimbInput : 0f;
        return Mathf.Abs(climbInput) <= ClimbInputDeadZone ? 0f : climbInput;
    }

    /// <summary>Restores ordinary animation and weapon handling when leaving the ladder.</summary>
    public override void Exit()
    {
        player.SetLadderGroundCollisionMode(false);
        player.SuppressAirborneLadderGrabUntilClimbReleased();
        player.SetLadderVisualMode(false);
        if (player.anim != null)
        {
            player.anim.SetFloat(ClimbParameter, 0f);
            player.anim.SetBool(OnLadderParameter, false);
        }

        player.ReleaseActiveLadder();
        ladder = null;
        base.Exit();
    }
}
