using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Displays a full-inventory message and exposes an Open Inventory request for the future menu controller.</summary>
[DisallowMultipleComponent]
public sealed class ItemPickupFeedbackUI : MonoBehaviour
{
    public static ItemPickupFeedbackUI Instance { get; private set; }

    /// <summary>Raised when the player chooses Open Inventory from a pickup feedback prompt.</summary>
    public static event Action OpenInventoryRequested;

    [Header("Inventory full dialog")]
    [Tooltip("Full-screen dimmer and dialog root shown when a pickup cannot be accepted.")]
    [SerializeField] private GameObject inventoryFullPanel;
    [Tooltip("Message label describing the pickup that did not fit.")]
    [SerializeField] private Text messageText;
    [Tooltip("Button that requests the inventory menu from its future controller.")]
    [SerializeField] private Button openInventoryButton;
    [Tooltip("Button that dismisses the pickup feedback without opening the inventory.")]
    [SerializeField] private Button closeButton;

    private GameObject previouslySelectedObject;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate {nameof(ItemPickupFeedbackUI)} was disabled.", this);
            enabled = false;
            return;
        }

        Instance = this;
        HideInventoryFull();
        if (openInventoryButton == null || messageText == null || inventoryFullPanel == null)
        {
            Debug.LogWarning($"{nameof(ItemPickupFeedbackUI)} is missing its required panel, message label, or Open Inventory button reference.", this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Shows the full-inventory dialog and preserves the uncollected world item.</summary>
    public static void ShowInventoryFull(ItemDefinition definition, int remainingQuantity)
    {
        if (Instance == null)
        {
            Debug.LogWarning("Inventory Full: the pickup remains in the world, but no pickup feedback UI is active.");
            return;
        }

        Instance.ShowMessage(definition, remainingQuantity, 0);
    }

    /// <summary>Shows the accepted and remaining counts when only part of an ammo pickup fits.</summary>
    public static void ShowPartialPickup(ItemDefinition definition, int acceptedQuantity, int remainingQuantity)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"Partial pickup: accepted {acceptedQuantity}, remaining {remainingQuantity} for '{GetDisplayName(definition)}'.");
            return;
        }

        Instance.ShowMessage(definition, remainingQuantity, acceptedQuantity);
    }

    /// <summary>Hides the pickup message when the player dismisses it.</summary>
    public void DismissInventoryFull()
    {
        HideInventoryFull();
    }

    /// <summary>Closes the message and requests that the inventory menu open.</summary>
    public void RequestOpenInventory()
    {
        HideInventoryFull();
        OpenInventoryRequested?.Invoke();
    }

    private void ShowMessage(ItemDefinition definition, int remainingQuantity, int acceptedQuantity)
    {
        if (inventoryFullPanel == null || messageText == null)
        {
            Debug.LogWarning($"Inventory Full: '{GetDisplayName(definition)}' remains in the world; the feedback UI is not fully wired.", this);
            return;
        }

        string quantityText = remainingQuantity > 1 ? $" ({remainingQuantity} remain)" : string.Empty;
        string itemName = definition != null ? definition.DisplayName : "Item";
        messageText.text = acceptedQuantity > 0
            ? $"{acceptedQuantity} {itemName} collected. Inventory full{quantityText}."
            : $"Inventory full. {itemName}{quantityText} remains in the world.";

        if (!inventoryFullPanel.activeSelf)
        {
            previouslySelectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        }

        inventoryFullPanel.SetActive(true);
        if (EventSystem.current != null && openInventoryButton != null)
        {
            EventSystem.current.SetSelectedGameObject(openInventoryButton.gameObject);
        }
    }

    private void HideInventoryFull()
    {
        if (inventoryFullPanel != null)
        {
            inventoryFullPanel.SetActive(false);
        }

        if (EventSystem.current != null && previouslySelectedObject != null && previouslySelectedObject.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(previouslySelectedObject);
        }

        previouslySelectedObject = null;
    }
    private static string GetDisplayName(ItemDefinition definition)
    {
        return definition != null ? definition.DisplayName : "Item";
    }

}
