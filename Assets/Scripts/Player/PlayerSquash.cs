using UnityEngine;

/// <summary>
/// Scales this GameObject's transform to simulate squash on landing.
/// Attach to the Player's Animator child so only visuals are affected,
/// never the physics collider on the Player root.
/// Uses LateUpdate to guarantee the scale is applied after the Animator updates.
/// </summary>
public class PlayerSquash : MonoBehaviour
{
    [Header("Squash Shape")]
    [SerializeField] private float squashScaleX = 1.25f;
    [SerializeField] private float squashScaleY = 0.75f;

    [Header("Recovery")]
    [SerializeField] private float duration = 0.25f;
    [SerializeField] private AnimationCurve recoveryCurve;

    private bool  isSquashing = false;
    private float elapsed     = 0f;

    private void Reset()
    {
        // Pre-bake a slight overshoot so the robot snaps back with a spring feel.
        recoveryCurve = new AnimationCurve(
            new Keyframe(0f,    0f,  0f, 4f),
            new Keyframe(0.55f, 1.1f),
            new Keyframe(1f,    1f)
        );
    }

    /// <summary>
    /// Instantly squashes the transform, then springs back to normal scale.
    /// Safe to call mid-animation — restarts the squash from the beginning.
    /// </summary>
    public void TriggerSquash()
    {
        isSquashing = true;
        elapsed     = 0f;
    }

    // LateUpdate runs after the Animator, so our scale always wins.
    private void LateUpdate()
    {
        if (!isSquashing) return;

        elapsed += Time.deltaTime;

        if (elapsed >= duration)
        {
            transform.localScale = Vector3.one;
            isSquashing = false;
            return;
        }

        float t = recoveryCurve.Evaluate(elapsed / duration);
        transform.localScale = new Vector3(
            Mathf.Lerp(squashScaleX, 1f, t),
            Mathf.Lerp(squashScaleY, 1f, t),
            1f
        );
    }
}
