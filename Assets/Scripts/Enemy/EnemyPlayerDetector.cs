using System;
using UnityEngine;

/// <summary>Detects the player in a forward vision cone or at close range behind the enemy.</summary>
public sealed class EnemyPlayerDetector : MonoBehaviour
{
    private const int DetectedColliderCapacity = 16;
    private const int VisionArcSegments = 24;
    private const string PlayerLayerName = "Player";
    private const string WallLayerName = "Wall";

    [Header("Detection Origin")]
    [Tooltip("Move this child object to choose where the enemy's vision cone and visibility ray begin.")]
    [SerializeField] private Transform detectionOrigin;
    [Tooltip("The patrol movement component provides the enemy's current facing direction.")]
    [SerializeField] private EnemyPatrolMovement movement;

    [Header("Front Vision")]
    [Tooltip("How far away the enemy can notice the player in front.")]
    [SerializeField, Min(0f)] private float frontDetectionRadius = 6f;
    [Tooltip("Width of the front vision cone in degrees. 110 degrees gives a broad forward view.")]
    [SerializeField, Range(0f, 180f)] private float fieldOfViewAngle = 110f;

    [Header("Close-Range Awareness")]
    [Tooltip("How close the player must be to be noticed from behind. This is intentionally short.")]
    [SerializeField, Min(0f)] private float rearAwarenessDistance = 1f;

    [Header("Detection Layers")]
    [Tooltip("Only objects on these layers are considered players. The player's root or collider should use Player.")]
    [SerializeField] private LayerMask playerLayers;
    [Tooltip("Layers that block vision between the enemy and player, such as Wall.")]
    [SerializeField] private LayerMask visionBlockerLayers;

    private readonly Collider2D[] detectedColliders = new Collider2D[DetectedColliderCapacity];
    private Player currentTarget;

    /// <summary>The currently visible or rear-detected player, if any.</summary>
    public Player CurrentTarget => currentTarget;

    /// <summary>Raised when a player enters the detection area and has line of sight.</summary>
    public event Action<Player> PlayerDetected;

    /// <summary>Raised when the current player leaves detection or is occluded.</summary>
    public event Action<Player> PlayerLost;

    private void Awake()
    {
        if (detectionOrigin == null)
        {
            detectionOrigin = transform;
        }

        if (movement == null)
        {
            movement = GetComponent<EnemyPatrolMovement>();
        }

        if (playerLayers.value == 0)
        {
            playerLayers = LayerMask.GetMask(PlayerLayerName);
        }

        if (visionBlockerLayers.value == 0)
        {
            visionBlockerLayers = LayerMask.GetMask(WallLayerName);
        }
    }

    private void Update()
    {
        Player detectedPlayer = FindDetectedPlayer();
        if (detectedPlayer == currentTarget)
        {
            return;
        }

        if (currentTarget != null)
        {
            PlayerLost?.Invoke(currentTarget);
        }

        currentTarget = detectedPlayer;
        if (currentTarget != null)
        {
            PlayerDetected?.Invoke(currentTarget);
        }
    }

    private Player FindDetectedPlayer()
    {
        if (playerLayers.value == 0)
        {
            return null;
        }

        Vector2 origin = detectionOrigin.position;
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            origin,
            Mathf.Max(frontDetectionRadius, rearAwarenessDistance),
            detectedColliders,
            playerLayers);

        Player closestPlayer = null;
        float closestDistance = float.PositiveInfinity;
        Vector2 facing = Vector2.right * GetFacingDirection();

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D candidateCollider = detectedColliders[i];
            if (candidateCollider == null)
            {
                continue;
            }

            Player candidatePlayer = candidateCollider.GetComponentInParent<Player>();
            if (candidatePlayer == null)
            {
                continue;
            }

            Vector2 toPlayer = (Vector2)candidateCollider.bounds.center - origin;
            float distance = toPlayer.magnitude;
            bool isBehind = Vector2.Dot(facing, toPlayer) < 0f;
            bool isInFrontCone = distance <= frontDetectionRadius
                && Vector2.Angle(facing, toPlayer) <= fieldOfViewAngle * 0.5f;
            bool isWithinRearAwareness = isBehind && distance <= rearAwarenessDistance;

            if ((!isInFrontCone && !isWithinRearAwareness)
                || distance >= closestDistance
                || IsVisionBlocked(origin, toPlayer, distance))
            {
                continue;
            }

            closestPlayer = candidatePlayer;
            closestDistance = distance;
        }

        return closestPlayer;
    }

    private bool IsVisionBlocked(Vector2 origin, Vector2 toPlayer, float distance)
    {
        if (visionBlockerLayers.value == 0 || distance <= Mathf.Epsilon)
        {
            return false;
        }

        return Physics2D.Raycast(
            origin,
            toPlayer / distance,
            distance,
            visionBlockerLayers).collider != null;
    }

    private int GetFacingDirection()
    {
        if (movement != null)
        {
            return movement.FacingDirection;
        }

        return transform.lossyScale.x < 0f ? -1 : 1;
    }

    private void OnDrawGizmosSelected()
    {
        Transform originTransform = detectionOrigin != null ? detectionOrigin : transform;
        Vector2 origin = originTransform.position;
        int facingDirection = movement != null
            ? movement.FacingDirection
            : (transform.lossyScale.x < 0f ? -1 : 1);
        float halfAngle = fieldOfViewAngle * 0.5f;
        Vector2 facing = Vector2.right * facingDirection;

        Gizmos.color = currentTarget != null ? Color.red : Color.yellow;
        Vector2 previousArcPoint = origin + Rotate(facing, -halfAngle) * frontDetectionRadius;
        Gizmos.DrawLine(origin, previousArcPoint);
        for (int i = 1; i <= VisionArcSegments; i++)
        {
            float angle = -halfAngle + fieldOfViewAngle * i / VisionArcSegments;
            Vector2 arcPoint = origin + Rotate(facing, angle) * frontDetectionRadius;
            Gizmos.DrawLine(previousArcPoint, arcPoint);
            previousArcPoint = arcPoint;
        }

        Gizmos.DrawLine(origin, previousArcPoint);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, rearAwarenessDistance);
    }

    private static Vector2 Rotate(Vector2 direction, float angleDegrees)
    {
        float angleRadians = angleDegrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(angleRadians);
        float sine = Mathf.Sin(angleRadians);
        return new Vector2(
            direction.x * cosine - direction.y * sine,
            direction.x * sine + direction.y * cosine);
    }
}
