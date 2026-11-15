using UnityEngine;

/// <summary>Coordinates the unarmed knife guard, directional slash input and additive authored upper-body aim.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Player), typeof(PlayerInputReader), typeof(PlayerMeleeAttackController))]
[DefaultExecutionOrder(-15)]
public sealed class PlayerKnifeAimController : MonoBehaviour
{
    private const string ReadyParameter = "KnifeAimReady";
    private const string ChestPath = "root/pelvis/abdomen/chest";
    private const float DefaultAimBlendSpeed = 540f;
    private const float DefaultMaximumAimAngle = 85f;
    private const float FacingDeadZone = 0.01f;

    [Header("Knife directional pose")]
    [Tooltip("Chest pivot receiving additive local aim after animation and firearm pose release. Resolved automatically from the player Animator when omitted.")]
    [SerializeField] private Transform chest;
    [Tooltip("Maximum upper-body elevation from the authored forward pose, in degrees.")]
    [SerializeField, Range(0f, 90f)] private float maximumAimAngle = DefaultMaximumAimAngle;
    [Tooltip("Directional pose rotation speed in degrees per second.")]
    [SerializeField, Min(0f)] private float aimBlendSpeed = DefaultAimBlendSpeed;

    private Player player;
    private PlayerInputReader input;
    private PlayerMeleeAttackController attack;
    private PlayerMeleeWeaponController knife;
    private WeaponHoldController hold;
    private PlayerReloadController reload;
    private Animator animator;
    private Vector2 direction = Vector2.right;
    private Quaternion authoredChestRotation;
    private Quaternion appliedChestRotation;
    private bool hasAppliedPose;
    private float aimAngle;

    /// <summary>True while no ranged weapon is equipped and the knife guard is eligible and requested.</summary>
    public bool IsReady { get; private set; }

    /// <summary>Gets the finite world-space guard direction, or the committed slash direction during recovery.</summary>
    public Vector2 AimDirection => attack != null && attack.IsAimedSlashCommitted ? attack.AttackDirection : direction;

    private void Awake()
    {
        player = GetComponent<Player>();
        input = GetComponent<PlayerInputReader>();
        attack = GetComponent<PlayerMeleeAttackController>();
        knife = GetComponent<PlayerMeleeWeaponController>();
        hold = GetComponentInChildren<WeaponHoldController>(true);
        reload = GetComponentInChildren<PlayerReloadController>(true);
        animator = GetComponentInChildren<Animator>(true);
        if (chest == null && animator != null) chest = animator.transform.Find(ChestPath);
    }

    private void Update()
    {
        RestoreAuthoredPose();
        RefreshReady();
        if (!IsReady) return;

        Vector3 origin = chest != null ? chest.position : transform.position;
        direction = hold.GetCombatAimDirection(origin);
        FaceDirection(direction);
        // Snapshot conversion above must precede the melee input mask.
        if (input.FireThrowPressedThisFrame) attack.TryBeginAimedSlash(direction);
    }

    private void LateUpdate()
    {
        // Equipment and locomotion may change later in Update; do not leave a stale guard/IK owner.
        RefreshReady();
        bool slashing = attack != null && attack.IsAimedSlashCommitted;
        if (!IsReady && !slashing)
        {
            aimAngle = 0f;
            return;
        }

        Vector2 currentDirection = AimDirection;
        FaceDirection(currentDirection);
        if (chest == null) return;
        Vector3 localDirection = transform.InverseTransformDirection(currentDirection);
        float targetAngle = Mathf.Clamp(Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg,
            -maximumAimAngle, maximumAimAngle);
        // A committed slash uses its snapshot immediately; retain every authored chest/arm translation and rotation.
        aimAngle = slashing ? targetAngle : Mathf.MoveTowardsAngle(aimAngle, targetAngle, aimBlendSpeed * Time.deltaTime);
        authoredChestRotation = chest.localRotation;
        appliedChestRotation = authoredChestRotation * Quaternion.Euler(0f, 0f, aimAngle);
        chest.localRotation = appliedChestRotation;
        hasAppliedPose = true;
    }

    private bool CanReady()
    {
        return player != null && player.isActiveAndEnabled && !player.IsOnLadder &&
            input != null && input.isActiveAndEnabled && hold != null && hold.isActiveAndEnabled &&
            hold.CurrentWeapon == null && knife != null && knife.isActiveAndEnabled &&
            attack != null && attack.isActiveAndEnabled &&
            (reload == null || !reload.IsReloading) &&
            (player.stateMachine == null || player.stateMachine.CurrentState != player.quickStepState) &&
            (GameManager.Instance == null || !GameManager.Instance.IsGameplayInputLocked);
    }

    private void RefreshReady()
    {
        bool eligible = CanReady();
        if (!eligible && attack != null && attack.IsAimedSlashCommitted) attack.CancelMeleeAttack();
        bool slashing = attack != null && attack.IsAimedSlashCommitted;
        IsReady = eligible && (slashing || (!input.IsMeleeAttackCommitted && input.FocusAimHeld));
        if (animator != null) animator.SetBool(ReadyParameter, IsReady);
        knife?.SetReadyVisualActive(IsReady);
        hold?.SetKnifeAimMode(IsReady);
    }

    private void FaceDirection(Vector2 aimDirection)
    {
        if (Mathf.Abs(aimDirection.x) > FacingDeadZone)
            player.SetFacingDirection(aimDirection.x > 0f ? 1 : -1);
    }

    private void RestoreAuthoredPose()
    {
        // Do not overwrite an Animator evaluation or a different owner that already replaced our pose.
        if (hasAppliedPose && chest != null && chest.localRotation == appliedChestRotation)
            chest.localRotation = authoredChestRotation;
        hasAppliedPose = false;
    }

    private void OnDisable()
    {
        RestoreAuthoredPose();
        if (attack != null && attack.IsAimedSlashCommitted) attack.CancelMeleeAttack();
        IsReady = false;
        aimAngle = 0f;
        if (animator != null) animator.SetBool(ReadyParameter, false);
        knife?.SetReadyVisualActive(false);
        hold?.SetKnifeAimMode(false);
    }
}
