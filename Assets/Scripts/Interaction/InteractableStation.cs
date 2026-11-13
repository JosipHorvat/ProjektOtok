using System.Collections.Generic;
using UnityEngine;

/// <summary>Interactable shop or workbench station that opens its configured prototype menu.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class InteractableStation : MonoBehaviour, IInteractable
{
    [Header("Station menu")]
    [SerializeField] private string stationTitle = "Station";
    [TextArea(3, 8)]
    [SerializeField] private string prototypeMessage = "This station is not configured yet.";
    [Tooltip("Optional device-aware interaction prompt on this station or one of its children.")]
    [SerializeField] private DeviceInteractionPromptUI interactionPrompt;

    private readonly HashSet<Collider2D> playerCollidersInRange = new HashSet<Collider2D>();
    private Player playerInRange;
    private CircleCollider2D interactionCollider;

    private void Awake()
    {
        interactionCollider = GetComponent<CircleCollider2D>();
        interactionCollider.isTrigger = true;
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

    /// <summary>Opens the station's simple prototype menu for the player in range.</summary>
    public void Interact()
    {
        if (!isActiveAndEnabled || playerInRange == null ||
            playerInRange.GetComponent<PlayerItemInventory>() == null)
        {
            return;
        }

        if (IngameUIManager.Instance == null)
        {
            Debug.LogWarning($"Station '{name}' could not open because the persistent in-game UI is unavailable.", this);
            return;
        }

        IngameUIManager.Instance.OpenStationPrototype(stationTitle, prototypeMessage);
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
