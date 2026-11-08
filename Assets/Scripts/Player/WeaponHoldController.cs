using UnityEngine;
using UnityEngine.U2D.IK;

/// <summary>
/// Drives the weapon rig: snaps the aim pivot to the shoulder joint, rotates the held weapon
/// toward the aim target, and places both hand IK targets on the weapon's grip markers so the
/// hands always hold whatever weapon is equipped. Runs before the 2D IK manager so the solvers
/// see this frame's weapon pose, not last frame's.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(ExecutionOrder)]
public sealed class WeaponHoldController : MonoBehaviour
{
    /// <summary>Must be lower than UnityEngine.U2D.IK.UpdateOrder.ikUpdateOrder (-10).</summary>
    public const int ExecutionOrder = -20;

    private const float MinimumAimDirectionSqrMagnitude = 0.04f;
    private const float AimFacingDeadZone = 0.01f;
    private const float MoveInputDeadZoneSqr = 0.0001f;
    private const float AimRotationSpeed = 1080f;
    private const float RelaxRotationSpeed = 360f;
    private const float HoldOffsetBlendSpeed = 6f;
    private const string AimAnimatorParameter = "Aim";

    private const float AimBlendInSpeed = 8f;
    private const float AimBlendOutSpeed = 6f;
    private const float StrideCadenceSpeedMultiplier = 0.7f;
    private const float MinimumSpeedThreshold = 0.05f;
    private const float DefaultPlayerMoveSpeed = 5f;
    private const float HalfCirclePhaseOffset = 0.4f;
    private const float StrideBounceFrequencyMultiplier = 2f;
    private const float VerticalLagSmoothSpeed = 12f;
    private const float MinimumMotionWeightThreshold = 0.001f;

    [Header("Rig")]
    [SerializeField]
    private Transform weaponAimPivot;

    [SerializeField]
    private Transform weaponSocket;

    [Tooltip("Bone the weapon rotates around. Use the root joint of the main (near) arm chain.")]
    [SerializeField]
    private Transform aimPivotBone;

    [SerializeField]
    private Vector2 aimPivotBoneOffset;

    [Header("Torso coupling")]
    [Tooltip("Optional bone (chest/spine) whose animated rotation influences the relaxed weapon tilt.")]
    [SerializeField]
    private Transform torsoBone;

    [Tooltip("How much the torso bone's tilt influences the weapon angle when relaxed (0 = none, 1 = full).")]
    [SerializeField, Range(0f, 1f)]
    private float torsoTiltInfluence = 0.4f;

    [Header("Hold motion - Breathing")]
    [Tooltip("Speed of the breathing cycle in radians per second (~2.2 = one breath every 2.8s).")]
    [SerializeField, Min(0f)]
    private float idleBreatheFrequency = 2.2f;

    [Tooltip("Vertical rise and fall in meters during idle breathing.")]
    [SerializeField, Min(0f)]
    private float idleBreatheVerticalAmplitude = 0.025f;

    [Tooltip("Horizontal expansion in meters during idle breathing.")]
    [SerializeField, Min(0f)]
    private float idleBreatheHorizontalAmplitude = 0.012f;

    [Tooltip("Subtle weapon pitch rotation in degrees during idle breathing.")]
    [SerializeField, Min(0f)]
    private float idleBreatheAngleAmplitude = 2f;

    [Header("Hold motion - Locomotion bob")]
    [Tooltip("Speed of the running stride bob in radians per second (~9.5 matches the run cycle).")]
    [SerializeField, Min(0f)]
    private float locomotionBobFrequency = 9.5f;

    [Tooltip("Vertical bounce in meters during running stride.")]
    [SerializeField, Min(0f)]
    private float locomotionBobVerticalAmplitude = 0.055f;

    [Tooltip("Horizontal forward/back stride pump in meters during running.")]
    [SerializeField, Min(0f)]
    private float locomotionBobHorizontalAmplitude = 0.035f;

    [Tooltip("Rhythmic weapon pitch tilt in degrees during running.")]
    [SerializeField, Min(0f)]
    private float locomotionBobAngleAmplitude = 4.5f;

    [Header("Hold motion - Inertia")]
    [Tooltip("Vertical weapon lag reacting to jump/fall velocity.")]
    [SerializeField, Min(0f)]
    private float verticalVelocityLagMultiplier = 0.008f;

    [Tooltip("Maximum vertical offset displacement from jumping/falling inertia.")]
    [SerializeField, Min(0f)]
    private float maximumVerticalLag = 0.06f;

    [Header("Main hand (near arm)")]
    [SerializeField]
    private LimbSolver2D mainArmSolver;

    [SerializeField]
    private Transform mainHandTarget;

    [Header("Support hand (far arm)")]
    [SerializeField]
    private LimbSolver2D supportArmSolver;

    [SerializeField]
    private Transform supportHandTarget;

    [Tooltip("Fraction of the far arm's full length the support hand may reach before it slides back along the weapon.")]
    [SerializeField, Range(0.5f, 1f)]
    private float supportReachFraction = 0.92f;

    [SerializeField, Min(0f)]
    private float armIkBlendSpeed = 10f;

    [Header("Aim input")]
    [SerializeField]
    private Camera aimCamera;

    [SerializeField]
    private PlayerInputReader inputReader;

    [Header("Head")]
    [SerializeField]
    private Transform aimHead;

    [SerializeField, Range(0f, 90f)]
    private float maximumHeadLookUpAngle = 60f;

    [SerializeField, Range(0f, 90f)]
    private float maximumHeadLookDownAngle = 45f;

    [SerializeField, Min(0f)]
    private float headAimSmoothTime = 0.12f;

    [Header("Weapons")]
    [SerializeField]
    private HeldWeapon startingWeapon;

    [Tooltip("Prototype loadout cycled with the Select Previous/Next Equipment inputs. Replace with the inventory later.")]
    [SerializeField]
    private HeldWeapon[] testWeapons;

    private int testWeaponIndex = -1;

    private Animator animator;
    private Player player;
    private HeldWeapon currentWeapon;
    private float supportArmLength;
    private float currentAngle;
    private Vector2 currentHoldOffset;
    private Quaternion headBaseLocalRotation;
    private float currentHeadAimAngle;
    private float headAimAngleVelocity;
    private bool hasHeadBaseLocalRotation;
    private bool wasAiming;

    private float idleBreatheTimer;
    private float locomotionBobTimer;
    private float aimMotionBlend;
    private float currentVerticalLag;
    private Quaternion torsoRestLocalRotation;
    private bool hasTorsoRestLocalRotation;

    /// <summary>Weapon currently in the hands, or null when unarmed.</summary>
    public HeldWeapon CurrentWeapon => currentWeapon;

    /// <summary>True while Focus Aim is held and a weapon is equipped.</summary>
    public bool IsAiming => wasAiming;

    /// <summary>World-space direction the barrel points. Valid only while a weapon is equipped.</summary>
    public Vector3 AimDirection => currentWeapon != null ? currentWeapon.transform.right : transform.right;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        player = GetComponentInParent<Player>();

        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        if (inputReader == null)
        {
            inputReader = GetComponentInParent<PlayerInputReader>();
        }

        if (torsoBone == null && aimPivotBone != null)
        {
            Transform parentTransform = aimPivotBone.parent;
            while (parentTransform != null && parentTransform != transform)
            {
                if (parentTransform.name.IndexOf("chest", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    torsoBone = parentTransform;
                    break;
                }

                parentTransform = parentTransform.parent;
            }

            if (torsoBone == null && aimPivotBone.parent != null)
            {
                torsoBone = aimPivotBone.parent.parent;
            }
        }

        supportArmLength = MeasureChainLength(supportArmSolver);
    }

    private void Start()
    {
        if (torsoBone != null)
        {
            torsoRestLocalRotation = torsoBone.localRotation;
            hasTorsoRestLocalRotation = true;
        }

        if (startingWeapon != null)
        {
            testWeaponIndex = System.Array.IndexOf(testWeapons ?? System.Array.Empty<HeldWeapon>(), startingWeapon);
            Equip(startingWeapon);
        }
        else
        {
            SetArmWeights(0f, 0f, true);
        }
    }

    private void Update()
    {
        if (wasAiming && aimHead != null && hasHeadBaseLocalRotation)
        {
            aimHead.localRotation = headBaseLocalRotation;
        }

        HandleDebugWeaponSwap();
    }

    private void LateUpdate()
    {
        bool isAiming = currentWeapon != null && inputReader != null && inputReader.FocusAimHeld;

        float horizontalSpeed = player != null && player.rb != null
            ? Mathf.Abs(player.rb.linearVelocity.x)
            : (inputReader != null ? Mathf.Abs(inputReader.MoveInput.x) : 0f);

        float maxSpeed = player != null ? Mathf.Max(player.moveSpeed, 1f) : DefaultPlayerMoveSpeed;
        float normalizedSpeed = Mathf.Clamp01(horizontalSpeed / maxSpeed);
        bool isGrounded = player == null || player.HasGroundContact;
        bool isMoving = normalizedSpeed > MinimumSpeedThreshold || (inputReader != null && inputReader.MoveInput.sqrMagnitude > MoveInputDeadZoneSqr);

        if (animator != null)
        {
            animator.SetBool(AimAnimatorParameter, isAiming);
        }

        if (currentWeapon == null || weaponAimPivot == null)
        {
            wasAiming = false;
            BlendArmWeights(0f, 0f);
            return;
        }

        if (aimPivotBone != null)
        {
            weaponAimPivot.position = aimPivotBone.position + aimPivotBone.TransformVector(aimPivotBoneOffset);
        }

        float aimBlendTarget = isAiming ? 1f : 0f;
        float aimBlendRate = isAiming ? AimBlendInSpeed : AimBlendOutSpeed;
        aimMotionBlend = Mathf.MoveTowards(aimMotionBlend, aimBlendTarget, aimBlendRate * Time.deltaTime);
        float motionWeight = 1f - aimMotionBlend;

        idleBreatheTimer += Time.deltaTime * idleBreatheFrequency;

        float locomotionAdvanceRate = isGrounded && isMoving
            ? locomotionBobFrequency * (1f - StrideCadenceSpeedMultiplier + StrideCadenceSpeedMultiplier * normalizedSpeed)
            : 0f;
        locomotionBobTimer += Time.deltaTime * locomotionAdvanceRate;

        Vector2 proceduralHoldOffset = Vector2.zero;
        float proceduralAngle = 0f;

        if (motionWeight > MinimumMotionWeightThreshold)
        {
            float breatheWaveY = Mathf.Sin(idleBreatheTimer);
            float breatheWaveX = Mathf.Cos(idleBreatheTimer);
            Vector2 breatheOffset = new Vector2(
                breatheWaveX * idleBreatheHorizontalAmplitude,
                breatheWaveY * idleBreatheVerticalAmplitude);
            float breatheAngle = Mathf.Sin(idleBreatheTimer - HalfCirclePhaseOffset) * idleBreatheAngleAmplitude;

            float strideBounceY = Mathf.Sin(locomotionBobTimer * StrideBounceFrequencyMultiplier);
            float strideSwayX = Mathf.Cos(locomotionBobTimer);
            Vector2 locomotionOffset = new Vector2(
                strideSwayX * locomotionBobHorizontalAmplitude,
                strideBounceY * locomotionBobVerticalAmplitude) * normalizedSpeed;
            float locomotionAngle = strideSwayX * locomotionBobAngleAmplitude * normalizedSpeed;

            float verticalVelocity = player != null && player.rb != null ? player.rb.linearVelocity.y : 0f;
            float targetLag = Mathf.Clamp(-verticalVelocity * verticalVelocityLagMultiplier, -maximumVerticalLag, maximumVerticalLag);
            currentVerticalLag = Mathf.Lerp(currentVerticalLag, targetLag, VerticalLagSmoothSpeed * Time.deltaTime);

            float torsoTilt = 0f;
            if (torsoBone != null && hasTorsoRestLocalRotation)
            {
                Quaternion deltaRotation = torsoBone.localRotation * Quaternion.Inverse(torsoRestLocalRotation);
                float torsoLocalZ = Mathf.DeltaAngle(0f, deltaRotation.eulerAngles.z);
                torsoTilt = torsoLocalZ * torsoTiltInfluence;
            }

            float moveBlend = isGrounded ? normalizedSpeed : 0f;
            Vector2 blendedMotionOffset = Vector2.Lerp(breatheOffset, locomotionOffset, moveBlend);
            blendedMotionOffset.y += currentVerticalLag;
            float blendedMotionAngle = Mathf.Lerp(breatheAngle, locomotionAngle, moveBlend) + torsoTilt;

            proceduralHoldOffset = blendedMotionOffset * motionWeight;
            proceduralAngle = blendedMotionAngle * motionWeight;
        }

        float targetAngle;
        Vector2 targetHoldOffset;
        if (isAiming)
        {
            BeginAimIfNeeded();
            bool hasAimDirection = TryGetAimDirection(out Vector2 localAimDirection, out Vector3 worldAimDirection);
            if (hasAimDirection)
            {
                UpdateHeadAim(worldAimDirection);
                float rawAngle = Mathf.Atan2(localAimDirection.y, localAimDirection.x) * Mathf.Rad2Deg;
                targetAngle = currentWeapon.ClampAimAngle(rawAngle + MuzzleOffsetCorrection(localAimDirection.magnitude));
            }
            else
            {
                targetAngle = currentAngle;
            }

            targetHoldOffset = currentWeapon.AimHoldOffset;
        }
        else
        {
            wasAiming = false;
            hasHeadBaseLocalRotation = false;

            float baseAngle = isMoving ? currentWeapon.RunningAngle : currentWeapon.RelaxedAngle;
            Vector2 baseOffset = isMoving ? currentWeapon.RunningHoldOffset : currentWeapon.RelaxedHoldOffset;

            targetAngle = baseAngle + proceduralAngle;
            targetHoldOffset = baseOffset + proceduralHoldOffset;
        }

        float rotationSpeed = isAiming ? AimRotationSpeed : RelaxRotationSpeed;
        currentAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
        currentHoldOffset = Vector2.Lerp(currentHoldOffset, targetHoldOffset, HoldOffsetBlendSpeed * Time.deltaTime);

        weaponAimPivot.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
        ApplyWeaponHoldOffset();
        PlaceHandTargets();

        float targetSupportWeight = currentWeapon.IsSupportHandActive(isAiming, isMoving) ? 1f : 0f;
        BlendArmWeights(1f, targetSupportWeight);
    }

    /// <summary>Instantiates the weapon prefab into the socket and puts the hands on it.</summary>
    public void Equip(HeldWeapon weaponPrefab)
    {
        Unequip();
        if (weaponPrefab == null || weaponSocket == null)
        {
            return;
        }

        currentWeapon = Instantiate(weaponPrefab, weaponSocket);
        currentWeapon.name = weaponPrefab.name;
        currentWeapon.transform.localRotation = Quaternion.identity;
        currentWeapon.transform.localScale = Vector3.one;
        currentHoldOffset = currentWeapon.RelaxedHoldOffset;
        currentAngle = currentWeapon.RelaxedAngle;
        ApplyWeaponHoldOffset();
        PlaceHandTargets();
    }

    /// <summary>Removes the held weapon; arms fall back to animation.</summary>
    public void Unequip()
    {
        if (currentWeapon == null)
        {
            return;
        }

        Destroy(currentWeapon.gameObject);
        currentWeapon = null;
    }

    private void BeginAimIfNeeded()
    {
        if (wasAiming)
        {
            return;
        }

        currentHeadAimAngle = 0f;
        headAimAngleVelocity = 0f;
        if (aimHead != null)
        {
            headBaseLocalRotation = aimHead.localRotation;
            hasHeadBaseLocalRotation = true;
        }

        wasAiming = true;
    }

    /// <summary>Positions the weapon so its main grip sits at the hold offset from the pivot.</summary>
    private void ApplyWeaponHoldOffset()
    {
        Transform weaponTransform = currentWeapon.transform;
        Vector3 gripLocalOffset = weaponTransform.InverseTransformPoint(currentWeapon.MainGrip.position);
        Vector3 socketLocalHold = weaponSocket.InverseTransformPoint(
            weaponAimPivot.TransformPoint(new Vector3(currentHoldOffset.x, currentHoldOffset.y, 0f)));
        weaponTransform.localPosition = socketLocalHold - gripLocalOffset;
    }

    /// <summary>Puts the hand IK targets on the weapon grips, keeping the support hand within reach.</summary>
    private void PlaceHandTargets()
    {
        Transform mainGrip = currentWeapon.MainGrip;
        if (mainHandTarget != null)
        {
            mainHandTarget.SetPositionAndRotation(mainGrip.position, mainGrip.rotation);
        }

        if (supportHandTarget == null)
        {
            return;
        }

        Transform supportGrip = currentWeapon.SupportGrip;
        Vector3 supportPosition = supportGrip.position;

        Transform supportShoulder = GetChainRoot(supportArmSolver);
        if (supportShoulder != null && supportArmLength > 0f)
        {
            float maxReach = supportArmLength * supportReachFraction;
            Vector3 toSupport = supportPosition - supportShoulder.position;
            if (toSupport.sqrMagnitude > maxReach * maxReach)
            {
                supportPosition = ClosestReachablePointOnSegment(
                    mainGrip.position,
                    supportGrip.position,
                    supportShoulder.position,
                    maxReach);
            }
        }

        supportHandTarget.SetPositionAndRotation(supportPosition, supportGrip.rotation);
    }

    /// <summary>Slides a point from <paramref name="end"/> toward <paramref name="start"/> until it is within reach of <paramref name="center"/>.</summary>
    private static Vector3 ClosestReachablePointOnSegment(Vector3 start, Vector3 end, Vector3 center, float radius)
    {
        Vector3 segment = end - start;
        float segmentLength = segment.magnitude;
        if (segmentLength < Mathf.Epsilon)
        {
            return start;
        }

        Vector3 direction = segment / segmentLength;
        Vector3 toStart = start - center;
        float b = Vector3.Dot(toStart, direction);
        float c = toStart.sqrMagnitude - radius * radius;
        float discriminant = b * b - c;
        if (discriminant < 0f)
        {
            return start;
        }

        float t = -b + Mathf.Sqrt(discriminant);
        return start + direction * Mathf.Clamp(t, 0f, segmentLength);
    }

    /// <summary>Corrects the pivot angle so the barrel, not the shoulder line, points at the target.</summary>
    private float MuzzleOffsetCorrection(float distanceToTarget)
    {
        Transform muzzle = currentWeapon.Muzzle;
        if (muzzle == null || distanceToTarget <= 0f)
        {
            return 0f;
        }

        float perpendicularOffset = weaponAimPivot.InverseTransformPoint(muzzle.position).y;
        if (Mathf.Abs(perpendicularOffset) >= distanceToTarget)
        {
            return 0f;
        }

        return -Mathf.Asin(perpendicularOffset / distanceToTarget) * Mathf.Rad2Deg;
    }

    private void UpdateHeadAim(Vector3 worldAimDirection)
    {
        if (aimHead == null)
        {
            return;
        }

        Vector3 localAimDirection = transform.InverseTransformDirection(worldAimDirection);
        float targetAngle = Mathf.Atan2(localAimDirection.y, Mathf.Abs(localAimDirection.x)) * Mathf.Rad2Deg;
        targetAngle = Mathf.Clamp(targetAngle, -maximumHeadLookDownAngle, maximumHeadLookUpAngle);

        currentHeadAimAngle = Mathf.SmoothDampAngle(
            currentHeadAimAngle,
            targetAngle,
            ref headAimAngleVelocity,
            headAimSmoothTime);

        headBaseLocalRotation = aimHead.localRotation;
        hasHeadBaseLocalRotation = true;
        aimHead.localRotation = headBaseLocalRotation * Quaternion.Euler(0f, 0f, currentHeadAimAngle);
    }

    private void BlendArmWeights(float mainWeight, float supportWeight)
    {
        SetArmWeights(mainWeight, supportWeight, false);
    }

    private void SetArmWeights(float mainWeight, float supportWeight, bool immediate)
    {
        float step = immediate ? 1f : armIkBlendSpeed * Time.deltaTime;
        if (mainArmSolver != null)
        {
            mainArmSolver.weight = Mathf.MoveTowards(mainArmSolver.weight, mainWeight, step);
        }

        if (supportArmSolver != null)
        {
            supportArmSolver.weight = Mathf.MoveTowards(supportArmSolver.weight, supportWeight, step);
        }
    }

    private bool TryGetAimDirection(out Vector2 localAimDirection, out Vector3 worldAimDirection)
    {
        localAimDirection = Vector2.zero;
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
            worldAimDirection = (cameraRight * aimInput.x + cameraUp * aimInput.y).normalized * 10f;
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

            Vector3 mouseWorldPosition = aimCamera.ScreenToWorldPoint(new Vector3(aimInput.x, aimInput.y, cameraDepth));
            worldAimDirection = mouseWorldPosition - weaponAimPivot.position;
            worldAimDirection.z = 0f;
        }

        if (worldAimDirection.sqrMagnitude < MinimumAimDirectionSqrMagnitude)
        {
            return false;
        }

        if (player != null && Mathf.Abs(worldAimDirection.x) > AimFacingDeadZone)
        {
            player.SetFacingDirection(worldAimDirection.x > 0f ? 1 : -1);
        }

        Transform pivotParent = weaponAimPivot.parent != null ? weaponAimPivot.parent : transform;
        localAimDirection = pivotParent.InverseTransformDirection(worldAimDirection);
        return localAimDirection.sqrMagnitude >= MinimumAimDirectionSqrMagnitude;
    }

    private static Transform GetChainRoot(LimbSolver2D solver)
    {
        if (solver == null)
        {
            return null;
        }

        IKChain2D chain = solver.GetChain(0);
        return chain != null && chain.transformCount > 0 ? chain.transforms[0] : null;
    }

    /// <summary>Sum of bone lengths from the chain root to its effector.</summary>
    private static float MeasureChainLength(LimbSolver2D solver)
    {
        if (solver == null)
        {
            return 0f;
        }

        IKChain2D chain = solver.GetChain(0);
        if (chain == null || chain.transforms == null)
        {
            return 0f;
        }

        float length = 0f;
        for (int i = 1; i < chain.transforms.Length; i++)
        {
            if (chain.transforms[i] != null && chain.transforms[i - 1] != null)
            {
                length += Vector3.Distance(chain.transforms[i].position, chain.transforms[i - 1].position);
            }
        }

        return length;
    }

    private void HandleDebugWeaponSwap()
    {
        if (inputReader == null || testWeapons == null || testWeapons.Length == 0)
        {
            return;
        }

        int step = 0;
        if (inputReader.SelectNextEquipmentPressedThisFrame)
        {
            step = 1;
        }
        else if (inputReader.SelectPreviousEquipmentPressedThisFrame)
        {
            step = -1;
        }

        if (step == 0)
        {
            return;
        }

        testWeaponIndex = (testWeaponIndex + step + testWeapons.Length) % testWeapons.Length;
        Equip(testWeapons[testWeaponIndex]);
    }
}
