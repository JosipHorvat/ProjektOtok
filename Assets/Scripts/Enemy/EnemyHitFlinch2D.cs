using UnityEngine;

/// <summary>
/// Twitches the rig bones of the body part struck by a nonlethal hit.
/// Runs after the Animator and 2D IK have posed the rig and before SpriteSkin deforms the sprites,
/// which is the only point in the frame where an additive bone rotation reaches the screen.
/// </summary>
[DefaultExecutionOrder(FlinchExecutionOrder)]
[DisallowMultipleComponent]
public sealed class EnemyHitFlinch2D : MonoBehaviour
{
    // IKManager2D runs LateUpdate at -10 and SpriteSkin deformation at 10; the flinch must sit between them.
    private const int FlinchExecutionOrder = 5;

    private const string ChestBonePath = "Root/Pelvis/Chest";
    private const string NeckBonePath = "Root/Pelvis/Chest/Neck";
    private const string HeadBonePath = "Root/Pelvis/Chest/Neck/Head_1";
    private const string LeftUpperArmBonePath = "Root/Pelvis/Chest/UpperArm_L";
    private const string LeftForearmBonePath = "Root/Pelvis/Chest/UpperArm_L/Forearm_L";
    private const string LeftHandBonePath = "Root/Pelvis/Chest/UpperArm_L/Forearm_L/Hand_L";
    private const string RightUpperArmBonePath = "Root/Pelvis/Chest/UpperArm_R";
    private const string RightForearmBonePath = "Root/Pelvis/Chest/UpperArm_R/Forearm_R";
    private const string RightHandBonePath = "Root/Pelvis/Chest/UpperArm_R/Forearm_R/Hand_R";
    private const string LeftThighBonePath = "Root/Pelvis/Thigh_L";
    private const string LeftShinBonePath = "Root/Pelvis/Thigh_L/Shin_L";
    private const string LeftFootBonePath = "Root/Pelvis/Thigh_L/Shin_L/Foot_L";
    private const string RightThighBonePath = "Root/Pelvis/Thigh_R";
    private const string RightShinBonePath = "Root/Pelvis/Thigh_R/Shin_R";
    private const string RightFootBonePath = "Root/Pelvis/Thigh_R/Shin_R/Foot_R";

    private const int ChestBoneIndex = 0;
    private const int NeckBoneIndex = 1;
    private const int LeftUpperArmBoneIndex = 2;
    private const int LeftForearmBoneIndex = 3;
    private const int RightUpperArmBoneIndex = 4;
    private const int RightForearmBoneIndex = 5;
    private const int LeftThighBoneIndex = 6;
    private const int LeftShinBoneIndex = 7;
    private const int RightThighBoneIndex = 8;
    private const int RightShinBoneIndex = 9;
    private const int BoneCount = 10;

    private const float DefaultFlinchDuration = 0.22f;
    private const float DefaultHeadAngle = 24f;
    private const float DefaultTorsoAngle = 12f;
    private const float DefaultArmAngle = 34f;
    private const float DefaultLegAngle = 20f;
    private const float DefaultSecondaryJointFraction = 0.5f;
    private const float DefaultBodySympathyFraction = 0.35f;
    private const float DefaultRegionSearchTolerance = 0.12f;

    private const float AttackPortion = 0.2f;
    private const float MinimumDirectionSqrMagnitude = 0.0001f;
    private const float MinimumLeverResponse = 0.001f;

    [Header("Timing")]
    [Tooltip("Total length of one twitch, from snap to full recovery.")]
    [SerializeField, Min(0.05f)] private float flinchDuration = DefaultFlinchDuration;

    [Header("Peak angles (degrees)")]
    [Tooltip("How far the neck snaps when the head is struck.")]
    [SerializeField, Range(0f, 60f)] private float headAngle = DefaultHeadAngle;
    [Tooltip("How far the chest leans when the torso is struck.")]
    [SerializeField, Range(0f, 45f)] private float torsoAngle = DefaultTorsoAngle;
    [Tooltip("How far the upper arm swings when an arm is struck.")]
    [SerializeField, Range(0f, 80f)] private float armAngle = DefaultArmAngle;
    [Tooltip("How far the thigh swings when a leg is struck.")]
    [SerializeField, Range(0f, 60f)] private float legAngle = DefaultLegAngle;

    [Header("Follow-through")]
    [Tooltip("Fraction of the limb angle applied in reverse to the forearm or shin so the limb bends instead of swinging stiffly.")]
    [SerializeField, Range(0f, 1f)] private float secondaryJointFraction = DefaultSecondaryJointFraction;
    [Tooltip("Fraction of the torso angle added to the chest on head and limb hits so the whole body reacts slightly.")]
    [SerializeField, Range(0f, 1f)] private float bodySympathyFraction = DefaultBodySympathyFraction;

    [Header("Region lookup")]
    [Tooltip("How far from a body-part hitbox a hit point may land and still count as that body part.")]
    [SerializeField, Min(0f)] private float regionSearchTolerance = DefaultRegionSearchTolerance;

    private EnemyHealth health;
    private FlinchBone[] bones;
    private Collider2D[] regionColliders;
    private HitRegionMarker[] regionMarkers;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        CacheBones();
        CacheRegionColliders();
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Damaged += HandleDamaged;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= HandleDamaged;
        }

        StopAllFlinches();
    }

    private void Update()
    {
        // Remove last frame's additive rotation before the Animator evaluates, so offsets never accumulate
        // on bones the current clip does not key.
        RemoveAppliedRotations();
    }

    private void LateUpdate()
    {
        if (bones == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        for (int index = 0; index < bones.Length; index++)
        {
            FlinchBone bone = bones[index];
            if (bone == null || !bone.IsActive)
            {
                continue;
            }

            bone.Elapsed += deltaTime;
            if (bone.Elapsed >= flinchDuration)
            {
                bone.IsActive = false;
                continue;
            }

            float angle = bone.PeakLocalAngle * EvaluateEnvelope(bone.Elapsed / flinchDuration);
            bone.PoseBeforeFlinch = bone.Bone.localRotation;
            bone.Bone.localRotation = Quaternion.Euler(0f, 0f, angle) * bone.PoseBeforeFlinch;
            bone.HasAppliedRotation = true;
        }
    }

    /// <summary>Plays a twitch on the given body region as if it had been struck along the given world direction.</summary>
    /// <param name="hitRegion">The body region to twitch.</param>
    /// <param name="hitDirection">World-space travel direction of the hit.</param>
    public void PlayFlinch(CharacterHitRegion hitRegion, Vector2 hitDirection)
    {
        if (bones == null || !isActiveAndEnabled)
        {
            return;
        }

        Vector2 pushDirection = hitDirection.sqrMagnitude > MinimumDirectionSqrMagnitude
            ? hitDirection.normalized
            : Vector2.right;

        switch (hitRegion)
        {
            case CharacterHitRegion.Head:
                Kick(NeckBoneIndex, pushDirection, headAngle);
                Kick(ChestBoneIndex, pushDirection, torsoAngle * bodySympathyFraction);
                break;
            case CharacterHitRegion.LeftArm:
                KickLimb(LeftUpperArmBoneIndex, LeftForearmBoneIndex, pushDirection, armAngle);
                break;
            case CharacterHitRegion.RightArm:
            case CharacterHitRegion.WeaponArm:
                KickLimb(RightUpperArmBoneIndex, RightForearmBoneIndex, pushDirection, armAngle);
                break;
            case CharacterHitRegion.LeftLeg:
                KickLimb(LeftThighBoneIndex, LeftShinBoneIndex, pushDirection, legAngle);
                break;
            case CharacterHitRegion.RightLeg:
                KickLimb(RightThighBoneIndex, RightShinBoneIndex, pushDirection, legAngle);
                break;
            default:
                Kick(ChestBoneIndex, pushDirection, torsoAngle);
                Kick(NeckBoneIndex, pushDirection, -headAngle * bodySympathyFraction);
                break;
        }
    }

    private void HandleDamaged(float damage, Vector2 hitPoint, Vector2 hitDirection)
    {
        if (health == null || health.IsDead || health.CurrentHealth <= 0f)
        {
            // The ragdoll takes over the rig on a lethal hit, so hand it a clean pose.
            StopAllFlinches();
            return;
        }

        Vector2 pushDirection = hitDirection.sqrMagnitude > MinimumDirectionSqrMagnitude
            ? hitDirection
            : (Vector2)transform.position - hitPoint;
        PlayFlinch(ResolveHitRegion(hitPoint), pushDirection);
    }

    private void KickLimb(int upperBoneIndex, int lowerBoneIndex, Vector2 pushDirection, float degrees)
    {
        float upperLocalAngle = Kick(upperBoneIndex, pushDirection, degrees);
        SetFlinch(lowerBoneIndex, -upperLocalAngle * secondaryJointFraction);
        Kick(ChestBoneIndex, pushDirection, torsoAngle * bodySympathyFraction);
    }

    /// <summary>Rotates a bone so its far end is pushed along the hit direction, whatever its pose or facing.</summary>
    private float Kick(int boneIndex, Vector2 pushDirection, float degrees)
    {
        FlinchBone bone = bones[boneIndex];
        if (bone == null)
        {
            return 0f;
        }

        Vector2 lever = bone.Tip.position - bone.Bone.position;
        Vector2 counterClockwiseTangent = new Vector2(-lever.y, lever.x);
        float leverResponse = Vector2.Dot(pushDirection, counterClockwiseTangent);
        float worldSign = Mathf.Abs(leverResponse) > MinimumLeverResponse
            ? Mathf.Sign(leverResponse)
            : (pushDirection.x >= 0f ? -1f : 1f);

        float localAngle = worldSign * degrees * GetParentHandedness(bone.Bone);
        SetFlinch(boneIndex, localAngle);
        return localAngle;
    }

    private void SetFlinch(int boneIndex, float peakLocalAngle)
    {
        FlinchBone bone = bones[boneIndex];
        if (bone == null)
        {
            return;
        }

        bone.PeakLocalAngle = peakLocalAngle;
        bone.Elapsed = 0f;
        bone.IsActive = true;
    }

    /// <summary>Returns -1 when the bone's parent space is mirrored (flipped facing), otherwise 1.</summary>
    private static float GetParentHandedness(Transform bone)
    {
        Transform parent = bone.parent;
        if (parent == null)
        {
            return 1f;
        }

        Vector3 parentRight = parent.TransformVector(Vector3.right);
        Vector3 parentUp = parent.TransformVector(Vector3.up);
        float determinant = parentRight.x * parentUp.y - parentRight.y * parentUp.x;
        return determinant < 0f ? -1f : 1f;
    }

    /// <summary>Fast snap to the peak, then a smooth settle back to the animated pose.</summary>
    private static float EvaluateEnvelope(float normalizedTime)
    {
        if (normalizedTime < AttackPortion)
        {
            return Mathf.SmoothStep(0f, 1f, normalizedTime / AttackPortion);
        }

        float recovery = (normalizedTime - AttackPortion) / (1f - AttackPortion);
        return 1f - Mathf.SmoothStep(0f, 1f, recovery);
    }

    private CharacterHitRegion ResolveHitRegion(Vector2 hitPoint)
    {
        CharacterHitRegion closestRegion = CharacterHitRegion.Torso;
        float closestSqrDistance = regionSearchTolerance * regionSearchTolerance;

        for (int index = 0; index < regionColliders.Length; index++)
        {
            Collider2D regionCollider = regionColliders[index];
            if (regionCollider == null || !regionCollider.enabled || !regionCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            float sqrDistance = (regionCollider.ClosestPoint(hitPoint) - hitPoint).sqrMagnitude;
            if (sqrDistance <= closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closestRegion = regionMarkers[index].Region;
                if (sqrDistance <= 0f)
                {
                    break;
                }
            }
        }

        return closestRegion;
    }

    private void RemoveAppliedRotations()
    {
        if (bones == null)
        {
            return;
        }

        for (int index = 0; index < bones.Length; index++)
        {
            FlinchBone bone = bones[index];
            if (bone == null || !bone.HasAppliedRotation)
            {
                continue;
            }

            bone.Bone.localRotation = bone.PoseBeforeFlinch;
            bone.HasAppliedRotation = false;
        }
    }

    private void StopAllFlinches()
    {
        RemoveAppliedRotations();
        if (bones == null)
        {
            return;
        }

        for (int index = 0; index < bones.Length; index++)
        {
            if (bones[index] != null)
            {
                bones[index].IsActive = false;
            }
        }
    }

    private void CacheBones()
    {
        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            Debug.LogWarning($"{nameof(EnemyHitFlinch2D)} on '{name}' could not find the enemy Animator.", this);
            return;
        }

        Transform rigRoot = animator.transform;
        bones = new FlinchBone[BoneCount];
        CacheBone(rigRoot, ChestBoneIndex, ChestBonePath, NeckBonePath);
        CacheBone(rigRoot, NeckBoneIndex, NeckBonePath, HeadBonePath);
        CacheBone(rigRoot, LeftUpperArmBoneIndex, LeftUpperArmBonePath, LeftForearmBonePath);
        CacheBone(rigRoot, LeftForearmBoneIndex, LeftForearmBonePath, LeftHandBonePath);
        CacheBone(rigRoot, RightUpperArmBoneIndex, RightUpperArmBonePath, RightForearmBonePath);
        CacheBone(rigRoot, RightForearmBoneIndex, RightForearmBonePath, RightHandBonePath);
        CacheBone(rigRoot, LeftThighBoneIndex, LeftThighBonePath, LeftShinBonePath);
        CacheBone(rigRoot, LeftShinBoneIndex, LeftShinBonePath, LeftFootBonePath);
        CacheBone(rigRoot, RightThighBoneIndex, RightThighBonePath, RightShinBonePath);
        CacheBone(rigRoot, RightShinBoneIndex, RightShinBonePath, RightFootBonePath);
    }

    private void CacheBone(Transform rigRoot, int boneIndex, string bonePath, string tipPath)
    {
        Transform bone = rigRoot.Find(bonePath);
        Transform tip = rigRoot.Find(tipPath);
        if (bone == null || tip == null)
        {
            Debug.LogWarning($"{nameof(EnemyHitFlinch2D)} on '{name}' could not find rig bone '{bonePath}' or '{tipPath}'.", this);
            return;
        }

        bones[boneIndex] = new FlinchBone(bone, tip);
    }

    private void CacheRegionColliders()
    {
        HitRegionMarker[] markers = GetComponentsInChildren<HitRegionMarker>(true);
        int specificRegionCount = 0;
        for (int index = 0; index < markers.Length; index++)
        {
            if (IsSpecificRegionHitbox(markers[index]))
            {
                specificRegionCount++;
            }
        }

        regionColliders = new Collider2D[specificRegionCount];
        regionMarkers = new HitRegionMarker[specificRegionCount];
        int writeIndex = 0;
        for (int index = 0; index < markers.Length; index++)
        {
            if (!IsSpecificRegionHitbox(markers[index]))
            {
                continue;
            }

            regionMarkers[writeIndex] = markers[index];
            regionColliders[writeIndex] = markers[index].GetComponent<Collider2D>();
            writeIndex++;
        }
    }

    private static bool IsSpecificRegionHitbox(HitRegionMarker marker)
    {
        return marker.Region != CharacterHitRegion.Torso && marker.GetComponent<Collider2D>() != null;
    }

    private sealed class FlinchBone
    {
        public readonly Transform Bone;
        public readonly Transform Tip;
        public Quaternion PoseBeforeFlinch;
        public float PeakLocalAngle;
        public float Elapsed;
        public bool IsActive;
        public bool HasAppliedRotation;

        public FlinchBone(Transform bone, Transform tip)
        {
            Bone = bone;
            Tip = tip;
        }
    }
}
