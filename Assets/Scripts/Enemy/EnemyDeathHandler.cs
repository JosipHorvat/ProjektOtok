using UnityEngine;

/// <summary>Stops enemy gameplay and plays its one-shot death animation when health is depleted.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public sealed class EnemyDeathHandler : MonoBehaviour
{
    private const string DeathAnimatorTrigger = "Die";
    private const string WalkingAnimatorParameter = "IsWalking";

    [Header("References")]
    [Tooltip("Animator on the visual child that contains the enemy death state.")]
    [SerializeField] private Animator animator;

    private EnemyHealth health;
    private EnemyPatrolMovement movement;
    private EnemyPatrolStateMachine stateMachine;
    private EnemyPlayerDetector playerDetector;
    private Rigidbody2D body;
    private Collider2D[] colliders;
    private bool hasHandledDeath;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        movement = GetComponent<EnemyPatrolMovement>();
        stateMachine = GetComponent<EnemyPatrolStateMachine>();
        playerDetector = GetComponent<EnemyPlayerDetector>();
        body = GetComponent<Rigidbody2D>();
        colliders = GetComponentsInChildren<Collider2D>(true);

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Died += HandleDeath;
            if (health.IsDead)
            {
                HandleDeath();
            }
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= HandleDeath;
        }
    }

    private void HandleDeath()
    {
        if (hasHandledDeath)
        {
            return;
        }

        hasHandledDeath = true;

        if (stateMachine != null)
        {
            stateMachine.enabled = false;
        }

        if (movement != null)
        {
            movement.enabled = false;
        }

        if (playerDetector != null)
        {
            playerDetector.enabled = false;
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        if (animator != null && animator.isActiveAndEnabled)
        {
            animator.SetBool(WalkingAnimatorParameter, false);
            animator.ResetTrigger(DeathAnimatorTrigger);
            animator.SetTrigger(DeathAnimatorTrigger);
        }
    }
}
