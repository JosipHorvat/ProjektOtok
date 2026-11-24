using System;
using UnityEngine;

/// <summary>Discrete states used by the basic enemy patrol.</summary>
public enum EnemyPatrolState
{
    Idle,
    Walk,
    Chase
}

/// <summary>Owns idle and walking transitions for a simple back-and-forth patrol.</summary>
public sealed class EnemyPatrolStateMachine : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("How long the enemy waits in its initial idle state before its first walk.")]
    [SerializeField, Min(0f)] private float initialIdleDuration = 1f;

    [Header("Chase")]
    [Tooltip("How long the enemy continues chasing after losing sight of the player before returning to patrol.")]
    [SerializeField, Min(0f)] private float lostPlayerGraceDuration = 1.5f;

    [Header("Wall Response")]
    [Tooltip("Chance to pause after turning around at a wall. Otherwise the enemy immediately walks back.")]
    [SerializeField, Range(0f, 1f)] private float idleChanceAfterTurnaround = 0.5f;
    [Tooltip("Shortest pause after turning around at a wall.")]
    [SerializeField, Min(0f)] private float minimumWallIdleDuration = 0.5f;
    [Tooltip("Longest pause after turning around at a wall.")]
    [SerializeField, Min(0f)] private float maximumWallIdleDuration = 1.5f;

    private EnemyPatrolMovement movement;
    private EnemyPlayerDetector playerDetector;
    private CharacterStagger characterStagger;
    private Transform chaseTarget;
    private float idleTimeRemaining;
    private float lostPlayerTime;
    private bool hasWarnedMissingMovement;

    /// <summary>The state currently active on this enemy.</summary>
    public EnemyPatrolState CurrentState { get; private set; } = EnemyPatrolState.Idle;

    /// <summary>Raised after the active patrol state changes.</summary>
    public event Action<EnemyPatrolState> StateChanged;

    private void Awake()
    {
        movement = GetComponent<EnemyPatrolMovement>();
        playerDetector = GetComponent<EnemyPlayerDetector>();
        characterStagger = GetComponent<CharacterStagger>();
        CurrentState = EnemyPatrolState.Idle;
        idleTimeRemaining = initialIdleDuration;

        if (movement == null && !hasWarnedMissingMovement)
        {
            Debug.LogWarning($"{nameof(EnemyPatrolStateMachine)} on '{name}' requires an {nameof(EnemyPatrolMovement)} component.", this);
            hasWarnedMissingMovement = true;
        }
    }

    private void OnEnable()
    {
        if (playerDetector != null)
        {
            playerDetector.PlayerDetected += HandlePlayerDetected;
            playerDetector.PlayerLost += HandlePlayerLost;
        }
    }

    private void OnDisable()
    {
        if (playerDetector != null)
        {
            playerDetector.PlayerDetected -= HandlePlayerDetected;
            playerDetector.PlayerLost -= HandlePlayerLost;
        }
    }

    private void Start()
    {
        if (movement != null && CurrentState != EnemyPatrolState.Chase)
        {
            movement.SetWalking(false);
        }
    }

    private void Update()
    {
        if (movement == null || characterStagger != null && characterStagger.IsStaggered)
        {
            return;
        }

        if (CurrentState == EnemyPatrolState.Chase)
        {
            UpdateChaseState();
            return;
        }

        if (CurrentState == EnemyPatrolState.Walk)
        {
            if (movement.HasObstacleAhead())
            {
                TurnAroundAndChooseResponse();
            }

            return;
        }

        idleTimeRemaining -= Time.deltaTime;
        if (idleTimeRemaining > 0f)
        {
            return;
        }

        if (movement.HasObstacleAhead())
        {
            TurnAroundAndChooseResponse();
            return;
        }

        TransitionTo(EnemyPatrolState.Walk);
    }

    private void UpdateChaseState()
    {
        if (playerDetector != null && playerDetector.CurrentTarget != null)
        {
            chaseTarget = playerDetector.CurrentTarget.transform;
            lostPlayerTime = 0f;
            movement.SetChasing(chaseTarget);
            return;
        }

        lostPlayerTime += Time.deltaTime;
        if (lostPlayerTime >= lostPlayerGraceDuration)
        {
            ReturnToPatrol();
        }
    }

    private void HandlePlayerDetected(Player player)
    {
        if (player == null || movement == null)
        {
            return;
        }

        chaseTarget = player.transform;
        lostPlayerTime = 0f;
        TransitionTo(EnemyPatrolState.Chase);
    }

    private void HandlePlayerLost(Player player)
    {
        if (player != null && chaseTarget == player.transform)
        {
            lostPlayerTime = 0f;
        }
    }

    private void ReturnToPatrol()
    {
        chaseTarget = null;
        if (movement.HasObstacleAhead())
        {
            movement.TurnAround();
        }

        TransitionTo(EnemyPatrolState.Walk);
    }

    private void TurnAroundAndChooseResponse()
    {
        movement.TurnAround();

        if (UnityEngine.Random.value < idleChanceAfterTurnaround)
        {
            float minimumDuration = Mathf.Min(minimumWallIdleDuration, maximumWallIdleDuration);
            float maximumDuration = Mathf.Max(minimumWallIdleDuration, maximumWallIdleDuration);
            idleTimeRemaining = UnityEngine.Random.Range(minimumDuration, maximumDuration);
            TransitionTo(EnemyPatrolState.Idle);
            return;
        }

        TransitionTo(EnemyPatrolState.Walk);
    }

    private void TransitionTo(EnemyPatrolState nextState)
    {
        if (CurrentState == nextState)
        {
            return;
        }

        CurrentState = nextState;
        if (nextState == EnemyPatrolState.Chase)
        {
            movement.SetChasing(chaseTarget);
        }
        else
        {
            movement.SetWalking(nextState == EnemyPatrolState.Walk);
        }

        StateChanged?.Invoke(CurrentState);
    }
}
