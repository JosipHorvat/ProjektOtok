using UnityEngine;

/// <summary>One-time interactive lever that remains visible after being pulled.</summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(OneTimeGameEvent))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PersistentLever : MonoBehaviour, IInteractable
{
    private SpriteRenderer spriteRenderer;
    private Player playerInTrigger;
    private bool isPulled;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        OneTimeGameEvent oneTimeEvent = GetComponent<OneTimeGameEvent>();
        isPulled = OneTimeGameEventManager.Instance != null && OneTimeGameEventManager.Instance.IsEventCompleted(oneTimeEvent.uID);
        UpdateVisual();
        Debug.Log($"[Lever] State loaded: {(isPulled ? "Pulled" : "Not pulled")}");
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
        if (detectedPlayer != null)
        {
            playerInTrigger = detectedPlayer;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Player exitingPlayer = other.GetComponentInParent<Player>();
        if (exitingPlayer == playerInTrigger) playerInTrigger = null;
    }

    /// <summary>Pulls the lever once, changes it to green, and keeps it visible in the scene.</summary>
    public void Interact()
    {
        if (isPulled) return;
        isPulled = true;
        UpdateVisual();
        GetComponent<OneTimeGameEvent>().MarkCompleted();
        Debug.Log("[Lever] Pulled once. State saved as green; lever remains visible.");
    }

    private void UpdateVisual()
    {
        if (spriteRenderer != null) spriteRenderer.color = isPulled ? Color.green : Color.blue;
    }
}
