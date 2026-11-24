using UnityEngine;

/// <summary>Builds poise from weapon impacts and applies short, non-repeatable stagger reactions.</summary>
[DisallowMultipleComponent]
public sealed class CharacterStagger : MonoBehaviour, IHitReactionReceiver2D
{
    private const string StaggerAnimatorTrigger = "Stagger";
    private const float DefaultPoiseThreshold = 30f;
    private const float DefaultHitSlowMultiplier = 0.85f;
    private const float DefaultHitSlowDuration = 0.12f;
    private const float DefaultStaggerDuration = 0.35f;
    private const float DefaultStaggerCooldown = 1.5f;
    private const float DefaultPoiseRecoveryDelay = 0.8f;
    private const float DefaultPoiseRecoveryRate = 18f;

    [Header("References")]
    [Tooltip("Optional health/event source. Death cancels stagger and suppresses further hit reactions.")]
    [SerializeField] private MonoBehaviour damageEventSource;
    [Tooltip("Optional movement adapter implementing IHitReactionMovement2D.")]
    [SerializeField] private MonoBehaviour movementAdapter;
    [Tooltip("Animator containing a Stagger trigger and one-shot reaction state.")]
    [SerializeField] private Animator animator;

    [Header("Light Hit Response")]
    [Tooltip("Movement speed multiplier briefly applied after each accepted hit. 0.85 means 15% slower.")]
    [SerializeField, Range(0.1f, 1f)] private float hitSlowMultiplier = DefaultHitSlowMultiplier;
    [Tooltip("Duration of the small movement slow after a hit, in seconds.")]
    [SerializeField, Min(0f)] private float hitSlowDuration = DefaultHitSlowDuration;

    [Header("Stagger Balance")]
    [Tooltip("Poise required to trigger a stagger. Weapon assets contribute their own stagger impact per hit.")]
    [SerializeField, Min(1f)] private float poiseThreshold = DefaultPoiseThreshold;
    [Tooltip("How long character movement is interrupted when stagger triggers.")]
    [SerializeField, Min(0.05f)] private float staggerDuration = DefaultStaggerDuration;
    [Tooltip("Minimum time between full staggers. Hits during this period still deal health damage and flash, but cannot restart or build another stagger.")]
    [SerializeField, Min(0f)] private float staggerCooldown = DefaultStaggerCooldown;
    [Tooltip("Delay after the last hit before unused poise begins to recover.")]
    [SerializeField, Min(0f)] private float poiseRecoveryDelay = DefaultPoiseRecoveryDelay;
    [Tooltip("Poise recovered per second after the recovery delay.")]
    [SerializeField, Min(0f)] private float poiseRecoveryRate = DefaultPoiseRecoveryRate;

    private IDamageFeedbackSource damageSource;
    private IHitReactionMovement2D movement;
    private float currentPoise;
    private float staggerTimeRemaining;
    private float staggerCooldownRemaining;
    private float timeSinceLastHit;
    private bool isDead;

    /// <summary>Current poise accumulated toward the next stagger.</summary>
    public float CurrentPoise => currentPoise;

    /// <summary>Whether movement is currently interrupted by a stagger.</summary>
    public bool IsStaggered { get; private set; }

    /// <summary>Body-region tag received by the latest accepted hit.</summary>
    public CharacterHitRegion LastHitRegion { get; private set; } = CharacterHitRegion.Torso;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (damageSource != null)
        {
            damageSource.Died += HandleDeath;
            isDead = damageSource.IsDead;
        }
    }

    private void OnDisable()
    {
        if (damageSource != null)
        {
            damageSource.Died -= HandleDeath;
        }

        SetStaggered(false);
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        timeSinceLastHit += deltaTime;
        staggerCooldownRemaining = Mathf.Max(0f, staggerCooldownRemaining - deltaTime);

        if (IsStaggered)
        {
            staggerTimeRemaining -= deltaTime;
            if (staggerTimeRemaining <= 0f)
            {
                SetStaggered(false);
            }
        }

        if (!IsStaggered && staggerCooldownRemaining <= 0f && timeSinceLastHit >= poiseRecoveryDelay)
        {
            currentPoise = Mathf.Max(0f, currentPoise - poiseRecoveryRate * deltaTime);
        }
    }

    /// <summary>Applies a small hit slow and adds weapon impact toward the stagger threshold.</summary>
    public void ReceiveHitReaction(float staggerImpact, Vector2 hitPoint, Vector2 hitDirection, CharacterHitRegion hitRegion)
    {
        if (isDead || damageSource != null && damageSource.IsDead)
        {
            return;
        }

        LastHitRegion = hitRegion;
        timeSinceLastHit = 0f;
        movement?.ApplyTemporaryMovementSlow(hitSlowMultiplier, hitSlowDuration);

        if (staggerImpact <= 0f || IsStaggered || staggerCooldownRemaining > 0f)
        {
            return;
        }

        currentPoise += staggerImpact;
        if (currentPoise >= poiseThreshold)
        {
            BeginStagger();
        }
    }

    private void BeginStagger()
    {
        currentPoise = 0f;
        staggerCooldownRemaining = staggerCooldown;
        staggerTimeRemaining = staggerDuration;
        SetStaggered(true);

        if (animator != null && animator.isActiveAndEnabled)
        {
            animator.ResetTrigger(StaggerAnimatorTrigger);
            animator.SetTrigger(StaggerAnimatorTrigger);
        }
    }

    private void SetStaggered(bool staggered)
    {
        IsStaggered = staggered;
        if (!staggered)
        {
            staggerTimeRemaining = 0f;
        }

        movement?.SetStaggered(staggered);
    }

    private void HandleDeath()
    {
        isDead = true;
        currentPoise = 0f;
        SetStaggered(false);
    }

    private void ResolveReferences()
    {
        if (damageEventSource == null)
        {
            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is IDamageFeedbackSource)
                {
                    damageEventSource = components[i];
                    break;
                }
            }
        }

        damageSource = damageEventSource as IDamageFeedbackSource;

        if (movementAdapter == null)
        {
            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is IHitReactionMovement2D)
                {
                    movementAdapter = components[i];
                    break;
                }
            }
        }

        movement = movementAdapter as IHitReactionMovement2D;
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }
}
