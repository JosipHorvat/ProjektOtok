using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Persistent Credits shop with atomic carry-grid purchases and eligible item sales.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class ShopStation : MonoBehaviour, IInteractable, ISaveManager
{
    [Header("Persistent shop identity")]
    [Tooltip("Stable unique ID for this shop across scene and save loads. Do not change after saves depend on it.")]
    [SerializeField] private string persistentShopId = string.Empty;

    [Header("Catalog and stock")]
    [SerializeField] private List<ShopStockEntry> stockEntries = new List<ShopStockEntry>();
    [Tooltip("Optional device-aware prompt displayed while the player is in range.")]
    [SerializeField] private DeviceInteractionPromptUI interactionPrompt;

    private readonly HashSet<Collider2D> playerCollidersInRange = new HashSet<Collider2D>();
    private Player playerInRange;
    private CircleCollider2D interactionCollider;
    private bool stockInitialized;

    /// <summary>Gets the player currently inside this shop's interaction trigger.</summary>
    public Player PlayerInRange => playerInRange;

    /// <summary>Gets the stable key used to persist the station's stock counts.</summary>
    public string PersistentShopId => GetPersistentShopId();

    /// <summary>Gets the configured shop catalog entries.</summary>
    public IReadOnlyList<ShopStockEntry> StockEntries
    {
        get
        {
            EnsureStockInitialized();
            return stockEntries.AsReadOnly();
        }
    }

    private void Awake()
    {
        interactionCollider = GetComponent<CircleCollider2D>();
        interactionCollider.isTrigger = true;
        if (interactionPrompt == null)
        {
            interactionPrompt = GetComponentInChildren<DeviceInteractionPromptUI>(true);
        }

        EnsureStockInitialized();
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

    /// <summary>Opens this Credits shop for the player currently in interaction range.</summary>
    public void Interact()
    {
        if (!isActiveAndEnabled || playerInRange == null ||
            playerInRange.GetComponent<PlayerItemInventory>() == null)
        {
            return;
        }

        if (IngameUIManager.Instance == null)
        {
            Debug.LogWarning($"Shop '{name}' could not open because the persistent in-game UI is unavailable.", this);
            return;
        }

        IngameUIManager.Instance.OpenShopMenu(this);
    }

    /// <summary>Purchases one configured package, rejecting it without changes when stock, funds, or grid space is insufficient.</summary>
    public bool TryBuy(PlayerItemInventory playerInventory, string stableItemId, out string resultMessage)
    {
        resultMessage = string.Empty;
        EnsureStockInitialized();
        if (playerInventory == null)
        {
            resultMessage = "No player inventory is available.";
            return false;
        }

        if (!TryGetUniqueValidEntry(stableItemId, out ShopStockEntry entry, out bool ambiguous, out _))
        {
            resultMessage = ambiguous
                ? "This item has an ambiguous catalog entry and cannot be purchased."
                : "This item is not in the shop catalog or its catalog entry is invalid.";
            return false;
        }

        ItemDefinition definition = entry.ItemDefinition;
        int purchaseQuantity = Mathf.Max(1, entry.QuantityPerPurchase);
        if (!definition.CanBePurchased || definition.BuyPrice <= 0)
        {
            resultMessage = $"{definition.DisplayName} is not available for purchase.";
            return false;
        }

        if (entry.CurrentStock < purchaseQuantity)
        {
            resultMessage = $"{definition.DisplayName} is out of stock.";
            return false;
        }

        long totalCostValue = (long)definition.BuyPrice * purchaseQuantity;
        if (totalCostValue > int.MaxValue)
        {
            resultMessage = "The configured price is too large to process.";
            return false;
        }

        int totalCost = (int)totalCostValue;
        if (playerInventory.Credits < totalCost)
        {
            resultMessage = $"Not enough Credits. Cost: {totalCost}; balance: {playerInventory.Credits}.";
            return false;
        }

        bool purchased = playerInventory.TryPurchaseItem(definition, purchaseQuantity, totalCost, out InventoryOperationFailure failure);
        if (!purchased)
        {
            resultMessage = failure == InventoryOperationFailure.NoSpace
                ? "Purchase rejected: the item does not fit in the carry grid. Credits and shop stock were not changed."
                : $"Purchase rejected: {failure}. Credits and shop stock were not changed.";
            return false;
        }

        entry.SetCurrentStock(entry.CurrentStock - purchaseQuantity);
        resultMessage = $"Purchased {definition.DisplayName} ×{purchaseQuantity} for {totalCost} Credits.";
        return true;
    }

    /// <summary>Sells an eligible carried stack atomically; unique equipment is blocked until shop stock retains its instance state.</summary>
    public bool TrySell(PlayerItemInventory playerInventory, string instanceId, out string resultMessage)
    {
        resultMessage = string.Empty;
        EnsureStockInitialized();
        if (playerInventory == null || playerInventory.Inventory == null)
        {
            resultMessage = "No player inventory is available.";
            return false;
        }

        ItemInstance item = playerInventory.Inventory.FindItem(instanceId);
        if (item == null)
        {
            resultMessage = "This item is no longer in the carry grid.";
            return false;
        }

        ItemDefinition definition = item.Definition;
        if (definition == null || string.IsNullOrWhiteSpace(definition.StableItemId))
        {
            resultMessage = "This item has invalid catalog identity and cannot be sold.";
            return false;
        }

        if (!TryGetUniqueValidEntry(definition.StableItemId, out ShopStockEntry catalogEntry, out bool ambiguous, out bool hasMatchingEntry))
        {
            if (ambiguous || hasMatchingEntry)
            {
                resultMessage = ambiguous
                    ? "This item has an ambiguous shop catalog entry and cannot be sold."
                    : "This item has an invalid shop catalog entry and cannot be sold.";
                return false;
            }
        }

        if (!definition.CanBeSold || definition.SellPrice <= 0)
        {
            resultMessage = $"{definition.DisplayName} is protected or not eligible for sale.";
            return false;
        }

        if (item.IsEquipped)
        {
            resultMessage = $"Unequip {definition.DisplayName} before selling it.";
            return false;
        }

        if (definition.Category == ItemCategory.Weapon || definition.Category == ItemCategory.Equipment ||
            definition.Category == ItemCategory.Attachment || definition.Category == ItemCategory.CapacityUpgrade)
        {
            resultMessage = $"{definition.DisplayName} has per-instance state the shop does not preserve yet, so it cannot be sold.";
            return false;
        }

        long saleValueLong = (long)definition.SellPrice * item.Quantity;
        if (saleValueLong <= 0 || saleValueLong > int.MaxValue || (long)playerInventory.Credits + saleValueLong > int.MaxValue)
        {
            resultMessage = "The sale value exceeds the supported Credits balance.";
            return false;
        }

        bool sold = playerInventory.TrySellCarryItem(instanceId, definition.SellPrice, out int quantitySold, out int saleValue, out string failureReason);
        if (!sold)
        {
            resultMessage = failureReason;
            return false;
        }

        ShopStockEntry entry = catalogEntry;
        if (entry != null)
        {
            entry.SetCurrentStock((int)Math.Min(int.MaxValue, (long)entry.CurrentStock + quantitySold));
        }

        resultMessage = $"Sold {definition.DisplayName} ×{quantitySold} for {saleValue} Credits.";
        return true;
    }

    /// <summary>Restores this station's item counts from the active save slot.</summary>
    public void LoadData(GameData data)
    {
        EnsureStockInitialized();
        if (data == null || data.shopStockCounts == null)
        {
            return;
        }

        for (int index = 0; index < stockEntries.Count; index++)
        {
            ShopStockEntry entry = stockEntries[index];
            if (!IsCatalogEntryUniqueAndValid(entry))
            {
                continue;
            }

            string key = GetStockKey(entry.ItemDefinition.StableItemId);
            if (data.shopStockCounts.TryGetValue(key, out int savedStock) && savedStock >= 0)
            {
                entry.SetCurrentStock(savedStock);
            }
        }
    }

    /// <summary>Writes this station's current item counts without replacing other shops' stock entries.</summary>
    public void SaveData(ref GameData data)
    {
        EnsureStockInitialized();
        if (data == null)
        {
            data = new GameData();
        }

        if (data.shopStockCounts == null)
        {
            data.shopStockCounts = new SerializableDictionary<string, int>();
        }

        for (int index = 0; index < stockEntries.Count; index++)
        {
            ShopStockEntry entry = stockEntries[index];
            if (IsCatalogEntryUniqueAndValid(entry))
            {
                data.shopStockCounts[GetStockKey(entry.ItemDefinition.StableItemId)] = Mathf.Max(0, entry.CurrentStock);
            }
        }
    }

    /// <summary>Returns a catalog entry by the definition's stable item ID.</summary>
    public ShopStockEntry FindEntry(string stableItemId)
    {
        EnsureStockInitialized();
        if (string.IsNullOrWhiteSpace(stableItemId))
        {
            return null;
        }

        if (TryGetUniqueValidEntry(stableItemId, out ShopStockEntry entry, out _, out _))
        {
            return entry;
        }

        return null;
    }

    private bool TryGetUniqueValidEntry(string stableItemId, out ShopStockEntry entry, out bool ambiguous, out bool hasMatchingEntry)
    {
        entry = null;
        ambiguous = false;
        hasMatchingEntry = false;
        if (string.IsNullOrWhiteSpace(stableItemId))
        {
            return false;
        }

        for (int index = 0; index < stockEntries.Count; index++)
        {
            ShopStockEntry candidate = stockEntries[index];
            if (candidate == null || candidate.ItemDefinition == null ||
                !string.Equals(candidate.ItemDefinition.StableItemId, stableItemId, StringComparison.Ordinal))
            {
                continue;
            }

            hasMatchingEntry = true;
            if (entry != null)
            {
                ambiguous = true;
                return false;
            }

            entry = candidate;
        }

        if (entry == null || !IsEntryValid(entry))
        {
            entry = null;
            return false;
        }

        return true;
    }

    private bool IsCatalogEntryUniqueAndValid(ShopStockEntry entry)
    {
        if (entry == null || entry.ItemDefinition == null || string.IsNullOrWhiteSpace(entry.ItemDefinition.StableItemId))
        {
            return false;
        }

        return TryGetUniqueValidEntry(entry.ItemDefinition.StableItemId, out ShopStockEntry uniqueEntry, out _, out _) &&
               uniqueEntry == entry;
    }

    private static bool IsEntryValid(ShopStockEntry entry)
    {
        return entry != null && entry.ItemDefinition != null &&
               !string.IsNullOrWhiteSpace(entry.ItemDefinition.StableItemId) &&
               entry.QuantityPerPurchase > 0 && entry.CurrentStock >= 0;
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

    private void EnsureStockInitialized()
    {
        if (stockInitialized)
        {
            return;
        }

        stockInitialized = true;
        if (stockEntries == null)
        {
            stockEntries = new List<ShopStockEntry>();
        }

        for (int index = 0; index < stockEntries.Count; index++)
        {
            ShopStockEntry entry = stockEntries[index];
            if (entry == null)
            {
                stockEntries[index] = new ShopStockEntry();
                entry = stockEntries[index];
            }

            entry.InitializeStock();
        }
    }

    private string GetPersistentShopId()
    {
        if (!string.IsNullOrWhiteSpace(persistentShopId))
        {
            return persistentShopId.Trim();
        }

        return $"{gameObject.scene.name}/{gameObject.name}";
    }

    private string GetStockKey(string stableItemId)
    {
        return $"{GetPersistentShopId()}|{stableItemId}";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        stockInitialized = false;
        if (stockEntries == null)
        {
            stockEntries = new List<ShopStockEntry>();
        }

        Dictionary<string, int> itemIdCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < stockEntries.Count; index++)
        {
            ShopStockEntry entry = stockEntries[index];
            if (entry == null)
            {
                Debug.LogWarning($"Shop '{name}' catalog entry at index {index} is missing.", this);
                continue;
            }

            entry.Validate();
            ItemDefinition definition = entry.ItemDefinition;
            if (definition == null)
            {
                Debug.LogWarning($"Shop '{name}' catalog entry at index {index} has no ItemDefinition and cannot be used.", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(definition.StableItemId))
            {
                Debug.LogWarning($"Shop '{name}' catalog entry '{definition.name}' has no stable item ID and cannot be used.", this);
                continue;
            }

            itemIdCounts.TryGetValue(definition.StableItemId, out int count);
            itemIdCounts[definition.StableItemId] = count + 1;
        }

        foreach (KeyValuePair<string, int> itemIdCount in itemIdCounts)
        {
            if (itemIdCount.Value > 1)
            {
                Debug.LogWarning($"Shop '{name}' has {itemIdCount.Value} catalog entries with stable item ID '{itemIdCount.Key}'. Entries with this ID cannot be bought or sold until the ID is unique.", this);
            }
        }
    }
#endif
}

/// <summary>One catalog item, package size, and persistent quantity of shop stock.</summary>
[Serializable]
public sealed class ShopStockEntry
{
    [SerializeField] private ItemDefinition itemDefinition;
    [Min(0)] [SerializeField] private int startingStock = 10;
    [Min(1)] [SerializeField] private int quantityPerPurchase = 1;
    [SerializeField] private int currentStock = -1;
    [NonSerialized] private bool stockInitialized;

    /// <summary>Gets the definition offered by this catalog entry.</summary>
    public ItemDefinition ItemDefinition => itemDefinition;

    /// <summary>Gets the remaining available quantity.</summary>
    public int CurrentStock => currentStock;

    /// <summary>Gets the number of item units provided by one purchase.</summary>
    public int QuantityPerPurchase => quantityPerPurchase;

    internal void SetCurrentStock(int value)
    {
        currentStock = Mathf.Max(0, value);
        stockInitialized = true;
    }

    internal void InitializeStock()
    {
        if (!stockInitialized)
        {
            currentStock = currentStock >= 0 ? currentStock : Mathf.Max(0, startingStock);
            stockInitialized = true;
        }

        startingStock = Mathf.Max(0, startingStock);
        quantityPerPurchase = Mathf.Max(1, quantityPerPurchase);
        currentStock = Mathf.Max(0, currentStock);
    }

    internal void Validate()
    {
        if (startingStock < 0)
        {
            Debug.LogWarning($"Shop entry '{(itemDefinition != null ? itemDefinition.name : "<missing definition>")}' has negative starting stock; it will be clamped to zero.", itemDefinition);
        }

        if (quantityPerPurchase < 1)
        {
            Debug.LogWarning($"Shop entry '{(itemDefinition != null ? itemDefinition.name : "<missing definition>")}' has an invalid purchase quantity; it will be clamped to one.", itemDefinition);
        }

        startingStock = Mathf.Max(0, startingStock);
        quantityPerPurchase = Mathf.Max(1, quantityPerPurchase);
        if (itemDefinition != null && itemDefinition.CanBePurchased && itemDefinition.BuyPrice <= 0)
        {
            Debug.LogWarning($"Shop entry '{itemDefinition.name}' is marked purchasable but has no positive BuyPrice.", itemDefinition);
        }
    }

    // Stock counts are persisted by ShopStation and initialized from startingStock on first load.
}
