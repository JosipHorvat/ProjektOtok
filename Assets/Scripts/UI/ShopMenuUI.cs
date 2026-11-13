using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Renders the Credits catalog and sellable carry items for one configured shop station.</summary>
[DisallowMultipleComponent]
public sealed class ShopMenuUI : MonoBehaviour
{
    private const float ShopRowHeight = 48f;
    private static readonly Color BackdropColor = new Color(0.01f, 0.014f, 0.02f, 0.9f);
    private static readonly Color CardColor = new Color(0.055f, 0.065f, 0.08f, 0.99f);
    private static readonly Color ButtonColor = new Color(0.16f, 0.2f, 0.26f, 1f);
    private static readonly Color DisabledButtonColor = new Color(0.09f, 0.11f, 0.14f, 1f);
    private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.98f, 1f);

    private readonly List<GameObject> rows = new List<GameObject>();
    private GameObject menuRoot;
    private RectTransform listContent;
    private RectTransform listViewport;
    private ScrollRect listScrollRect;
    private Text titleText;
    private Text balanceText;
    private Text statusText;
    private Font uiFont;
    private ShopStation activeShop;
    private PlayerItemInventory activePlayerInventory;

    /// <summary>Gets whether the shop owns the current modal UI state.</summary>
    public bool IsOpen => menuRoot != null && menuRoot.activeSelf;

    private void Awake()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildMenu();
        menuRoot.SetActive(false);
    }

    /// <summary>Opens this station's catalog for the player currently in range.</summary>
    public bool Open(ShopStation shopStation)
    {
        Player player = shopStation != null ? shopStation.PlayerInRange : null;
        PlayerItemInventory playerInventory = player != null ? player.GetComponent<PlayerItemInventory>() : null;
        if (shopStation == null || playerInventory == null)
        {
            return false;
        }

        activeShop = shopStation;
        activePlayerInventory = playerInventory;
        titleText.text = shopStation.name;
        statusText.text = "Select an item to buy or sell.";
        menuRoot.SetActive(true);
        RefreshRows();
        if (EventSystem.current != null)
        {
            for (int index = 0; index < rows.Count; index++)
            {
                Button rowButton = rows[index].GetComponent<Button>();
                if (rowButton != null && rowButton.interactable)
                {
                    EventSystem.current.SetSelectedGameObject(rowButton.gameObject);
                    break;
                }
            }
        }

        return true;
    }

    /// <summary>Closes the shop without resuming time, for scene or modal cleanup owned by IngameUIManager.</summary>
    public void CloseFromOwner()
    {
        if (menuRoot != null)
        {
            menuRoot.SetActive(false);
        }

        activeShop = null;
        activePlayerInventory = null;
    }

    private void BuildMenu()
    {
        menuRoot = new GameObject("Shop Menu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        menuRoot.transform.SetParent(transform, false);
        RectTransform rootRect = menuRoot.GetComponent<RectTransform>();
        Stretch(rootRect, Vector2.zero, Vector2.one);
        Image backdrop = menuRoot.GetComponent<Image>();
        backdrop.color = BackdropColor;
        backdrop.raycastTarget = true;

        GameObject card = new GameObject("Shop Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        card.transform.SetParent(menuRoot.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        Stretch(cardRect, new Vector2(0.17f, 0.07f), new Vector2(0.83f, 0.93f));
        card.GetComponent<Image>().color = CardColor;

        titleText = CreateText("Shop Title", cardRect, string.Empty, 34, TextAnchor.MiddleCenter,
            new Vector2(0.04f, 0.89f), new Vector2(0.96f, 0.98f));
        balanceText = CreateText("Credits Balance", cardRect, string.Empty, 24, TextAnchor.MiddleRight,
            new Vector2(0.58f, 0.82f), new Vector2(0.96f, 0.89f));
        Text description = CreateText("Shop Description", cardRect,
            "Buy purchase-eligible catalog items or sell eligible carried stacks. A full grid rejects purchases without charging you.",
            17, TextAnchor.MiddleLeft, new Vector2(0.05f, 0.82f), new Vector2(0.58f, 0.89f));
        description.horizontalOverflow = HorizontalWrapMode.Wrap;

        GameObject viewportObject = new GameObject("Shop List Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(cardRect, false);
        listViewport = viewportObject.GetComponent<RectTransform>();
        Stretch(listViewport, new Vector2(0.045f, 0.17f), new Vector2(0.955f, 0.8f));
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0.035f, 0.04f, 0.05f, 1f);
        viewportImage.raycastTarget = true;
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject("Shop List Content", typeof(RectTransform));
        contentObject.transform.SetParent(listViewport, false);
        listContent = contentObject.GetComponent<RectTransform>();
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.anchoredPosition = Vector2.zero;
        listContent.sizeDelta = Vector2.zero;

        listScrollRect = card.AddComponent<ScrollRect>();
        listScrollRect.viewport = listViewport;
        listScrollRect.content = listContent;
        listScrollRect.horizontal = false;
        listScrollRect.vertical = true;
        listScrollRect.movementType = ScrollRect.MovementType.Clamped;
        listScrollRect.scrollSensitivity = 32f;

        statusText = CreateText("Shop Status", cardRect, string.Empty, 17, TextAnchor.MiddleLeft,
            new Vector2(0.05f, 0.1f), new Vector2(0.72f, 0.16f));
        statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        CreateButton("Close Shop", cardRect, "Close / Back", new Vector2(0.76f, 0.075f), new Vector2(0.96f, 0.16f), CloseShop);
    }

    private void RefreshRows()
    {
        if (activeShop == null || activePlayerInventory == null)
        {
            return;
        }

        for (int index = 0; index < rows.Count; index++)
        {
            if (rows[index] != null)
            {
                Destroy(rows[index]);
            }
        }

        rows.Clear();
        balanceText.text = $"CREDITS: {activePlayerInventory.Credits}";
        List<ShopRowData> rowData = new List<ShopRowData>();
        rowData.Add(new ShopRowData("CATALOG  —  BUY", false, null));

        IReadOnlyList<ShopStockEntry> catalog = activeShop.StockEntries;
        for (int index = 0; index < catalog.Count; index++)
        {
            ShopStockEntry entry = catalog[index];
            ItemDefinition definition = entry.ItemDefinition;
            if (definition == null)
            {
                continue;
            }

            int quantity = entry.QuantityPerPurchase;
            long totalCost = (long)definition.BuyPrice * quantity;
            bool priced = definition.CanBePurchased && definition.BuyPrice > 0 && totalCost <= int.MaxValue;
            bool inStock = entry.CurrentStock >= quantity;
            string label = !priced
                ? $"Unavailable  |  {definition.DisplayName}"
                : !inStock
                    ? $"Out of stock  |  {definition.DisplayName} ×{quantity}"
                    : $"Buy  {definition.DisplayName} ×{quantity}  —  {totalCost} Credits  —  Stock {entry.CurrentStock}";
            string stableItemId = definition.StableItemId;
            rowData.Add(new ShopRowData(label, priced && inStock, () => BuyItem(stableItemId)));
        }

        rowData.Add(new ShopRowData("CARRIED ITEMS  —  SELL", false, null));
        IReadOnlyList<ItemInstance> carryItems = activePlayerInventory.Inventory.Items;
        for (int index = 0; index < carryItems.Count; index++)
        {
            ItemInstance item = carryItems[index];
            ItemDefinition definition = item.Definition;
            long saleValue = (long)definition.SellPrice * item.Quantity;
            bool statePreservingSaleSupported = definition.Category != ItemCategory.Weapon &&
                definition.Category != ItemCategory.Equipment && definition.Category != ItemCategory.Attachment &&
                definition.Category != ItemCategory.CapacityUpgrade;
            bool canSell = definition.CanBeSold && definition.SellPrice > 0 && !item.IsEquipped &&
                statePreservingSaleSupported && saleValue > 0 && saleValue <= int.MaxValue &&
                (long)activePlayerInventory.Credits + saleValue <= int.MaxValue;
            string label = !definition.CanBeSold || definition.SellPrice <= 0
                ? $"Protected / not for sale  |  {definition.DisplayName} ×{item.Quantity}"
                : item.IsEquipped
                    ? $"Unequip first  |  {definition.DisplayName} ×{item.Quantity}"
                    : !statePreservingSaleSupported
                        ? $"State-preserving sale unavailable  |  {definition.DisplayName} ×{item.Quantity}"
                        : $"Sell  {definition.DisplayName} ×{item.Quantity}  —  +{saleValue} Credits";
            string instanceId = item.InstanceId;
            rowData.Add(new ShopRowData(label, canSell, () => SellItem(instanceId)));
        }

        float contentHeight = Mathf.Max(listViewport.rect.height, rowData.Count * ShopRowHeight);
        listContent.sizeDelta = new Vector2(0f, contentHeight);
        for (int index = 0; index < rowData.Count; index++)
        {
            ShopRowData row = rowData[index];
            float top = 1f - index * ShopRowHeight / contentHeight;
            float bottom = 1f - (index + 1) * ShopRowHeight / contentHeight;
            Button button = CreateButton($"Shop Row {index}", listContent, row.Label,
                new Vector2(0.015f, bottom), new Vector2(0.985f, top), row.Action ?? (() => { }));
            button.interactable = row.Interactable;
            if (!row.Interactable)
            {
                button.image.color = DisabledButtonColor;
            }

            rows.Add(button.gameObject);
        }

        listScrollRect.verticalNormalizedPosition = 1f;
    }

    private void BuyItem(string stableItemId)
    {
        if (activeShop == null)
        {
            return;
        }

        activeShop.TryBuy(activePlayerInventory, stableItemId, out string resultMessage);
        statusText.text = resultMessage;
        RefreshRows();
    }

    private void SellItem(string instanceId)
    {
        if (activeShop != null)
        {
            activeShop.TrySell(activePlayerInventory, instanceId, out string resultMessage);
            statusText.text = resultMessage;
        }

        RefreshRows();
    }

    private void CloseShop()
    {
        IngameUIManager.Instance?.ResumeGame();
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(4f, 2f);
        rect.offsetMax = new Vector2(-4f, -2f);
        Image image = buttonObject.GetComponent<Image>();
        image.color = ButtonColor;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic, wrapAround = false };
        button.onClick.AddListener(action);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        Stretch(labelRect, Vector2.zero, Vector2.one);
        Text text = labelObject.GetComponent<Text>();
        text.font = uiFont;
        text.text = label;
        text.fontSize = 17;
        text.color = TextColor;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.raycastTarget = false;
        return button;
    }

    private Text CreateText(
        string objectName,
        Transform parent,
        string text,
        int fontSize,
        TextAnchor alignment,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        Stretch(rect, anchorMin, anchorMax);
        Text uiText = textObject.GetComponent<Text>();
        uiText.font = uiFont;
        uiText.text = text;
        uiText.fontSize = fontSize;
        uiText.color = TextColor;
        uiText.alignment = alignment;
        uiText.raycastTarget = false;
        return uiText;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private readonly struct ShopRowData
    {
        public ShopRowData(string label, bool interactable, UnityEngine.Events.UnityAction action)
        {
            Label = label;
            Interactable = interactable;
            Action = action;
        }

        public string Label { get; }
        public bool Interactable { get; }
        public UnityEngine.Events.UnityAction Action { get; }
    }
}
