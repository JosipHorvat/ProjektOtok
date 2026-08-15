using UnityEngine;

/// <summary>Applies a landing squash to the visual hierarchy without affecting player physics.</summary>
public sealed class PlayerSquash : MonoBehaviour
{
    private const float RecoveryOvershootTime = 0.55f;
    private const float RecoveryOvershootScale = 1.1f;

    [Header("Squash Shape")]
    [SerializeField] private float squashScaleX = 1.25f;
    [SerializeField] private float squashScaleY = 0.75f;

    [Header("Recovery")]
    [SerializeField] private float duration = 0.25f;
    [SerializeField] private AnimationCurve recoveryCurve;

    private Vector3 baseLocalScale;
    private bool isSquashing;
    private float elapsed;

    private void Awake()
    {
        baseLocalScale = transform.localScale;

        if (recoveryCurve == null || recoveryCurve.length == 0)
        {
            recoveryCurve = CreateDefaultRecoveryCurve();
        }
    }

    private void Reset()
    {
        recoveryCurve = CreateDefaultRecoveryCurve();
    }

    /// <summary>Restarts the landing squash effect from its compressed pose.</summary>
    public void TriggerSquash()
    {
        isSquashing = true;
        elapsed = 0f;
    }

    private void LateUpdate()
    {
        if (!isSquashing)
        {
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed >= duration)
        {
            transform.localScale = baseLocalScale;
            isSquashing = false;
            return;
        }

        float recovery = recoveryCurve.Evaluate(elapsed / duration);
        transform.localScale = new Vector3(
            baseLocalScale.x * Mathf.Lerp(squashScaleX, 1f, recovery),
            baseLocalScale.y * Mathf.Lerp(squashScaleY, 1f, recovery),
            baseLocalScale.z);
    }

    private static AnimationCurve CreateDefaultRecoveryCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 4f),
            new Keyframe(RecoveryOvershootTime, RecoveryOvershootScale),
            new Keyframe(1f, 1f));
    }
}
