using System.Collections.Generic;
using UnityEngine;

/// <summary>Interactable safe-room access point for the player's persistent, unlimited stash.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class SafeRoomStash : MonoBehaviour, IInteractable
{
    [Tooltip("Optional device-aware prompt displayed while the player is in range.")]
    [SerializeField] private DeviceInteractionPromptUI interactionPrompt;

    private readonly HashSet<Collider2D> playerCollidersInRange = new HashSet<Collider2D>();
    private Player playerInRange;
    private CircleCollider2D accessCollider;

    /// <summary>Gets the player currently in interaction range, if any.</summary>
    public Player PlayerInRange => playerInRange;

    private void Awake()
    {
        accessCollider = GetComponent<CircleCollider2D>();
        accessCollider.isTrigger = true;
        if (interactionPrompt == null)
        {
            interactionPrompt = GetComponentInChildren<DeviceInteractionPromptUI>(true);
        }
    }

    private void Update()
    {
        if (playerInRange != null && playerInRange.InputReader != null &&
            playerInRange.InputReader.InteractPressedThisFrame)
        {
            Interact();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        RegisterPlayerCollider(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        RegisterPlayerCollider(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Player detectedPlayer = other.GetComponentInParent<Player>();
        if (detectedPlayer == null || detectedPlayer != playerInRange)
        {
            return;
        }

        playerCollidersInRange.Remove(other);
        if (playerCollidersInRange.Count == 0)
        {
            playerInRange = null;
            interactionPrompt?.Hide();
        }
    }

    /// <summary>Opens the stash menu while a player is inside the safe-room access trigger.</summary>
    public void Interact()
    {
        if (!isActiveAndEnabled || playerInRange == null ||
            playerInRange.GetComponent<PlayerItemInventory>() == null)
        {
            return;
        }

        if (IngameUIManager.Instance == null)
        {
            Debug.LogWarning($"Safe-room stash '{name}' could not open because the persistent in-game UI is unavailable.", this);
            return;
        }

        IngameUIManager.Instance.OpenStashMenu(this);
    }

    private void RegisterPlayerCollider(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        Player detectedPlayer = other.GetComponentInParent<Player>();
        if (detectedPlayer == null)
        {
            return;
        }

        playerCollidersInRange.Add(other);
        if (playerInRange == detectedPlayer)
        {
            return;
        }

        playerInRange = detectedPlayer;
        interactionPrompt?.Show();
    }
}
