using UnityEngine;
using UnityEngine.U2D.IK;

/// <summary>Rotates the weapon toward mouse or gamepad aim while Focus Aim is held.</summary>
[DisallowMultipleComponent]
public sealed class KnightWeaponAimController : MonoBehaviour
{
    private const float MinimumAimDirectionSqrMagnitude = 0.04f;
    private const float AimFacingDeadZone = 0.01f;
    private const float AimRotationSpeed = 1080f;
    private const string AimAnimatorParameter = "Aim";

    [SerializeField]
    private Transform weaponAimPivot;

    [SerializeField]
    private Camera aimCamera;

    [SerializeField]
    private PlayerInputReader inputReader;

    [SerializeField, Range(-180f, 180f)]
    private float aimAngleOffset;

    [SerializeField, Range(-45f, 15f)]
    private float relaxedWeaponAngle = -20f;

    [SerializeField, Min(0f)]
    private float armIkBlendSpeed = 10f;

    [SerializeField, Range(0.4f, 1f)]
    private float runningArmIkWeight = 0.68f;

    [SerializeField]
    private Transform aimHead;

    [SerializeField, Range(0f, 90f)]
    private float maximumHeadLookUpAngle = 90f;

    [SerializeField, Range(0f, 90f)]
    private float maximumHeadLookDownAngle = 90f;

    [SerializeField, Min(0f)]
    private float headAimSmoothTime = 0.12f;

    private Animator animator;
    private Player player;
    private LimbSolver2D leftArmSolver;
    private LimbSolver2D rightArmSolver;
    private Quaternion relaxedLocalRotation;
    private Quaternion headBaseLocalRotation;
    private float currentHeadAimAngle;
    private float headAimAngleVelocity;
    private bool hasHeadBaseLocalRotation;
    private bool wasAiming;

    private void Awake()
    {
        if (weaponAimPivot == null)
        {
            weaponAimPivot = transform.Find("WeaponAimPivot");
        }

        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        if (inputReader == null)
        {
            inputReader = FindAnyObjectByType<PlayerInputReader>();
        }

        animator = GetComponent<Animator>();
        player = GetComponentInParent<Player>();
        if (aimHead == null)
        {
            aimHead = transform.Find("root/pelvis/abdomen/chest/neck_1/head_1");
        }

        leftArmSolver = transform.Find("IKSolvers/Solver_ArmL")?.GetComponent<LimbSolver2D>();
        rightArmSolver = transform.Find("IKSolvers/Solver_ArmR")?.GetComponent<LimbSolver2D>();

        if (weaponAimPivot != null)
        {
            relaxedLocalRotation = Quaternion.Euler(0f, 0f, relaxedWeaponAngle);
        }
    }

    private void Update()
    {
        if (wasAiming && aimHead != null && hasHeadBaseLocalRotation)
        {
            aimHead.localRotation = headBaseLocalRotation;
        }

        bool isAiming = inputReader != null && inputReader.FocusAimHeld;
        UpdateArmIkWeight(isAiming);
    }

    private void LateUpdate()
    {
        bool isAiming = inputReader != null && inputReader.FocusAimHeld;
        if (animator != null)
        {
            animator.SetBool(AimAnimatorParameter, isAiming);
        }

        if (weaponAimPivot == null)
        {
            return;
        }

        if (!isAiming)
        {
            wasAiming = false;
            hasHeadBaseLocalRotation = false;
            weaponAimPivot.localRotation = Quaternion.RotateTowards(
                weaponAimPivot.localRotation,
                relaxedLocalRotation,
                AimRotationSpeed * Time.deltaTime);
            return;
        }

        if (!wasAiming)
        {
            currentHeadAimAngle = 0f;
            headAimAngleVelocity = 0f;
            if (aimHead != null)
            {
                headBaseLocalRotation = aimHead.localRotation;
                hasHeadBaseLocalRotation = true;
            }
            wasAiming = true;
        }

        if (!TryGetAimDirection(out Vector2 aimDirection, out Vector3 worldAimDirection))
        {
            return;
        }

        UpdateHeadAim(worldAimDirection);

        float aimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, aimAngle + aimAngleOffset);
        weaponAimPivot.localRotation = Quaternion.RotateTowards(
            weaponAimPivot.localRotation,
            targetRotation,
            AimRotationSpeed * Time.deltaTime);
    }

    private void UpdateHeadAim(Vector3 worldAimDirection)
    {
        if (aimHead == null)
        {
            return;
        }

        Vector3 playerLocalAimDirection = player != null
            ? player.transform.InverseTransformDirection(worldAimDirection)
            : weaponAimPivot.parent != null
                ? weaponAimPivot.parent.InverseTransformDirection(worldAimDirection)
                : worldAimDirection;
        float targetAngle = Mathf.Atan2(
            playerLocalAimDirection.y,
            Mathf.Abs(playerLocalAimDirection.x)) * Mathf.Rad2Deg;

        targetAngle = Mathf.Clamp(
            targetAngle,
            -maximumHeadLookDownAngle,
            maximumHeadLookUpAngle);

        currentHeadAimAngle = Mathf.SmoothDampAngle(
            currentHeadAimAngle,
            targetAngle,
            ref headAimAngleVelocity,
            headAimSmoothTime);

        headBaseLocalRotation = aimHead.localRotation;
        hasHeadBaseLocalRotation = true;
        aimHead.localRotation = headBaseLocalRotation * Quaternion.Euler(0f, 0f, currentHeadAimAngle);
    }

    private void UpdateArmIkWeight(bool isAiming)
    {
        float targetWeight = isAiming
            ? 1f
            : inputReader != null && inputReader.MoveInput.sqrMagnitude > 0.0001f
                ? runningArmIkWeight
                : 0.85f;
        float weightChange = armIkBlendSpeed * Time.deltaTime;

        if (leftArmSolver != null)
        {
            leftArmSolver.weight = Mathf.MoveTowards(leftArmSolver.weight, targetWeight, weightChange);
        }

        if (rightArmSolver != null)
        {
            rightArmSolver.weight = Mathf.MoveTowards(rightArmSolver.weight, targetWeight, weightChange);
        }
    }

    private bool TryGetAimDirection(out Vector2 aimDirection, out Vector3 worldAimDirection)
    {
        aimDirection = Vector2.zero;
        worldAimDirection = Vector3.zero;
        if (inputReader == null)
        {
            return false;
        }

        Vector2 aimInput = inputReader.AimInput;

        if (inputReader.FocusAimUsesGamepad)
        {
            if (aimInput.sqrMagnitude < MinimumAimDirectionSqrMagnitude)
            {
                return false;
            }

            Vector3 cameraRight = aimCamera != null ? aimCamera.transform.right : Vector3.right;
            Vector3 cameraUp = aimCamera != null ? aimCamera.transform.up : Vector3.up;
            worldAimDirection = cameraRight * aimInput.x + cameraUp * aimInput.y;
        }
        else
        {
            if (aimCamera == null)
            {
                return false;
            }

            float cameraDepth = Vector3.Dot(
                weaponAimPivot.position - aimCamera.transform.position,
                aimCamera.transform.forward);

            if (cameraDepth <= 0f)
            {
                return false;
            }

            Vector3 mouseWorldPosition = aimCamera.ScreenToWorldPoint(
                new Vector3(aimInput.x, aimInput.y, cameraDepth));
            worldAimDirection = mouseWorldPosition - weaponAimPivot.position;
        }

        if (worldAimDirection.sqrMagnitude < MinimumAimDirectionSqrMagnitude)
        {
            return false;
        }

        if (player != null && Mathf.Abs(worldAimDirection.x) > AimFacingDeadZone)
        {
            player.SetFacingDirection(worldAimDirection.x > 0f ? 1 : -1);
        }

        aimDirection = weaponAimPivot.parent != null
            ? weaponAimPivot.parent.InverseTransformDirection(worldAimDirection)
            : worldAimDirection;

        return aimDirection.sqrMagnitude >= MinimumAimDirectionSqrMagnitude;
    }
}
