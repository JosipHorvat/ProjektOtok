using UnityEngine;

/// <summary>Tilts the player's visual hierarchy for slopes and adds a subtle speed-scaled forward lean.</summary>
public sealed class PlayerSlopeVisuals : MonoBehaviour
{
    [SerializeField] private float maximumVisualTilt = 12f;
    [SerializeField] private float tiltSmoothTime = 0.08f;

    [Header("Forward movement lean")]
    [Tooltip("Maximum forward visual lean at the player's maximum horizontal speed.")]
    [SerializeField, Min(0f)] private float maximumForwardLean = 2.5f;

    private const float MinimumSpeedDenominator = 0.01f;

    private Player player;
    private Quaternion baseLocalRotation;
    private float currentTilt;
    private float tiltVelocity;

    private void Awake()
    {
        player = GetComponentInParent<Player>();
        baseLocalRotation = transform.localRotation;
    }

    private void LateUpdate()
    {
        if (player == null)
        {
            return;
        }

        float targetTilt = 0f;
        bool isWalkingOnSlope = player.HasGroundContact
            && Mathf.Abs(player.MoveInput.x) > PlayerState.HorizontalInputThreshold;

        if (isWalkingOnSlope)
        {
            float facingAdjustedSlopeAngle = player.CurrentGroundInfo.Angle * player.facingDir;
            targetTilt = Mathf.Clamp(facingAdjustedSlopeAngle, -maximumVisualTilt, maximumVisualTilt);
        }

        float horizontalSpeed = player.rb != null
            ? Mathf.Abs(player.rb.linearVelocity.x)
            : 0f;
        float maximumSpeed = Mathf.Max(player.moveSpeed, MinimumSpeedDenominator);
        float normalizedSpeed = Mathf.Clamp01(horizontalSpeed / maximumSpeed);
        float speedWeightedLean = Mathf.SmoothStep(0f, 1f, normalizedSpeed) * maximumForwardLean;
        targetTilt -= speedWeightedLean * player.facingDir;

        currentTilt = Mathf.SmoothDampAngle(currentTilt, targetTilt, ref tiltVelocity, tiltSmoothTime);
        transform.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, currentTilt);
    }

    private void OnDisable()
    {
        transform.localRotation = baseLocalRotation;
        currentTilt = 0f;
        tiltVelocity = 0f;
    }
}
