using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Owns the generated input actions and exposes player input to gameplay systems.</summary>
[DefaultExecutionOrder(-100)]
public sealed class PlayerInputReader : MonoBehaviour
{
    private InputSystem_Actions inputActions;
    private InputAction grabLadderAction;
    private Player player;
    private Vector2 moveInput;
    private float lastJumpPressedTime = float.NegativeInfinity;
    private float previousClimbInput;
    private bool climbUpPressedThisFrame;

    [SerializeField]
    private bool logInputDebug = true;

    /// <summary>Current movement input from the Player action map.</summary>
    public Vector2 MoveInput => moveInput;

    /// <summary>Raw mouse screen position or gamepad right-stick aim input.</summary>
    public Vector2 AimInput =>
        CanUseCombatInput && inputActions != null
            ? inputActions.Player.Aim.ReadValue<Vector2>()
            : Vector2.zero;

    /// <summary>Returns true while the Fire/Throw action is held.</summary>
    public bool FireThrowHeld =>
        CanUseCombatInput &&
        inputActions != null &&
        inputActions.Player.FireThrow.IsPressed();

    /// <summary>Returns true during the frame in which Fire/Throw is pressed.</summary>
    public bool FireThrowPressedThisFrame =>
        CanUseCombatInput &&
        inputActions != null &&
        inputActions.Player.FireThrow.WasPressedThisFrame();

    /// <summary>Returns true while Focus Aim is held.</summary>
    public bool FocusAimHeld =>
        CanUseCombatInput &&
        inputActions != null &&
        inputActions.Player.FocusAim.IsPressed();

    /// <summary>Returns true during the frame in which Focus Aim is pressed.</summary>
    public bool FocusAimPressedThisFrame =>
        CanUseCombatInput &&
        inputActions != null &&
        inputActions.Player.FocusAim.WasPressedThisFrame();

    /// <summary>Returns true when the held Focus Aim action came from a gamepad.</summary>
    public bool FocusAimUsesGamepad =>
        FocusAimHeld &&
        inputActions != null &&
        inputActions.Player.FocusAim.activeControl?.device is Gamepad;

    /// <summary>Returns true when the current aim input comes from a gamepad.</summary>
    public bool AimUsesGamepad =>
        inputActions != null &&
        inputActions.Player.Aim.activeControl?.device is Gamepad;

    /// <summary>Returns true during the frame in which Reload is pressed.</summary>
    public bool ReloadPressedThisFrame =>
        CanUseCombatInput &&
        inputActions != null &&
        inputActions.Player.Reload.WasPressedThisFrame();

    /// <summary>Returns true during the frame in which Melee Attack is pressed.</summary>
    public bool MeleeAttackPressedThisFrame =>
        CanUseCombatInput &&
        inputActions != null &&
        inputActions.Player.MeleeAttack.WasPressedThisFrame();

    /// <summary>Returns true while Quick Step is held.</summary>
    public bool QuickStepHeld =>
        CanUseCombatInput &&
        inputActions != null &&
        inputActions.Player.QuickStep.IsPressed();

    /// <summary>Returns true during the frame in which Quick Step is pressed.</summary>
    public bool QuickStepPressedThisFrame =>
        CanUseCombatInput &&
        inputActions != null &&
        inputActions.Player.QuickStep.WasPressedThisFrame();

    /// <summary>Returns the current vertical climb input, intended for ladder contexts.</summary>
    public float ClimbInput =>
        CanReadGameplayInput && inputActions != null
            ? inputActions.Player.Climb.ReadValue<float>()
            : 0f;

    /// <summary>Returns true during the frame in which the upward climb input is pressed.</summary>
    public bool ClimbUpPressedThisFrame =>
        CanReadGameplayInput && climbUpPressedThisFrame;

    /// <summary>Returns true during the frame in which Interact is pressed.</summary>
    public bool InteractPressedThisFrame =>
        CanUseCombatInput &&
        inputActions != null &&
        inputActions.Player.Interact.WasPressedThisFrame();

    /// <summary>Returns true while Interact is held.</summary>
    public bool InteractHeld =>
        CanUseCombatInput &&
        inputActions != null &&
        inputActions.Player.Interact.IsPressed();

    /// <summary>Returns true during the frame in which the healing-item action is pressed.</summary>
    public bool UseHealingItemPressedThisFrame =>
        CanUseCombatInput &&
        inputActions != null &&
        inputActions.Player.UseHealingItem.WasPressedThisFrame();

    /// <summary>Returns true when the direct ladder-grab key is pressed in a valid ladder overlap.</summary>
    public bool ExplicitLadderGrabPressedThisFrame =>
        IsLadderGrabBindingPressed &&
        player != null &&
        player.CanGrabAvailableLadder;

    /// <summary>Returns true when either the direct grab key or upward climb input requests attachment.</summary>
    public bool GrabLadderPressedThisFrame =>
        (IsLadderGrabBindingPressed || ClimbUpPressedThisFrame) &&
        player != null &&
        player.CanGrabAvailableLadder;

    /// <summary>Returns true during the frame in which quick slot 1 is selected.</summary>
    public bool SelectQuickSlot1PressedThisFrame =>
        CanUseCombatInput &&
        inputActions != null &&
        inputActions.Player.SelectQuickSlot1.WasPressedThisFrame();

    /// <summary>Returns true during the frame in which quick slot 2 is selected.</summary>
    public bool SelectQuickSlot2PressedThisFrame =>
        CanUseCombatInput &&
        inputActions != null &&
        inputActions.Player.SelectQuickSlot2.WasPressedThisFrame();

    /// <summary>Returns true during the frame in which previous equipment is selected.</summary>
    public bool SelectPreviousEquipmentPressedThisFrame =>
        CanUseCombatInput &&
        inputActions != null &&
        inputActions.Player.SelectPreviousEquipment.WasPressedThisFrame();

    /// <summary>Returns true during the frame in which next equipment is selected.</summary>
    public bool SelectNextEquipmentPressedThisFrame =>
        CanUseCombatInput &&
        inputActions != null &&
        inputActions.Player.SelectNextEquipment.WasPressedThisFrame();

    /// <summary>Returns -1 or 1 when the mouse wheel requests previous or next equipment.</summary>
    public int EquipmentScrollDirection
    {
        get
        {
            if (!CanUseCombatInput || inputActions == null ||
                !inputActions.Player.ScrollEquipment.WasPerformedThisFrame())
            {
                return 0;
            }

            float scroll = inputActions.Player.ScrollEquipment.ReadValue<Vector2>().y;
            return scroll > 0f ? 1 : scroll < 0f ? -1 : 0;
        }
    }

    /// <summary>Returns mouse-wheel input for equipment selection.</summary>
    public Vector2 ScrollEquipmentInput =>
        CanUseCombatInput && inputActions != null
            ? inputActions.Player.ScrollEquipment.ReadValue<Vector2>()
            : Vector2.zero;

    /// <summary>Returns true during the frame in which the inventory action is pressed.</summary>
    public bool OpenInventoryPressedThisFrame =>
        CanReadGameplayInput &&
        inputActions != null &&
        inputActions.Player.OpenInventory.WasPressedThisFrame();

    /// <summary>Returns true during the frame in which the map action is pressed.</summary>
    public bool OpenMapPressedThisFrame =>
        CanReadGameplayInput &&
        inputActions != null &&
        inputActions.Player.OpenMap.WasPressedThisFrame();

    /// <summary>Returns true during the frame in which the Pause action is pressed.</summary>
    public bool PausePressedThisFrame =>
        inputActions != null &&
        inputActions.Player.Pause.WasPressedThisFrame();

    /// <summary>Returns true while the Jump action is held.</summary>
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

    private bool CanUseCombatInput =>
        CanReadGameplayInput &&
        (player == null || !player.IsOnLadder);

    private bool IsLadderGrabBindingPressed =>
        CanReadGameplayInput &&
        grabLadderAction != null &&
        grabLadderAction.WasPressedThisFrame();

    private void Awake()
    {
        player = GetComponent<Player>();
        inputActions = new InputSystem_Actions();
        grabLadderAction = inputActions.asset.FindAction("Player/GrabLadder", false);
        inputActions.Player.Aim.started += OnAimStarted;
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
        if (inputActions != null)
        {
            inputActions.Player.Aim.started -= OnAimStarted;
            inputActions.Dispose();
        }
    }

    private void Update()
    {
        if (inputActions == null)
        {
            return;
        }

        LogButtonState(inputActions.Player.Pause, "Pause", true);

        if (!CanReadGameplayInput)
        {
            ClearGameplayInput();
            return;
        }

        Vector2 nextMoveInput = inputActions.Player.Move.ReadValue<Vector2>();
        if (moveInput == Vector2.zero && nextMoveInput != Vector2.zero)
        {
            LogInput("Move");
        }

        SetMoveInput(nextMoveInput);
        UpdateClimbInput();
        LogEquipmentScroll();

        if (inputActions.Player.Jump.WasPressedThisFrame())
        {
            lastJumpPressedTime = Time.time;
            LogInput("Jump");
        }

        LogButtonState(inputActions.Player.FireThrow, "Fire / Throw", true);
        LogButtonState(inputActions.Player.FocusAim, "Focus Aim", true);
        LogButtonState(inputActions.Player.Reload, "Reload");
        LogButtonState(inputActions.Player.MeleeAttack, "Melee Attack");
        LogButtonState(inputActions.Player.QuickStep, "Dodge / Quick Step", true);
        LogButtonState(inputActions.Player.Interact, "Interact / Pick Up");
        LogButtonState(inputActions.Player.UseHealingItem, "Use Healing Item");
        LogButtonState(inputActions.Player.SelectQuickSlot1, "Select Quick Slot 1");
        LogButtonState(inputActions.Player.SelectQuickSlot2, "Select Quick Slot 2");
        LogButtonState(inputActions.Player.SelectPreviousEquipment, "Select Previous Equipment");
        LogButtonState(inputActions.Player.SelectNextEquipment, "Select Next Equipment");
        LogButtonState(inputActions.Player.OpenInventory, "Open Inventory");
        LogButtonState(inputActions.Player.OpenMap, "Open Map");
    }

    /// <summary>Returns whether a jump was buffered within the requested duration.</summary>
    public bool HasBufferedJump(float bufferDuration)
    {
        float clampedDuration = Mathf.Max(0f, bufferDuration);

        return CanReadGameplayInput &&
               Time.time - lastJumpPressedTime <= clampedDuration;
    }

    /// <summary>Clears the buffered jump input.</summary>
    public void ConsumeJumpBuffer()
    {
        lastJumpPressedTime = float.NegativeInfinity;
    }

    private void ClearGameplayInput()
    {
        SetMoveInput(Vector2.zero);
        previousClimbInput = 0f;
        climbUpPressedThisFrame = false;
        ConsumeJumpBuffer();
    }

    private void SetMoveInput(Vector2 nextInput)
    {
        if (moveInput == nextInput)
        {
            return;
        }

        moveInput = nextInput;
    }

    private void UpdateClimbInput()
    {
        float climbInput = inputActions.Player.Climb.ReadValue<float>();
        const float climbPressThreshold = 0.1f;
        climbUpPressedThisFrame =
            climbInput > climbPressThreshold &&
            previousClimbInput <= climbPressThreshold;

        if (Mathf.Approximately(previousClimbInput, 0f) && !Mathf.Approximately(climbInput, 0f))
        {
            LogInput("Climb");
        }

        previousClimbInput = climbInput;
    }

    private void LogEquipmentScroll()
    {
        if (!logInputDebug || !inputActions.Player.ScrollEquipment.WasPerformedThisFrame())
        {
            return;
        }

        float scrollDirection = inputActions.Player.ScrollEquipment.ReadValue<Vector2>().y;
        string directionName = scrollDirection >= 0f ? "Next" : "Previous";
        Debug.Log($"[Player Input] Scroll Equipment: {directionName}", this);
    }

    private void LogButtonState(InputAction action, string actionName, bool logRelease = false)
    {
        if (!logInputDebug)
        {
            return;
        }

        if (action.WasPressedThisFrame())
        {
            LogInput($"{actionName} pressed");
        }

        if (logRelease && action.WasReleasedThisFrame())
        {
            LogInput($"{actionName} released");
        }
    }

    private void OnAimStarted(InputAction.CallbackContext context)
    {
        if (!logInputDebug || !CanReadGameplayInput)
        {
            return;
        }

        string deviceName = context.control?.device.displayName ?? "Unknown device";
        LogInput($"Aim input detected from {deviceName}");
    }

    private void LogInput(string message)
    {
        if (logInputDebug)
        {
            Debug.Log($"[Player Input] {message}", this);
        }
    }
}
