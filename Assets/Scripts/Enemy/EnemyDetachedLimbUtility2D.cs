using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;

/// <summary>Shares the common construction and cleanup used by animated limb dismemberment.</summary>
internal static class EnemyDetachedLimbUtility2D
{
    private const float MinimumHitDirectionSqrMagnitude = 0.0001f;
    private const float MinimumColliderWidth = 0.16f;
    private const float MinimumColliderHeight = 0.35f;
    private const float MinimumWorldScale = 0.001f;
    private const float DefaultGravityScale = 1f;
    private const float DefaultLinearDamping = 0.2f;
    private const float DefaultAngularDamping = 1.2f;

    internal static Transform FindAnimatorRoot(MonoBehaviour owner)
    {
        Animator animator = owner.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            Debug.LogError($"{owner.GetType().Name} on '{owner.name}' could not find the enemy Animator.", owner);
            return null;
        }

        return animator.transform;
    }

    internal static SpriteRenderer FindSpriteRenderer(Transform skeletonRoot, string objectName)
    {
        Transform spriteTransform = skeletonRoot.Find(objectName);
        return spriteTransform != null ? spriteTransform.GetComponent<SpriteRenderer>() : null;
    }

    internal static bool CanDetach(params SpriteRenderer[] sourceRenderers)
    {
        if (sourceRenderers == null || sourceRenderers.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < sourceRenderers.Length; index++)
        {
            SpriteRenderer sourceRenderer = sourceRenderers[index];
            if (sourceRenderer == null || !sourceRenderer.enabled || sourceRenderer.sprite == null)
            {
                return false;
            }
        }

        return true;
    }

    internal static Bounds GetLimbBounds(params SpriteRenderer[] sourceRenderers)
    {
        Bounds limbBounds = sourceRenderers[0].bounds;
        for (int index = 1; index < sourceRenderers.Length; index++)
        {
            limbBounds.Encapsulate(sourceRenderers[index].bounds);
        }

        return limbBounds;
    }

    internal static GameObject CreateDetachedLimb(
        MonoBehaviour owner,
        string detachedName,
        Transform skeletonRoot,
        Bounds limbBounds,
        SpriteRenderer[] sourceRenderers,
        Transform[] requiredBones,
        Vector2 hitPoint,
        Vector2 hitDirection,
        float mass,
        float detachImpulse,
        float upwardBias,
        float minimumSpinImpulse,
        float maximumSpinImpulse,
        float detachedLifetime)
    {
        if (owner == null || skeletonRoot == null || sourceRenderers == null || requiredBones == null)
        {
            return null;
        }

        Dictionary<Transform, Transform> detachedBones = new Dictionary<Transform, Transform>();
        GameObject detachedRoot = new GameObject(detachedName);
        Transform detachedTransform = detachedRoot.transform;
        detachedTransform.SetPositionAndRotation(skeletonRoot.position, skeletonRoot.rotation);
        detachedTransform.localScale = skeletonRoot.lossyScale;
        detachedRoot.layer = owner.gameObject.layer;
        detachedRoot.SetActive(false);

        for (int index = 0; index < requiredBones.Length; index++)
        {
            if (!CreateBoneChain(requiredBones[index], skeletonRoot, detachedTransform, detachedBones))
            {
                Object.Destroy(detachedRoot);
                return null;
            }
        }

        for (int index = 0; index < sourceRenderers.Length; index++)
        {
            if (!CreateDetachedSprite(owner, sourceRenderers[index], detachedTransform, detachedBones))
            {
                Object.Destroy(detachedRoot);
                return null;
            }
        }

        Rigidbody2D body = detachedRoot.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.mass = mass;
        body.gravityScale = DefaultGravityScale;
        body.linearDamping = DefaultLinearDamping;
        body.angularDamping = DefaultAngularDamping;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        CapsuleCollider2D collider = detachedRoot.AddComponent<CapsuleCollider2D>();
        collider.direction = CapsuleDirection2D.Vertical;
        Vector3 worldScale = detachedTransform.lossyScale;
        float scaleX = Mathf.Max(MinimumWorldScale, Mathf.Abs(worldScale.x));
        float scaleY = Mathf.Max(MinimumWorldScale, Mathf.Abs(worldScale.y));
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
            Object.Destroy(detachedRoot, detachedLifetime);
        }

        return detachedRoot;
    }

    internal static void HideSourcePart(SpriteRenderer sourceRenderer)
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

    internal static void DisableLimbHitboxes(MonoBehaviour owner, CharacterHitRegion limbRegion)
    {
        HitRegionMarker[] regionMarkers = owner.GetComponentsInChildren<HitRegionMarker>(true);
        for (int markerIndex = 0; markerIndex < regionMarkers.Length; markerIndex++)
        {
            HitRegionMarker marker = regionMarkers[markerIndex];
            if (marker.Region != limbRegion)
            {
                continue;
            }

            Collider2D[] limbColliders = marker.GetComponents<Collider2D>();
            for (int colliderIndex = 0; colliderIndex < limbColliders.Length; colliderIndex++)
            {
                limbColliders[colliderIndex].enabled = false;
            }
        }
    }

    private static bool CreateBoneChain(
        Transform sourceBone,
        Transform skeletonRoot,
        Transform detachedRoot,
        Dictionary<Transform, Transform> detachedBones)
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
            if (sourceParent == null || !CreateBoneChain(sourceParent, skeletonRoot, detachedRoot, detachedBones))
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

    private static bool CreateDetachedSprite(
        MonoBehaviour owner,
        SpriteRenderer sourceRenderer,
        Transform detachedRoot,
        Dictionary<Transform, Transform> detachedBones)
    {
        SpriteSkin sourceSkin = sourceRenderer.GetComponent<SpriteSkin>();
        if (sourceSkin == null || sourceSkin.boneTransforms == null || sourceSkin.boneTransforms.Length == 0)
        {
            Debug.LogError($"{owner.GetType().Name} on '{owner.name}' found an invalid SpriteSkin on '{sourceRenderer.name}'.", owner);
            return false;
        }

        Transform[] sourceBones = sourceSkin.boneTransforms;
        Transform[] mappedBones = new Transform[sourceBones.Length];
        for (int index = 0; index < sourceBones.Length; index++)
        {
            if (!detachedBones.TryGetValue(sourceBones[index], out mappedBones[index]))
            {
                Debug.LogError($"{owner.GetType().Name} on '{owner.name}' could not map a SpriteSkin bone for '{sourceRenderer.name}'.", owner);
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
}
