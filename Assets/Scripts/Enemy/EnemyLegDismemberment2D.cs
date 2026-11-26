using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;

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
    private const float MinimumHitDirectionSqrMagnitude = 0.0001f;
    private const float DefaultDetachImpulse = 2.4f;
    private const float DefaultUpwardBias = 0.4f;
    private const float DefaultMinimumSpinImpulse = 0.8f;
    private const float DefaultMaximumSpinImpulse = 1.8f;
    private const float DefaultDetachedLifetime = 12f;
    private const float MinimumColliderWidth = 0.16f;
    private const float MinimumColliderHeight = 0.35f;

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
    private readonly Dictionary<Transform, Transform> detachedBones = new Dictionary<Transform, Transform>();
    private bool leftLegDetached;
    private bool rightLegDetached;

    private void Awake()
    {
        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            Debug.LogError($"{nameof(EnemyLegDismemberment2D)} on '{name}' could not find the enemy Animator.", this);
            return;
        }

        skeletonRoot = animator.transform;
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

        SpriteRenderer upperRenderer = FindSpriteRenderer(upperSpriteName);
        SpriteRenderer lowerRenderer = FindSpriteRenderer(lowerSpriteName);
        if (!CanDetach(upperRenderer, lowerRenderer))
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

        Bounds limbBounds = upperRenderer.bounds;
        limbBounds.Encapsulate(lowerRenderer.bounds);
        GameObject detachedLeg = CreateDetachedLeg(isLeftLeg, hitPoint, hitDirection, limbBounds,
            new[] { upperRenderer, lowerRenderer }, new[] { upperBone, lowerBone, footBone });
        if (detachedLeg == null)
        {
            return;
        }

        HideSourcePart(upperRenderer);
        HideSourcePart(lowerRenderer);
        if (isLeftLeg)
        {
            leftLegDetached = true;
        }
        else
        {
            rightLegDetached = true;
        }
    }

    private GameObject CreateDetachedLeg(
        bool isLeftLeg,
        Vector2 hitPoint,
        Vector2 hitDirection,
        Bounds limbBounds,
        SpriteRenderer[] sourceRenderers,
        Transform[] requiredBones)
    {
        detachedBones.Clear();
        GameObject detachedRoot = new GameObject(isLeftLeg ? "DetachedLeftLeg" : "DetachedRightLeg");
        Transform detachedTransform = detachedRoot.transform;
        detachedTransform.SetPositionAndRotation(skeletonRoot.position, skeletonRoot.rotation);
        detachedTransform.localScale = skeletonRoot.lossyScale;
        detachedRoot.layer = gameObject.layer;
        detachedRoot.SetActive(false);

        for (int index = 0; index < requiredBones.Length; index++)
        {
            if (!CreateBoneChain(requiredBones[index], detachedTransform))
            {
                Destroy(detachedRoot);
                return null;
            }
        }

        for (int index = 0; index < sourceRenderers.Length; index++)
        {
            if (!CreateDetachedSprite(sourceRenderers[index], detachedTransform))
            {
                Destroy(detachedRoot);
                return null;
            }
        }

        Rigidbody2D body = detachedRoot.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.mass = 0.65f;
        body.gravityScale = 1f;
        body.linearDamping = 0.2f;
        body.angularDamping = 1.2f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        CapsuleCollider2D collider = detachedRoot.AddComponent<CapsuleCollider2D>();
        collider.direction = CapsuleDirection2D.Vertical;
        Vector3 worldScale = detachedTransform.lossyScale;
        float scaleX = Mathf.Max(0.001f, Mathf.Abs(worldScale.x));
        float scaleY = Mathf.Max(0.001f, Mathf.Abs(worldScale.y));
        collider.size = new Vector2(
            Mathf.Max(MinimumColliderWidth, limbBounds.size.x / scaleX),
            Mathf.Max(MinimumColliderHeight, limbBounds.size.y / scaleY));
        collider.offset = detachedTransform.InverseTransformPoint(limbBounds.center);

        detachedRoot.SetActive(true);
        Vector2 awayFromHit = hitDirection.sqrMagnitude > MinimumHitDirectionSqrMagnitude
            ? -hitDirection.normalized
            : ((Vector2)limbBounds.center - hitPoint).normalized;
        if (awayFromHit.sqrMagnitude <= MinimumHitDirectionSqrMagnitude)
        {
            awayFromHit = Vector2.right;
        }

        Vector2 launchDirection = (awayFromHit + Vector2.up * upwardBias).normalized;
        body.AddForceAtPosition(launchDirection * detachImpulse, hitPoint, ForceMode2D.Impulse);
        float spinSign = awayFromHit.x >= 0f ? 1f : -1f;
        float spinImpulse = Random.Range(minimumSpinImpulse, Mathf.Max(minimumSpinImpulse, maximumSpinImpulse));
        body.AddTorque(spinSign * spinImpulse, ForceMode2D.Impulse);

        if (detachedLifetime > 0f)
        {
            Destroy(detachedRoot, detachedLifetime);
        }

        return detachedRoot;
    }

    private bool CreateBoneChain(Transform sourceBone, Transform detachedRoot)
    {
        if (sourceBone == null || detachedBones.ContainsKey(sourceBone))
        {
            return sourceBone != null;
        }

        Transform sourceParent = sourceBone.parent;
        Transform detachedParent = sourceParent == skeletonRoot
            ? detachedRoot
            : detachedBones.TryGetValue(sourceParent, out Transform mappedParent) ? mappedParent : null;
        if (detachedParent == null)
        {
            if (!CreateBoneChain(sourceParent, detachedRoot))
            {
                return false;
            }

            detachedParent = detachedBones[sourceParent];
        }

        GameObject boneObject = new GameObject(sourceBone.name);
        Transform detachedBone = boneObject.transform;
        detachedBone.SetParent(detachedParent, false);
        detachedBone.localPosition = sourceBone.localPosition;
        detachedBone.localRotation = sourceBone.localRotation;
        detachedBone.localScale = sourceBone.localScale;
        detachedBones.Add(sourceBone, detachedBone);
        return true;
    }

    private bool CreateDetachedSprite(SpriteRenderer sourceRenderer, Transform detachedRoot)
    {
        SpriteSkin sourceSkin = sourceRenderer.GetComponent<SpriteSkin>();
        if (sourceSkin == null || sourceSkin.boneTransforms == null || sourceSkin.boneTransforms.Length == 0)
        {
            Debug.LogError($"{nameof(EnemyLegDismemberment2D)} on '{name}' found an invalid SpriteSkin on '{sourceRenderer.name}'.", this);
            return false;
        }

        Transform[] sourceBones = sourceSkin.boneTransforms;
        Transform[] mappedBones = new Transform[sourceBones.Length];
        for (int index = 0; index < sourceBones.Length; index++)
        {
            if (!detachedBones.TryGetValue(sourceBones[index], out mappedBones[index]))
            {
                Debug.LogError($"{nameof(EnemyLegDismemberment2D)} on '{name}' could not map a SpriteSkin bone for '{sourceRenderer.name}'.", this);
                return false;
            }
        }

        GameObject spriteObject = new GameObject(sourceRenderer.name);
        Transform spriteTransform = spriteObject.transform;
        spriteTransform.SetParent(detachedRoot, false);
        spriteTransform.localPosition = sourceRenderer.transform.localPosition;
        spriteTransform.localRotation = sourceRenderer.transform.localRotation;
        spriteTransform.localScale = sourceRenderer.transform.localScale;
        spriteObject.layer = detachedRoot.gameObject.layer;

        SpriteRenderer detachedRenderer = spriteObject.AddComponent<SpriteRenderer>();
        CopyRendererSettings(sourceRenderer, detachedRenderer);
        SpriteSkin detachedSkin = spriteObject.AddComponent<SpriteSkin>();
        detachedSkin.autoRebind = false;
        detachedSkin.SetRootBone(detachedRoot);
        detachedSkin.SetBoneTransforms(mappedBones);
        detachedSkin.alwaysUpdate = true;
        return true;
    }

    private static void CopyRendererSettings(SpriteRenderer source, SpriteRenderer destination)
    {
        destination.sprite = source.sprite;
        destination.color = source.color;
        destination.flipX = source.flipX;
        destination.flipY = source.flipY;
        destination.drawMode = source.drawMode;
        destination.maskInteraction = source.maskInteraction;
        destination.spriteSortPoint = source.spriteSortPoint;
        destination.sharedMaterial = source.sharedMaterial;
        destination.sortingLayerID = source.sortingLayerID;
        destination.sortingOrder = source.sortingOrder;
        destination.size = source.size;
        destination.tileMode = source.tileMode;
        destination.adaptiveModeThreshold = source.adaptiveModeThreshold;
        destination.renderingLayerMask = source.renderingLayerMask;
        destination.enabled = source.enabled;
    }

    private SpriteRenderer FindSpriteRenderer(string objectName)
    {
        Transform spriteTransform = skeletonRoot.Find(objectName);
        return spriteTransform != null ? spriteTransform.GetComponent<SpriteRenderer>() : null;
    }

    private static bool CanDetach(SpriteRenderer upperRenderer, SpriteRenderer lowerRenderer)
    {
        return upperRenderer != null
            && lowerRenderer != null
            && upperRenderer.enabled
            && lowerRenderer.enabled
            && upperRenderer.sprite != null
            && lowerRenderer.sprite != null;
    }

    private static void HideSourcePart(SpriteRenderer sourceRenderer)
    {
        sourceRenderer.enabled = false;
        SpriteSkin sourceSkin = sourceRenderer.GetComponent<SpriteSkin>();
        if (sourceSkin != null)
        {
            sourceSkin.enabled = false;
        }

        Collider2D[] colliders = sourceRenderer.GetComponents<Collider2D>();
        for (int index = 0; index < colliders.Length; index++)
        {
            colliders[index].enabled = false;
        }
    }
}
