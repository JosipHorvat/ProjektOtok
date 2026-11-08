using UnityEngine;

/// <summary>Plays surface-aware footsteps while the player moves on the ground.</summary>
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

    [Header("Automatic Footstep Timing")]
    [Tooltip("Minimum horizontal speed required to play grounded footsteps.")]
    [SerializeField, Min(0f)] private float minimumMovementSpeed = 0.25f;
    [Tooltip("Footstep interval while moving slowly.")]
    [SerializeField, Min(0.05f)] private float slowStepInterval = 0.45f;
    [Tooltip("Footstep interval at maximum horizontal speed.")]
    [SerializeField, Min(0.05f)] private float fastStepInterval = 0.28f;

    private Player player;
    private float stepTimer;
    private bool wasMovingOnGround;

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

    private void Update()
    {
        if (player == null)
        {
            return;
        }

        float horizontalSpeed = player.rb != null
            ? Mathf.Abs(player.rb.linearVelocity.x)
            : 0f;
        bool isMovingOnGround = player.HasGroundContact && horizontalSpeed >= minimumMovementSpeed;
        if (!isMovingOnGround)
        {
            stepTimer = 0f;
            wasMovingOnGround = false;
            return;
        }

        float maximumSpeed = Mathf.Max(player.moveSpeed, 0.01f);
        float normalizedSpeed = Mathf.Clamp01(horizontalSpeed / maximumSpeed);
        float stepInterval = Mathf.Lerp(slowStepInterval, fastStepInterval, normalizedSpeed);

        if (!wasMovingOnGround)
        {
            PlayFootstep();
            stepTimer = 0f;
            wasMovingOnGround = true;
            return;
        }

        stepTimer += Time.deltaTime;
        if (stepTimer >= stepInterval)
        {
            PlayFootstep();
            stepTimer = 0f;
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
