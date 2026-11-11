using UnityEngine;

/// <summary>Spawns visible projectiles using the existing player input and weapon-hold rig.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(0)]
public sealed class WeaponFireController : MonoBehaviour
{
    private const float MinimumShotInterval = 0.001f;
    private const float MinimumAimInputSqrMagnitude = 0.04f;
    private const float MinimumDirectionSqrMagnitude = 0.0001f;

    public PlayerInputReader inputReader;

    public WeaponHoldController weaponHoldController;

    public Animator animator;

    public Camera aimCamera;

    [Header("Shot diagnostics")]
    public bool logShotDebug = true;

    private Player player;
    private HeldWeapon observedWeapon;
    private HeldWeapon pendingShotWeapon;
    private WeaponDefinition pendingShotDefinition;
    private float nextShotTime;
    private float currentAimedSpreadBloomDegrees;
    private int automaticFireParameterHash;
    private bool automaticFireParameterActive;
    private bool hasPendingShot;
    private bool pendingShotRequiresAimPose;

    /// <summary>Current configured angular spread for the equipped weapon and input aim state.</summary>
    public float CurrentSpreadDegrees
    {
        get
        {
            HeldWeapon weapon = weaponHoldController != null ? weaponHoldController.CurrentWeapon : null;
            WeaponDefinition definition = weapon != null ? weapon.Definition : null;
            if (definition == null)
            {
                return 0f;
            }

            return inputReader != null && inputReader.FocusAimHeld
                ? GetCurrentAimedSpread(definition)
                : definition.HipFireSpread;
        }
    }

    private void Awake()
    {
        if (inputReader == null)
        {
            inputReader = GetComponentInParent<PlayerInputReader>();
        }

        if (weaponHoldController == null)
        {
            weaponHoldController = GetComponentInChildren<WeaponHoldController>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        player = GetComponentInParent<Player>();
    }

    private void Update()
    {
        HeldWeapon weapon = weaponHoldController != null ? weaponHoldController.CurrentWeapon : null;
        if (weapon != observedWeapon)
        {
            ClearAutomaticFireParameter();
            observedWeapon = weapon;
            nextShotTime = 0f;
            currentAimedSpreadBloomDegrees = 0f;
            ClearPendingShot();
        }

        WeaponDefinition definition = weapon != null ? weapon.Definition : null;
        if (definition == null)
        {
            currentAimedSpreadBloomDegrees = 0f;
        }
        else
        {
            float maximumBloom = Mathf.Max(0f, definition.MaximumAimedSpread - definition.AimedSpread);
            currentAimedSpreadBloomDegrees = Mathf.Min(currentAimedSpreadBloomDegrees, maximumBloom);
            currentAimedSpreadBloomDegrees = Mathf.MoveTowards(
                currentAimedSpreadBloomDegrees,
                0f,
                definition.AimedSpreadRecoveryDegreesPerSecond * Time.deltaTime);
        }

        WeaponAmmoState ammoState = weaponHoldController != null ? weaponHoldController.CurrentAmmoState : null;
        bool firePressedThisFrame = inputReader != null && inputReader.FireThrowPressedThisFrame;
        if (firePressedThisFrame && logShotDebug)
        {
            string weaponName = weapon != null ? weapon.name : "<no equipped weapon>";
            string definitionName = definition != null ? definition.name : "<no definition>";
            string ammoText = ammoState != null ? ammoState.LoadedRounds.ToString() : "<no ammo state>";
            Debug.Log($"[WeaponFire] Fire input received. Weapon={weaponName}, Definition={definitionName}, Ammo={ammoText}, Blocked={IsBlocked()}.", this);
        }

        if (definition == null || ammoState == null || inputReader == null || IsBlocked())
        {
            if (firePressedThisFrame && logShotDebug)
            {
                Debug.LogWarning("[WeaponFire] Shot stopped: weapon definition, ammo state, input reader, or gameplay state is unavailable.", this);
            }

            ClearPendingShot();
            ClearAutomaticFireParameter();
            return;
        }

        bool automaticHeld = definition.Mode == WeaponDefinition.FireMode.Automatic && inputReader.FireThrowHeld;
        if (definition.Mode == WeaponDefinition.FireMode.Automatic)
        {
            SetAutomaticFireParameter(definition, automaticHeld && ammoState.LoadedRounds > 0);
        }
        else
        {
            ClearAutomaticFireParameter();
        }

        if (ammoState.LoadedRounds <= 0)
        {
            if (firePressedThisFrame && logShotDebug)
            {
                Debug.LogWarning("[WeaponFire] Shot stopped: magazine is empty.", this);
            }

            ClearPendingShot();
            ClearAutomaticFireParameter();
            return;
        }

        if (hasPendingShot)
        {
            weaponHoldController.RequestFirePose(pendingShotDefinition.FirePoseDuration);
            return;
        }

        bool shouldAttemptShot = definition.Mode == WeaponDefinition.FireMode.SemiAutomatic
            ? firePressedThisFrame
            : automaticHeld;
        if (!shouldAttemptShot || Time.time < nextShotTime)
        {
            return;
        }

        pendingShotWeapon = weapon;
        pendingShotDefinition = definition;
        pendingShotRequiresAimPose = true;
        hasPendingShot = true;
        weaponHoldController.RequestFirePose(definition.FirePoseDuration);
    }

    private void LateUpdate()
    {
        if (!hasPendingShot || pendingShotWeapon == null || pendingShotDefinition == null || IsBlocked())
        {
            return;
        }

        if (weaponHoldController == null || weaponHoldController.CurrentWeapon != pendingShotWeapon)
        {
            ClearPendingShot();
            return;
        }

        if (pendingShotRequiresAimPose && !weaponHoldController.AimPoseReady)
        {
            return;
        }

        WeaponAmmoState ammoState = weaponHoldController.CurrentAmmoState;
        if (ammoState == null || ammoState.LoadedRounds <= 0)
        {
            ClearPendingShot();
            return;
        }

        HeldWeapon weapon = pendingShotWeapon;
        WeaponDefinition definition = pendingShotDefinition;
        bool fired = FireOneShot(weapon, definition, ammoState);
        if (fired)
        {
            nextShotTime = Time.time + Mathf.Max(1f / Mathf.Max(definition.ShotsPerSecond, 0.01f), MinimumShotInterval);
        }

        ClearPendingShot();
    }

    private void ClearPendingShot()
    {
        hasPendingShot = false;
        pendingShotRequiresAimPose = false;
        pendingShotWeapon = null;
        pendingShotDefinition = null;
    }


    private bool IsBlocked()
    {
        return player != null && player.IsOnLadder;
    }

    private bool FireOneShot(HeldWeapon weapon, WeaponDefinition definition, WeaponAmmoState ammoState)
    {
        if (definition.ProjectilePrefab == null)
        {
            if (logShotDebug)
            {
                Debug.LogError("[WeaponFire] Shot stopped: Projectile Prefab is not assigned in the weapon definition.", this);
            }

            return false;
        }

        BulletProjectile2D projectilePrefab = definition.ProjectilePrefab.GetComponent<BulletProjectile2D>();
        if (projectilePrefab == null)
        {
            if (logShotDebug)
            {
                Debug.LogError($"[WeaponFire] Shot stopped: {definition.ProjectilePrefab.name} has no BulletProjectile2D component.", definition.ProjectilePrefab);
            }

            return false;
        }

        if (!ammoState.TrySpendRound(definition))
        {
            if (logShotDebug)
            {
                Debug.LogWarning("[WeaponFire] Shot stopped: loaded ammo could not be spent.", this);
            }

            return false;
        }

        Transform muzzle = weapon.Muzzle != null ? weapon.Muzzle : weapon.transform;
        Transform bulletEjectPoint = weapon.BulletEjectPoint;
        bool isAimedShot = inputReader.FocusAimHeld;
        Vector2 direction = GetShotDirection(weapon, bulletEjectPoint, isAimedShot);
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = weapon.transform.right;
        }

        float spread = isAimedShot ? GetCurrentAimedSpread(definition) : definition.HipFireSpread;
        direction = Quaternion.Euler(0f, 0f, Random.Range(-spread, spread)) * direction.normalized;
        Quaternion shotRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        BulletProjectile2D bullet = Instantiate(projectilePrefab, bulletEjectPoint.position, shotRotation);
        Transform ownerRoot = player != null ? player.transform : transform.root;
        bullet.Initialize(direction, definition.ProjectileSpeed, definition.Range, definition.HitLayers, ownerRoot);
        if (logShotDebug)
        {
            Debug.Log($"[WeaponFire] Spawned projectile at bullet eject point {bulletEjectPoint.position} heading {direction}, ammo remaining={ammoState.LoadedRounds}.", bullet);
        }

        SpawnMuzzleEffect(definition.MuzzleFlashPrefab, muzzle.position, shotRotation, definition.MuzzleFlashDuration, definition.MuzzleFlashScale);
        SpawnMuzzleEffect(definition.MuzzleSmokePrefab, muzzle.position, shotRotation, definition.MuzzleSmokeDuration, definition.MuzzleSmokeScale);
        SpawnShellCasing(weapon, definition, direction);
        weaponHoldController.ApplyShotRecoil(definition.RecoilDegrees, definition.RecoilRecoveryDegreesPerSecond);
        if (isAimedShot)
        {
            AddAimedSpreadBloom(definition);
        }

        if (definition.Mode == WeaponDefinition.FireMode.SemiAutomatic)
        {
            PlayPistolFireAnimation(definition);
        }

        return true;
    }

    private Vector2 GetShotDirection(HeldWeapon weapon, Transform bulletEjectPoint, bool isAimedShot)
    {
        if (isAimedShot || inputReader == null)
        {
            return weaponHoldController.AimDirection;
        }

        Vector2 aimInput = inputReader.AimInput;
        if (inputReader.AimUsesGamepad)
        {
            if (aimInput.sqrMagnitude < MinimumAimInputSqrMagnitude)
            {
                return weaponHoldController.AimDirection;
            }

            Vector3 cameraRight = aimCamera != null ? aimCamera.transform.right : Vector3.right;
            Vector3 cameraUp = aimCamera != null ? aimCamera.transform.up : Vector3.up;
            Vector3 gamepadDirection = cameraRight * aimInput.x + cameraUp * aimInput.y;
            return new Vector2(gamepadDirection.x, gamepadDirection.y).normalized;
        }

        if (aimCamera == null)
        {
            return weaponHoldController.AimDirection;
        }

        float cameraDepth = Vector3.Dot(
            bulletEjectPoint.position - aimCamera.transform.position,
            aimCamera.transform.forward);
        if (cameraDepth <= 0f)
        {
            return weaponHoldController.AimDirection;
        }

        Vector3 mouseWorldPosition = aimCamera.ScreenToWorldPoint(
            new Vector3(aimInput.x, aimInput.y, cameraDepth));
        Vector2 mouseDirection = mouseWorldPosition - bulletEjectPoint.position;
        return mouseDirection.sqrMagnitude > MinimumDirectionSqrMagnitude
            ? mouseDirection.normalized
            : weapon.transform.right;
    }

    private float GetCurrentAimedSpread(WeaponDefinition definition)
    {
        float maximumSpread = Mathf.Max(definition.AimedSpread, definition.MaximumAimedSpread);
        return Mathf.Clamp(definition.AimedSpread + currentAimedSpreadBloomDegrees, 0f, maximumSpread);
    }

    private void AddAimedSpreadBloom(WeaponDefinition definition)
    {
        float maximumBloom = Mathf.Max(0f, definition.MaximumAimedSpread - definition.AimedSpread);
        currentAimedSpreadBloomDegrees = Mathf.Min(
            currentAimedSpreadBloomDegrees + definition.AimedSpreadIncreasePerShot,
            maximumBloom);
    }

    private static void SpawnMuzzleEffect(GameObject effectPrefab, Vector3 position, Quaternion rotation, float duration, float scaleMultiplier)
    {
        if (effectPrefab == null)
        {
            return;
        }

        GameObject effectObject = Instantiate(effectPrefab, position, rotation);
        MuzzleFlashEffect2D effect = effectObject.GetComponent<MuzzleFlashEffect2D>();
        if (effect != null)
        {
            effect.Initialize(duration, scaleMultiplier);
        }
    }

    private static void SpawnShellCasing(HeldWeapon weapon, WeaponDefinition definition, Vector2 shotDirection)
    {
        if (definition.ShellCasingPrefab == null)
        {
            return;
        }

        Transform ejectPoint = weapon.ShellEjectPoint != null ? weapon.ShellEjectPoint : weapon.transform;
        GameObject shellObject = Instantiate(definition.ShellCasingPrefab, ejectPoint.position, ejectPoint.rotation);
        ShellCasing2D casing = shellObject.GetComponent<ShellCasing2D>();
        if (casing == null)
        {
            return;
        }

        float ejectionAngle = Random.Range(-definition.ShellEjectionAngleRange, definition.ShellEjectionAngleRange);
        Vector2 ejectionDirection = Quaternion.Euler(0f, 0f, ejectionAngle) * -shotDirection;
        float force = Random.Range(definition.ShellEjectionForce * 0.75f, definition.ShellEjectionForce * 1.25f);
        float spin = Random.Range(-definition.ShellSpinDegreesPerSecond, definition.ShellSpinDegreesPerSecond);
        casing.Initialize(ejectionDirection * force, spin);
    }

    private void PlayPistolFireAnimation(WeaponDefinition definition)
    {
        if (animator == null || string.IsNullOrWhiteSpace(definition.SemiAutomaticFireTrigger))
        {
            return;
        }

        animator.SetTrigger(definition.SemiAutomaticFireTrigger);
    }

    private void SetAutomaticFireParameter(WeaponDefinition definition, bool isActive)
    {
        if (animator == null || string.IsNullOrWhiteSpace(definition.AutomaticFireBool))
        {
            ClearAutomaticFireParameter();
            return;
        }

        int parameterHash = Animator.StringToHash(definition.AutomaticFireBool);
        if (automaticFireParameterActive && automaticFireParameterHash != parameterHash)
        {
            animator.SetBool(automaticFireParameterHash, false);
        }

        automaticFireParameterHash = parameterHash;
        automaticFireParameterActive = isActive;
        animator.SetBool(automaticFireParameterHash, isActive);
    }

    private void ClearAutomaticFireParameter()
    {
        if (animator != null && automaticFireParameterActive && automaticFireParameterHash != 0)
        {
            animator.SetBool(automaticFireParameterHash, false);
        }

        automaticFireParameterActive = false;
    }
}
