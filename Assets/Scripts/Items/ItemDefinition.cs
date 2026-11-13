using UnityEngine;

/// <summary>Reusable item data shared by authored definition assets.</summary>
public abstract class ItemDefinition : ScriptableObject
{
    [Header("Identity and presentation")]
    [Tooltip("Stable identifier used to resolve this definition from saved item instances. Do not change after saves depend on it.")]
    [SerializeField] private string stableItemId = string.Empty;
    [Tooltip("Player-facing item name. The asset name is used when this is empty.")]
    [SerializeField] private string displayName = string.Empty;
    [Tooltip("Description shown in inventory and inspection UI.")]
    [TextArea(2, 5)]
    [SerializeField] private string description = string.Empty;
    [Tooltip("Sprite shown in inventory, pickup, and stash UI.")]
    [SerializeField] private Sprite inventoryIcon;
    [Tooltip("Optional tags used for compatibility, filtering, and quest rules.")]
    [SerializeField] private string[] tags = System.Array.Empty<string>();

    [Header("Inventory footprint and stacking")]
    [Tooltip("Item width in carry-grid cells before rotation.")]
    [Min(1)]
    [SerializeField] private int inventoryWidth = 1;
    [Tooltip("Item height in carry-grid cells before rotation.")]
    [Min(1)]
    [SerializeField] private int inventoryHeight = 1;
    [Tooltip("Legacy authoring flag retained for asset compatibility; all inventory items can now rotate.")]
    [HideInInspector]
    [SerializeField] private bool canRotate;
    [Tooltip("Maximum quantity in a stack. Non-stackable definitions should use 1.")]
    [Min(1)]
    [SerializeField] private int maximumStackSize = 1;

    [Header("Storage and merchant rules")]
    [Tooltip("Whether this item can be transferred to safe-room storage.")]
    [SerializeField] private bool canBeStored = true;
    [Tooltip("Whether the player may discard this item, unless a specialized definition protects it.")]
    [SerializeField] private bool canBeDiscarded = true;
    [Tooltip("Whether the item may be sold to a merchant, unless a specialized definition protects it.")]
    [SerializeField] private bool canBeSold;
    [Tooltip("Whether the item may be purchased from a merchant.")]
    [SerializeField] private bool canBePurchased;
    [Tooltip("Prevents this item from being sold even if sale is enabled.")]
    [SerializeField] private bool protectedFromSale;
    [Tooltip("Prevents this item from being discarded even if discard is enabled.")]
    [SerializeField] private bool protectedFromDiscard;
    [Tooltip("Configured merchant purchase value; zero means not priced yet.")]
    [Min(0)]
    [SerializeField] private int buyPrice;
    [Tooltip("Configured merchant sale value; zero means not priced yet.")]
    [Min(0)]
    [SerializeField] private int sellPrice;

    /// <summary>Gets the inventory category represented by this specialized definition type.</summary>
    public abstract ItemCategory Category { get; }

    /// <summary>Gets the stable save identifier.</summary>
    public string StableItemId => stableItemId;

    /// <summary>Gets the display name, falling back to this asset's name when unset.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

    /// <summary>Gets the player-facing item description.</summary>
    public string Description => description;

    /// <summary>Gets the icon used by inventory-related UI.</summary>
    public Sprite InventoryIcon => inventoryIcon;

    /// <summary>Gets optional tags used by compatibility and filtering rules.</summary>
    public string[] Tags => tags;

    /// <summary>Gets the item footprint width in grid cells.</summary>
    public int InventoryWidth => inventoryWidth;

    /// <summary>Gets the item footprint height in grid cells.</summary>
    public int InventoryHeight => inventoryHeight;

    /// <summary>Gets the item footprint as a width-height pair.</summary>
    public Vector2Int InventoryFootprint => new Vector2Int(inventoryWidth, inventoryHeight);

    /// <summary>Gets whether the item can rotate within the grid.</summary>
    public bool CanRotate => true;

    /// <summary>Gets the maximum stack quantity.</summary>
    public int MaximumStackSize => maximumStackSize;

    /// <summary>Gets whether the item can be stored in a separate container.</summary>
    public bool CanBeStored => canBeStored;

    /// <summary>Gets whether the item can be discarded.</summary>
    public virtual bool CanBeDiscarded => canBeDiscarded && !protectedFromDiscard;

    /// <summary>Gets whether the item can be sold.</summary>
    public virtual bool CanBeSold => canBeSold && !protectedFromSale;

    /// <summary>Gets whether the item can be purchased.</summary>
    public bool CanBePurchased => canBePurchased;

    /// <summary>Gets the configured merchant purchase value.</summary>
    public int BuyPrice => buyPrice;

    /// <summary>Gets the configured merchant sale value.</summary>
    public int SellPrice => sellPrice;

    /// <summary>Validates and clamps common item-definition data in the Inspector.</summary>
    protected virtual void OnValidate()
    {
        inventoryWidth = Mathf.Max(1, inventoryWidth);
        inventoryHeight = Mathf.Max(1, inventoryHeight);
        maximumStackSize = Mathf.Max(1, maximumStackSize);
        buyPrice = Mathf.Max(0, buyPrice);
        sellPrice = Mathf.Max(0, sellPrice);

        if (string.IsNullOrWhiteSpace(stableItemId))
        {
            Debug.LogWarning($"{nameof(ItemDefinition)} '{name}' needs a stable item ID.", this);
        }

        if (inventoryIcon == null)
        {
            Debug.LogWarning($"{nameof(ItemDefinition)} '{name}' has no inventory icon assigned.", this);
        }

        if (tags == null)
        {
            tags = System.Array.Empty<string>();
        }
    }
}
