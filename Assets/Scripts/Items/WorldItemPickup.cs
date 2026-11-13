using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Interact-driven world pickup that transfers only accepted quantities to player item ownership.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class WorldItemPickup : MonoBehaviour, IInteractable, ISaveManager
{
    [Header("Pickup item")]
    [Tooltip("Shared item definition granted when the player interacts with this pickup.")]
    [SerializeField] private ItemDefinition itemDefinition;
    [Tooltip("Quantity offered by this pickup. A partial pickup leaves the remainder in the world.")]
    [Min(1)]
    [SerializeField] private int quantity = 1;
    [Tooltip("Optional authored stable ID for pickup quantity persistence. When empty, a scene and hierarchy path is used.")]
    [SerializeField] private string persistentSaveId = string.Empty;
    [Tooltip("Optional existing device-aware prompt shown while the player is in pickup range.")]
    [SerializeField] private DeviceInteractionPromptUI interactionPrompt;

    private readonly HashSet<Collider2D> playerCollidersInRange = new HashSet<Collider2D>();
    private Player playerInRange;
    private Collider2D pickupCollider;
    private OneTimeGameEvent oneTimeEvent;
    private bool isProcessingPickup;
    private bool isCollected;

    /// <summary>Raised for each completed pickup attempt with the exact accepted and remaining quantity.</summary>
    public event Action<WorldItemPickup, InventoryOperationResult> PickupAttemptCompleted;

    /// <summary>Gets the definition offered by this pickup.</summary>
    public ItemDefinition ItemDefinition => itemDefinition;

    /// <summary>Gets the amount still present on the world pickup.</summary>
    public int Quantity => quantity;

    private void Awake()
    {
        pickupCollider = GetComponent<Collider2D>();
        if (pickupCollider != null)
        {
            pickupCollider.isTrigger = true;
        }

        if (interactionPrompt == null)
        {
            interactionPrompt = GetComponentInChildren<DeviceInteractionPromptUI>(true);
        }

        oneTimeEvent = GetComponent<OneTimeGameEvent>();
        quantity = Mathf.Max(1, quantity);
        ValidateConfiguration();
    }

    private void Start()
    {
        if (oneTimeEvent != null && OneTimeGameEventManager.Instance != null &&
            OneTimeGameEventManager.Instance.IsEventCompleted(oneTimeEvent.uID))
        {
            isCollected = true;
            gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (isCollected || isProcessingPickup || playerInRange == null || playerInRange.InputReader == null)
        {
            return;
        }

        if (playerInRange.InputReader.InteractPressedThisFrame)
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

    /// <summary>Attempts to collect the item when a player is in range and has a live item inventory.</summary>
    public void Interact()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy || isCollected || isProcessingPickup || playerInRange == null)
        {
            return;
        }

        PlayerItemInventory playerItemInventory = playerInRange.GetComponent<PlayerItemInventory>();
        if (playerItemInventory == null || itemDefinition == null)
        {
            Debug.LogWarning($"World item pickup '{name}' is missing its item definition or the player item inventory.", this);
            return;
        }

        isProcessingPickup = true;
        try
        {
            int requestedQuantity = quantity;
            InventoryOperationResult result = playerItemInventory.TryAddPickupQuantity(itemDefinition, requestedQuantity);
            if (result.AcceptedQuantity > 0)
            {
                quantity -= result.AcceptedQuantity;
            }

            if (quantity <= 0)
            {
                CompletePickup();
            }
            else if (result.AcceptedQuantity == 0)
            {
                ItemPickupFeedbackUI.ShowInventoryFull(itemDefinition, quantity);
            }
            else
            {
                ItemPickupFeedbackUI.ShowPartialPickup(itemDefinition, result.AcceptedQuantity, quantity);
            }

            PickupAttemptCompleted?.Invoke(this, result);
        }
        finally
        {
            isProcessingPickup = false;
        }
    }

    /// <summary>Restores this pickup's remaining world quantity from its stable save identity.</summary>
    public void LoadData(GameData data)
    {
        if (data == null || data.worldPickupQuantities == null)
        {
            return;
        }

        string saveId = GetPersistenceId();
        if (string.IsNullOrWhiteSpace(saveId) || !data.worldPickupQuantities.TryGetValue(saveId, out int savedQuantity))
        {
            return;
        }

        if (savedQuantity < 0)
        {
            Debug.LogWarning($"World item pickup '{name}' ignored invalid saved quantity {savedQuantity} for ID '{saveId}'.", this);
            return;
        }

        quantity = savedQuantity;
        playerCollidersInRange.Clear();
        playerInRange = null;
        isProcessingPickup = false;
        isCollected = savedQuantity == 0;
        if (isCollected)
        {
            interactionPrompt?.Hide();
            if (pickupCollider != null)
            {
                pickupCollider.enabled = false;
            }

            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        if (pickupCollider != null)
        {
            pickupCollider.enabled = true;
            pickupCollider.isTrigger = true;
        }
    }

    /// <summary>Saves this pickup's current remaining quantity without replacing other scene pickup records.</summary>
    public void SaveData(ref GameData data)
    {
        if (data == null)
        {
            data = new GameData();
        }

        if (data.worldPickupQuantities == null)
        {
            data.worldPickupQuantities = new SerializableDictionary<string, int>();
        }

        string saveId = GetPersistenceId();
        if (string.IsNullOrWhiteSpace(saveId))
        {
            Debug.LogWarning($"World item pickup '{name}' could not save because it has no stable persistence ID.", this);
            return;
        }

        data.worldPickupQuantities[saveId] = Mathf.Max(0, quantity);
    }

    private string GetPersistenceId()
    {
        if (!string.IsNullOrWhiteSpace(persistentSaveId))
        {
            return persistentSaveId.Trim();
        }

        string hierarchyPath = string.Empty;
        Transform currentTransform = transform;
        while (currentTransform != null)
        {
            hierarchyPath = $"/{currentTransform.GetSiblingIndex()}:{currentTransform.name}{hierarchyPath}";
            currentTransform = currentTransform.parent;
        }

        return $"{gameObject.scene.name}{hierarchyPath}";
    }

    private void RegisterPlayerCollider(Collider2D other)
    {
        if (isCollected || other == null)
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

    private void CompletePickup()
    {
        isCollected = true;
        playerCollidersInRange.Clear();
        playerInRange = null;
        interactionPrompt?.Hide();
        if (oneTimeEvent != null)
        {
            oneTimeEvent.MarkCompleted();
        }

        if (pickupCollider != null)
        {
            pickupCollider.enabled = false;
        }

        gameObject.SetActive(false);
    }

    private void ValidateConfiguration()
    {
        if (itemDefinition == null)
        {
            Debug.LogWarning($"World item pickup '{name}' has no item definition assigned.", this);
        }

        if (quantity < 1)
        {
            Debug.LogWarning($"World item pickup '{name}' must offer at least one item.", this);
        }

        if (pickupCollider == null || !pickupCollider.isTrigger)
        {
            Debug.LogWarning($"World item pickup '{name}' requires a trigger collider.", this);
        }
    }
}
