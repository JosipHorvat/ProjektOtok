using System.Collections.Generic;
using UnityEngine;

/// <summary>Interactable upgrade and crafting station with a configured recipe list.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class WorkbenchStation : MonoBehaviour, IInteractable
{
    [Header("Workbench services")]
    [SerializeField] private List<ItemRecipeDefinition> recipes = new List<ItemRecipeDefinition>();
    [Tooltip("Optional device-aware prompt displayed while the player is in range.")]
    [SerializeField] private DeviceInteractionPromptUI interactionPrompt;

    private readonly HashSet<Collider2D> playerCollidersInRange = new HashSet<Collider2D>();
    private Player playerInRange;
    private CircleCollider2D interactionCollider;

    /// <summary>Gets the player currently inside this workbench's interaction trigger.</summary>
    public Player PlayerInRange => playerInRange;

    /// <summary>Gets the recipes configured for this workbench.</summary>
    public IReadOnlyList<ItemRecipeDefinition> Recipes => recipes;

    private void Awake()
    {
        interactionCollider = GetComponent<CircleCollider2D>();
        interactionCollider.isTrigger = true;
        if (interactionPrompt == null)
        {
            interactionPrompt = GetComponentInChildren<DeviceInteractionPromptUI>(true);
        }

        if (recipes == null)
        {
            recipes = new List<ItemRecipeDefinition>();
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

    private void OnDisable()
    {
        playerCollidersInRange.Clear();
        playerInRange = null;
        interactionPrompt?.Hide();
    }

    /// <summary>Opens this workbench for the player currently in interaction range.</summary>
    public void Interact()
    {
        if (!isActiveAndEnabled || playerInRange == null ||
            playerInRange.GetComponent<PlayerItemInventory>() == null)
        {
            return;
        }

        if (IngameUIManager.Instance == null)
        {
            Debug.LogWarning($"Workbench '{name}' could not open because the persistent in-game UI is unavailable.", this);
            return;
        }

        IngameUIManager.Instance.OpenWorkbenchMenu(this);
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
