using UnityEngine;

/// <summary>Plays surface-aware footstep sounds from animation events.</summary>
public sealed class PlayerFootstepSFX : MonoBehaviour
{
    [Header("Surface Detection")]
    [SerializeField] private Transform footstepOrigin;
    [SerializeField] private LayerMask groundMask;
    [SerializeField, Min(0.05f)] private float surfaceCheckDistance = 0.5f;

    [Header("Random Footstep Players")]
    [SerializeField] private RandomAudioPlayer defaultFootsteps;
    [SerializeField] private RandomAudioPlayer groundFootsteps;
    [SerializeField] private RandomAudioPlayer concreteFootsteps;
    [SerializeField] private RandomAudioPlayer grassFootsteps;
    [SerializeField] private RandomAudioPlayer woodFootsteps;
    [SerializeField] private RandomAudioPlayer waterFootsteps;
    [SerializeField] private RandomAudioPlayer metalFootsteps;
    [SerializeField] private RandomAudioPlayer snowFootsteps;
    [SerializeField] private RandomAudioPlayer mudFootSteps;

    private Player player;

    private void Awake()
    {
        player = GetComponentInParent<Player>();

        if (footstepOrigin == null && player != null)
        {
            footstepOrigin = player.groundCheck;
        }

        if (groundMask.value == 0 && player != null)
        {
            groundMask = player.whatIsGround;
        }
    }

    /// <summary>Detects the surface below the player and plays its assigned random footstep sound.</summary>
    public void PlayFootstep()
    {
        if (player != null && !player.HasGroundContact)
        {
            return;
        }

        SurfaceType surfaceType = DetectSurface();
        RandomAudioPlayer footstepPlayer = GetFootstepPlayer(surfaceType);

        if (footstepPlayer != null)
        {
            footstepPlayer.PlayRandomSound();
        }
    }

    private SurfaceType DetectSurface()
    {
        Vector2 origin = footstepOrigin != null
            ? footstepOrigin.position
            : transform.position;

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            Vector2.down,
            surfaceCheckDistance,
            groundMask);

        if (hit.collider == null)
        {
            return SurfaceType.Default;
        }

        SurfaceIdentifier identifier = hit.collider.GetComponent<SurfaceIdentifier>();

        if (identifier == null)
        {
            identifier = hit.collider.GetComponentInParent<SurfaceIdentifier>();
        }

        return identifier != null ? identifier.SurfaceType : SurfaceType.Default;
    }

    private RandomAudioPlayer GetFootstepPlayer(SurfaceType surfaceType)
    {
        RandomAudioPlayer surfacePlayer = surfaceType switch
        {
            SurfaceType.Concrete => concreteFootsteps,
            SurfaceType.Ground => groundFootsteps,
            SurfaceType.Grass => grassFootsteps,
            SurfaceType.Wood => woodFootsteps,
            SurfaceType.Water => waterFootsteps,
            SurfaceType.Metal => metalFootsteps,
            SurfaceType.Snow => snowFootsteps,
            SurfaceType.Mud => mudFootSteps,
            _ => defaultFootsteps
        };

        return surfacePlayer != null ? surfacePlayer : defaultFootsteps;
    }
}
