using UnityEngine;

/// <summary>One-time interactive lever that remains pulled after its event is saved.</summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(OneTimeGameEvent))]
public sealed class PersistentLever : MonoBehaviour, IInteractable
{
    private static readonly int IsPulledHash = Animator.StringToHash("IsPulled");
    private const string IdleState = "PersistantLever_Idle";
    private const string PulledState = "PersistantLever_Pulled";

    private Animator animator;
    private OneTimeGameEvent oneTimeEvent;
    private DeviceInteractionPromptUI interactionPrompt;
    private Player playerInTrigger;
    private bool isPulled;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        oneTimeEvent = GetComponent<OneTimeGameEvent>();
        animator = GetComponentInChildren<Animator>(true);
        interactionPrompt = GetComponentInChildren<DeviceInteractionPromptUI>(true);

        if (animator == null)
        {
            Debug.LogWarning($"PersistentLever '{name}': no child Animator was found.", this);
        }
    }

    private void Start()
    {
        isPulled = OneTimeGameEventManager.Instance != null
            && OneTimeGameEventManager.Instance.IsEventCompleted(oneTimeEvent.uID);

        ApplyState();
    }

    private void Update()
    {
        if (isPulled || playerInTrigger == null || playerInTrigger.InputReader == null)
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
        Player detectedPlayer = other.GetComponentInParent<Player>();
        if (detectedPlayer != null && !isPulled)
        {
            playerInTrigger = detectedPlayer;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<Player>() == playerInTrigger)
        {
            playerInTrigger = null;
        }
    }

    /// <summary>Pulls the lever once and saves its pulled state.</summary>
    public void Interact()
    {
        if (isPulled)
        {
            return;
        }

        isPulled = true;
        playerInTrigger = null;
        animator?.SetBool(IsPulledHash, true);
        oneTimeEvent.MarkCompleted();
        DisableInteractionPrompt();
    }

    private void ApplyState()
    {
        if (animator != null)
        {
            animator.SetBool(IsPulledHash, isPulled);
            animator.Play(isPulled ? PulledState : IdleState, 0, isPulled ? 1f : 0f);
            animator.Update(0f);
        }

        if (isPulled)
        {
            DisableInteractionPrompt();
        }
    }

    private void DisableInteractionPrompt()
    {
        if (interactionPrompt == null)
        {
            return;
        }

        interactionPrompt.Hide();
        interactionPrompt.gameObject.SetActive(false);
    }
}
