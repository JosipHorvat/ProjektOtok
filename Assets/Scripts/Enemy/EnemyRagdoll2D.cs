using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.IK;

/// <summary>Builds a dormant 2D ragdoll on the enemy sprite rig and activates it on death.</summary>
[DisallowMultipleComponent]
public sealed class EnemyRagdoll2D : MonoBehaviour
{
    private const string HeadBonePath = "Root/Pelvis/Chest/Neck/Head_1";
    private const string HeadVisualPath = "Head";
    private const string LeftLegBoneRootPath = "Root/Pelvis/Thigh_L";
    private const string RightLegBoneRootPath = "Root/Pelvis/Thigh_R";
    private const string LeftArmBoneRootPath = "Root/Pelvis/Chest/UpperArm_L";
    private const string RightArmBoneRootPath = "Root/Pelvis/Chest/UpperArm_R";
    private const string PlayerLayerName = "Player";
    private const string EnemyLayerName = "Enemy";
    private const string EnemyAttackLayerName = "EnemyAttack";
    private const float SettleLinearSpeedThreshold = 0.04f;
    private const float SettleAngularSpeedThreshold = 1.5f;

    [Header("Death response")]
    [Tooltip("Base impulse applied to the chest when the enemy dies, before scaling by weapon shot power.")]
    [SerializeField, Min(0f)] private float deathImpulse = 1.5f;
    [Tooltip("Maximum magnitude of the weapon-scaled death impulse, in impulse units.")]
    [SerializeField, Min(0f)] private float maximumDeathImpulse = 3f;
    [Tooltip("Small upward bias added to bullet direction so impacts lift the ragdoll slightly.")]
    [SerializeField, Range(0f, 0.5f)] private float upwardImpulseBias = 0.12f;
    [Tooltip("Seconds the complete rig must remain almost motionless before its pose is frozen.")]
    [SerializeField, Min(0.1f)] private float settleDuration = 0.6f;

    private static readonly BoneDefinition[] BoneDefinitions =
    {
        new BoneDefinition("Root/Pelvis", null, 0.32f, 0.29f, 0.14f, 0.9f, -180f, 180f),
        new BoneDefinition("Root/Pelvis/Chest", "Root/Pelvis", 0.42f, 0.32f, 0.16f, 1.1f, -25f, 25f),
        new BoneDefinition("Root/Pelvis/Chest/Neck", "Root/Pelvis/Chest", 0.20f, 0.16f, 0.07f, 0.25f, -30f, 30f),
        new BoneDefinition(HeadBonePath, "Root/Pelvis/Chest/Neck", 0.34f, 0.34f, 0.12f, 0.45f, -35f, 35f),
        new BoneDefinition("Root/Pelvis/Chest/UpperArm_L", "Root/Pelvis/Chest", 0.36f, 0.15f, 0.17f, 0.3f, -65f, 65f),
        new BoneDefinition("Root/Pelvis/Chest/UpperArm_L/Forearm_L", "Root/Pelvis/Chest/UpperArm_L", 0.35f, 0.13f, 0.16f, 0.22f, -80f, 80f),
        new BoneDefinition("Root/Pelvis/Chest/UpperArm_L/Forearm_L/Hand_L", "Root/Pelvis/Chest/UpperArm_L/Forearm_L", 0.21f, 0.14f, 0.08f, 0.15f, -35f, 35f),
        new BoneDefinition("Root/Pelvis/Chest/UpperArm_R", "Root/Pelvis/Chest", 0.36f, 0.15f, 0.17f, 0.3f, -65f, 65f),
        new BoneDefinition("Root/Pelvis/Chest/UpperArm_R/Forearm_R", "Root/Pelvis/Chest/UpperArm_R", 0.35f, 0.13f, 0.16f, 0.22f, -80f, 80f),
        new BoneDefinition("Root/Pelvis/Chest/UpperArm_R/Forearm_R/Hand_R", "Root/Pelvis/Chest/UpperArm_R/Forearm_R", 0.21f, 0.14f, 0.08f, 0.15f, -35f, 35f),
        new BoneDefinition("Root/Pelvis/Thigh_L", "Root/Pelvis", 0.40f, 0.18f, 0.19f, 0.38f, -50f, 50f),
        new BoneDefinition("Root/Pelvis/Thigh_L/Shin_L", "Root/Pelvis/Thigh_L", 0.38f, 0.14f, 0.18f, 0.26f, -75f, 75f),
        new BoneDefinition("Root/Pelvis/Thigh_L/Shin_L/Foot_L", "Root/Pelvis/Thigh_L/Shin_L", 0.24f, 0.13f, 0.08f, 0.18f, -35f, 35f),
        new BoneDefinition("Root/Pelvis/Thigh_R", "Root/Pelvis", 0.40f, 0.18f, 0.19f, 0.38f, -50f, 50f),
        new BoneDefinition("Root/Pelvis/Thigh_R/Shin_R", "Root/Pelvis/Thigh_R", 0.38f, 0.14f, 0.18f, 0.26f, -75f, 75f),
        new BoneDefinition("Root/Pelvis/Thigh_R/Shin_R/Foot_R", "Root/Pelvis/Thigh_R/Shin_R", 0.24f, 0.13f, 0.08f, 0.18f, -35f, 35f)
    };

    private readonly List<RagdollBone> ragdollBones = new List<RagdollBone>();
    private Animator rigAnimator;
    private Transform skeletonRoot;
    private Rigidbody2D chestBody;
    private bool isInitialized;
    private bool isActive;
    private float settleTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureLayerCollisions()
    {
        int playerLayer = LayerMask.NameToLayer(PlayerLayerName);
        int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
        int enemyAttackLayer = LayerMask.NameToLayer(EnemyAttackLayerName);
        if (playerLayer < 0 || enemyLayer < 0 || enemyAttackLayer < 0)
        {
            return;
        }

        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);
        Physics2D.IgnoreLayerCollision(enemyLayer, enemyLayer, true);
        Physics2D.IgnoreLayerCollision(playerLayer, enemyAttackLayer, false);
        Physics2D.IgnoreLayerCollision(enemyLayer, enemyAttackLayer, true);
        Physics2D.IgnoreLayerCollision(enemyAttackLayer, enemyAttackLayer, true);
    }

    private void Awake()
    {
        AssignEnemyLayers();
        InitializeRagdoll();
    }

    private void FixedUpdate()
    {
        if (!isActive || !isInitialized)
        {
            return;
        }

        if (!AreAllBodiesSettled())
        {
            settleTimer = 0f;
            return;
        }

        settleTimer += Time.fixedDeltaTime;
        if (settleTimer >= settleDuration)
        {
            FreezeRagdollPose();
        }
    }

    private void AssignEnemyLayers()
    {
        int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
        int enemyAttackLayer = LayerMask.NameToLayer(EnemyAttackLayerName);
        if (enemyLayer < 0)
        {
            return;
        }

        Transform[] hierarchy = GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < hierarchy.Length; index++)
        {
            bool isAttackObject = hierarchy[index].GetComponent<EnemyMeleeAttack>() != null;
            hierarchy[index].gameObject.layer = isAttackObject && enemyAttackLayer >= 0
                ? enemyAttackLayer
                : enemyLayer;
        }
    }

    private bool AreAllBodiesSettled()
    {
        float linearThresholdSquared = SettleLinearSpeedThreshold * SettleLinearSpeedThreshold;
        bool foundSimulatedBody = false;
        for (int index = 0; index < ragdollBones.Count; index++)
        {
            Rigidbody2D boneBody = ragdollBones[index].Body;
            if (!boneBody.simulated)
            {
                continue;
            }

            foundSimulatedBody = true;
            if (boneBody.linearVelocity.sqrMagnitude > linearThresholdSquared
                || Mathf.Abs(boneBody.angularVelocity) > SettleAngularSpeedThreshold)
            {
                return false;
            }
        }

        return foundSimulatedBody;
    }

    private void FreezeRagdollPose()
    {
        for (int index = 0; index < ragdollBones.Count; index++)
        {
            Rigidbody2D boneBody = ragdollBones[index].Body;
            if (!boneBody.simulated)
            {
                continue;
            }

            boneBody.linearVelocity = Vector2.zero;
            boneBody.angularVelocity = 0f;
            boneBody.simulated = false;
        }

        isActive = false;
    }

    /// <summary>Enables the rig's physics bodies and applies a capped death impulse scaled by the lethal weapon's shot power.</summary>
    public bool Activate(Vector2 hitPoint, Vector2 hitDirection, float weaponShotPower)
    {
        if (isActive || !isInitialized)
        {
            return false;
        }

        isActive = true;
        DisableRigDrivers();

        SpriteRenderer headRenderer = skeletonRoot.Find(HeadVisualPath)?.GetComponent<SpriteRenderer>();
        bool headWasDetached = headRenderer != null && !headRenderer.enabled;
        bool leftLegWasDetached = IsSpriteDisabled("LegLTop") || IsSpriteDisabled("LegLBot");
        bool rightLegWasDetached = IsSpriteDisabled("LegRTop") || IsSpriteDisabled("LegRBot");
        bool leftArmWasDetached = IsSpriteDisabled("HandLTop") || IsSpriteDisabled("HandLBot");
        bool rightArmWasDetached = IsSpriteDisabled("ArmRTop") || IsSpriteDisabled("ArmRBot");

        for (int index = 0; index < ragdollBones.Count; index++)
        {
            RagdollBone ragdollBone = ragdollBones[index];
            bool isDetachedHeadBone = headWasDetached && ragdollBone.Definition.Path == HeadBonePath;
            bool isDetachedLimbBone = IsDetachedLimbBone(
                ragdollBone.Definition.Path,
                leftLegWasDetached,
                rightLegWasDetached,
                leftArmWasDetached,
                rightArmWasDetached);
            if (isDetachedHeadBone || isDetachedLimbBone)
            {
                ragdollBone.Collider.enabled = false;
                ragdollBone.Joint.enabled = false;
                ragdollBone.Body.simulated = false;
                continue;
            }

            ragdollBone.Body.linearVelocity = Vector2.zero;
            ragdollBone.Body.angularVelocity = 0f;
            ragdollBone.Body.simulated = true;
            ragdollBone.Collider.enabled = true;
        }

        for (int index = 0; index < ragdollBones.Count; index++)
        {
            RagdollBone ragdollBone = ragdollBones[index];
            if (!ragdollBone.Body.simulated || ragdollBone.Definition.ParentPath == null)
            {
                continue;
            }

            Rigidbody2D connectedBody = ragdollBone.Joint.connectedBody;
            ragdollBone.Joint.connectedAnchor = connectedBody.transform.InverseTransformPoint(ragdollBone.Transform.position);
            float deathPoseJointAngle = ragdollBone.Joint.jointAngle;
            ragdollBone.Joint.limits = new JointAngleLimits2D
            {
                min = Mathf.Clamp(deathPoseJointAngle + ragdollBone.Definition.MinimumJointAngle, -180f, 180f),
                max = Mathf.Clamp(deathPoseJointAngle + ragdollBone.Definition.MaximumJointAngle, -180f, 180f)
            };
            ragdollBone.Joint.enabled = true;
        }

        Physics2D.SyncTransforms();
        ApplyDeathImpulse(hitPoint, hitDirection, weaponShotPower);
        return true;
    }

    private static bool IsDetachedLimbBone(
        string bonePath,
        bool leftLegWasDetached,
        bool rightLegWasDetached,
        bool leftArmWasDetached,
        bool rightArmWasDetached)
    {
        return leftLegWasDetached && bonePath.StartsWith(LeftLegBoneRootPath)
            || rightLegWasDetached && bonePath.StartsWith(RightLegBoneRootPath)
            || leftArmWasDetached && bonePath.StartsWith(LeftArmBoneRootPath)
            || rightArmWasDetached && bonePath.StartsWith(RightArmBoneRootPath);
    }

    private bool IsSpriteDisabled(string spriteName)
    {
        SpriteRenderer spriteRenderer = skeletonRoot.Find(spriteName)?.GetComponent<SpriteRenderer>();
        return spriteRenderer != null && !spriteRenderer.enabled;
    }

    private void InitializeRagdoll()
    {
        rigAnimator = GetComponentInChildren<Animator>(true);
        if (rigAnimator == null)
        {
            return;
        }

        skeletonRoot = rigAnimator.transform;
        Dictionary<string, Rigidbody2D> bodiesByPath = new Dictionary<string, Rigidbody2D>();

        for (int index = 0; index < BoneDefinitions.Length; index++)
        {
            BoneDefinition definition = BoneDefinitions[index];
            Transform boneTransform = skeletonRoot.Find(definition.Path);
            if (boneTransform == null)
            {
                Debug.LogError($"{nameof(EnemyRagdoll2D)} on '{name}' could not find rig bone '{definition.Path}'.", this);
                return;
            }

            Rigidbody2D boneBody = boneTransform.GetComponent<Rigidbody2D>();
            if (boneBody == null)
            {
                boneBody = boneTransform.gameObject.AddComponent<Rigidbody2D>();
            }

            boneBody.bodyType = RigidbodyType2D.Dynamic;
            boneBody.mass = definition.Mass;
            boneBody.gravityScale = 1f;
            boneBody.linearDamping = 0.2f;
            boneBody.angularDamping = 2.2f;
            boneBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            boneBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            boneBody.constraints = RigidbodyConstraints2D.None;
            boneBody.simulated = false;

            CapsuleCollider2D boneCollider = boneTransform.GetComponent<CapsuleCollider2D>();
            if (boneCollider == null)
            {
                boneCollider = boneTransform.gameObject.AddComponent<CapsuleCollider2D>();
            }

            boneCollider.direction = CapsuleDirection2D.Horizontal;
            boneCollider.size = new Vector2(definition.ColliderLength, definition.ColliderWidth);
            boneCollider.offset = new Vector2(definition.ColliderOffset, 0f);
            boneCollider.isTrigger = false;
            boneCollider.enabled = false;

            HingeJoint2D boneJoint = boneTransform.GetComponent<HingeJoint2D>();
            if (boneJoint == null)
            {
                boneJoint = boneTransform.gameObject.AddComponent<HingeJoint2D>();
            }

            boneJoint.autoConfigureConnectedAnchor = false;
            boneJoint.anchor = Vector2.zero;
            boneJoint.enableCollision = false;
            boneJoint.useLimits = true;
            boneJoint.limits = new JointAngleLimits2D
            {
                min = definition.MinimumJointAngle,
                max = definition.MaximumJointAngle
            };
            boneJoint.enabled = false;

            bodiesByPath.Add(definition.Path, boneBody);
            ragdollBones.Add(new RagdollBone(definition, boneTransform, boneBody, boneCollider, boneJoint));
            if (definition.Path == "Root/Pelvis/Chest")
            {
                chestBody = boneBody;
            }
        }

        for (int index = 0; index < ragdollBones.Count; index++)
        {
            RagdollBone ragdollBone = ragdollBones[index];
            if (ragdollBone.Definition.ParentPath == null)
            {
                continue;
            }

            Rigidbody2D connectedBody = bodiesByPath[ragdollBone.Definition.ParentPath];
            ragdollBone.Joint.connectedBody = connectedBody;
            ragdollBone.Joint.connectedAnchor = connectedBody.transform.InverseTransformPoint(ragdollBone.Transform.position);
        }

        isInitialized = chestBody != null;
    }

    private void DisableRigDrivers()
    {
        Behaviour[] rigBehaviours = skeletonRoot.GetComponentsInChildren<Behaviour>(true);
        for (int index = 0; index < rigBehaviours.Length; index++)
        {
            Behaviour rigBehaviour = rigBehaviours[index];
            if (rigBehaviour is Animator
                || rigBehaviour is IKManager2D
                || rigBehaviour is LimbSolver2D)
            {
                rigBehaviour.enabled = false;
            }
        }
    }

    private void ApplyDeathImpulse(Vector2 hitPoint, Vector2 hitDirection, float weaponShotPower)
    {
        if (chestBody == null
            || deathImpulse <= 0f
            || weaponShotPower <= 0f
            || hitDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        float scaledImpulse = Mathf.Min(deathImpulse * weaponShotPower, maximumDeathImpulse);
        Vector2 impulseDirection = (hitDirection.normalized + Vector2.up * upwardImpulseBias).normalized;
        chestBody.AddForceAtPosition(impulseDirection * scaledImpulse, hitPoint, ForceMode2D.Impulse);
    }

    private sealed class BoneDefinition
    {
        public readonly string Path;
        public readonly string ParentPath;
        public readonly float ColliderLength;
        public readonly float ColliderWidth;
        public readonly float ColliderOffset;
        public readonly float Mass;
        public readonly float MinimumJointAngle;
        public readonly float MaximumJointAngle;

        public BoneDefinition(string path, string parentPath, float colliderLength, float colliderWidth, float colliderOffset, float mass, float minimumJointAngle, float maximumJointAngle)
        {
            Path = path;
            ParentPath = parentPath;
            ColliderLength = colliderLength;
            ColliderWidth = colliderWidth;
            ColliderOffset = colliderOffset;
            Mass = mass;
            MinimumJointAngle = minimumJointAngle;
            MaximumJointAngle = maximumJointAngle;
        }
    }

    private sealed class RagdollBone
    {
        public readonly BoneDefinition Definition;
        public readonly Transform Transform;
        public readonly Rigidbody2D Body;
        public readonly CapsuleCollider2D Collider;
        public readonly HingeJoint2D Joint;

        public RagdollBone(BoneDefinition definition, Transform transform, Rigidbody2D body, CapsuleCollider2D collider, HingeJoint2D joint)
        {
            Definition = definition;
            Transform = transform;
            Body = body;
            Collider = collider;
            Joint = joint;
        }
    }
}
