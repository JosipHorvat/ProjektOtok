using UnityEngine;

/// <summary>Detaches a whole animated leg when that leg receives a critical hit.</summary>
[DisallowMultipleComponent]
public sealed class EnemyLegDismemberment2D : MonoBehaviour, IHitRegionDamageReceiver2D
{
    private const string LeftUpperLegPath = "Root/Pelvis/Thigh_L";
    private const string LeftLowerLegPath = "Root/Pelvis/Thigh_L/Shin_L";
    private const string LeftFootPath = "Root/Pelvis/Thigh_L/Shin_L/Foot_L";
    private const string RightUpperLegPath = "Root/Pelvis/Thigh_R";
    private const string RightLowerLegPath = "Root/Pelvis/Thigh_R/Shin_R";
    private const string RightFootPath = "Root/Pelvis/Thigh_R/Shin_R/Foot_R";
    private const string LeftUpperSpriteName = "LegLTop";
    private const string LeftLowerSpriteName = "LegLBot";
    private const string RightUpperSpriteName = "LegRTop";
    private const string RightLowerSpriteName = "LegRBot";
    private const float DetachedMass = 0.65f;
    private const float DefaultDetachImpulse = 2.4f;
    private const float DefaultUpwardBias = 0.4f;
    private const float DefaultMinimumSpinImpulse = 0.8f;
    private const float DefaultMaximumSpinImpulse = 1.8f;
    private const float DefaultDetachedLifetime = 12f;

    [Header("Detached limb physics")]
    [Tooltip("Impulse applied away from the hit when a leg separates.")]
    [SerializeField, Min(0f)] private float detachImpulse = DefaultDetachImpulse;
    [Tooltip("Upward bias added to the separation direction.")]
    [SerializeField, Range(0f, 1f)] private float upwardBias = DefaultUpwardBias;
    [Tooltip("Minimum random spin impulse applied to a detached leg.")]
    [SerializeField, Min(0f)] private float minimumSpinImpulse = DefaultMinimumSpinImpulse;
    [Tooltip("Maximum random spin impulse applied to a detached leg.")]
    [SerializeField, Min(0f)] private float maximumSpinImpulse = DefaultMaximumSpinImpulse;
    [Tooltip("Seconds before a detached leg is removed. Set to zero to keep it for the scene lifetime.")]
    [SerializeField, Min(0f)] private float detachedLifetime = DefaultDetachedLifetime;


    private Transform skeletonRoot;
    private EnemyLimbCondition2D limbCondition;
    private bool leftLegDetached;
    private bool rightLegDetached;

    private void Awake()
    {
        skeletonRoot = EnemyDetachedLimbUtility2D.FindAnimatorRoot(this);
        limbCondition = GetComponentInParent<EnemyLimbCondition2D>();
    }

    /// <summary>Detaches the struck leg on a critical hit; ordinary hits do not dismember.</summary>
    /// <param name="hitData">Resolved hit region and impact information.</param>
    public void ReceiveHitRegionDamage(CombatHitData hitData)
    {
        if (!hitData.IsCritical || skeletonRoot == null)
        {
            return;
        }

        switch (hitData.HitRegion)
        {
            case CharacterHitRegion.LeftLeg:
                if (!leftLegDetached)
                {
                    DetachLeg(true, hitData.HitPoint, hitData.HitDirection);
                }
                break;
            case CharacterHitRegion.RightLeg:
                if (!rightLegDetached)
                {
                    DetachLeg(false, hitData.HitPoint, hitData.HitDirection);
                }
                break;
        }
    }

    private void DetachLeg(bool isLeftLeg, Vector2 hitPoint, Vector2 hitDirection)
    {
        string upperSpriteName = isLeftLeg ? LeftUpperSpriteName : RightUpperSpriteName;
        string lowerSpriteName = isLeftLeg ? LeftLowerSpriteName : RightLowerSpriteName;
        string upperBonePath = isLeftLeg ? LeftUpperLegPath : RightUpperLegPath;
        string lowerBonePath = isLeftLeg ? LeftLowerLegPath : RightLowerLegPath;
        string footBonePath = isLeftLeg ? LeftFootPath : RightFootPath;

        SpriteRenderer upperRenderer = EnemyDetachedLimbUtility2D.FindSpriteRenderer(skeletonRoot, upperSpriteName);
        SpriteRenderer lowerRenderer = EnemyDetachedLimbUtility2D.FindSpriteRenderer(skeletonRoot, lowerSpriteName);
        if (!EnemyDetachedLimbUtility2D.CanDetach(upperRenderer, lowerRenderer))
        {
            Debug.LogError($"{nameof(EnemyLegDismemberment2D)} on '{name}' could not find both rendered parts of the {(isLeftLeg ? "left" : "right")} leg.", this);
            return;
        }

        Transform upperBone = skeletonRoot.Find(upperBonePath);
        Transform lowerBone = skeletonRoot.Find(lowerBonePath);
        Transform footBone = skeletonRoot.Find(footBonePath);
        if (upperBone == null || lowerBone == null || footBone == null)
        {
            Debug.LogError($"{nameof(EnemyLegDismemberment2D)} on '{name}' could not find the {(isLeftLeg ? "left" : "right")} leg bones.", this);
            return;
        }

        Bounds limbBounds = EnemyDetachedLimbUtility2D.GetLimbBounds(upperRenderer, lowerRenderer);
        GameObject detachedLeg = EnemyDetachedLimbUtility2D.CreateDetachedLimb(
            this,
            isLeftLeg ? "DetachedLeftLeg" : "DetachedRightLeg",
            skeletonRoot,
            limbBounds,
            new[] { upperRenderer, lowerRenderer },
            new[] { upperBone, lowerBone, footBone },
            hitPoint,
            hitDirection,
            DetachedMass,
            detachImpulse,
            upwardBias,
            minimumSpinImpulse,
            maximumSpinImpulse,
            detachedLifetime);
        if (detachedLeg == null)
        {
            return;
        }

        EnemyLimbCondition2D.Limb limb = isLeftLeg ? EnemyLimbCondition2D.Limb.LeftLeg : EnemyLimbCondition2D.Limb.RightLeg;
        // EnemyLimbVisuals2D enables the authored replacement-part GameObject when this loss is recorded.

        EnemyDetachedLimbUtility2D.HideSourcePart(upperRenderer);
        EnemyDetachedLimbUtility2D.HideSourcePart(lowerRenderer);
        EnemyDetachedLimbUtility2D.DisableLimbHitboxes(
            this,
            isLeftLeg ? CharacterHitRegion.LeftLeg : CharacterHitRegion.RightLeg);

        if (isLeftLeg)
        {
            leftLegDetached = true;
        }
        else
        {
            rightLegDetached = true;
        }

        if (limbCondition != null)
        {
            limbCondition.RecordLimbLoss(limb);
        }
    }
}
