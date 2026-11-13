using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Runtime workbench modal for weapon upgrades, removable attachments, and station recipes.</summary>
[DisallowMultipleComponent]
public sealed class WorkbenchMenuUI : MonoBehaviour
{
    private const float WorkbenchRowHeight = 48f;
    private const int HeaderFontSize = 34;
    private const int RowFontSize = 16;
    private static readonly Color BackdropColor = new Color(0.01f, 0.014f, 0.02f, 0.9f);
    private static readonly Color CardColor = new Color(0.055f, 0.065f, 0.08f, 0.99f);
    private static readonly Color ButtonColor = new Color(0.16f, 0.2f, 0.26f, 1f);
    private static readonly Color ActionButtonColor = new Color(0.19f, 0.27f, 0.34f, 1f);
    private static readonly Color DisabledButtonColor = new Color(0.09f, 0.11f, 0.14f, 1f);
    private static readonly Color TextColor = new Color(0.94f, 0.95f, 0.98f, 1f);
    private static readonly Color SectionColor = new Color(0.12f, 0.16f, 0.21f, 1f);

    private readonly List<GameObject> rows = new List<GameObject>();
    private GameObject menuRoot;
    private RectTransform listContent;
    private RectTransform listViewport;
    private ScrollRect listScrollRect;
    private Text titleText;
    private Text creditsText;
    private Text statusText;
    private Font uiFont;
    private WorkbenchStation activeWorkbench;
    private PlayerItemInventory activePlayerInventory;

    /// <summary>Gets whether the workbench currently owns the active modal UI state.</summary>
    public bool IsOpen => menuRoot != null && menuRoot.activeSelf;

    private void Awake()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildMenu();
        menuRoot.SetActive(false);
    }

    /// <summary>Opens this station's services for its player currently in range.</summary>
    public bool Open(WorkbenchStation workbenchStation)
    {
        Player player = workbenchStation != null ? workbenchStation.PlayerInRange : null;
        PlayerItemInventory playerInventory = player != null ? player.GetComponent<PlayerItemInventory>() : null;
        if (workbenchStation == null || playerInventory == null)
        {
            return false;
        }

        activeWorkbench = workbenchStation;
        activePlayerInventory = playerInventory;
        titleText.text = $"{workbenchStation.name}  —  WORKBENCH";
        statusText.text = "Select an upgrade, attachment operation, or recipe.";
        menuRoot.SetActive(true);
        RefreshRows();
        return true;
    }

    /// <summary>Closes the workbench without resuming time, for owner-controlled modal and scene cleanup.</summary>
    public void CloseFromOwner()
    {
        if (menuRoot != null)
        {
            menuRoot.SetActive(false);
        }

        activeWorkbench = null;
        activePlayerInventory = null;
    }

    private void BuildMenu()
    {
        menuRoot = new GameObject("Workbench Menu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        menuRoot.transform.SetParent(transform, false);
        RectTransform rootRect = menuRoot.GetComponent<RectTransform>();
        Stretch(rootRect, Vector2.zero, Vector2.one);
        Image backdrop = menuRoot.GetComponent<Image>();
        backdrop.color = BackdropColor;
        backdrop.raycastTarget = true;

        GameObject card = new GameObject("Workbench Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        card.transform.SetParent(menuRoot.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        Stretch(cardRect, new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.94f));
        card.GetComponent<Image>().color = CardColor;

        titleText = CreateText("Workbench Title", cardRect, string.Empty, HeaderFontSize, TextAnchor.MiddleCenter,
            new Vector2(0.04f, 0.89f), new Vector2(0.96f, 0.98f));
        creditsText = CreateText("Workbench Credits", cardRect, string.Empty, 22, TextAnchor.MiddleRight,
            new Vector2(0.68f, 0.82f), new Vector2(0.96f, 0.89f));
        Text description = CreateText("Workbench Description", cardRect,
            "Upgrades affect carried ranged weapon instances. Attachments and recipes use only owned carry items.",
            16, TextAnchor.MiddleLeft, new Vector2(0.05f, 0.82f), new Vector2(0.66f, 0.89f));
        description.horizontalOverflow = HorizontalWrapMode.Wrap;

        GameObject viewportObject = new GameObject("Workbench List Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(cardRect, false);
        listViewport = viewportObject.GetComponent<RectTransform>();
        Stretch(listViewport, new Vector2(0.045f, 0.17f), new Vector2(0.955f, 0.8f));
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0.035f, 0.04f, 0.05f, 1f);
        viewportImage.raycastTarget = true;
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject("Workbench List Content", typeof(RectTransform));
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

        statusText = CreateText("Workbench Status", cardRect, string.Empty, 16, TextAnchor.MiddleLeft,
            new Vector2(0.05f, 0.09f), new Vector2(0.7f, 0.16f));
        statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        CreateButton("Close Workbench", cardRect, "Close / Back",
            new Vector2(0.76f, 0.075f), new Vector2(0.96f, 0.16f), CloseWorkbench, false, false);
    }

    private void RefreshRows()
    {
        if (activeWorkbench == null || activePlayerInventory == null || activePlayerInventory.Inventory == null)
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
        creditsText.text = $"CREDITS: {activePlayerInventory.Credits}";
        IReadOnlyList<ItemInstance> carriedItems = activePlayerInventory.Inventory.Items;

        AddSection("RANGED WEAPON UPGRADES");
        int rangedWeaponCount = 0;
        for (int itemIndex = 0; itemIndex < carriedItems.Count; itemIndex++)
        {
            ItemInstance weaponItem = carriedItems[itemIndex];
            if (!(weaponItem.Definition is WeaponDefinition weaponDefinition))
            {
                continue;
            }

            rangedWeaponCount++;
            AddSection($"{weaponDefinition.DisplayName}  |  ID {weaponItem.InstanceId}");
            AddWeaponUpgradeRows(weaponItem, weaponDefinition);
        }

        if (rangedWeaponCount == 0)
        {
            AddRow("No carried ranged weapons are available.", false, null, false);
        }

        AddSection("CARRIED / INSTALLED ATTACHMENTS");
        int attachmentOperationCount = 0;
        for (int itemIndex = 0; itemIndex < carriedItems.Count; itemIndex++)
        {
            ItemInstance weaponItem = carriedItems[itemIndex];
            if (!(weaponItem.Definition is WeaponDefinition weaponDefinition))
            {
                continue;
            }

            AddSection($"{weaponDefinition.DisplayName} attachments");
            IReadOnlyList<ItemInstance> installedAttachments = activePlayerInventory.GetInstalledAttachmentsForWeapon(weaponItem.InstanceId);
            if (installedAttachments.Count == 0)
            {
                AddRow("No compatible attachments installed.", false, null, false);
            }

            for (int installedIndex = 0; installedIndex < installedAttachments.Count; installedIndex++)
            {
                ItemInstance attachmentItem = installedAttachments[installedIndex];
                if (!(attachmentItem.Definition is WeaponAttachmentDefinition attachmentDefinition) ||
                    !IsCompatible(weaponDefinition, attachmentDefinition))
                {
                    continue;
                }

                string weaponInstanceId = weaponItem.InstanceId;
                string attachmentInstanceId = attachmentItem.InstanceId;
                string removeLabel = $"Remove {attachmentDefinition.DisplayName} [{attachmentDefinition.AttachmentSlotId}]  |  ID {attachmentInstanceId}";
                AddRow(removeLabel, true,
                    () => RemoveAttachment(weaponInstanceId, attachmentInstanceId), true);
                attachmentOperationCount++;
            }

            int compatibleCarriedCount = 0;
            for (int attachmentIndex = 0; attachmentIndex < carriedItems.Count; attachmentIndex++)
            {
                ItemInstance attachmentItem = carriedItems[attachmentIndex];
                if (!(attachmentItem.Definition is WeaponAttachmentDefinition attachmentDefinition) ||
                    !IsCompatible(weaponDefinition, attachmentDefinition))
                {
                    continue;
                }

                compatibleCarriedCount++;
                bool slotOccupied = HasInstalledSlot(weaponItem, activePlayerInventory, attachmentDefinition.AttachmentSlotId);
                string weaponInstanceId = weaponItem.InstanceId;
                string attachmentInstanceId = attachmentItem.InstanceId;
                string label = slotOccupied
                    ? $"Slot occupied  |  {attachmentDefinition.DisplayName} [{attachmentDefinition.AttachmentSlotId}]  |  ID {attachmentInstanceId}"
                    : $"Install {attachmentDefinition.DisplayName} [{attachmentDefinition.AttachmentSlotId}]  |  ID {attachmentInstanceId}";
                AddRow(label, !slotOccupied,
                    () => InstallAttachment(weaponInstanceId, attachmentInstanceId), !slotOccupied);
                if (!slotOccupied)
                {
                    attachmentOperationCount++;
                }
            }

            if (compatibleCarriedCount == 0)
            {
                AddRow("No compatible carried attachments.", false, null, false);
            }
        }

        if (rangedWeaponCount == 0 || attachmentOperationCount == 0)
        {
            AddRow("No attachment changes are currently available.", false, null, false);
        }

        AddSection("STATION RECIPES");
        IReadOnlyList<ItemRecipeDefinition> recipes = activeWorkbench.Recipes;
        int recipeCount = 0;
        for (int recipeIndex = 0; recipes != null && recipeIndex < recipes.Count; recipeIndex++)
        {
            ItemRecipeDefinition recipe = recipes[recipeIndex];
            if (recipe == null)
            {
                continue;
            }

            recipeCount++;
            string recipeStableId = GetRecipeStableId(recipe);
            string label = $"Craft {recipe.name}  |  {FormatRecipe(recipe)}  |  ID {recipeStableId}";
            AddRow(label, true, () => CraftRecipe(recipeStableId), true);
        }

        if (recipeCount == 0)
        {
            AddRow("No recipes are configured at this station.", false, null, false);
        }

        float contentHeight = Mathf.Max(listViewport.rect.height, rows.Count * WorkbenchRowHeight);
        listContent.sizeDelta = new Vector2(0f, contentHeight);
        for (int index = 0; index < rows.Count; index++)
        {
            RectTransform rowRect = rows[index].GetComponent<RectTransform>();
            float top = 1f - index * WorkbenchRowHeight / contentHeight;
            float bottom = 1f - (index + 1) * WorkbenchRowHeight / contentHeight;
            rowRect.anchorMin = new Vector2(0.015f, bottom);
            rowRect.anchorMax = new Vector2(0.985f, top);
            rowRect.offsetMin = new Vector2(4f, 2f);
            rowRect.offsetMax = new Vector2(-4f, -2f);
        }

        listScrollRect.verticalNormalizedPosition = 1f;
        SelectFirstAvailableRow();
    }

    private void AddWeaponUpgradeRows(ItemInstance weaponItem, WeaponDefinition weaponDefinition)
    {
        WeaponUpgradeOption[] upgradeOptions = weaponDefinition.upgradeOptions;
        if (upgradeOptions == null || upgradeOptions.Length == 0)
        {
            AddRow("No upgrades are configured for this weapon.", false, null, false);
            return;
        }

        HashSet<WeaponUpgradeType> shownUpgradeTypes = new HashSet<WeaponUpgradeType>();
        for (int optionIndex = 0; optionIndex < upgradeOptions.Length; optionIndex++)
        {
            WeaponUpgradeType upgradeType = upgradeOptions[optionIndex].upgradeType;
            if (!shownUpgradeTypes.Add(upgradeType))
            {
                continue;
            }

            int currentLevel = weaponItem.GetUpgradeLevel(upgradeType);
            int maximumLevel = upgradeOptions[optionIndex].maximumLevel;
            bool hasNextLevel = activePlayerInventory.TryGetWeaponUpgradeCost(weaponItem.InstanceId, upgradeType, out int exactCost);
            string label = hasNextLevel
                ? $"Upgrade {upgradeType}  |  Level {currentLevel}/{maximumLevel} → {currentLevel + 1}  |  Cost {exactCost} Credits"
                : $"{upgradeType}  |  Level {currentLevel}/{maximumLevel}  |  {(currentLevel >= maximumLevel ? "MAX LEVEL" : "Unavailable / no configured cost")}";
            string weaponInstanceId = weaponItem.InstanceId;
            AddRow(label, hasNextLevel,
                () => PurchaseUpgrade(weaponInstanceId, upgradeType, exactCost), hasNextLevel);
        }
    }

    private void PurchaseUpgrade(string weaponInstanceId, WeaponUpgradeType upgradeType, int exactCost)
    {
        if (activePlayerInventory == null)
        {
            return;
        }

        InventoryOperationResult result = activePlayerInventory.TryPurchaseWeaponUpgrade(weaponInstanceId, upgradeType, exactCost);
        statusText.text = result.Succeeded
            ? $"Purchased {upgradeType} upgrade for {exactCost} Credits."
            : $"Upgrade rejected: {result.Failure}. Weapon level and Credits were not changed.";
        RefreshRows();
    }

    private void InstallAttachment(string weaponInstanceId, string attachmentInstanceId)
    {
        if (activePlayerInventory == null)
        {
            return;
        }

        InventoryOperationResult result = activePlayerInventory.TryInstallWeaponAttachment(weaponInstanceId, attachmentInstanceId);
        statusText.text = result.Succeeded
            ? "Attachment installed."
            : $"Installation rejected: {result.Failure}. Inventory and weapon state were not changed.";
        RefreshRows();
    }

    private void RemoveAttachment(string weaponInstanceId, string attachmentInstanceId)
    {
        if (activePlayerInventory == null)
        {
            return;
        }

        InventoryOperationResult result = activePlayerInventory.TryRemoveWeaponAttachment(weaponInstanceId, attachmentInstanceId);
        statusText.text = result.Succeeded
            ? "Attachment removed to the carry grid."
            : $"Removal rejected: {result.Failure}. The attachment remains installed.";
        RefreshRows();
    }

    private void CraftRecipe(string recipeStableId)
    {
        if (activePlayerInventory == null || activeWorkbench == null)
        {
            return;
        }

        ItemRecipeDefinition recipe = FindRecipe(recipeStableId);
        InventoryOperationResult result = recipe != null
            ? activePlayerInventory.TryCraftRecipe(recipe)
            : InventoryOperationResult.Rejected(InventoryOperationFailure.InvalidDefinition);
        statusText.text = result.Succeeded
            ? $"Crafted {recipe.name}."
            : $"Crafting rejected: {result.Failure}. Ingredients and outputs were not changed.";
        RefreshRows();
    }

    private ItemRecipeDefinition FindRecipe(string recipeStableId)
    {
        IReadOnlyList<ItemRecipeDefinition> recipes = activeWorkbench != null ? activeWorkbench.Recipes : null;
        for (int index = 0; recipes != null && index < recipes.Count; index++)
        {
            ItemRecipeDefinition recipe = recipes[index];
            if (recipe != null && string.Equals(GetRecipeStableId(recipe), recipeStableId, StringComparison.Ordinal))
            {
                return recipe;
            }
        }

        return null;
    }

    private void AddSection(string label)
    {
        AddRow(label, false, null, false, true);
    }

    private void AddRow(string label, bool interactable, UnityEngine.Events.UnityAction action, bool emphasize, bool isSection = false)
    {
        GameObject rowObject = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        rowObject.transform.SetParent(listContent, false);
        Image background = rowObject.GetComponent<Image>();
        background.color = isSection ? SectionColor : interactable ? (emphasize ? ActionButtonColor : ButtonColor) : DisabledButtonColor;
        Button button = rowObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.interactable = interactable;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic, wrapAround = false };
        if (action != null)
        {
            button.onClick.AddListener(action);
        }

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(rowObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        Stretch(labelRect, Vector2.zero, Vector2.one);
        Text text = labelObject.GetComponent<Text>();
        text.font = uiFont;
        text.text = label;
        text.fontSize = isSection ? 17 : RowFontSize;
        text.color = TextColor;
        text.alignment = isSection ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        rows.Add(rowObject);
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        UnityEngine.Events.UnityAction action,
        bool interactable,
        bool emphasize)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        Stretch(rect, anchorMin, anchorMax);
        Image image = buttonObject.GetComponent<Image>();
        image.color = interactable ? (emphasize ? ActionButtonColor : ButtonColor) : DisabledButtonColor;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.interactable = interactable;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic, wrapAround = false };
        button.onClick.AddListener(action);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        Stretch(labelRect, Vector2.zero, Vector2.one);
        Text text = labelObject.GetComponent<Text>();
        text.font = uiFont;
        text.text = label;
        text.fontSize = RowFontSize + 1;
        text.color = TextColor;
        text.alignment = TextAnchor.MiddleCenter;
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

    private void SelectFirstAvailableRow()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        for (int index = 0; index < rows.Count; index++)
        {
            Button rowButton = rows[index].GetComponent<Button>();
            if (rowButton != null && rowButton.interactable)
            {
                EventSystem.current.SetSelectedGameObject(rowButton.gameObject);
                return;
            }
        }
    }

    private void CloseWorkbench()
    {
        IngameUIManager.Instance?.ResumeGame();
    }

    private static bool IsCompatible(WeaponDefinition weaponDefinition, WeaponAttachmentDefinition attachmentDefinition)
    {
        if (weaponDefinition == null || attachmentDefinition == null ||
            string.IsNullOrWhiteSpace(attachmentDefinition.AttachmentSlotId))
        {
            return false;
        }

        string[] requiredTags = attachmentDefinition.CompatibleWeaponTags;
        string[] weaponTags = weaponDefinition.Tags;
        if (requiredTags == null || weaponTags == null || requiredTags.Length == 0)
        {
            return false;
        }

        for (int requiredIndex = 0; requiredIndex < requiredTags.Length; requiredIndex++)
        {
            if (string.IsNullOrWhiteSpace(requiredTags[requiredIndex]))
            {
                continue;
            }

            for (int weaponTagIndex = 0; weaponTagIndex < weaponTags.Length; weaponTagIndex++)
            {
                if (string.Equals(requiredTags[requiredIndex], weaponTags[weaponTagIndex], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasInstalledSlot(ItemInstance weaponItem, PlayerItemInventory playerInventory, string slotId)
    {
        IReadOnlyList<ItemInstance> installed = playerInventory.GetInstalledAttachmentsForWeapon(weaponItem.InstanceId);
        for (int index = 0; index < installed.Count; index++)
        {
            if (installed[index].Definition is WeaponAttachmentDefinition installedDefinition &&
                string.Equals(installedDefinition.AttachmentSlotId, slotId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatRecipe(ItemRecipeDefinition recipe)
    {
        return $"[{FormatEntries(recipe.Ingredients)} → {FormatEntries(recipe.Outputs)}]";
    }

    private static string FormatEntries(IReadOnlyList<ItemRecipeEntry> entries)
    {
        if (entries == null || entries.Count == 0)
        {
            return "none";
        }

        StringBuilder description = new StringBuilder();
        for (int index = 0; index < entries.Count; index++)
        {
            if (index > 0)
            {
                description.Append(", ");
            }

            ItemRecipeEntry entry = entries[index];
            description.Append(entry.Definition != null ? entry.Definition.DisplayName : "Missing item");
            description.Append(" ×");
            description.Append(entry.Quantity);
        }

        return description.ToString();
    }

    private static string GetRecipeStableId(ItemRecipeDefinition recipe)
    {
        StringBuilder stableId = new StringBuilder(recipe != null ? recipe.name : "MissingRecipe");
        stableId.Append('|');
        AppendRecipeEntries(stableId, recipe != null ? recipe.Ingredients : null);
        stableId.Append("->");
        AppendRecipeEntries(stableId, recipe != null ? recipe.Outputs : null);
        return stableId.ToString();
    }

    private static void AppendRecipeEntries(StringBuilder stableId, IReadOnlyList<ItemRecipeEntry> entries)
    {
        for (int index = 0; entries != null && index < entries.Count; index++)
        {
            ItemRecipeEntry entry = entries[index];
            string itemStableId = entry.Definition != null ? entry.Definition.StableItemId : "missing";
            stableId.Append(itemStableId.Length);
            stableId.Append(':');
            stableId.Append(itemStableId);
            stableId.Append('x');
            stableId.Append(entry.Quantity);
            stableId.Append(';');
        }
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
