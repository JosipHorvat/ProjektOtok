using System.Collections.Generic;
using UnityEngine;

/// <summary>Starts a cooldown-based melee animation while the enemy is close to its chase target.</summary>
[DisallowMultipleComponent]
public sealed class EnemyMeleeAttack : MonoBehaviour
{
    private const string AttackTriggerParameter = "Attack";
    private const string WalkingParameter = "IsWalking";
    private const string CombatIdleParameter = "IsCombatIdle";
    private const float DefaultOneArmDamageMultiplier = 0.75f;
    private const float DefaultNoArmsDamageMultiplier = 0.5f;
    private const float DefaultOneArmCooldownMultiplier = 1.25f;
    private const float DefaultNoArmsCooldownMultiplier = 1.5f;

    [Header("Attack Timing")]
    [Tooltip("Maximum horizontal distance from the enemy root to the target at which an attack can start.")]
    [SerializeField, Min(0f)] private float attackRange = 1f;
    [Tooltip("Minimum time between the starts of consecutive attacks.")]
    [SerializeField, Min(0f)] private float attackCooldown = 1.25f;
    [Tooltip("Maximum vertical separation allowed for a melee attack.")]
    [SerializeField, Min(0f)] private float maximumVerticalAttackDistance = 0.8f;
    [Header("Attack Movement")]
    [Tooltip("Distance the enemy lunges forward during each attack, in world units.")]
    [SerializeField, Min(0f)] private float attackLungeDistance = 0.25f;
    [Tooltip("Time taken to complete the forward lunge, in seconds.")]
    [SerializeField, Min(0.01f)] private float attackLungeDuration = 0.2f;
    [Tooltip("Damage applied to each IDamageable2D target hit once during an attack. The player needs a damage receiver before this can reduce player health.")]
    [SerializeField, Min(0f)] private float attackDamage = 10f;
    [Tooltip("Damage multiplier when exactly one arm remains.")]
    [SerializeField, Min(0f)] private float oneArmDamageMultiplier = DefaultOneArmDamageMultiplier;
    [Tooltip("Damage multiplier when no arms remain.")]
    [SerializeField, Min(0f)] private float noArmsDamageMultiplier = DefaultNoArmsDamageMultiplier;
    [Tooltip("Cooldown multiplier when exactly one arm remains.")]
    [SerializeField, Min(1f)] private float oneArmCooldownMultiplier = DefaultOneArmCooldownMultiplier;
    [Tooltip("Cooldown multiplier when no arms remain.")]
    [SerializeField, Min(1f)] private float noArmsCooldownMultiplier = DefaultNoArmsCooldownMultiplier;

    [Header("References")]
    [Tooltip("Animator on this GameObject or a child. Automatically cached when left empty.")]
    [SerializeField] private Animator animator;
    [Tooltip("Assign the melee trigger explicitly if it is not on this GameObject. Animation events enable it only during the strike.")]
    [SerializeField] private BoxCollider2D attackHitbox;
    [Tooltip("Limb condition source. Automatically cached from a parent when left empty.")]
    [SerializeField] private EnemyLimbCondition2D limbCondition;

    private readonly HashSet<IDamageable2D> hitTargets = new HashSet<IDamageable2D>();
    private EnemyPatrolMovement movement;
    private EnemyPatrolStateMachine stateMachine;
    private EnemyPlayerDetector playerDetector;
    private CharacterStagger characterStagger;
    private EnemyHealth health;
    private float nextAttackTime;
    private float effectiveDamageMultiplier = 1f;
    private float effectiveCooldownMultiplier = 1f;
    private float currentAttackDamage;
    private bool attackInProgress;
    private bool isCoolingDown;
    private bool hasWarnedMissingSetup;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (attackHitbox == null)
        {
            attackHitbox = GetComponent<BoxCollider2D>();
        }

        movement = GetComponentInParent<EnemyPatrolMovement>();
        stateMachine = GetComponentInParent<EnemyPatrolStateMachine>();
        playerDetector = GetComponentInParent<EnemyPlayerDetector>();
        characterStagger = GetComponentInParent<CharacterStagger>();
        health = GetComponentInParent<EnemyHealth>();
        if (limbCondition == null)
        {
            limbCondition = GetComponentInParent<EnemyLimbCondition2D>();
        }

        if (attackHitbox != null)
        {
            attackHitbox.isTrigger = true;
            attackHitbox.enabled = false;
        }

        if ((animator == null || attackHitbox == null || movement == null || stateMachine == null || playerDetector == null)
            && !hasWarnedMissingSetup)
        {
            Debug.LogWarning($"{nameof(EnemyMeleeAttack)} on '{name}' requires an Animator, BoxCollider2D, patrol movement, state machine, and player detector.", this);
            hasWarnedMissingSetup = true;
        }
    }

    private void OnEnable()
    {
        if (limbCondition == null)
        {
            limbCondition = GetComponentInParent<EnemyLimbCondition2D>();
        }

        if (limbCondition != null)
        {
            limbCondition.LimbLost += HandleLimbLost;
        }

        RefreshArmModifiers();

        if (health != null)
        {
            health.Died += HandleDeath;
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromLimbCondition();

        if (health != null)
        {
            health.Died -= HandleDeath;
        }

        CancelAttack();
    }

    private void OnDestroy()
    {
        UnsubscribeFromLimbCondition();

        if (health != null)
        {
            health.Died -= HandleDeath;
        }
    }

    private void HandleLimbLost(EnemyLimbCondition2D condition, EnemyLimbCondition2D.Limb limb)
    {
        RefreshArmModifiers();
    }

    private void RefreshArmModifiers()
    {
        int remainingArms = limbCondition != null ? limbCondition.RemainingArmCount : 2;
        if (remainingArms == 1)
        {
            effectiveDamageMultiplier = oneArmDamageMultiplier;
            effectiveCooldownMultiplier = Mathf.Max(1f, oneArmCooldownMultiplier);
        }
        else if (remainingArms == 0)
        {
            effectiveDamageMultiplier = noArmsDamageMultiplier;
            effectiveCooldownMultiplier = Mathf.Max(1f, noArmsCooldownMultiplier);
        }
        else
        {
            effectiveDamageMultiplier = 1f;
            effectiveCooldownMultiplier = 1f;
        }
    }

    private void UnsubscribeFromLimbCondition()
    {
        if (limbCondition != null)
        {
            limbCondition.LimbLost -= HandleLimbLost;
        }
    }

    private void Update()
    {
        if (health != null && health.IsDead || characterStagger != null && characterStagger.IsStaggered)
        {
            CancelAttack();
            return;
        }

        if (attackInProgress)
        {
            return;
        }

        if (isCoolingDown)
        {
            if (Time.time < nextAttackTime)
            {
                return;
            }

            isCoolingDown = false;
            if (animator != null)
            {
                animator.SetBool(CombatIdleParameter, false);
            }

            if (movement != null)
            {
                movement.SetAttackPaused(false);
            }

            UpdateAnimatorLocomotionParameters();
        }

        if (animator == null
            || attackHitbox == null
            || movement == null
            || stateMachine == null
            || playerDetector == null
            || stateMachine.CurrentState != EnemyPatrolState.Chase
            || playerDetector.CurrentTarget == null
            || Time.time < nextAttackTime)
        {
            return;
        }

        Transform target = playerDetector.CurrentTarget.transform;
        Vector2 displacement = target.position - transform.position;
        if (Mathf.Abs(displacement.x) > attackRange
            || Mathf.Abs(displacement.y) > maximumVerticalAttackDistance)
        {
            return;
        }

        StartAttack(target);
    }

    /// <summary>Enables the configured attack trigger collider; called by the attack animation at the strike frame.</summary>
    public void EnableAttackHitbox()
    {
        if (attackInProgress
            && attackHitbox != null
            && (health == null || !health.IsDead)
            && (characterStagger == null || !characterStagger.IsStaggered))
        {
            attackHitbox.enabled = true;
        }
    }

    /// <summary>Disables the attack trigger collider after the active strike frames.</summary>
    public void DisableAttackHitbox()
    {
        if (attackHitbox != null)
        {
            attackHitbox.enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!attackInProgress
            || attackHitbox == null
            || !attackHitbox.enabled
            || other == null
            || other.transform.root == transform.root)
        {
            return;
        }

        MonoBehaviour[] targetComponents = other.GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < targetComponents.Length; i++)
        {
            IDamageable2D damageable = targetComponents[i] as IDamageable2D;
            if (damageable == null || !hitTargets.Add(damageable))
            {
                continue;
            }

            Vector2 hitPoint = other.ClosestPoint(transform.position);
            Vector2 hitDirection = ((Vector2)other.transform.position - (Vector2)transform.position).normalized;
            damageable.ReceiveDamage(currentAttackDamage, hitPoint, hitDirection);
            break;
        }
    }

    /// <summary>Ends the attack and releases movement; called at the end of the attack animation.</summary>
    public void FinishAttack()
    {
        DisableAttackHitbox();
        if (movement != null)
        {
            movement.StopAttackLunge();
        }

        attackInProgress = false;
        isCoolingDown = Time.time < nextAttackTime;
        hitTargets.Clear();

        if (animator != null)
        {
            animator.SetBool(CombatIdleParameter, isCoolingDown);
        }

        if (!isCoolingDown)
        {
            if (movement != null)
            {
                movement.SetAttackPaused(false);
            }

            UpdateAnimatorLocomotionParameters();
        }
    }

    private void StartAttack(Transform target)
    {
        movement.FaceTarget(target);
        movement.SetAttackPaused(true);
        movement.StartAttackLunge(attackLungeDistance, attackLungeDuration);
        hitTargets.Clear();
        attackInProgress = true;
        isCoolingDown = false;
        currentAttackDamage = attackDamage * effectiveDamageMultiplier;
        float currentAttackCooldown = attackCooldown * effectiveCooldownMultiplier;
        nextAttackTime = Time.time + currentAttackCooldown;
        animator.SetBool(CombatIdleParameter, false);
        animator.SetBool(WalkingParameter, false);
        animator.ResetTrigger(AttackTriggerParameter);
        animator.SetTrigger(AttackTriggerParameter);
    }

    private void HandleDeath()
    {
        CancelAttack();
    }

    private void CancelAttack()
    {
        DisableAttackHitbox();
        attackInProgress = false;
        isCoolingDown = false;
        hitTargets.Clear();

        if (animator != null)
        {
            animator.SetBool(CombatIdleParameter, false);
        }

        if (movement != null)
        {
            movement.SetAttackPaused(false);
        }

        UpdateAnimatorLocomotionParameters();
    }

    private void UpdateAnimatorLocomotionParameters()
    {
        if (animator == null || stateMachine == null)
        {
            return;
        }

        EnemyAnimatorBridge.ApplyLocomotionState(animator, stateMachine.CurrentState);
    }
}
