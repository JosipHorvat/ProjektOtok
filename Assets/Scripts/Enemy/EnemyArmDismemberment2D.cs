using UnityEngine;

/// <summary>Detaches a whole animated arm when that arm receives a critical hit.</summary>
[DisallowMultipleComponent]
public sealed class EnemyArmDismemberment2D : MonoBehaviour, IHitRegionDamageReceiver2D
{
    private const string LeftUpperArmPath = "Root/Pelvis/Chest/UpperArm_L";
    private const string LeftForearmPath = "Root/Pelvis/Chest/UpperArm_L/Forearm_L";
    private const string LeftHandPath = "Root/Pelvis/Chest/UpperArm_L/Forearm_L/Hand_L";
    private const string RightUpperArmPath = "Root/Pelvis/Chest/UpperArm_R";
    private const string RightForearmPath = "Root/Pelvis/Chest/UpperArm_R/Forearm_R";
    private const string RightHandPath = "Root/Pelvis/Chest/UpperArm_R/Forearm_R/Hand_R";
    private const string LeftUpperSpriteName = "HandLTop";
    private const string LeftLowerSpriteName = "HandLBot";
    private const string RightUpperSpriteName = "ArmRTop";
    private const string RightLowerSpriteName = "ArmRBot";
    private const float DetachedMass = 0.55f;
    private const float DefaultDetachImpulse = 2f;
    private const float DefaultUpwardBias = 0.3f;
    private const float DefaultMinimumSpinImpulse = 0.6f;
    private const float DefaultMaximumSpinImpulse = 1.5f;
    private const float DefaultDetachedLifetime = 12f;

    [Header("Detached limb physics")]
    [Tooltip("Impulse applied away from the hit when an arm separates.")]
    [SerializeField, Min(0f)] private float detachImpulse = DefaultDetachImpulse;
    [Tooltip("Upward bias added to the separation direction.")]
    [SerializeField, Range(0f, 1f)] private float upwardBias = DefaultUpwardBias;
    [Tooltip("Minimum random spin impulse applied to a detached arm.")]
    [SerializeField, Min(0f)] private float minimumSpinImpulse = DefaultMinimumSpinImpulse;
    [Tooltip("Maximum random spin impulse applied to a detached arm.")]
    [SerializeField, Min(0f)] private float maximumSpinImpulse = DefaultMaximumSpinImpulse;
    [Tooltip("Seconds before a detached arm is removed. Set to zero to keep it for the scene lifetime.")]
    [SerializeField, Min(0f)] private float detachedLifetime = DefaultDetachedLifetime;


    private Transform skeletonRoot;
    private EnemyLimbCondition2D limbCondition;
    private bool leftArmDetached;
    private bool rightArmDetached;

    private void Awake()
    {
        skeletonRoot = EnemyDetachedLimbUtility2D.FindAnimatorRoot(this);
        limbCondition = GetComponentInParent<EnemyLimbCondition2D>();
    }

    /// <summary>Detaches the struck arm on a critical hit; ordinary hits do not dismember.</summary>
    /// <param name="hitData">Resolved hit region and impact information.</param>
    public void ReceiveHitRegionDamage(CombatHitData hitData)
    {
        if (!hitData.IsCritical || skeletonRoot == null)
        {
            return;
        }

        switch (hitData.HitRegion)
        {
            case CharacterHitRegion.LeftArm:
                if (!leftArmDetached)
                {
                    DetachArm(true, hitData.HitPoint, hitData.HitDirection);
                }
                break;
            case CharacterHitRegion.RightArm:
            case CharacterHitRegion.WeaponArm:
                if (!rightArmDetached)
                {
                    DetachArm(false, hitData.HitPoint, hitData.HitDirection);
                }
                break;
        }
    }

    private void DetachArm(bool isLeftArm, Vector2 hitPoint, Vector2 hitDirection)
    {
        string upperSpriteName = isLeftArm ? LeftUpperSpriteName : RightUpperSpriteName;
        string lowerSpriteName = isLeftArm ? LeftLowerSpriteName : RightLowerSpriteName;
        string upperBonePath = isLeftArm ? LeftUpperArmPath : RightUpperArmPath;
        string lowerBonePath = isLeftArm ? LeftForearmPath : RightForearmPath;
        string handBonePath = isLeftArm ? LeftHandPath : RightHandPath;

        SpriteRenderer upperRenderer = EnemyDetachedLimbUtility2D.FindSpriteRenderer(skeletonRoot, upperSpriteName);
        SpriteRenderer lowerRenderer = EnemyDetachedLimbUtility2D.FindSpriteRenderer(skeletonRoot, lowerSpriteName);
        if (!EnemyDetachedLimbUtility2D.CanDetach(upperRenderer, lowerRenderer))
        {
            Debug.LogError($"{nameof(EnemyArmDismemberment2D)} on '{name}' could not find both rendered parts of the {(isLeftArm ? "left" : "right")} arm.", this);
            return;
        }

        Transform upperBone = skeletonRoot.Find(upperBonePath);
        Transform lowerBone = skeletonRoot.Find(lowerBonePath);
        Transform handBone = skeletonRoot.Find(handBonePath);
        if (upperBone == null || lowerBone == null || handBone == null)
        {
            Debug.LogError($"{nameof(EnemyArmDismemberment2D)} on '{name}' could not find the {(isLeftArm ? "left" : "right")} arm bones.", this);
            return;
        }

        Bounds limbBounds = EnemyDetachedLimbUtility2D.GetLimbBounds(upperRenderer, lowerRenderer);
        GameObject detachedArm = EnemyDetachedLimbUtility2D.CreateDetachedLimb(
            this,
            isLeftArm ? "DetachedLeftArm" : "DetachedRightArm",
            skeletonRoot,
            limbBounds,
            new[] { upperRenderer, lowerRenderer },
            new[] { upperBone, lowerBone, handBone },
            hitPoint,
            hitDirection,
            DetachedMass,
            detachImpulse,
            upwardBias,
            minimumSpinImpulse,
            maximumSpinImpulse,
            detachedLifetime);
        if (detachedArm == null)
        {
            return;
        }

        EnemyLimbCondition2D.Limb limb = isLeftArm ? EnemyLimbCondition2D.Limb.LeftArm : EnemyLimbCondition2D.Limb.RightArm;
        // EnemyLimbVisuals2D enables the authored replacement-part GameObject when this loss is recorded.

        EnemyDetachedLimbUtility2D.HideSourcePart(upperRenderer);
        EnemyDetachedLimbUtility2D.HideSourcePart(lowerRenderer);
        EnemyDetachedLimbUtility2D.DisableLimbHitboxes(
            this,
            isLeftArm ? CharacterHitRegion.LeftArm : CharacterHitRegion.RightArm);

        if (isLeftArm)
        {
            leftArmDetached = true;
        }
        else
        {
            rightArmDetached = true;
        }

        if (limbCondition != null)
        {
            limbCondition.RecordLimbLoss(limb);
        }
    }
}
