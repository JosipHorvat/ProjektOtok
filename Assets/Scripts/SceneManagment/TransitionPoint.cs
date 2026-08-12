using UnityEngine;

/// <summary>Marks a room exit and starts a scene transition when activated.</summary>
[RequireComponent(typeof(Collider2D))]
public sealed class TransitionPoint : MonoBehaviour, IInteractable
{
    public enum TransitionWhen
    {
        InteractPressed,
        OnTriggerEnter
    }

    [Header("Exit Settings")]
    [SerializeField] private string newSceneName;
    [SerializeField] private SceneLocation destinationEntrance = SceneLocation.A;

    [Header("Activation")]
    [SerializeField] private TransitionWhen transitionWhen = TransitionWhen.InteractPressed;

    private Player playerInTrigger;
    private bool transitionStarted;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Update()
    {
        if (transitionWhen != TransitionWhen.InteractPressed
            || playerInTrigger == null
            || playerInTrigger.InputReader == null)
        {
            return;
        }

        if (playerInTrigger.InputReader.InteractPressedThisFrame)
        {
            Interact();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null)
        {
            return;
        }

        playerInTrigger = player;
        if (transitionWhen == TransitionWhen.OnTriggerEnter)
        {
            Interact();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<Player>() == playerInTrigger)
        {
            playerInTrigger = null;
        }
    }

    /// <summary>Starts this transition for the player currently in the trigger.</summary>
    public void Interact()
    {
        if (transitionStarted || playerInTrigger == null)
        {
            return;
        }

        if (SceneManagement.Instance == null)
        {
            Debug.LogError($"TransitionPoint '{name}': SceneManagement instance not found.");
            return;
        }

        if (string.IsNullOrWhiteSpace(newSceneName))
        {
            Debug.LogError($"TransitionPoint '{name}': destination scene is empty.");
            return;
        }

        transitionStarted = true;
        GameManager.Instance?.LockGameplayInput();
        SceneManagement.Instance.LoadRoom(newSceneName, destinationEntrance);
    }
}
