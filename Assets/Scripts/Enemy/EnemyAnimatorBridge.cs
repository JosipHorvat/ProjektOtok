using UnityEngine;

/// <summary>Maps enemy locomotion states to the Animator's IsWalking boolean.</summary>
public sealed class EnemyAnimatorBridge : MonoBehaviour
{
    private const string IsWalkingParameter = "IsWalking";

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
            animator.SetBool(IsWalkingParameter, stateMachine != null && stateMachine.CurrentState == EnemyPatrolState.Walk);
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
            animator.SetBool(IsWalkingParameter, state != EnemyPatrolState.Idle);
        }
    }
}
