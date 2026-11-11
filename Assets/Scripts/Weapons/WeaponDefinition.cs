using UnityEngine;

/// <summary>Reusable ranged-weapon configuration, separated from the held weapon's pose data.</summary>
[CreateAssetMenu(fileName = "WeaponDefinition", menuName = "Weapons/Weapon Definition")]
public sealed class WeaponDefinition : ScriptableObject
{
    public enum FireMode
    {
        SemiAutomatic = 0,
        Automatic = 1,
    }

    public enum AmmunitionType
    {
        None = 0,
        Pistol = 1,
        Rifle = 2,
        Shotgun = 3,
    }

    public enum ReloadStyle
    {
        MagazineSwap = 0,
        RoundByRound = 1,
    }

    [Header("Ranged weapon stats")]
    public FireMode fireMode = FireMode.SemiAutomatic;
    public AmmunitionType ammunitionType = AmmunitionType.Pistol;
    [Min(0.01f)] public float shotsPerSecond = 2.5f;
    [Min(1)] public int magazineCapacity = 10;
    [Min(0f)] public float damage = 18f;
    [Min(0)] public int pierce;
    [Min(0f)] public float shotPower = 1f;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    [Min(0.01f)] public float projectileSpeed = 28f;
    [Min(0.01f)] public float range = 25f;
    public LayerMask hitLayers = ~0;

    [Header("Accuracy and recoil")]
    [Tooltip("Angular spread in degrees while firing without holding the Aim input.")]
    [Min(0f)] public float hipFireSpread;
    [Tooltip("Base angular spread in degrees while Focus Aim is held. Keep this at zero for an accurate first shot.")]
    [Min(0f)] public float aimedSpread;
    [Tooltip("Additional aimed spread in degrees added after each successful aimed shot.")]
    [Min(0f)] public float aimedSpreadIncreasePerShot = 0.7f;
    [Tooltip("Maximum total aimed spread in degrees, including the base aimed spread.")]
    [Min(0f)] public float maximumAimedSpread = 6f;
    [Tooltip("Degrees per second that aimed spread recovers toward its base value.")]
    [Min(0f)] public float aimedSpreadRecoveryDegreesPerSecond = 2f;
    [Tooltip("Upward aim kick applied after a successful shot, in degrees.")]
    [Min(0f)] public float recoilDegrees = 8f;
    [Tooltip("Degrees per second at which recoil returns to the mouse aim direction.")]
    [Min(0f)] public float recoilRecoveryDegreesPerSecond = 12f;
    [Tooltip("How long a tap-fire request holds the weapon raised before returning to the relaxed pose.")]
    [Min(0.01f)] public float firePoseDuration = 0.28f;

    [Header("Pistol feedback")]
    public GameObject muzzleFlashPrefab;
    [Min(0.01f)] public float muzzleFlashDuration = 0.16f;
    [Min(0.1f)] public float muzzleFlashScale = 2.5f;
    public GameObject muzzleSmokePrefab;
    [Min(0.01f)] public float muzzleSmokeDuration = 0.65f;
    [Min(0.1f)] public float muzzleSmokeScale = 3.5f;
    public GameObject shellCasingPrefab;
    [Min(0f)] public float shellEjectionForce = 1.5f;
    [Min(0f)] public float shellEjectionAngleRange = 55f;
    [Min(0f)] public float shellSpinDegreesPerSecond = 720f;
    [Tooltip("Animator trigger used for a successful semi-automatic shot.")]
    public string semiAutomaticFireTrigger = "Fire";
    [Tooltip("Animator bool held while an automatic weapon is actively firing.")]
    public string automaticFireBool = "Firing";

    [Header("Reload")]
    [Tooltip("MagazineSwap replaces the magazine at one animation event. RoundByRound transfers one round per insertion event.")]
    public ReloadStyle reloadStyle = ReloadStyle.MagazineSwap;
    [Tooltip("Duration, in seconds, of the complete magazine-swap reload animation.")]
    [Min(0.05f)] public float reloadDuration = 2f;
    [Tooltip("Duration, in seconds, of one round-insertion animation loop.")]
    [Min(0.05f)] public float roundInsertInterval = 0.5f;
    [Tooltip("Animator trigger used to start a magazine-swap reload.")]
    public string magazineReloadTrigger = "ReloadMagazine";
    [Tooltip("Animator trigger used to start a round-by-round reload.")]
    public string roundByRoundReloadTrigger = "ReloadRoundByRound";
    [Tooltip("Animator float parameter used only to scale the active reload state.")]
    public string reloadSpeedParameter = "ReloadSpeed";
    [Tooltip("Animator trigger used to leave an interrupted or completed reload state.")]
    public string reloadCancelTrigger = "ReloadComplete";

    [Header("Future inventory data")]
    public Sprite inventoryIcon;
    [Min(1)] public int inventoryWidth = 1;
    [Min(1)] public int inventoryHeight = 2;

    public FireMode Mode => fireMode;

    public AmmunitionType AmmoType => ammunitionType;

    public float ShotsPerSecond => shotsPerSecond;

    public int MagazineCapacity => magazineCapacity;

    public float Damage => damage;

    public int Pierce => pierce;

    public float ShotPower => shotPower;

    public GameObject ProjectilePrefab => projectilePrefab;

    public float ProjectileSpeed => projectileSpeed;

    public float Range => range;

    public LayerMask HitLayers => hitLayers;

    public float HipFireSpread => hipFireSpread;

    public float AimedSpread => aimedSpread;

    public float AimedSpreadIncreasePerShot => aimedSpreadIncreasePerShot;

    public float MaximumAimedSpread => maximumAimedSpread;

    public float AimedSpreadRecoveryDegreesPerSecond => aimedSpreadRecoveryDegreesPerSecond;

    public float RecoilDegrees => recoilDegrees;

    public float RecoilRecoveryDegreesPerSecond => recoilRecoveryDegreesPerSecond;

    public float FirePoseDuration => firePoseDuration;

    public GameObject MuzzleFlashPrefab => muzzleFlashPrefab;

    public float MuzzleFlashDuration => muzzleFlashDuration > 0f ? muzzleFlashDuration : 0.16f;

    public float MuzzleFlashScale => muzzleFlashScale > 0f ? muzzleFlashScale : 2.5f;

    public GameObject MuzzleSmokePrefab => muzzleSmokePrefab;

    public float MuzzleSmokeDuration => muzzleSmokeDuration > 0f ? muzzleSmokeDuration : 0.65f;

    public float MuzzleSmokeScale => muzzleSmokeScale > 0f ? muzzleSmokeScale : 3.5f;

    public GameObject ShellCasingPrefab => shellCasingPrefab;

    public float ShellEjectionForce => shellEjectionForce > 0f ? shellEjectionForce : 1.5f;

    public float ShellEjectionAngleRange => shellEjectionAngleRange > 0f ? shellEjectionAngleRange : 55f;

    public float ShellSpinDegreesPerSecond => shellSpinDegreesPerSecond > 0f ? shellSpinDegreesPerSecond : 720f;


    private void OnValidate()
    {
        reloadDuration = Mathf.Max(0.05f, reloadDuration);
        roundInsertInterval = Mathf.Max(0.05f, roundInsertInterval);
        magazineCapacity = Mathf.Max(1, magazineCapacity);
    }

    public string SemiAutomaticFireTrigger => semiAutomaticFireTrigger;

    public string AutomaticFireBool => automaticFireBool;

    public ReloadStyle ReloadingStyle => reloadStyle;

    public float ReloadDuration => Mathf.Max(0.05f, reloadDuration);

    public float RoundInsertInterval => Mathf.Max(0.05f, roundInsertInterval);

    public string MagazineReloadTrigger => magazineReloadTrigger;

    public string RoundByRoundReloadTrigger => roundByRoundReloadTrigger;

    public string ReloadSpeedParameter => reloadSpeedParameter;

    public string ReloadCancelTrigger => reloadCancelTrigger;

    public Sprite InventoryIcon => inventoryIcon;

    public Vector2Int InventoryFootprint => new Vector2Int(inventoryWidth, inventoryHeight);
}
