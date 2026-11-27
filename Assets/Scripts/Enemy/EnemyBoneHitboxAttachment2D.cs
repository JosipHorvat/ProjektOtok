using System.Collections.Generic;
using UnityEngine;

/// <summary>Keeps animated hurtboxes on the active enemy rigidbody while following their rig bones.</summary>
[DefaultExecutionOrder(11)]
[DisallowMultipleComponent]
public sealed class EnemyBoneHitboxAttachment2D : MonoBehaviour
{
    private const string BoneHitboxPrefix = "Hitbox_";
    private const string TorsoHurtboxName = "TorsoHurtbox";

    private readonly List<BoneHitboxFollower> followers = new List<BoneHitboxFollower>();
    private EnemyHealth health;

    private void Start()
    {
        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            Debug.LogWarning($"{nameof(EnemyBoneHitboxAttachment2D)} on '{name}' could not find an Animator to follow.", this);
            return;
        }

        health = GetComponent<EnemyHealth>();
        Transform rigRoot = animator.transform;
        HitRegionMarker[] hitRegionMarkers = GetComponentsInChildren<HitRegionMarker>(true);
        for (int index = 0; index < hitRegionMarkers.Length; index++)
        {
            HitRegionMarker marker = hitRegionMarkers[index];
            Transform hitboxTransform = marker.transform;
            if (!IsManagedHitbox(hitboxTransform) || hitboxTransform.parent == null)
            {
                continue;
            }

            Collider2D hitboxCollider = hitboxTransform.GetComponent<Collider2D>();
            Transform sourceBone = hitboxTransform.parent;
            if (hitboxCollider == null || sourceBone == rigRoot || !sourceBone.IsChildOf(rigRoot))
            {
                continue;
            }

            Vector3 boneLocalPosition = sourceBone.InverseTransformPoint(hitboxTransform.position);
            Quaternion boneLocalRotation = Quaternion.Inverse(sourceBone.rotation) * hitboxTransform.rotation;
            hitboxTransform.SetParent(rigRoot, true);
            followers.Add(new BoneHitboxFollower(hitboxTransform, sourceBone, boneLocalPosition, boneLocalRotation));
        }

        if (followers.Count == 0)
        {
            Debug.LogWarning($"{nameof(EnemyBoneHitboxAttachment2D)} on '{name}' found no bone-following hurtboxes to attach to the active Rigidbody2D.", this);
        }
    }

    private void LateUpdate()
    {
        if (followers.Count == 0 || (health != null && health.IsDead))
        {
            return;
        }

        for (int index = 0; index < followers.Count; index++)
        {
            BoneHitboxFollower follower = followers[index];
            if (follower.Hitbox == null || follower.SourceBone == null)
            {
                continue;
            }

            follower.Hitbox.SetPositionAndRotation(
                follower.SourceBone.TransformPoint(follower.BoneLocalPosition),
                follower.SourceBone.rotation * follower.BoneLocalRotation);
        }

        Physics2D.SyncTransforms();
    }

    private static bool IsManagedHitbox(Transform hitboxTransform)
    {
        return hitboxTransform.name.StartsWith(BoneHitboxPrefix)
            || hitboxTransform.name == TorsoHurtboxName;
    }

    private readonly struct BoneHitboxFollower
    {
        public readonly Transform Hitbox;
        public readonly Transform SourceBone;
        public readonly Vector3 BoneLocalPosition;
        public readonly Quaternion BoneLocalRotation;

        public BoneHitboxFollower(Transform hitbox, Transform sourceBone, Vector3 boneLocalPosition, Quaternion boneLocalRotation)
        {
            Hitbox = hitbox;
            SourceBone = sourceBone;
            BoneLocalPosition = boneLocalPosition;
            BoneLocalRotation = boneLocalRotation;
        }
    }
}
