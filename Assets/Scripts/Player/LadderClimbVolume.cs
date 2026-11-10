using UnityEngine;

/// <summary>Registers players overlapping this trigger and exposes its climb lane and endpoints.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class LadderClimbVolume : MonoBehaviour
{
    /// <summary>Controls whether ladder endpoints follow the trigger or use child endpoint markers.</summary>
    public enum LadderEndpointMode
    {
        FollowTriggerBounds,
        UseEndpointMarkers
    }

    /// <summary>Specifies which direction the player faces while using this ladder.</summary>
    public enum LadderFacingDirection
    {
        Left,
        Right
    }

    private const float DefaultEndpointDeadZone = 0.12f;
    private const float ClimbPoseStepDuration = 0.2f;
    private const float DismountClearance = 0.05f;
    private const string BottomExitName = "BottomExit";
    private const string LadderMountName = "LadderMount";
    private const string RungOriginName = "RungOrigin";
    private const string TopExitName = "TopExit";

    [SerializeField, Tooltip("Direction the player faces while climbing this ladder.")]
    private LadderFacingDirection ladderFacingDirection = LadderFacingDirection.Right;
    [SerializeField, Tooltip("Follow the trigger size automatically, or use TopExit and BottomExit for custom endpoints.")]
    private LadderEndpointMode endpointMode = LadderEndpointMode.FollowTriggerBounds;
    [SerializeField, Min(0f)] private float endpointDeadZone = DefaultEndpointDeadZone;
    [SerializeField, Min(0f)] private float bodyOffsetFromRung = 0.48f;
    [SerializeField, Min(0.05f)] private float rungSpacing = 0.48f;

    private BoxCollider2D climbTrigger;
    private Transform bottomExit;
    private Transform ladderMount;
    private Transform rungOrigin;
    private Transform topExit;

    public float ClimbSpeed => rungSpacing / ClimbPoseStepDuration;
    public float RungSpacing => rungSpacing;
    public Bounds TriggerBounds => climbTrigger != null ? climbTrigger.bounds : GetComponent<BoxCollider2D>().bounds;

    private void Awake()
    {
        climbTrigger = GetComponent<BoxCollider2D>();
        bottomExit = transform.Find(BottomExitName);
        ladderMount = transform.Find(LadderMountName);
        rungOrigin = transform.Find(RungOriginName);
        topExit = transform.Find(TopExitName);

        if (!climbTrigger.isTrigger)
        {
            Debug.LogWarning("LadderClimbVolume requires its BoxCollider2D to have Is Trigger enabled.", this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        RegisterPlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        RegisterPlayer(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player != null)
        {
            player.ClearAvailableLadder(this);
        }
    }

    /// <summary>Returns whether the requested direction can enter from the player's current position.</summary>
    public bool CanEnter(Player player, float direction)
    {
        if (player == null || Mathf.Abs(direction) <= PlayerState.HorizontalInputThreshold)
        {
            return false;
        }

        Vector2 limits = GetRootYLimits(player);
        float playerY = player.transform.position.y;

        return direction > 0f
            ? playerY < limits.y - endpointDeadZone
            : playerY > limits.x + endpointDeadZone;
    }

    /// <summary>Returns the player root's lowest and highest positions on this ladder.</summary>
    public Vector2 GetRootYLimits(Player player)
    {
        Bounds bounds = climbTrigger != null ? climbTrigger.bounds : GetComponent<BoxCollider2D>().bounds;
        float footOffset = GetPlayerFootOffset(player);
        float bottomFeetY = GetEndpointWorldY(false, bounds);
        float topFeetY = GetEndpointWorldY(true, bounds);

        return new Vector2(
            bottomFeetY - footOffset,
            topFeetY - footOffset);
    }

    /// <summary>Returns the nearest permitted rung height as a player-root position.</summary>
    public float GetNearestRungRootY(Player player)
    {
        if (player == null)
        {
            return 0f;
        }

        Vector2 limits = GetRootYLimits(player);
        Bounds bounds = TriggerBounds;
        float footOffset = GetPlayerFootOffset(player);
        if (rungOrigin == null || rungSpacing <= PlayerState.HorizontalInputThreshold)
        {
            return Mathf.Clamp(player.transform.position.y, limits.x, limits.y);
        }

        float currentFeetY = player.transform.position.y + footOffset;
        float rungOriginY = Mathf.Clamp(rungOrigin.position.y, bounds.min.y, bounds.max.y);
        int nearestRungIndex = Mathf.RoundToInt(
            (currentFeetY - rungOriginY) / rungSpacing);
        float rungRootY = rungOriginY + nearestRungIndex * rungSpacing - footOffset;

        return Mathf.Clamp(rungRootY, limits.x, limits.y);
    }

    private float GetEndpointWorldY(bool top, Bounds bounds)
    {
        if (endpointMode == LadderEndpointMode.FollowTriggerBounds)
        {
            return top ? bounds.max.y : bounds.min.y;
        }

        Transform endpoint = top ? topExit : bottomExit;
        if (endpoint == null)
        {
            return top ? bounds.max.y : bounds.min.y;
        }

        return Mathf.Clamp(endpoint.position.y, bounds.min.y, bounds.max.y);
    }

    private float GetPlayerFootOffset(Player player)
    {
        CapsuleCollider2D bodyCollider = player != null ? player.BodyCollider : null;
        if (bodyCollider == null)
        {
            return 0f;
        }

        Vector3 localFootOffset = new Vector3(
            0f,
            bodyCollider.offset.y - bodyCollider.size.y * 0.5f,
            0f);
        return player.transform.TransformVector(localFootOffset).y;
    }

    /// <summary>Returns the configured facing direction for the player on this ladder.</summary>
    public int GetFacingDirectionToLadder(Player player)
    {
        if (player == null)
        {
            return 0;
        }

        return ladderFacingDirection == LadderFacingDirection.Right ? 1 : -1;
    }

    /// <summary>Returns a root X that places the player collider clear of the ladder lane.</summary>
    public float GetPlayerDismountRootX(Player player, float horizontalDirection)
    {
        if (player == null || Mathf.Abs(horizontalDirection) <= PlayerState.HorizontalInputThreshold)
        {
            return player != null ? GetPlayerRootX(player, player.facingDir) : transform.position.x;
        }

        Bounds bounds = TriggerBounds;
        CapsuleCollider2D bodyCollider = player.BodyCollider;
        float colliderCenterOffset = bodyCollider != null
            ? player.transform.TransformVector(new Vector3(bodyCollider.offset.x, 0f, 0f)).x
            : 0f;
        float colliderHalfWidth = bodyCollider != null ? bodyCollider.bounds.extents.x : 0f;
        float direction = Mathf.Sign(horizontalDirection);
        float targetColliderCenterX = direction > 0f
            ? bounds.max.x + colliderHalfWidth + DismountClearance
            : bounds.min.x - colliderHalfWidth - DismountClearance;

        return targetColliderCenterX - colliderCenterOffset;
    }

    /// <summary>Returns the player root X that places the reach-adjusted hands on the rung line.</summary>
    public float GetPlayerRootX(Player player, int facingDirection)
    {
        float colliderCenterOffset = 0f;
        CapsuleCollider2D bodyCollider = player != null ? player.BodyCollider : null;

        if (bodyCollider != null)
        {
            colliderCenterOffset = player.transform.TransformVector(
                new Vector3(bodyCollider.offset.x, 0f, 0f)).x;
        }

        int direction = facingDirection == 0 ? 1 : facingDirection;
        return GetMountWorldX() - direction * bodyOffsetFromRung - colliderCenterOffset;
    }

    private float GetMountWorldX()
    {
        if (ladderMount != null)
        {
            return ladderMount.position.x;
        }

        Bounds bounds = climbTrigger != null ? climbTrigger.bounds : GetComponent<BoxCollider2D>().bounds;
        return bounds.center.x;
    }

    /// <summary>Returns true when the player's feet reach a ladder endpoint within its dead zone.</summary>
    public bool IsAtEndpoint(Player player, bool top)
    {
        Vector2 limits = GetRootYLimits(player);
        float playerY = player.transform.position.y;
        return top
            ? playerY >= limits.y - endpointDeadZone
            : playerY <= limits.x + endpointDeadZone;
    }

    private void OnDrawGizmosSelected()
    {
        if (rungSpacing <= 0f)
        {
            return;
        }

        Transform origin = transform.Find(RungOriginName);
        BoxCollider2D trigger = climbTrigger != null ? climbTrigger : GetComponent<BoxCollider2D>();
        if (origin == null || trigger == null)
        {
            return;
        }

        Bounds bounds = trigger.bounds;
        float rungOriginY = Mathf.Clamp(origin.position.y, bounds.min.y, bounds.max.y);
        float topY = GetEndpointWorldY(true, bounds);
        Gizmos.color = new Color(1f, 0.62f, 0.18f, 0.9f);

        for (int rungIndex = 0; rungIndex < 512; rungIndex++)
        {
            float rungY = rungOriginY + rungIndex * rungSpacing;
            if (rungY > topY + 0.001f)
            {
                break;
            }

            Gizmos.DrawSphere(
                new Vector3(origin.position.x, rungY, origin.position.z),
                0.055f);
        }
    }

    private void RegisterPlayer(Collider2D other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player != null)
        {
            player.SetAvailableLadder(this);
        }
    }
}
