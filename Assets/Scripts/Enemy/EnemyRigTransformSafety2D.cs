using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;

/// <summary>
/// Restores the most recent valid Animator rig pose if IK or procedural animation produces a non-finite transform.
/// </summary>
[DefaultExecutionOrder(6)]
[DisallowMultipleComponent]
public sealed class EnemyRigTransformSafety2D : MonoBehaviour
{
    private Transform[] rigTransforms;
    private LocalTransformPose[] previousValidPoses;
    private bool hasPreviousValidPose;
    private bool hasLoggedInvalidTransform;

    private void Awake()
    {
        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            return;
        }

        SpriteSkin[] spriteSkins = animator.GetComponentsInChildren<SpriteSkin>(true);
        List<Transform> boneHierarchy = new List<Transform>();
        for (int skinIndex = 0; skinIndex < spriteSkins.Length; skinIndex++)
        {
            Transform[] skinBones = spriteSkins[skinIndex].boneTransforms;
            if (skinBones == null)
            {
                continue;
            }

            for (int boneIndex = 0; boneIndex < skinBones.Length; boneIndex++)
            {
                Transform currentBone = skinBones[boneIndex];
                while (currentBone != null)
                {
                    if (!boneHierarchy.Contains(currentBone))
                    {
                        boneHierarchy.Add(currentBone);
                    }

                    if (currentBone == animator.transform)
                    {
                        break;
                    }

                    currentBone = currentBone.parent;
                }
            }
        }

        rigTransforms = boneHierarchy.ToArray();
        previousValidPoses = new LocalTransformPose[rigTransforms.Length];
        hasPreviousValidPose = CaptureCurrentPoseIfValid();
    }

    private void LateUpdate()
    {
        if (rigTransforms == null || rigTransforms.Length == 0)
        {
            return;
        }

        if (TryFindInvalidTransform(out int invalidTransformIndex))
        {
            if (hasPreviousValidPose)
            {
                RestorePreviousValidPose();
            }

            if (!hasLoggedInvalidTransform)
            {
                Transform invalidTransform = rigTransforms[invalidTransformIndex];
                string transformName = invalidTransform != null ? invalidTransform.name : "<destroyed transform>";
                Debug.LogError(
                    $"{nameof(EnemyRigTransformSafety2D)} on '{name}' detected a non-finite or zero-length transform on '{transformName}'." +
                    (hasPreviousValidPose ? " The previous valid rig pose was restored." : " No valid rig pose was available to restore."),
                    this);
                hasLoggedInvalidTransform = true;
            }

            return;
        }

        CaptureCurrentPose();
        hasPreviousValidPose = true;
    }

    private bool CaptureCurrentPoseIfValid()
    {
        if (TryFindInvalidTransform(out _))
        {
            return false;
        }

        CaptureCurrentPose();
        return true;
    }

    private void CaptureCurrentPose()
    {
        for (int index = 0; index < rigTransforms.Length; index++)
        {
            Transform rigTransform = rigTransforms[index];
            previousValidPoses[index] = new LocalTransformPose(
                rigTransform.localPosition,
                rigTransform.localRotation,
                rigTransform.localScale);
        }
    }

    private bool TryFindInvalidTransform(out int invalidTransformIndex)
    {
        for (int index = 0; index < rigTransforms.Length; index++)
        {
            Transform rigTransform = rigTransforms[index];
            if (rigTransform == null ||
                !IsFinite(rigTransform.localPosition) ||
                !IsValidRotation(rigTransform.localRotation) ||
                !IsFinite(rigTransform.localScale) ||
                !IsFinite(rigTransform.position) ||
                !IsValidRotation(rigTransform.rotation) ||
                !IsFinite(rigTransform.lossyScale))
            {
                invalidTransformIndex = index;
                return true;
            }
        }

        invalidTransformIndex = -1;
        return false;
    }

    private void RestorePreviousValidPose()
    {
        for (int index = 0; index < rigTransforms.Length; index++)
        {
            Transform rigTransform = rigTransforms[index];
            if (rigTransform == null)
            {
                continue;
            }

            LocalTransformPose pose = previousValidPoses[index];
            rigTransform.localPosition = pose.Position;
            rigTransform.localRotation = pose.Rotation;
            rigTransform.localScale = pose.Scale;
        }
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsValidRotation(Quaternion value)
    {
        float squaredLength = value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z) &&
               IsFinite(value.w) &&
               IsFinite(squaredLength) &&
               squaredLength > 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private struct LocalTransformPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;

        /// <summary>Creates a cached local transform pose.</summary>
        public LocalTransformPose(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
    }
}
