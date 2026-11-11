using UnityEngine;

/// <summary>
/// Describes how a weapon sits in the player's hands. Lives on the weapon prefab root so that
/// swapping weapons (inventory, pickups) only swaps data — the rig itself never changes.
/// The prefab root is the main-hand grip point and its +X axis is the barrel direction.
/// </summary>
[DisallowMultipleComponent]
public sealed class HeldWeapon : MonoBehaviour
{
    public enum SupportHandMode
    {
        /// <summary>Far hand grabs the weapon at <see cref="SupportGrip"/> at all times.</summary>
        OnWeapon = 0,
        /// <summary>Far hand grabs the weapon while aiming; released to character animation when relaxed.</summary>
        OnWeaponWhileAiming = 1,
        /// <summary>Far hand is released to animation at all times (one-handed weapons).</summary>
        Free = 2,
    }

    private const float MinimumOffsetThresholdSqr = 0.0001f;

    [Header("Fit markers (local to this transform)")]
    public Transform mainGrip;

    public Transform supportGrip;

    public Transform muzzle;

    [Tooltip("World-space point where the projectile is instantiated.")]
    public Transform bulletEjectPoint;

    [Tooltip("World-space spawn point for the casing ejection effect.")]
    public Transform shellEjectPoint;

    [Header("Fire configuration")]
    public WeaponDefinition definition;

    [Header("Hold pose")]
    [Tooltip("Where the main grip sits relative to the aim pivot (shoulder) while aiming. X is along the aim direction.")]
    public Vector2 aimHoldOffset = new Vector2(0.65f, -0.45f);

    [Tooltip("Where the main grip sits relative to the aim pivot while idle and not aiming.")]
    public Vector2 relaxedHoldOffset = new Vector2(0.6f, -0.5f);

    [Tooltip("Where the main grip sits relative to the aim pivot while moving/running.")]
    public Vector2 runningHoldOffset = new Vector2(0.48f, -0.48f);

    public SupportHandMode supportHand = SupportHandMode.OnWeapon;

    [Header("Angles (degrees, local to the player, 0 = straight ahead)")]
    [Range(-90f, 90f)] public float relaxedAngle = -15f;
    [Range(-90f, 90f)] public float runningAngle = -30f;
    [Range(-90f, 0f)] public float minimumAimAngle = -75f;
    [Range(0f, 90f)] public float maximumAimAngle = 80f;

    [Header("Support hand engagement")]
    [Tooltip("Whether the far hand holds the weapon during idle.")]
    public bool supportHandInIdle;
    [Tooltip("Whether the far hand holds the weapon while moving/running.")]
    public bool supportHandInRun = true;
    [Tooltip("Whether the far hand holds the weapon while aiming.")]
    public bool supportHandInAim = true;

    /// <summary>Marker the main (near) hand holds. Falls back to this transform.</summary>
    public Transform MainGrip => mainGrip != null ? mainGrip : transform;

    /// <summary>Marker the support (far) hand holds. Falls back to the main grip.</summary>
    public Transform SupportGrip => supportGrip != null ? supportGrip : MainGrip;

    /// <summary>Barrel exit point. Null when the weapon has no muzzle marker.</summary>
    public Transform Muzzle => muzzle;

    /// <summary>Position where the projectile is spawned. Falls back to the muzzle, then weapon root.</summary>
    public Transform BulletEjectPoint => bulletEjectPoint != null ? bulletEjectPoint : Muzzle != null ? Muzzle : transform;

    /// <summary>Position where a future casing ejection effect is spawned.</summary>
    public Transform ShellEjectPoint => shellEjectPoint;

    /// <summary>Fire and ammunition settings for this weapon. Null until configured.</summary>
    public WeaponDefinition Definition => definition;

    public Vector2 AimHoldOffset => aimHoldOffset;

    public Vector2 RelaxedHoldOffset => relaxedHoldOffset;

    /// <summary>Where the main grip sits relative to the aim pivot while moving or running.</summary>
    public Vector2 RunningHoldOffset => runningHoldOffset.sqrMagnitude > MinimumOffsetThresholdSqr ? runningHoldOffset : relaxedHoldOffset;

    public SupportHandMode SupportHand => supportHand;

    public float RelaxedAngle => relaxedAngle;

    public float RunningAngle => runningAngle;

    public float MinimumAimAngle => minimumAimAngle;

    public float MaximumAimAngle => maximumAimAngle;

    /// <summary>True if the support (far) hand grabs the weapon when standing idle.</summary>
    public bool SupportHandInIdle => supportHandInIdle;

    /// <summary>True if the support (far) hand grabs the weapon while moving or running.</summary>
    public bool SupportHandInRun => supportHandInRun;

    /// <summary>True if the support (far) hand grabs the weapon while aiming.</summary>
    public bool SupportHandInAim => supportHandInAim;

    /// <summary>
    /// Checks whether the support (far) hand should hold the weapon given the current character state.
    /// </summary>
    /// <param name="isAiming">True if the character is actively aiming.</param>
    /// <param name="isMoving">True if the character is walking or running.</param>
    /// <returns>True if the support hand IK solver should place the hand on the weapon.</returns>
    public bool IsSupportHandActive(bool isAiming, bool isMoving)
    {
        if (isAiming)
        {
            return supportHandInAim;
        }

        return isMoving ? supportHandInRun : supportHandInIdle;
    }

    /// <summary>Clamps an aim angle to the range this weapon can be raised or lowered to.</summary>
    public float ClampAimAngle(float angle) => Mathf.Clamp(angle, minimumAimAngle, maximumAimAngle);
}
