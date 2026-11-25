using UnityEngine;

/// <summary>Maps patrol and chase states to the Animator's locomotion parameters.</summary>
public sealed class EnemyAnimatorBridge : MonoBehaviour
{
    private const string IsWalkingParameter = "IsWalking";
    private const string IsChasingParameter = "IsChasing";

    [Header("References")]
    [Tooltip("Animator receiving the IsWalking boolean. Automatically cached from this GameObject or its children when left empty.")]
    [SerializeField] private Animator animator;

    private EnemyPatrolStateMachine stateMachine;
    private bool hasWarnedMissingAnimator;
    private bool hasWarnedMissingStateMachine;

    private void OnEnable()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (stateMachine == null)
        {
            stateMachine = GetComponent<EnemyPatrolStateMachine>();
        }

        if (animator == null && !hasWarnedMissingAnimator)
        {
            Debug.LogWarning($"{nameof(EnemyAnimatorBridge)} on '{name}' requires an Animator on this GameObject or a child.", this);
            hasWarnedMissingAnimator = true;
        }

        if (stateMachine == null && !hasWarnedMissingStateMachine)
        {
            Debug.LogWarning($"{nameof(EnemyAnimatorBridge)} on '{name}' requires an {nameof(EnemyPatrolStateMachine)} component.", this);
            hasWarnedMissingStateMachine = true;
        }

        if (stateMachine != null)
        {
            stateMachine.StateChanged += HandleStateChanged;
        }

        if (animator != null)
        {
            ApplyLocomotionState(stateMachine != null ? stateMachine.CurrentState : EnemyPatrolState.Idle);
        }
    }

    private void OnDisable()
    {
        if (stateMachine != null)
        {
            stateMachine.StateChanged -= HandleStateChanged;
        }
    }

    private void HandleStateChanged(EnemyPatrolState state)
    {
        if (animator != null)
        {
            ApplyLocomotionState(state);
        }
    }

    private void ApplyLocomotionState(EnemyPatrolState state)
    {
        animator.SetBool(IsWalkingParameter, state != EnemyPatrolState.Idle);
        animator.SetBool(IsChasingParameter, state == EnemyPatrolState.Chase);
    }
}
