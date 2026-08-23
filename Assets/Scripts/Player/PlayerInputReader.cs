using UnityEngine;

/// <summary>Owns the generated input actions and exposes player input to gameplay systems.</summary>
[DefaultExecutionOrder(-100)]
public sealed class PlayerInputReader : MonoBehaviour
{
    private InputSystem_Actions inputActions;
    private Vector2 moveInput;
    private float lastJumpPressedTime = float.NegativeInfinity;
    private bool jumpWasHeldLastFrame;

    /// <summary>Current movement input from the Player action map.</summary>
    public Vector2 MoveInput => moveInput;

    /// <summary>Returns true during the frame in which the Interact action is pressed.</summary>
    public bool InteractPressedThisFrame =>
        CanReadGameplayInput &&
        inputActions != null &&
        inputActions.Player.Interact.WasPressedThisFrame();

    /// <summary>Returns true while the Interact action is held.</summary>
    public bool InteractHeld =>
        CanReadGameplayInput &&
        inputActions != null &&
        inputActions.Player.Interact.IsPressed();

    /// <summary>Returns true during the frame in which the Pause action is pressed.</summary>
    public bool PausePressedThisFrame =>
        inputActions != null &&
        inputActions.Player.Pause.WasPressedThisFrame();

    /// <summary>Returns true while the jump action is held.</summary>
    public bool JumpHeld =>
        CanReadGameplayInput &&
        inputActions != null &&
        inputActions.Player.Jump.IsPressed();

    /// <summary>Returns true during the frame in which the Jump action is released.</summary>
    public bool JumpReleasedThisFrame =>
        CanReadGameplayInput &&
        inputActions != null &&
        inputActions.Player.Jump.WasReleasedThisFrame();

    private bool CanReadGameplayInput =>
        GameManager.Instance == null ||
        !GameManager.Instance.IsGameplayInputLocked;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        inputActions?.Player.Enable();
    }

    private void OnDisable()
    {
        inputActions?.Player.Disable();
        ClearGameplayInput();
    }

    private void OnDestroy()
    {
        inputActions?.Dispose();
    }

    private void Update()
    {
        if (!CanReadGameplayInput)
        {
            ClearGameplayInput();
            return;
        }

        SetMoveInput(
            inputActions.Player.Move.ReadValue<Vector2>());

        bool jumpHeld = inputActions.Player.Jump.IsPressed();

        // Register a jump only when the button changes
        // from not held -> held.
        if (jumpHeld && !jumpWasHeldLastFrame)
        {
            lastJumpPressedTime = Time.time;
        }

        jumpWasHeldLastFrame = jumpHeld;
    }

    /// <summary>Returns whether a jump was buffered within the requested duration.</summary>
    public bool HasBufferedJump(float bufferDuration)
    {
        return CanReadGameplayInput &&
               Time.time <= lastJumpPressedTime + bufferDuration;
    }

    /// <summary>Clears the buffered jump input.</summary>
    public void ConsumeJumpBuffer()
    {
        lastJumpPressedTime = float.NegativeInfinity;
    }

    private void ClearGameplayInput()
    {
        SetMoveInput(Vector2.zero);
        ConsumeJumpBuffer();
        jumpWasHeldLastFrame = false;
    }

    private void SetMoveInput(Vector2 nextInput)
    {
        if (moveInput == nextInput)
        {
            return;
        }

        moveInput = nextInput;
    }
}