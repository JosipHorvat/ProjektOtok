using System.Collections.Generic;
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
    private const string RangedWeaponEquippedAnimatorParameter = "HasRangedWeapon";
    private const string BackSocketPath = "root/pelvis/abdomen/chest";

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

    [Tooltip("Held-weapon prefab catalog used to resolve the visual rig for inventory-owned WeaponDefinition items. Ownership and ammo state live on item instances.")]
    [SerializeField]
    private HeldWeapon[] testWeapons;

    [Header("Ladder weapon stow")]
    [SerializeField] private Vector3 ladderBackLocalPosition = new Vector3(-0.14f, 0.04f, 0f);
    [SerializeField] private float ladderBackLocalRotation = 60f;

    private bool isLadderMode;
    private bool isMeleeMode;
    private bool isKnifeAimMode;

    /// <summary>Releases firearm IK while the unarmed knife guard owns the authored upper-body pose.</summary>
    public void SetKnifeAimMode(bool active)
    {
        isKnifeAimMode = active;
        if (active && currentWeapon == null)
        {
            ResetAimForStow();
            SetArmWeights(0f, 0f, true);
        }
    }
    private HeldWeapon stowedWeapon;
    private Transform weaponParentBeforeStow;
    private Vector3 weaponPositionBeforeStow;
    private Quaternion weaponRotationBeforeStow;
    private Vector3 weaponScaleBeforeStow;
    private PlayerMeleeAttackController meleeAttackController;


    private int testWeaponIndex = -1;

    private Animator animator;
    private Player player;
    private PlayerItemInventory playerItemInventory;
    private PlayerReloadController reloadController;
    private HeldWeapon currentWeapon;
    private ItemInstance currentWeaponItem;
    private WeaponAmmoState currentAmmoState;
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
    private float firePoseUntilTime;
    private float currentRecoilDegrees;
    private float recoilRecoveryDegreesPerSecond = 28f;
    private bool aimPoseReady;
    private bool hasRangedWeaponEquippedAnimatorParameter;
    private int rangedWeaponEquippedAnimatorParameterHash;
    private Quaternion torsoRestLocalRotation;
    private bool hasTorsoRestLocalRotation;

    /// <summary>Equipped ranged weapon, including when temporarily stowed for melee or ladder traversal; null when unarmed.</summary>
    public HeldWeapon CurrentWeapon => currentWeapon;

    /// <summary>Owned weapon item instance currently equipped, or null when unarmed.</summary>
    public ItemInstance CurrentWeaponItem => currentWeaponItem;

    /// <summary>Runtime loaded-ammunition adapter for the equipped owned weapon item, or null if unconfigured.</summary>
    public WeaponAmmoState CurrentAmmoState => currentAmmoState;

    /// <summary>True while Focus Aim is held and a weapon is equipped.</summary>
    public bool IsAiming => wasAiming;

    /// <summary>World-space direction the barrel points. Valid only while a weapon is equipped.</summary>
    public Vector3 AimDirection => currentWeapon != null ? currentWeapon.transform.right : transform.right;

    /// <summary>True when the current raised weapon pose has settled for a shot.</summary>
    public bool AimPoseReady => aimPoseReady;

    /// <summary>Current accumulated upward recoil in degrees; recovers toward zero over time.</summary>
    public float CurrentRecoilDegrees => currentRecoilDegrees;

    /// <summary>Requests a short raised weapon pose for a tap-fired shot.</summary>
    public void RequestFirePose(float duration)
    {
        if (currentWeapon == null || isLadderMode || isMeleeMode)
        {
            return;
        }

        firePoseUntilTime = Mathf.Max(firePoseUntilTime, Time.time + Mathf.Max(0.01f, duration));
    }

    /// <summary>Adds upward recoil to the weapon aim and configures its recovery speed.</summary>
    public void ApplyShotRecoil(float recoilDegrees, float recoveryDegreesPerSecond)
    {
        if (currentWeapon == null || isLadderMode || isMeleeMode)
        {
            return;
        }

        currentRecoilDegrees += Mathf.Max(0f, recoilDegrees);
        recoilRecoveryDegreesPerSecond = Mathf.Max(0f, recoveryDegreesPerSecond);
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        hasRangedWeaponEquippedAnimatorParameter = TryGetAnimatorBoolParameterHash(
            animator,
            RangedWeaponEquippedAnimatorParameter,
            out rangedWeaponEquippedAnimatorParameterHash);
        UpdateRangedWeaponAnimatorParameter();
        player = GetComponentInParent<Player>();
        playerItemInventory = GetComponentInParent<PlayerItemInventory>();
        reloadController = GetComponent<PlayerReloadController>();
        meleeAttackController = GetComponentInParent<PlayerMeleeAttackController>();

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

    private void OnEnable()
    {
        isLadderMode = player != null && player.IsOnLadder;
        UpdateWeaponStow();
    }

    private void OnDisable()
    {
        meleeAttackController?.CancelMeleeAttack();
        isMeleeMode = false;
        isKnifeAimMode = false;
        isLadderMode = false;
        UpdateWeaponStow();
        SetArmWeights(0f, 0f, true);
    }

    /// <summary>Equips the saved active weapon instance after the player's inventory has been restored.</summary>
    public bool RestoreSavedWeaponSelection()
    {
        if (playerItemInventory == null)
        {
            playerItemInventory = GetComponent<PlayerItemInventory>();
        }

        if (playerItemInventory == null || playerItemInventory.Inventory == null)
        {
            return false;
        }

        ItemInstance savedWeapon = string.IsNullOrWhiteSpace(playerItemInventory.EquippedWeaponInstanceId)
            ? null
            : playerItemInventory.Inventory.FindItem(playerItemInventory.EquippedWeaponInstanceId);
        if (savedWeapon != null && !(savedWeapon.Definition is WeaponDefinition))
        {
            savedWeapon = null;
        }

        if (savedWeapon == null)
        {
            IReadOnlyList<ItemInstance> ownedItems = playerItemInventory.Inventory.Items;
            for (int index = 0; index < ownedItems.Count; index++)
            {
                if (ownedItems[index].IsEquipped && ownedItems[index].Definition is WeaponDefinition)
                {
                    savedWeapon = ownedItems[index];
                    break;
                }
            }
        }

        if (savedWeapon == null || currentWeaponItem == savedWeapon)
        {
            return currentWeaponItem == savedWeapon && savedWeapon != null;
        }

        return TryEquipOwnedWeapon(savedWeapon);
    }

    private void Start()
    {
        if (torsoBone != null)
        {
            torsoRestLocalRotation = torsoBone.localRotation;
            hasTorsoRestLocalRotation = true;
        }

        if (RestoreSavedWeaponSelection() || currentWeaponItem != null)
        {
            return;
        }

        if (startingWeapon != null && startingWeapon.Definition != null)
        {
            List<ItemInstance> ownedWeapons = GetOwnedWeaponItems();
            testWeaponIndex = FindOwnedWeaponIndex(ownedWeapons, startingWeapon.Definition);
            ItemInstance startingWeaponItem = testWeaponIndex >= 0 ? ownedWeapons[testWeaponIndex] : null;
            if (startingWeaponItem != null)
            {
                TryEquipOwnedWeapon(startingWeaponItem, startingWeapon);
                return;
            }
        }

        SetArmWeights(0f, 0f, true);
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
        UpdateRangedWeaponAnimatorParameter();
        if (isLadderMode)
        {
            aimPoseReady = false;
            if (animator != null)
            {
                animator.SetBool(AimAnimatorParameter, false);
            }

            SetArmWeights(1f, 1f, false);
            return;
        }

        if (isMeleeMode || inputReader != null && inputReader.IsMeleeAttackCommitted)
        {
            SetMeleeMode(true);
            return;
        }

        if (isKnifeAimMode && currentWeapon == null)
        {
            SetArmWeights(0f, 0f, true);
            return;
        }

        if (reloadController != null && reloadController.IsReloading)
        {
            aimPoseReady = false;
            if (wasAiming && aimHead != null && hasHeadBaseLocalRotation)
            {
                aimHead.localRotation = headBaseLocalRotation;
            }

            wasAiming = false;
            hasHeadBaseLocalRotation = false;
            if (animator != null)
            {
                animator.SetBool(AimAnimatorParameter, false);
            }

            BlendArmWeights(0f, 0f);
            return;
        }

        bool isAiming = currentWeapon != null &&
            ((inputReader != null && inputReader.FocusAimHeld) || Time.time < firePoseUntilTime);

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
            aimPoseReady = false;
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
                float facingSign = weaponAimPivot.lossyScale.x < 0f ? -1f : 1f;
                targetAngle = currentWeapon.ClampAimAngle(
                    rawAngle + MuzzleOffsetCorrection(localAimDirection.magnitude) + currentRecoilDegrees * facingSign);
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

            float facingSign = weaponAimPivot.lossyScale.x < 0f ? -1f : 1f;
            targetAngle = baseAngle + proceduralAngle + currentRecoilDegrees * facingSign;
            targetHoldOffset = baseOffset + proceduralHoldOffset;
        }

        float rotationSpeed = isAiming ? AimRotationSpeed : RelaxRotationSpeed;
        currentAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
        currentHoldOffset = Vector2.Lerp(currentHoldOffset, targetHoldOffset, HoldOffsetBlendSpeed * Time.deltaTime);
        currentRecoilDegrees = Mathf.MoveTowards(currentRecoilDegrees, 0f, recoilRecoveryDegreesPerSecond * Time.deltaTime);

        weaponAimPivot.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
        ApplyWeaponHoldOffset();
        PlaceHandTargets();

        float targetSupportWeight = currentWeapon.IsSupportHandActive(isAiming, isMoving) ? 1f : 0f;
        BlendArmWeights(1f, targetSupportWeight);

        bool mainArmSettled = mainArmSolver == null || mainArmSolver.weight >= 0.98f;
        bool supportArmSettled = supportArmSolver == null || supportArmSolver.weight >= targetSupportWeight - 0.02f;
        aimPoseReady = isAiming &&
            aimMotionBlend >= 0.98f &&
            Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngle)) <= 1.5f &&
            (currentHoldOffset - targetHoldOffset).sqrMagnitude <= 0.0004f &&
            mainArmSettled && supportArmSettled;
    }

    /// <summary>Equips the owned weapon instance matching a held-weapon prefab.</summary>
    public void Equip(HeldWeapon weaponPrefab)
    {
        if (weaponPrefab == null || weaponPrefab.Definition == null || playerItemInventory == null)
        {
            Unequip();
            return;
        }

        List<ItemInstance> ownedWeapons = GetOwnedWeaponItems();
        ItemInstance ownedItem = null;
        for (int index = 0; index < ownedWeapons.Count; index++)
        {
            if (ownedWeapons[index].Definition == weaponPrefab.Definition)
            {
                ownedItem = ownedWeapons[index];
                break;
            }
        }

        if (ownedItem == null)
        {
            Debug.LogWarning($"Cannot equip {weaponPrefab.name}: the player does not own a matching weapon item instance.", this);
            Unequip();
            return;
        }

        TryEquipOwnedWeapon(ownedItem, weaponPrefab);
    }

    /// <summary>Equips one specific owned weapon item using its existing held-weapon rig prefab.</summary>
    public bool TryEquipOwnedWeapon(ItemInstance weaponItem, HeldWeapon weaponPrefab)
    {
        if (weaponItem == null || weaponPrefab == null || weaponSocket == null || playerItemInventory == null ||
            !(weaponItem.Definition is WeaponDefinition weaponDefinition) || weaponPrefab.Definition != weaponDefinition ||
            playerItemInventory.Inventory == null ||
            !ReferenceEquals(playerItemInventory.Inventory.FindItem(weaponItem.InstanceId), weaponItem))
        {
            return false;
        }

        Unequip();
        currentWeaponItem = weaponItem;
        currentWeapon = Instantiate(weaponPrefab, weaponSocket);
        currentWeapon.name = weaponPrefab.name;
        currentAmmoState = new WeaponAmmoState(weaponItem);
        weaponItem.TrySetLoadedRounds(weaponItem.LoadedRounds);
        List<ItemInstance> ownedWeapons = GetOwnedWeaponItems();
        for (int index = 0; index < ownedWeapons.Count; index++)
        {
            playerItemInventory.Inventory.TrySetEquipped(
                ownedWeapons[index].InstanceId,
                ownedWeapons[index] == weaponItem);
        }

        playerItemInventory.SetEquippedWeaponInstanceId(weaponItem.InstanceId);
        aimPoseReady = false;
        currentWeapon.transform.localRotation = Quaternion.identity;
        currentWeapon.transform.localScale = Vector3.one;
        currentHoldOffset = currentWeapon.RelaxedHoldOffset;
        currentAngle = currentWeapon.RelaxedAngle;
        ApplyWeaponHoldOffset();
        PlaceHandTargets();
        UpdateWeaponStow();
        return true;
    }
    /// <summary>Equips an owned weapon instance using the matching prefab from this controller's visual catalog.</summary>
    public bool TryEquipOwnedWeapon(ItemInstance weaponItem)
    {
        if (weaponItem == null || !(weaponItem.Definition is WeaponDefinition weaponDefinition))
        {
            return false;
        }

        HeldWeapon weaponPrefab = FindHeldWeaponPrefab(weaponDefinition);
        if (weaponPrefab == null)
        {
            return false;
        }

        bool equipped = TryEquipOwnedWeapon(weaponItem, weaponPrefab);
        if (equipped)
        {
            List<ItemInstance> ownedWeapons = GetOwnedWeaponItems();
            for (int index = 0; index < ownedWeapons.Count; index++)
            {
                if (ReferenceEquals(ownedWeapons[index], weaponItem))
                {
                    testWeaponIndex = index;
                    break;
                }
            }
        }

        return equipped;
    }



    /// <summary>Removes the held weapon; arms fall back to animation while preserving its owned instance state.</summary>
    public void Unequip()
    {
        meleeAttackController?.CancelMeleeAttack();
        reloadController?.CancelReload();
        if (currentWeaponItem != null && playerItemInventory != null && playerItemInventory.Inventory != null)
        {
            playerItemInventory.Inventory.TrySetEquipped(currentWeaponItem.InstanceId, false);
        }

        playerItemInventory?.SetEquippedWeaponInstanceId(string.Empty);
        currentAmmoState = null;
        currentWeaponItem = null;
        RestoreStowedWeapon();
        if (currentWeapon == null)
        {
            return;
        }

        Destroy(currentWeapon.gameObject);
        currentWeapon = null;
    }

    private List<ItemInstance> GetOwnedWeaponItems()
    {
        List<ItemInstance> ownedWeapons = new List<ItemInstance>();
        if (playerItemInventory == null || playerItemInventory.Inventory == null)
        {
            return ownedWeapons;
        }

        IReadOnlyList<ItemInstance> inventoryItems = playerItemInventory.Inventory.Items;
        for (int index = 0; index < inventoryItems.Count; index++)
        {
            ItemInstance item = inventoryItems[index];
            if (item.Definition is WeaponDefinition && item.Definition.Category == ItemCategory.Weapon)
            {
                ownedWeapons.Add(item);
            }
        }

        return ownedWeapons;
    }

    private static int FindOwnedWeaponIndex(List<ItemInstance> ownedWeapons, WeaponDefinition definition)
    {
        for (int index = 0; index < ownedWeapons.Count; index++)
        {
            if (ownedWeapons[index].Definition == definition)
            {
                return index;
            }
        }

        return -1;
    }

    private HeldWeapon FindHeldWeaponPrefab(WeaponDefinition definition)
    {
        HeldWeapon[] weaponPrefabCatalog = testWeapons ?? System.Array.Empty<HeldWeapon>();
        for (int index = 0; index < weaponPrefabCatalog.Length; index++)
        {
            HeldWeapon weaponPrefab = weaponPrefabCatalog[index];
            if (weaponPrefab != null && weaponPrefab.Definition == definition)
            {
                return weaponPrefab;
            }
        }

        return null;
    }

    /// <summary>Moves the equipped weapon to the character's back and frees the hands for ladder poses.</summary>
    public void SetLadderMode(bool isClimbing)
    {
        if (isLadderMode == isClimbing)
        {
            return;
        }

        // Transfer an existing melee stow directly to the ladder without overwriting its hand pose snapshot.
        isLadderMode = isClimbing;
        if (isClimbing)
        {
            meleeAttackController?.CancelMeleeAttack();
            reloadController?.CancelReload();
        }

        ResetAimForStow();
        UpdateWeaponStow();
        if (isMeleeMode)
        {
            SetArmWeights(0f, 0f, true);
        }
        else
        {
            SetArmWeights(isLadderMode || currentWeapon != null ? 1f : 0f, isLadderMode ? 1f : 0f, true);
        }
    }

    /// <summary>Stows the equipped gun using the ladder back pose and immediately releases both arm IK solvers for melee animation.</summary>
    public void SetMeleeMode(bool active)
    {
        isMeleeMode = active;
        if (active)
        {
            ResetAimForStow();
        }

        UpdateWeaponStow();
        if (active)
        {
            SetArmWeights(0f, 0f, true);
        }
        else if (isLadderMode)
        {
            SetArmWeights(1f, 1f, true);
        }
    }

    private void ResetAimForStow()
    {
        if (wasAiming && aimHead != null && hasHeadBaseLocalRotation)
        {
            aimHead.localRotation = headBaseLocalRotation;
        }

        wasAiming = false;
        hasHeadBaseLocalRotation = false;
        aimPoseReady = false;
        firePoseUntilTime = 0f;
        currentRecoilDegrees = 0f;
        if (animator != null)
        {
            animator.SetBool(AimAnimatorParameter, false);
        }
    }

    private void UpdateWeaponStow()
    {
        if (stowedWeapon != null && (stowedWeapon != currentWeapon || !isLadderMode && !isMeleeMode))
        {
            RestoreStowedWeapon();
        }

        if ((!isLadderMode && !isMeleeMode) || currentWeapon == null || stowedWeapon == currentWeapon)
        {
            return;
        }

        stowedWeapon = currentWeapon;
        Transform weaponTransform = stowedWeapon.transform;
        weaponParentBeforeStow = weaponTransform.parent;
        weaponPositionBeforeStow = weaponTransform.localPosition;
        weaponRotationBeforeStow = weaponTransform.localRotation;
        weaponScaleBeforeStow = weaponTransform.localScale;

        Transform backSocket = transform.Find(BackSocketPath);
        weaponTransform.SetParent(backSocket != null ? backSocket : transform, false);
        weaponTransform.localPosition = ladderBackLocalPosition;
        weaponTransform.localRotation = Quaternion.Euler(0f, 0f, ladderBackLocalRotation);
        weaponTransform.localScale = weaponScaleBeforeStow;
    }

    private void RestoreStowedWeapon()
    {
        if (stowedWeapon != null)
        {
            Transform weaponTransform = stowedWeapon.transform;
            weaponTransform.SetParent(weaponParentBeforeStow != null ? weaponParentBeforeStow : weaponSocket, false);
            weaponTransform.localPosition = weaponPositionBeforeStow;
            weaponTransform.localRotation = weaponRotationBeforeStow;
            weaponTransform.localScale = weaponScaleBeforeStow;
            if (currentWeapon == stowedWeapon)
            {
                PlaceHandTargets();
            }
        }

        stowedWeapon = null;
        weaponParentBeforeStow = null;
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

    /// <summary>Resolves mouse or gamepad aim into a world direction without requiring an equipped firearm.</summary>
    public bool TryResolveAimDirection(Vector3 worldOrigin, out Vector2 worldDirection)
    {
        worldDirection = Vector2.zero;
        if (inputReader == null)
        {
            inputReader = GetComponentInParent<PlayerInputReader>();
        }

        if (player == null)
        {
            player = GetComponentInParent<Player>();
        }

        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        if (inputReader == null)
        {
            return false;
        }

        if (!TryGetWorldAimDirection(worldOrigin, out Vector3 resolvedDirection)) return false;
        worldDirection = ((Vector2)resolvedDirection).normalized;
        if (player != null && Mathf.Abs(worldDirection.x) > AimFacingDeadZone)
        {
            player.SetFacingDirection(worldDirection.x > 0f ? 1 : -1);
        }

        return true;
    }

    /// <summary>Converts the current mouse/stick input to a finite normalized world direction without requiring a gun; falls back to facing inside the dead zone.</summary>
    public Vector2 GetCombatAimDirection(Vector3 worldOrigin)
    {
        if (TryGetWorldAimDirection(worldOrigin, out Vector3 direction))
        {
            return ((Vector2)direction).normalized;
        }

        return new Vector2(player != null ? player.facingDir : 1, 0f);
    }

    private bool TryGetAimDirection(out Vector2 localAimDirection, out Vector3 worldAimDirection)
    {
        localAimDirection = Vector2.zero;
        if (!TryGetWorldAimDirection(weaponAimPivot.position, out worldAimDirection))
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

    private bool TryGetWorldAimDirection(Vector3 worldOrigin, out Vector3 worldAimDirection)
    {
        worldAimDirection = Vector3.zero;
        if (inputReader == null)
        {
            return false;
        }

        Vector2 aimInput = inputReader.AimInput;
        if (inputReader.AimUsesGamepad)
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
                worldOrigin - aimCamera.transform.position,
                aimCamera.transform.forward);
            if (cameraDepth <= 0f)
            {
                return false;
            }

            Vector3 mouseWorldPosition = aimCamera.ScreenToWorldPoint(new Vector3(aimInput.x, aimInput.y, cameraDepth));
            worldAimDirection = mouseWorldPosition - worldOrigin;
            worldAimDirection.z = 0f;
        }

        worldAimDirection.z = 0f;
        return !float.IsNaN(worldAimDirection.x) && !float.IsNaN(worldAimDirection.y) &&
            !float.IsInfinity(worldAimDirection.x) && !float.IsInfinity(worldAimDirection.y) &&
            worldAimDirection.sqrMagnitude >= MinimumAimDirectionSqrMagnitude;
    }

    private void UpdateRangedWeaponAnimatorParameter()
    {
        if (hasRangedWeaponEquippedAnimatorParameter && animator != null)
        {
            animator.SetBool(rangedWeaponEquippedAnimatorParameterHash, currentWeapon != null);
        }
    }

    private static bool TryGetAnimatorBoolParameterHash(Animator targetAnimator, string parameterName, out int parameterHash)
    {
        parameterHash = 0;
        if (targetAnimator == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in targetAnimator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Bool)
            {
                parameterHash = Animator.StringToHash(parameterName);
                return true;
            }
        }

        return false;
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
        if (inputReader == null || playerItemInventory == null)
        {
            return;
        }

        List<ItemInstance> ownedWeapons = GetOwnedWeaponItems();
        if (ownedWeapons.Count == 0)
        {
            return;
        }

        if (testWeaponIndex < 0 || testWeaponIndex >= ownedWeapons.Count)
        {
            testWeaponIndex = FindOwnedWeaponIndex(ownedWeapons, currentWeaponItem != null
                ? currentWeaponItem.Definition as WeaponDefinition
                : null);
        }

        int quickSlotIndex = -1;
        if (inputReader.SelectQuickSlot1PressedThisFrame)
        {
            quickSlotIndex = 0;
        }
        else if (inputReader.SelectQuickSlot2PressedThisFrame)
        {
            quickSlotIndex = 1;
        }

        if (quickSlotIndex >= 0)
        {
            ItemInstance slottedWeapon = playerItemInventory.GetWeaponInEquipmentSlot(quickSlotIndex);
            if (slottedWeapon != null)
            {
                TryEquipOwnedWeapon(slottedWeapon);
            }

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
        else
        {
            step = inputReader.EquipmentScrollDirection;
        }

        if (step == 0)
        {
            return;
        }

        int currentIndex = testWeaponIndex >= 0 ? testWeaponIndex : 0;
        int nextIndex = (currentIndex + step + ownedWeapons.Count) % ownedWeapons.Count;
        EquipOwnedWeaponAt(ownedWeapons, nextIndex);
    }

    private void EquipOwnedWeaponAt(List<ItemInstance> ownedWeapons, int index)
    {
        if (index < 0 || index >= ownedWeapons.Count)
        {
            return;
        }

        ItemInstance weaponItem = ownedWeapons[index];
        if (!(weaponItem.Definition is WeaponDefinition definition))
        {
            return;
        }

        HeldWeapon weaponPrefab = FindHeldWeaponPrefab(definition);
        if (weaponPrefab == null)
        {
            Debug.LogWarning($"No held-weapon prefab is configured for owned weapon definition '{definition.name}'.", this);
            return;
        }

        if (TryEquipOwnedWeapon(weaponItem, weaponPrefab))
        {
            testWeaponIndex = index;
        }
    }
}
