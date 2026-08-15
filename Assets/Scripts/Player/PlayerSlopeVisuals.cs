using UnityEngine;

/// <summary>Tilts only the player's visual hierarchy toward walkable slopes.</summary>
public sealed class PlayerSlopeVisuals : MonoBehaviour
{
    [SerializeField] private float maximumVisualTilt = 12f;
    [SerializeField] private float tiltSmoothTime = 0.08f;

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
