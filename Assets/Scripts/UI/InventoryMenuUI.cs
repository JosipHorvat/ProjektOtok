using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Builds and controls the first modal inventory, key-items, and placeholder-tab menu.</summary>
[DisallowMultipleComponent]
public sealed class InventoryMenuUI : MonoBehaviour, ICancelHandler
{
    private float gridCellSize = 56f;
    private const float GridCellSpacing = 4f;
    private const float GridPadding = 8f;
    private const float ActionPopupWidth = 210f;
    private const float ActionPopupHeaderHeight = 34f;
    private const float ActionPopupRowHeight = 30f;
    private const string InventoryTabName = "Items";

    private static readonly WeaponUpgradeType[] WeaponUpgradeOrder =
    {
        WeaponUpgradeType.Damage,
        WeaponUpgradeType.MagazineCapacity,
        WeaponUpgradeType.FireRate,
        WeaponUpgradeType.ReloadSpeed,
        WeaponUpgradeType.Accuracy,
        WeaponUpgradeType.Recoil,
        WeaponUpgradeType.Range,
        WeaponUpgradeType.Pierce,
    };

    private static readonly Color WindowColor = new Color(0.045f, 0.052f, 0.065f, 0.98f);
    private static readonly Color PanelColor = new Color(0.075f, 0.085f, 0.105f, 0.98f);
    private static readonly Color CellColor = new Color(0.13f, 0.15f, 0.18f, 1f);
    private static readonly Color ItemTileColor = new Color(0.025f, 0.03f, 0.04f, 0.5f);
    private static readonly Color SelectedColor = new Color(0.02f, 0.86f, 1f, 1f);
    private static readonly Color SelectedOutlineColor = Color.white;
    private static readonly Color ValidPlacementColor = new Color(0.16f, 0.72f, 0.4f, 0.88f);
    private static readonly Color InvalidPlacementColor = new Color(0.82f, 0.2f, 0.2f, 0.9f);
    private static readonly Color TextColor = new Color(0.93f, 0.94f, 0.97f, 1f);

    private sealed class MenuActionEntry
    {
        public readonly string Label;
        public readonly Action Execute;
        public readonly bool IsAvailable;

        public MenuActionEntry(string label, Action execute, bool isAvailable = true)
        {
            Label = label;
            Execute = execute;
            IsAvailable = isAvailable;
        }
    }

    private readonly List<Button> tabButtons = new List<Button>();
    private readonly List<GameObject> tabPanels = new List<GameObject>();
    private readonly List<Button> gridCellButtons = new List<Button>();
    private readonly List<Image> gridCellImages = new List<Image>();
    private readonly List<GameObject> itemTiles = new List<GameObject>();
    private readonly Dictionary<GameObject, string> itemTileInstanceIds = new Dictionary<GameObject, string>();
    private readonly List<GameObject> keyItemRows = new List<GameObject>();
    private readonly List<GameObject> stashRows = new List<GameObject>();
    private readonly List<Button> popupActionButtons = new List<Button>();

    private Font uiFont;
    private GameObject menuRoot;
    private Text comparisonText;
    private Text selectedItemNameText;
    private Text selectedItemCategoryText;
    private Text selectedItemDescriptionText;
    private Text selectedItemStatsText;
    private Text inventoryStatusText;
    private Text keyItemsHeadingText;
    private Text keyItemDetailText;
    private Text controlLegendText;
    private Text actionPopupHeadingText;
    private RectTransform gridContentTransform;
    private RectTransform gridScrollRootTransform;
    private RectTransform comparisonPanel;
    private RectTransform comparisonContentTransform;
    private RectTransform carryGridPanel;
    private RectTransform selectedItemPanel;
    private RectTransform stashPanel;
    private RectTransform stashContentTransform;

    private ScrollRect gridScrollRect;
    private ScrollRect stashScrollRect;
    private ScrollRect comparisonScrollRect;
    private GameObject actionPopupRoot;
    private RectTransform actionPopupRect;
    private RectTransform actionPopupListTransform;

    private ItemInventory activeInventory;
    private PlayerItemInventory activePlayerInventory;
    private string selectedItemInstanceId;
    private string focusedItemInstanceId;
    private string selectedStashItemInstanceId;
    private string selectedKeyItemInstanceId;
    private string statusMessage = "Select an item to inspect it.";
    private int activeTabIndex;
    private bool isMergeTargetSelectionActive;
    private bool isStashMode;
    private bool isActionPopupOpen;
    private bool isMoveMode;
    private Vector2Int movePreviewPosition;
    private int movePreviewQuarterTurns;
    private bool hasPickupExchange;
    private string exchangePlacedItemInstanceId;
    private Vector2Int exchangePlacedItemOriginalPosition;
    private int exchangePlacedItemOriginalRotationQuarterTurns;
    private string exchangePickedUpItemInstanceId;
    private Vector2Int exchangePickedUpItemOriginalPosition;
    private int exchangePickedUpItemOriginalRotationQuarterTurns;
    private string movingItemInstanceId;
    private GameObject movingItemTile;
    private CanvasGroup movingItemCanvasGroup;
    private string lastControlLegend;
    private bool isOpen;
    private GameObject previouslySelectedObject;

    /// <summary>Gets whether the inventory menu currently owns the modal UI state.</summary>
    public bool IsOpen => isOpen;

    private void Awake()
    {
        uiFont = GetComponentInChildren<Text>(true)?.font;
        if (uiFont == null)
        {
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        BuildMenu();
        menuRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        UnsubscribeFromInventory();
        if (menuRoot != null)
        {
            Destroy(menuRoot);
        }
    }

    /// <summary>Opens the menu and binds it to the active player's current inventory.</summary>
    public bool Open()
    {
        if (isOpen)
        {
            return true;
        }

        activePlayerInventory = FindAnyObjectByType<PlayerItemInventory>();
        activeInventory = activePlayerInventory != null ? activePlayerInventory.Inventory : null;
        if (activePlayerInventory == null || activeInventory == null)
        {
            SetStatus("No player item inventory is available in this scene.");
            return false;
        }

        previouslySelectedObject = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;
        activePlayerInventory.Changed += HandleInventoryChanged;
        isOpen = true;
        isStashMode = false;
        isActionPopupOpen = false;
        isMoveMode = false;
        movingItemTile = null;
        movingItemCanvasGroup = null;
        movingItemInstanceId = null;
        ClearPickupExchange();
        focusedItemInstanceId = null;
        actionPopupRoot.SetActive(false);
        selectedStashItemInstanceId = null;
        isMergeTargetSelectionActive = false;
        statusMessage = string.Empty;
        menuRoot.SetActive(true);
        SelectTab(1);
        RefreshInventoryView();
        FocusInitialGridItem();

        return true;
    }

    /// <summary>Opens the shared modal menu for the player accessing a safe-room stash.</summary>
    public bool OpenStash(SafeRoomStash stash)
    {
        Player player = stash != null ? stash.PlayerInRange : null;
        PlayerItemInventory playerInventory = player != null ? player.GetComponent<PlayerItemInventory>() : null;
        ItemInventory inventory = playerInventory != null ? playerInventory.Inventory : null;
        if (playerInventory == null || inventory == null)
        {
            SetStatus("The player accessing this stash has no item inventory.");
            return false;
        }

        if (isOpen && activePlayerInventory != playerInventory)
        {
            return false;
        }

        if (!isOpen)
        {
            previouslySelectedObject = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            playerInventory.Changed += HandleInventoryChanged;
            isOpen = true;
        }

        activePlayerInventory = playerInventory;
        activeInventory = inventory;
        isStashMode = true;
        isActionPopupOpen = false;
        isMoveMode = false;
        movingItemTile = null;
        movingItemCanvasGroup = null;
        movingItemInstanceId = null;
        ClearPickupExchange();
        focusedItemInstanceId = null;
        actionPopupRoot.SetActive(false);
        isMergeTargetSelectionActive = false;
        selectedStashItemInstanceId = null;
        statusMessage = string.Empty;
        menuRoot.SetActive(true);
        SelectTab(1);
        RefreshInventoryView();
        FocusInitialGridItem();

        return true;
    }

    /// <summary>Closes the menu and returns focus to the UI selection active before it opened.</summary>
    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        if (isMoveMode)
        {
            isMoveMode = false;
            RollbackPendingPickupExchange();
            movingItemInstanceId = null;
            RestoreMovingItemVisuals();
            RefreshInventoryView();
        }

        UnsubscribeFromInventory();
        isOpen = false;
        isMergeTargetSelectionActive = false;
        isStashMode = false;
        isActionPopupOpen = false;
        selectedItemInstanceId = null;
        focusedItemInstanceId = null;
        selectedStashItemInstanceId = null;
        selectedKeyItemInstanceId = null;
        menuRoot.SetActive(false);
        if (EventSystem.current != null && previouslySelectedObject != null && previouslySelectedObject.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(previouslySelectedObject);
        }

        previouslySelectedObject = null;
    }

    /// <summary>Hides the inventory panel while another already-paused modal panel is displayed.</summary>
    public void HideForOtherModal()
    {
        if (!isOpen)
        {
            return;
        }

        if (isMoveMode)
        {
            isMoveMode = false;
            RollbackPendingPickupExchange();
            movingItemInstanceId = null;
            RestoreMovingItemVisuals();
            RefreshInventoryView();
        }

        UnsubscribeFromInventory();
        isOpen = false;
        isStashMode = false;
        isActionPopupOpen = false;
        selectedStashItemInstanceId = null;
        menuRoot.SetActive(false);
    }

    /// <summary>Changes to an adjacent menu tab, wrapping across the five authored tab choices.</summary>
    public void ChangeTab(int direction)
    {
        if (!isOpen || direction == 0)
        {
            return;
        }

        if (isActionPopupOpen)
        {
            CloseItemActionPopup();
        }

        int tabCount = tabButtons.Count;
        int nextTabIndex = (activeTabIndex + Math.Sign(direction) + tabCount) % tabCount;
        if (nextTabIndex == tabCount - 1)
        {
            OpenExistingOptions();
            return;
        }

        SelectTab(nextTabIndex);
    }

    /// <summary>Opens the existing options panel when the Options tab is selected.</summary>
    public void OpenExistingOptions()
    {
        if (!isOpen)
        {
            return;
        }

        HideForOtherModal();
        IngameUIManager.Instance?.OpenOptions();
    }

    /// <summary>Closes the menu and resumes gameplay through the existing modal/pause owner.</summary>
    public void CloseAndResume()
    {
        IngameUIManager.Instance?.ResumeGame();
    }

    /// <summary>Returns from the action menu, cancels item placement, or closes the inventory.</summary>
    public void OnCancel(BaseEventData eventData)
    {
        if (!isOpen)
        {
            return;
        }

        if (isActionPopupOpen)
        {
            CloseItemActionPopup();
            return;
        }

        if (isMoveMode)
        {
            CancelMoveMode();
            return;
        }

        CloseAndResume();
    }

    /// <summary>Rotates the selected item clockwise and enters move mode when needed.</summary>
    public void RotateSelectedItem()
    {
        RotateSelectedItemRight();
    }

    /// <summary>Rotates the selected item counterclockwise by one quarter turn.</summary>
    public void RotateSelectedItemLeft()
    {
        RotateSelectedItemBy(-1);
    }

    /// <summary>Rotates the selected item clockwise by one quarter turn.</summary>
    public void RotateSelectedItemRight()
    {
        RotateSelectedItemBy(1);
    }

    private void RotateSelectedItemBy(int direction)
    {
        ItemInstance item = isMoveMode
            ? GetMovingCarryItem()
            : isActionPopupOpen ? GetSelectedCarryItem() : GetFocusedCarryItem() ?? GetSelectedCarryItem();
        if (item == null)
        {
            SetStatus("Select a carried item before rotating it.");
            return;
        }

        if (!isMoveMode)
        {
            StartMoveMode();
        }

        if (!isMoveMode)
        {
            return;
        }

        movePreviewQuarterTurns = ((movePreviewQuarterTurns + direction) % 4 + 4) % 4;
        ApplyMovePreview(movePreviewPosition);
        SetStatus($"{item.Definition.DisplayName} rotated {movePreviewQuarterTurns * 90}°. Choose a cell to place it.");
    }

    /// <summary>Equips or unequips the selected weapon through the existing held-weapon rig.</summary>
    public void ToggleSelectedEquipment()
    {
        ItemInstance item = GetSelectedCarryItem();
        if (item == null)
        {
            SetStatus("Select an item in the carry grid first.");
            return;
        }

        if (item.Definition is WeaponDefinition)
        {
            WeaponHoldController weaponHoldController = activePlayerInventory.GetComponentInChildren<WeaponHoldController>(true);
            if (weaponHoldController == null)
            {
                SetStatus("The player has no weapon equipment controller.");
                return;
            }

            if (weaponHoldController.CurrentWeaponItem == item)
            {
                weaponHoldController.Unequip();
                SetStatus($"{item.Definition.DisplayName} unequipped.");
            }
            else if (!weaponHoldController.TryEquipOwnedWeapon(item))
            {
                SetStatus($"Could not equip {item.Definition.DisplayName}; check the held-weapon prefab catalog.");
                return;
            }
            else
            {
                SetStatus($"{item.Definition.DisplayName} equipped.");
            }

            RefreshInventoryView();
            return;
        }

        if (item.Definition.Category == ItemCategory.Weapon)
        {
            SetStatus($"{item.Definition.DisplayName} cannot be equipped because this weapon type is not available in this build.");
            return;
        }

        if (item.Definition.Category != ItemCategory.Equipment)
        {
            SetStatus("This item has no equipment action in this build.");
            return;
        }

        bool wasEquipped = item.IsEquipped;
        InventoryOperationResult result = activeInventory.TrySetEquipped(item.InstanceId, !wasEquipped);
        SetOperationStatus(result, wasEquipped ? "Item unequipped." : "Item equipped.");
        if (result.Succeeded)
        {
            RefreshInventoryView();
        }
    }

    /// <summary>Reports that healing items are unavailable until the game has a player health system.</summary>
    public void UseOrToggleSelectedAction()
    {
        ItemInstance item = GetSelectedCarryItem();
        if (item != null && item.Definition is HealingItemDefinition)
        {
            SetStatus("Healing items are not available in this build because player health is not implemented.");
            return;
        }

        ToggleSelectedEquipment();
    }

    /// <summary>Splits half of the selected stack into the first legal free grid placement.</summary>
    public void SplitSelectedStack()
    {
        ItemInstance item = GetSelectedCarryItem();
        if (item == null || item.Definition.MaximumStackSize <= 1 || item.Quantity < 2)
        {
            SetStatus("Select a stack containing at least two items to split it.");
            return;
        }

        if (!activeInventory.TryFindFirstFit(item.Definition, out Vector2Int position, out bool rotated))
        {
            SetStatus("No free cells are available for the split stack.");
            return;
        }

        int quantityToSplit = item.Quantity / 2;
        InventoryOperationResult result = activeInventory.TrySplitStack(item.InstanceId, quantityToSplit, position, rotated, out ItemInstance splitItem);
        SetOperationStatus(result, $"Split {quantityToSplit} from the stack.");
        if (result.Succeeded && splitItem != null)
        {
            selectedItemInstanceId = splitItem.InstanceId;
            RefreshInventoryView();
        }
    }

    /// <summary>Arms or disarms merge mode; the next compatible stack click merges the selected source into it.</summary>
    public void ToggleMergeTargetSelection()
    {
        ItemInstance item = GetSelectedCarryItem();
        if (item == null || item.Definition.MaximumStackSize <= 1)
        {
            SetStatus("Select a stackable item before choosing a merge target.");
            return;
        }

        isMergeTargetSelectionActive = !isMergeTargetSelectionActive;
        SetStatus(isMergeTargetSelectionActive
            ? "Select another compatible stack to merge into."
            : "Merge selection cancelled.");
        RefreshDetailPanel();
    }

    /// <summary>Sorts only the carried inventory using the model's atomic deterministic operation.</summary>
    public void SortCarryItems()
    {
        if (!isOpen || activeTabIndex != 1 || activeInventory == null)
        {
            SetStatus("Open the Inventory tab to sort carried items.");
            return;
        }

        InventoryOperationResult result = activeInventory.TryAutoSort();
        SetStatus(result.Succeeded
            ? "Carry inventory sorted. Equipped items stayed fixed."
            : $"Could not sort carry inventory: {result.Failure}.");
        RefreshInventoryView();
    }

    private void Update()
    {
        if (!isOpen)
        {
            return;
        }

        if (isMoveMode && EventSystem.current != null)
        {
            UpdateMoveSelection();
        }

        if (activeTabIndex == 1 && !isActionPopupOpen)
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
                {
                    MoveGridFocus(Vector2Int.down);
                }
                else if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)
                {
                    MoveGridFocus(Vector2Int.up);
                }
                else if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
                {
                    MoveGridFocus(Vector2Int.left);
                }
                else if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
                {
                    MoveGridFocus(Vector2Int.right);
                }
            }

            if (Gamepad.current != null)
            {
                if (Gamepad.current.dpad.up.wasPressedThisFrame)
                {
                    MoveGridFocus(Vector2Int.down);
                }
                else if (Gamepad.current.dpad.down.wasPressedThisFrame)
                {
                    MoveGridFocus(Vector2Int.up);
                }
                else if (Gamepad.current.dpad.left.wasPressedThisFrame)
                {
                    MoveGridFocus(Vector2Int.left);
                }
                else if (Gamepad.current.dpad.right.wasPressedThisFrame)
                {
                    MoveGridFocus(Vector2Int.right);
                }
            }
        }

        if (activeTabIndex == 1 && Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            StartMoveMode();
        }

        if (!isActionPopupOpen && Keyboard.current != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                RotateSelectedItemLeft();
            }
            else if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.rKey.wasPressedThisFrame)
            {
                RotateSelectedItemRight();
            }
        }


        if (Gamepad.current != null)
        {
            if (Gamepad.current.leftTrigger.wasPressedThisFrame)
            {
                RotateSelectedItemLeft();
            }
            else if (Gamepad.current.rightTrigger.wasPressedThisFrame)
            {
                RotateSelectedItemRight();
            }
            else if (activeTabIndex == 1 && Gamepad.current.buttonWest.wasPressedThisFrame)
            {
                StartMoveMode();
            }
            else if (Gamepad.current.rightStickButton.wasPressedThisFrame && !isMoveMode && !isActionPopupOpen)
            {
                SortCarryItems();
            }
            else if (Gamepad.current.leftShoulder.wasPressedThisFrame)
            {
                ChangeTab(-1);
            }
            else if (Gamepad.current.rightShoulder.wasPressedThisFrame)
            {
                ChangeTab(1);
            }
        }

        UpdateSelectionPulse();
        RefreshControlsLegend();
    }

    private void UpdateSelectionPulse()
    {
        string focusedInstanceId = isActionPopupOpen ? selectedItemInstanceId : focusedItemInstanceId;
        if (string.IsNullOrWhiteSpace(focusedInstanceId))
        {
            focusedInstanceId = selectedItemInstanceId;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        for (int index = 0; index < itemTiles.Count; index++)
        {
            GameObject tile = itemTiles[index];
            if (tile == null)
            {
                continue;
            }

            Outline outline = tile.GetComponent<Outline>();
            if (outline == null)
            {
                continue;
            }

            bool isFocusedTile = !isMoveMode && itemTileInstanceIds.TryGetValue(tile, out string instanceId) &&
                                 instanceId == focusedInstanceId;
            outline.enabled = isFocusedTile;
            if (isFocusedTile)
            {
                outline.effectColor = new Color(1f, 1f, 1f, 0.65f + pulse * 0.35f);
                float outlineSize = 2f + pulse * 3f;
                outline.effectDistance = new Vector2(outlineSize, -outlineSize);
            }
        }
    }

    private void MoveGridFocus(Vector2Int direction)
    {
        if (activeTabIndex != 1 || activeInventory == null || direction == Vector2Int.zero)
        {
            return;
        }

        if (isMoveMode)
        {
            Vector2Int nextCell = new Vector2Int(
                Mathf.Clamp(movePreviewPosition.x + direction.x, 0, activeInventory.GridWidth - 1),
                Mathf.Clamp(movePreviewPosition.y + direction.y, 0, activeInventory.GridHeight - 1));
            if (nextCell != movePreviewPosition)
            {
                ApplyMovePreview(nextCell);
                FocusGridCell(nextCell);
            }

            return;
        }

        ItemInstance currentItem = GetFocusedCarryItem() ?? GetSelectedCarryItem();
        if (currentItem == null)
        {
            FocusInitialGridItem();
            currentItem = GetFocusedCarryItem();
        }

        ItemInstance nextItem = FindDirectionalItem(currentItem, direction);
        if (nextItem == null)
        {
            return;
        }

        focusedItemInstanceId = nextItem.InstanceId;
        GameObject nextItemObject = GetItemTileObject(nextItem.InstanceId);
        if (EventSystem.current != null && nextItemObject != null)
        {
            EventSystem.current.SetSelectedGameObject(nextItemObject);
        }

        FocusInventoryItem(nextItem);
    }

    private ItemInstance FindDirectionalItem(ItemInstance sourceItem, Vector2Int direction)
    {
        if (sourceItem == null || activeInventory == null)
        {
            return null;
        }

        float sourceWidth = sourceItem.IsRotated ? sourceItem.Definition.InventoryHeight : sourceItem.Definition.InventoryWidth;
        float sourceHeight = sourceItem.IsRotated ? sourceItem.Definition.InventoryWidth : sourceItem.Definition.InventoryHeight;
        Vector2 sourceCenter = new Vector2(sourceItem.GridPosition.x + sourceWidth * 0.5f, sourceItem.GridPosition.y + sourceHeight * 0.5f);
        IReadOnlyList<ItemInstance> items = activeInventory.Items;
        ItemInstance bestItem = null;
        float bestScore = float.MaxValue;
        for (int index = 0; index < items.Count; index++)
        {
            ItemInstance candidate = items[index];
            if (candidate == null || candidate.InstanceId == sourceItem.InstanceId)
            {
                continue;
            }

            float candidateWidth = candidate.IsRotated ? candidate.Definition.InventoryHeight : candidate.Definition.InventoryWidth;
            float candidateHeight = candidate.IsRotated ? candidate.Definition.InventoryWidth : candidate.Definition.InventoryHeight;
            Vector2 delta = new Vector2(
                candidate.GridPosition.x + candidateWidth * 0.5f - sourceCenter.x,
                candidate.GridPosition.y + candidateHeight * 0.5f - sourceCenter.y);
            float forwardDistance = delta.x * direction.x + delta.y * direction.y;
            if (forwardDistance <= 0f)
            {
                continue;
            }

            float lateralDistance = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
            float score = forwardDistance * forwardDistance + lateralDistance * lateralDistance * 4f;
            if (score < bestScore)
            {
                bestScore = score;
                bestItem = candidate;
            }
        }

        return bestItem;
    }

    private void FocusInitialGridItem()
    {
        if (EventSystem.current == null || activeTabIndex != 1)
        {
            return;
        }

        GameObject target = GetItemTileObject(focusedItemInstanceId) ?? GetSelectedItemObject();
        if (target == null && itemTiles.Count > 0)
        {
            target = itemTiles[0];
        }

        if (target == null && gridCellButtons.Count > 0)
        {
            target = gridCellButtons[0].gameObject;
        }

        if (target != null)
        {
            EventSystem.current.SetSelectedGameObject(target);
        }
    }

    private GameObject GetItemTileObject(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        foreach (KeyValuePair<GameObject, string> tile in itemTileInstanceIds)
        {
            if (tile.Value == instanceId)
            {
                return tile.Key;
            }
        }

        return null;
    }

    private void FocusInventoryItem(ItemInstance item)
    {
        if (item == null || isMoveMode)
        {
            return;
        }

        focusedItemInstanceId = item.InstanceId;
        RefreshDetailPanel();
    }

    private void BuildMenu()
    {
        GameObject rootObject = new GameObject("Inventory Menu", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        rootObject.transform.SetParent(transform, false);
        menuRoot = rootObject;
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        SetStretch(rootRect, Vector2.zero, Vector2.one, Vector2.zero);
        Image backdrop = rootObject.GetComponent<Image>();
        backdrop.color = new Color(0.01f, 0.014f, 0.02f, 0.86f);
        backdrop.raycastTarget = true;

        RectTransform window = CreatePanel("Window", rootRect, WindowColor, new Vector2(0.015f, 0.015f), new Vector2(0.985f, 0.985f));
        RectTransform tabBar = CreatePanel("Tab Bar", window, PanelColor, new Vector2(0.025f, 0.91f), new Vector2(0.975f, 0.985f));
        CreateLabel("Previous Tab Shortcut", tabBar, "L1", 16, TextAnchor.MiddleCenter, new Vector2(0f, 0.08f), new Vector2(0.1f, 0.92f));
        CreateLabel("Next Tab Shortcut", tabBar, "R1", 16, TextAnchor.MiddleCenter, new Vector2(0.9f, 0.08f), new Vector2(1f, 0.92f));
        string[] tabNames = { "Map", InventoryTabName, "Key Items", "Files", "Options" };
        for (int index = 0; index < tabNames.Length; index++)
        {
            int capturedIndex = index;
            float tabMin = 0.1f + (float)index / tabNames.Length * 0.8f;
            float tabMax = 0.1f + (float)(index + 1) / tabNames.Length * 0.8f;
            Button button = CreateButton(
                tabNames[index],
                tabBar,
                new Vector2(tabMin, 0.08f),
                new Vector2(tabMax, 0.92f),
                index == 1 ? SelectedColor : new Color(0.12f, 0.14f, 0.17f, 1f),
                22,
                () => OnTabSelected(capturedIndex));
            tabButtons.Add(button);
        }

        RectTransform contentHost = CreatePanel("Tab Content", window, Color.clear, new Vector2(0.025f, 0.075f), new Vector2(0.975f, 0.90f));
        contentHost.GetComponent<Image>().raycastTarget = false;
        BuildPlaceholderTab(contentHost, "Map Content", "MAP", "Map content will be connected when the world-map data is available.");
        BuildInventoryTab(contentHost);
        BuildKeyItemsTab(contentHost);
        BuildPlaceholderTab(contentHost, "Files Content", "FILES", "Collected documents and lore files will appear here.");
        BuildPlaceholderTab(contentHost, "Options Content", "OPTIONS", "Select the Options tab to open the existing options menu.");

        RectTransform footer = CreatePanel("Footer", window, Color.clear, new Vector2(0.025f, 0.005f), new Vector2(0.975f, 0.07f));
        footer.GetComponent<Image>().raycastTarget = false;
        CreateLabel("Status", footer, string.Empty, 16, TextAnchor.MiddleLeft, new Vector2(0.01f, 0.53f), new Vector2(0.99f, 0.98f));
        inventoryStatusText = footer.Find("Status")?.GetComponent<Text>();
        controlLegendText = CreateLabel("Control Legend", footer, string.Empty, 21, TextAnchor.MiddleCenter, new Vector2(0.01f, 0.02f), new Vector2(0.99f, 0.55f));
        controlLegendText.fontStyle = FontStyle.Bold;
        CreateItemActionPopup(rootRect);
    }

    private void CreateItemActionPopup(RectTransform rootRect)
    {
        actionPopupRect = CreatePanel("Item Action Popup", rootRect, PanelColor, new Vector2(0f, 1f), new Vector2(0f, 1f));
        actionPopupRect.pivot = new Vector2(0f, 1f);
        actionPopupRect.sizeDelta = new Vector2(ActionPopupWidth, 180f);
        actionPopupRoot = actionPopupRect.gameObject;
        Image popupImage = actionPopupRoot.GetComponent<Image>();
        popupImage.color = PanelColor;
        popupImage.raycastTarget = true;
        actionPopupHeadingText = CreateLabel("Popup Heading", actionPopupRect, "ITEM ACTIONS", 14, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.98f));
        actionPopupListTransform = CreateRect("Popup Actions", actionPopupRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -ActionPopupHeaderHeight), new Vector2(ActionPopupWidth - 16f, 120f));
        actionPopupRoot.SetActive(false);
    }

    private void OpenItemActionPopup()
    {
        if (GetSelectedCarryItem() == null && GetSelectedStashItem() == null && GetSelectedKeyItem() == null)
        {
            SetStatus("Select an item before opening its actions.");
            return;
        }

        isActionPopupOpen = true;
        actionPopupRoot.SetActive(true);
        RefreshActionPopup();
        PositionItemActionPopup();
        if (EventSystem.current != null)
        {
            for (int index = 0; index < popupActionButtons.Count; index++)
            {
                if (popupActionButtons[index] != null && popupActionButtons[index].interactable)
                {
                    EventSystem.current.SetSelectedGameObject(popupActionButtons[index].gameObject);
                    break;
                }
            }
        }

        RefreshControlsLegend();
    }

    private void CloseItemActionPopup()
    {
        if (!isActionPopupOpen)
        {
            return;
        }

        isActionPopupOpen = false;
        if (actionPopupRoot != null)
        {
            actionPopupRoot.SetActive(false);
        }

        GameObject focusTarget = GetSelectedItemObject();
        if (EventSystem.current != null && focusTarget != null && focusTarget.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(focusTarget);
        }

        RefreshControlsLegend();
    }

    private void PositionItemActionPopup()
    {
        if (actionPopupRect == null || menuRoot == null)
        {
            return;
        }

        GameObject selectedObject = GetSelectedItemObject();
        RectTransform itemRect = selectedObject != null ? selectedObject.GetComponent<RectTransform>() : null;
        RectTransform menuRect = menuRoot.GetComponent<RectTransform>();
        if (itemRect == null || menuRect == null)
        {
            return;
        }

        Vector3 itemTopRight = itemRect.TransformPoint(new Vector3(itemRect.rect.xMax, itemRect.rect.yMax, 0f));
        Vector3 localPoint = menuRect.InverseTransformPoint(itemTopRight);
        float x = localPoint.x - menuRect.rect.xMin + 10f;
        float y = localPoint.y - menuRect.rect.yMax;
        float maximumX = Mathf.Max(8f, menuRect.rect.width - actionPopupRect.sizeDelta.x - 8f);
        if (x > maximumX)
        {
            x = localPoint.x - menuRect.rect.xMin - itemRect.rect.width - actionPopupRect.sizeDelta.x - 10f;
        }

        float minimumY = Mathf.Min(-8f, -(menuRect.rect.height - actionPopupRect.sizeDelta.y - 8f));
        actionPopupRect.anchoredPosition = new Vector2(
            Mathf.Clamp(x, 8f, maximumX),
            Mathf.Clamp(y, minimumY, -8f));
    }

    private GameObject GetSelectedItemObject()
    {
        string instanceId = !string.IsNullOrWhiteSpace(selectedStashItemInstanceId)
            ? selectedStashItemInstanceId
            : !string.IsNullOrWhiteSpace(selectedKeyItemInstanceId)
                ? selectedKeyItemInstanceId
                : selectedItemInstanceId;
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        foreach (KeyValuePair<GameObject, string> tile in itemTileInstanceIds)
        {
            if (tile.Value == instanceId)
            {
                return tile.Key;
            }
        }

        if (activePlayerInventory != null)
        {
            IReadOnlyList<ItemInstance> stashItems = activePlayerInventory.StashItems;
            for (int index = 0; index < stashRows.Count && index < stashItems.Count; index++)
            {
                if (stashItems[index].InstanceId == instanceId)
                {
                    return stashRows[index];
                }
            }

            IReadOnlyList<ItemInstance> keyItems = activePlayerInventory.KeyItems;
            for (int index = 0; index < keyItemRows.Count && index < keyItems.Count; index++)
            {
                if (keyItems[index].InstanceId == instanceId)
                {
                    return keyItemRows[index];
                }
            }
        }

        return null;
    }

    private void RefreshActionPopup()
    {
        if (!isActionPopupOpen || actionPopupListTransform == null)
        {
            return;
        }

        for (int index = 0; index < popupActionButtons.Count; index++)
        {
            if (popupActionButtons[index] != null)
            {
                Destroy(popupActionButtons[index].gameObject);
            }
        }

        popupActionButtons.Clear();
        ItemInstance selectedItem = GetSelectedStashItem();
        bool selectedFromStash = selectedItem != null;
        bool selectedFromKeyItems = false;
        if (selectedItem == null)
        {
            selectedItem = GetSelectedKeyItem();
            selectedFromKeyItems = selectedItem != null;
        }

        if (selectedItem == null)
        {
            selectedItem = GetSelectedCarryItem();
        }

        if (selectedItem == null)
        {
            CloseItemActionPopup();
            return;
        }

        actionPopupHeadingText.text = $"ACTIONS  /  {selectedItem.Definition.DisplayName}";
        List<MenuActionEntry> actions = selectedFromKeyItems
            ? new List<MenuActionEntry>
            {
                new MenuActionEntry("Back / Cancel", CloseItemActionPopup)
            }
            : BuildAvailableItemActions(selectedItem, selectedFromStash);
        actionPopupRect.sizeDelta = new Vector2(ActionPopupWidth, ActionPopupHeaderHeight + actions.Count * ActionPopupRowHeight + 8f);
        actionPopupListTransform.sizeDelta = new Vector2(ActionPopupWidth - 16f, actions.Count * ActionPopupRowHeight);
        for (int index = 0; index < actions.Count; index++)
        {
            MenuActionEntry action = actions[index];
            Button actionButton = CreateButton(action.Label, actionPopupListTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                action.IsAvailable ? new Color(0.15f, 0.19f, 0.24f, 1f) : new Color(0.1f, 0.12f, 0.15f, 1f),
                14, () =>
                {
                    if (!action.IsAvailable)
                    {
                        return;
                    }

                    CloseItemActionPopup();
                    action.Execute?.Invoke();
                });
            RectTransform actionButtonRect = actionButton.GetComponent<RectTransform>();
            actionButtonRect.anchorMin = new Vector2(0f, 1f);
            actionButtonRect.anchorMax = new Vector2(0f, 1f);
            actionButtonRect.pivot = new Vector2(0f, 1f);
            actionButtonRect.sizeDelta = new Vector2(ActionPopupWidth - 16f, ActionPopupRowHeight - 2f);
            actionButtonRect.anchoredPosition = new Vector2(0f, -index * ActionPopupRowHeight);
            actionButton.interactable = action.IsAvailable;
            popupActionButtons.Add(actionButton);
        }

        PositionItemActionPopup();
        if (EventSystem.current != null)
        {
            GameObject currentSelection = EventSystem.current.currentSelectedGameObject;
            if (currentSelection == null || !currentSelection.transform.IsChildOf(actionPopupRoot.transform))
            {
                for (int index = 0; index < popupActionButtons.Count; index++)
                {
                    if (popupActionButtons[index] != null && popupActionButtons[index].interactable)
                    {
                        EventSystem.current.SetSelectedGameObject(popupActionButtons[index].gameObject);
                        break;
                    }
                }
            }
        }
    }

    private List<MenuActionEntry> BuildAvailableItemActions(ItemInstance item, bool selectedFromStash)
    {
        List<MenuActionEntry> actions = new List<MenuActionEntry>();
        ItemDefinition definition = item.Definition;
        if (selectedFromStash)
        {
            if (definition.CanBeStored)
            {
                actions.Add(new MenuActionEntry("Withdraw", WithdrawSelectedStashItem));
            }

            actions.Add(new MenuActionEntry("Back / Cancel", CloseItemActionPopup));
            return actions;
        }

        if (definition is WeaponDefinition)
        {
            WeaponHoldController weaponController = activePlayerInventory != null
                ? activePlayerInventory.GetComponentInChildren<WeaponHoldController>(true)
                : null;
            bool canEquip = weaponController != null;
            string equipLabel = item.IsEquipped ? "Unequip" : "Equip";
            actions.Add(new MenuActionEntry(canEquip ? equipLabel : $"{equipLabel} — unavailable", UseOrToggleSelectedAction, canEquip));

            ItemInstance primaryWeapon = activePlayerInventory.GetWeaponInEquipmentSlot(PlayerItemInventory.PrimaryWeaponSlotIndex);
            ItemInstance secondaryWeapon = activePlayerInventory.GetWeaponInEquipmentSlot(PlayerItemInventory.SecondaryWeaponSlotIndex);
            if (primaryWeapon == null || primaryWeapon.InstanceId != item.InstanceId)
            {
                actions.Add(new MenuActionEntry("Assign Primary", () => AssignSelectedWeapon(PlayerItemInventory.PrimaryWeaponSlotIndex)));
            }

            if (secondaryWeapon == null || secondaryWeapon.InstanceId != item.InstanceId)
            {
                actions.Add(new MenuActionEntry("Assign Secondary", () => AssignSelectedWeapon(PlayerItemInventory.SecondaryWeaponSlotIndex)));
            }
        }
        else if (definition.Category == ItemCategory.Weapon)
        {
            actions.Add(new MenuActionEntry("Equip — unavailable", UseOrToggleSelectedAction, false));
        }
        else if (definition.Category == ItemCategory.Equipment)
        {
            actions.Add(new MenuActionEntry(item.IsEquipped ? "Unequip" : "Equip", ToggleSelectedEquipment));
        }
        else if (definition is HealingItemDefinition || definition.Category == ItemCategory.Consumable)
        {
            actions.Add(new MenuActionEntry("Use — unavailable", UseOrToggleSelectedAction, false));
        }
        else if (definition is WeaponAttachmentDefinition)
        {
            actions.Add(new MenuActionEntry("Workbench — unavailable", () => SetStatus("Attachment changes are available only through an authored Workbench handler."), false));
        }
        else if (definition.Category == ItemCategory.Deployable)
        {
            actions.Add(new MenuActionEntry("Deploy — unavailable", () => SetStatus("Deployable actions are unavailable until their runtime handler is implemented."), false));
        }

        if (ItemInventory.TryGetNextCaseDimensions(activeInventory.GridWidth, activeInventory.GridHeight, out _, out _) &&
            definition is InventoryCapacityUpgradeDefinition)
        {
            actions.Add(new MenuActionEntry("Apply Upgrade", ApplySelectedCapacityUpgrade));
        }



        if (definition.Category != ItemCategory.Weapon && definition.CanBeDiscarded && !item.IsEquipped)
        {
            actions.Add(new MenuActionEntry("Discard", DiscardSelectedCarryItem));
        }

        if (isStashMode && definition.CanBeStored && !item.IsEquipped)
        {
            actions.Add(new MenuActionEntry("Store", StoreSelectedCarryItem));
        }

        actions.Add(new MenuActionEntry("Back / Cancel", CloseItemActionPopup));
        return actions;
    }

    private void RefreshControlsLegend()
    {
        if (controlLegendText == null)
        {
            return;
        }

        string hint;
        if (isMoveMode)
        {
            hint = "WASD / D-PAD  PLACE     X / A  PLACE     O / B  CANCEL     Q / E OR L2 / R2  ROTATE     L1 / R1  TABS";
        }
        else if (isActionPopupOpen)
        {
            hint = "SQUARE / M  MOVE ITEM     X / A  SELECT ACTION     O / B  BACK";
        }
        else if (activeTabIndex == 1 && (GetSelectedCarryItem() != null || GetFocusedCarryItem() != null))
        {
            hint = "WASD / D-PAD  ITEMS     SQUARE / M  MOVE     X / A  ACTIONS     R3  SORT     Q / E OR L2 / R2  ROTATE";
        }
        else if (activeTabIndex == 1 && GetSelectedStashItem() != null)
        {
            hint = "X / A  ITEM ACTIONS     O / B  CLOSE     L1 / R1  TABS";
        }
        else if (activeTabIndex == 1)
        {
            hint = "WASD / D-PAD  ITEMS     R3  SORT     X / A  SELECT ITEM     O / B  CLOSE     L1 / R1  TABS";
        }
        else if (activeTabIndex == 2 && GetSelectedKeyItem() != null)
        {
            hint = "X / A  SELECT ITEM     O / B  CLOSE     L1 / R1  TABS";
        }
        else
        {
            hint = "X / A  SELECT     O / B  CLOSE     L1 / R1  TABS";
        }

        if (lastControlLegend != hint)
        {
            controlLegendText.text = hint;
            lastControlLegend = hint;
        }
    }

    private void BuildInventoryTab(RectTransform contentHost)
    {
        GameObject panel = CreatePanel("Inventory Content", contentHost, Color.clear, Vector2.zero, Vector2.one).gameObject;
        panel.GetComponent<Image>().raycastTarget = false;
        tabPanels.Add(panel);

        carryGridPanel = CreatePanel("Carry Grid Panel", panel.transform, PanelColor, new Vector2(0.01f, 0.015f), new Vector2(0.42f, 0.985f));

        RectTransform scrollRoot = CreatePanel("Grid Scroll", carryGridPanel, new Color(0.035f, 0.04f, 0.05f, 1f), new Vector2(0.035f, 0.02f), new Vector2(0.965f, 0.98f));
        gridScrollRootTransform = scrollRoot;
        RectTransform viewport = CreateRect("Viewport", scrollRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Color.white;
        viewportImage.raycastTarget = true;
        Mask viewportMask = viewport.gameObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;


        gridContentTransform = CreateRect("Content", viewport, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        gridContentTransform.pivot = new Vector2(0f, 1f);
        gridScrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
        gridScrollRect.viewport = viewport;
        gridScrollRect.content = gridContentTransform;
        gridScrollRect.horizontal = true;
        gridScrollRect.vertical = true;
        gridScrollRect.movementType = ScrollRect.MovementType.Clamped;
        gridScrollRect.scrollSensitivity = 35f;

        stashPanel = CreatePanel("Safe Room Stash Panel", panel.transform, PanelColor, new Vector2(0.44f, 0.015f), new Vector2(0.62f, 0.985f));
        stashPanel.gameObject.SetActive(false);
        CreateLabel("Stash Heading", stashPanel, "STASH", 22, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.91f), new Vector2(0.94f, 0.99f));
        CreateLabel("Stash Hint", stashPanel, "Select a stored item to withdraw.", 14, TextAnchor.UpperLeft, new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.91f));
        RectTransform stashViewport = CreateRect("Stash Viewport", stashPanel, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.8f), Vector2.zero, Vector2.zero);
        Image stashViewportImage = stashViewport.gameObject.AddComponent<Image>();
        stashViewportImage.color = Color.white;
        stashViewportImage.raycastTarget = true;
        Mask stashMask = stashViewport.gameObject.AddComponent<Mask>();
        stashMask.showMaskGraphic = false;
        stashContentTransform = CreateRect("Stash Items", stashViewport, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        stashContentTransform.pivot = new Vector2(0.5f, 1f);
        stashScrollRect = stashPanel.gameObject.AddComponent<ScrollRect>();
        stashScrollRect.viewport = stashViewport;
        stashScrollRect.content = stashContentTransform;
        stashScrollRect.horizontal = false;
        stashScrollRect.vertical = true;
        stashScrollRect.movementType = ScrollRect.MovementType.Clamped;
        stashScrollRect.scrollSensitivity = 35f;

        selectedItemPanel = CreatePanel("Selected Item Panel", panel.transform, PanelColor, new Vector2(0.44f, 0.015f), new Vector2(0.99f, 0.985f));
        CreateLabel("Details Heading", selectedItemPanel, "ITEM DETAILS", 24, TextAnchor.MiddleLeft, new Vector2(0.045f, 0.9f), new Vector2(0.955f, 0.99f));
        selectedItemNameText = CreateLabel("Selected Item Name", selectedItemPanel, "Select an item", 29, TextAnchor.MiddleLeft, new Vector2(0.045f, 0.82f), new Vector2(0.955f, 0.9f));
        selectedItemCategoryText = CreateLabel("Selected Item Category", selectedItemPanel, string.Empty, 17, TextAnchor.MiddleLeft, new Vector2(0.045f, 0.76f), new Vector2(0.955f, 0.82f));
        selectedItemCategoryText.fontStyle = FontStyle.Bold;
        selectedItemCategoryText.color = SelectedColor;
        selectedItemDescriptionText = CreateLabel("Selected Item Description", selectedItemPanel, string.Empty, 18, TextAnchor.UpperLeft, new Vector2(0.045f, 0.59f), new Vector2(0.955f, 0.75f));
        selectedItemStatsText = CreateLabel("Selected Item Stats", selectedItemPanel, string.Empty, 16, TextAnchor.UpperLeft, new Vector2(0.045f, 0.39f), new Vector2(0.955f, 0.57f));
        selectedItemDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        selectedItemStatsText.horizontalOverflow = HorizontalWrapMode.Wrap;

        comparisonPanel = CreatePanel("Weapon Comparison", selectedItemPanel, new Color(0.045f, 0.052f, 0.065f, 0.96f), new Vector2(0.045f, 0.06f), new Vector2(0.955f, 0.37f));
        CreateLabel("Comparison Heading", comparisonPanel, "SELECTED vs ACTIVE WEAPON", 14, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.78f), new Vector2(0.96f, 0.98f));
        RectTransform comparisonViewport = CreateRect("Comparison Viewport", comparisonPanel, new Vector2(0.025f, 0.04f), new Vector2(0.975f, 0.76f), Vector2.zero, Vector2.zero);
        Image comparisonViewportImage = comparisonViewport.gameObject.AddComponent<Image>();
        comparisonViewportImage.color = Color.white;
        comparisonViewportImage.raycastTarget = true;
        Mask comparisonMask = comparisonViewport.gameObject.AddComponent<Mask>();
        comparisonMask.showMaskGraphic = false;
        comparisonContentTransform = CreateRect("Comparison Content", comparisonViewport, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        comparisonContentTransform.pivot = new Vector2(0.5f, 1f);
        RectTransform comparisonTextRect = CreateRect("Comparison Text", comparisonContentTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        comparisonText = comparisonTextRect.gameObject.AddComponent<Text>();
        comparisonText.font = uiFont;
        comparisonText.fontSize = 13;
        comparisonText.color = TextColor;
        comparisonText.alignment = TextAnchor.UpperLeft;
        comparisonText.horizontalOverflow = HorizontalWrapMode.Wrap;
        comparisonText.verticalOverflow = VerticalWrapMode.Overflow;
        comparisonText.raycastTarget = false;
        comparisonScrollRect = comparisonPanel.gameObject.AddComponent<ScrollRect>();
        comparisonScrollRect.viewport = comparisonViewport;
        comparisonScrollRect.content = comparisonContentTransform;
        comparisonScrollRect.horizontal = false;
        comparisonScrollRect.vertical = true;
        comparisonScrollRect.movementType = ScrollRect.MovementType.Clamped;
        comparisonScrollRect.scrollSensitivity = 24f;
        comparisonPanel.gameObject.SetActive(false);

        // Item actions are presented in the contextual popup.
    }

    private void BuildKeyItemsTab(RectTransform contentHost)
    {
        RectTransform panel = CreatePanel("Key Items Content", contentHost, PanelColor, Vector2.zero, Vector2.one);
        tabPanels.Add(panel.gameObject);
        keyItemsHeadingText = CreateLabel("Key Items Heading", panel, "KEY ITEMS", 25, TextAnchor.MiddleLeft, new Vector2(0.035f, 0.89f), new Vector2(0.965f, 0.98f));
        CreateLabel("Key Items Hint", panel, "Protected key and quest items are stored separately from the carry grid.", 17, TextAnchor.MiddleLeft, new Vector2(0.035f, 0.82f), new Vector2(0.965f, 0.89f));
        CreatePanel("Key Item List", panel, new Color(0.035f, 0.04f, 0.05f, 1f), new Vector2(0.035f, 0.06f), new Vector2(0.48f, 0.8f));
        RectTransform detail = CreatePanel("Key Item Details", panel, new Color(0.055f, 0.065f, 0.08f, 1f), new Vector2(0.52f, 0.06f), new Vector2(0.965f, 0.8f));
        keyItemDetailText = CreateLabel("Key Item Detail", detail, "Select a key item to inspect it. These items cannot be sold or discarded.", 20, TextAnchor.UpperLeft, new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.96f));
        keyItemDetailText.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    private void BuildPlaceholderTab(RectTransform contentHost, string objectName, string title, string message)
    {
        RectTransform panel = CreatePanel(objectName, contentHost, PanelColor, Vector2.zero, Vector2.one);
        tabPanels.Add(panel.gameObject);
        CreateLabel("Placeholder Title", panel, title, 32, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.57f), new Vector2(0.92f, 0.75f));
        Text messageText = CreateLabel("Placeholder Message", panel, message, 20, TextAnchor.MiddleCenter, new Vector2(0.12f, 0.36f), new Vector2(0.88f, 0.58f));
        messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    private void OnTabSelected(int index)
    {
        if (index == 4)
        {
            OpenExistingOptions();
            return;
        }

        SelectTab(index);
    }

    private void SelectTab(int index)
    {
        if (index < 0 || index >= tabPanels.Count)
        {
            return;
        }

        if (index != activeTabIndex)
        {
            if (isActionPopupOpen)
            {
                CloseItemActionPopup();
            }

            if (isMoveMode)
            {
                CancelMoveMode();
            }
        }

        activeTabIndex = index;
        SetStashLayout();
        for (int tabIndex = 0; tabIndex < tabPanels.Count; tabIndex++)
        {
            tabPanels[tabIndex].SetActive(tabIndex == activeTabIndex);
        }

        for (int tabIndex = 0; tabIndex < tabButtons.Count; tabIndex++)
        {
            Image background = tabButtons[tabIndex].GetComponent<Image>();
            background.color = tabIndex == activeTabIndex
                ? SelectedColor
                : new Color(0.12f, 0.14f, 0.17f, 1f);
        }

        if (activeTabIndex == 1)
        {
            RefreshInventoryView();
        }
        else if (activeTabIndex == 2)
        {
            RefreshKeyItemsView();
        }
    }

    private void RefreshInventoryView()
    {
        if (!isOpen || activeInventory == null)
        {
            return;
        }

        if (isMoveMode)
        {
            isMoveMode = false;
            RollbackPendingPickupExchange();
            RestoreMovingItemVisuals();
            movingItemInstanceId = null;
        }

        ItemInstance selectedItem = GetSelectedCarryItem();
        if (selectedItem == null)
        {
            selectedItemInstanceId = null;
        }

        if (!string.IsNullOrWhiteSpace(focusedItemInstanceId) && activeInventory.FindItem(focusedItemInstanceId) == null)
        {
            focusedItemInstanceId = null;
        }

        BuildGridCells();
        BuildGridItemTiles();
        RefreshStashView();
        if (isActionPopupOpen)
        {
            RefreshActionPopup();
            PositionItemActionPopup();
        }

        if (!isActionPopupOpen && activeTabIndex == 1 && EventSystem.current != null)
        {
            GameObject selectedObject = GetItemTileObject(focusedItemInstanceId) ?? GetSelectedItemObject();
            if (selectedObject == null && itemTiles.Count > 0)
            {
                selectedObject = itemTiles[0];
            }

            if (selectedObject == null && gridCellButtons.Count > 0)
            {
                selectedObject = gridCellButtons[0].gameObject;
            }

            if (selectedObject != null && selectedObject.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(selectedObject);
            }
        }

        RefreshDetailPanel();
        SetStatus(string.IsNullOrWhiteSpace(statusMessage)
            ? isStashMode
                ? $"Carry: {activeInventory.Count} items   |   Stash: {activePlayerInventory.StashItems.Count} items"
                : $"{activeInventory.Count} items"
            : statusMessage);
    }

    private void BuildGridCells()
    {
        for (int index = 0; index < gridCellButtons.Count; index++)
        {
            if (gridCellButtons[index] != null)
            {
                Destroy(gridCellButtons[index].gameObject);
            }
        }

        gridCellButtons.Clear();
        gridCellImages.Clear();
        int gridWidth = activeInventory.GridWidth;
        int gridHeight = activeInventory.GridHeight;
        float availableWidth = Mathf.Max(1f, carryGridPanel.rect.width * 0.93f);
        float availableHeight = Mathf.Max(1f, carryGridPanel.rect.height * 0.96f);
        float widthLimitedCellSize = (availableWidth - GridPadding * 2f - Mathf.Max(0, gridWidth - 1) * GridCellSpacing) / gridWidth;
        float heightLimitedCellSize = (availableHeight - GridPadding * 2f - Mathf.Max(0, gridHeight - 1) * GridCellSpacing) / gridHeight;
        gridCellSize = Mathf.Max(24f, Mathf.Min(64f, widthLimitedCellSize, heightLimitedCellSize));
        float contentWidth = GridPadding * 2f + gridWidth * gridCellSize + Mathf.Max(0, gridWidth - 1) * GridCellSpacing;
        float contentHeight = GridPadding * 2f + gridHeight * gridCellSize + Mathf.Max(0, gridHeight - 1) * GridCellSpacing;
        gridScrollRootTransform.anchorMin = new Vector2(0.5f, 0.5f);
        gridScrollRootTransform.anchorMax = new Vector2(0.5f, 0.5f);
        gridScrollRootTransform.pivot = new Vector2(0.5f, 0.5f);
        gridScrollRootTransform.sizeDelta = new Vector2(contentWidth, contentHeight);
        gridScrollRootTransform.anchoredPosition = Vector2.zero;
        gridContentTransform.sizeDelta = new Vector2(contentWidth, contentHeight);

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                GameObject cellObject = new GameObject($"Cell {x},{y}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(EventTrigger));
                cellObject.transform.SetParent(gridContentTransform, false);
                RectTransform cellRect = cellObject.GetComponent<RectTransform>();
                cellRect.anchorMin = new Vector2(0f, 1f);
                cellRect.anchorMax = new Vector2(0f, 1f);
                cellRect.pivot = new Vector2(0f, 1f);
                cellRect.sizeDelta = new Vector2(gridCellSize, gridCellSize);
                cellRect.anchoredPosition = new Vector2(GridPadding + x * (gridCellSize + GridCellSpacing), -GridPadding - y * (gridCellSize + GridCellSpacing));

                Image cellImage = cellObject.GetComponent<Image>();
                cellImage.color = CellColor;
                cellImage.raycastTarget = true;
                Button cellButton = cellObject.GetComponent<Button>();
                cellButton.targetGraphic = cellImage;
                cellButton.navigation = new Navigation { mode = Navigation.Mode.None };
                cellButton.onClick.AddListener(() => HandleGridCellClicked(cell));
                AddPointerEvent(cellObject.GetComponent<EventTrigger>(), EventTriggerType.PointerEnter, () => PreviewPlacement(cell));
                AddPointerEvent(cellObject.GetComponent<EventTrigger>(), EventTriggerType.PointerExit, ResetCellPreview);
                AddPointerEvent(cellObject.GetComponent<EventTrigger>(), EventTriggerType.Cancel, () => OnCancel(null));
                gridCellButtons.Add(cellButton);
                gridCellImages.Add(cellImage);
            }
        }

        gridScrollRect.horizontalNormalizedPosition = 0f;
        gridScrollRect.verticalNormalizedPosition = 1f;
    }

    private void BuildGridItemTiles()
    {
        for (int index = 0; index < itemTiles.Count; index++)
        {
            if (itemTiles[index] != null)
            {
                Destroy(itemTiles[index]);
            }
        }

        itemTiles.Clear();
        itemTileInstanceIds.Clear();
        IReadOnlyList<ItemInstance> items = activeInventory.Items;
        for (int index = 0; index < items.Count; index++)
        {
            ItemInstance item = items[index];
            ItemDefinition definition = item.Definition;
            int footprintWidth = item.IsRotated ? definition.InventoryHeight : definition.InventoryWidth;
            int footprintHeight = item.IsRotated ? definition.InventoryWidth : definition.InventoryHeight;
            float tileWidth = footprintWidth * gridCellSize + Mathf.Max(0, footprintWidth - 1) * GridCellSpacing - 6f;
            float tileHeight = footprintHeight * gridCellSize + Mathf.Max(0, footprintHeight - 1) * GridCellSpacing - 6f;
            GameObject tile = new GameObject($"Item {definition.DisplayName}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(EventTrigger));
            tile.transform.SetParent(gridContentTransform, false);
            RectTransform tileRect = tile.GetComponent<RectTransform>();
            tileRect.anchorMin = new Vector2(0f, 1f);
            tileRect.anchorMax = new Vector2(0f, 1f);
            tileRect.pivot = new Vector2(0f, 1f);
            tileRect.sizeDelta = new Vector2(tileWidth, tileHeight);
            tileRect.anchoredPosition = new Vector2(
                GridPadding + item.GridPosition.x * (gridCellSize + GridCellSpacing) + 3f,
                -GridPadding - item.GridPosition.y * (gridCellSize + GridCellSpacing) - 3f);

            Image tileBackground = tile.GetComponent<Image>();
            bool isSelectedItem = item.InstanceId == selectedItemInstanceId;
            tileBackground.color = GetCategoryColor(definition.Category, isSelectedItem);
            tileBackground.raycastTarget = true;
            Outline selectionOutline = tile.AddComponent<Outline>();
            selectionOutline.effectColor = SelectedOutlineColor;
            selectionOutline.effectDistance = new Vector2(4f, -4f);
            selectionOutline.useGraphicAlpha = false;
            selectionOutline.enabled = isSelectedItem;
            Button tileButton = tile.GetComponent<Button>();
            tileButton.targetGraphic = tileBackground;
            tileButton.transition = Selectable.Transition.None;
            tileButton.navigation = new Navigation { mode = Navigation.Mode.None };
            tileButton.onClick.AddListener(() => SelectOrMergeItem(item));
            AddPointerEvent(tile.GetComponent<EventTrigger>(), EventTriggerType.PointerEnter, () => PreviewPlacement(item.GridPosition));
            AddPointerEvent(tile.GetComponent<EventTrigger>(), EventTriggerType.PointerExit, ResetCellPreview);
            AddPointerEvent(tile.GetComponent<EventTrigger>(), EventTriggerType.Select, () => FocusInventoryItem(item));
            AddPointerEvent(tile.GetComponent<EventTrigger>(), EventTriggerType.Cancel, () => OnCancel(null));
            itemTileInstanceIds.Add(tile, item.InstanceId);

            if (definition.InventoryIcon != null)
            {
                RectTransform iconRect = CreateRect("Icon", tile.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                Image icon = iconRect.gameObject.AddComponent<Image>();
                icon.sprite = definition.InventoryIcon;
                icon.color = Color.white;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                SetItemIconRotation(iconRect, tileRect, item.RotationQuarterTurns);
            }

            string quantityBadge = item.Quantity > 1 ? $"×{item.Quantity}" : string.Empty;
            Text quantityText = CreateLabel("Quantity Badge", tile.transform, quantityBadge, 20, TextAnchor.LowerRight, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.28f));
            quantityText.fontStyle = FontStyle.Bold;
            quantityText.resizeTextForBestFit = true;
            quantityText.resizeTextMinSize = 15;
            quantityText.resizeTextMaxSize = 20;
            quantityText.horizontalOverflow = HorizontalWrapMode.Overflow;
            quantityText.verticalOverflow = VerticalWrapMode.Overflow;


            if (item.IsEquipped)
            {
                Text equippedText = CreateLabel("Equipped Badge", tile.transform, "E", 15, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.78f), new Vector2(0.22f, 0.98f));
                equippedText.fontStyle = FontStyle.Bold;
                equippedText.color = SelectedColor;
            }

            if (activePlayerInventory != null)
            {
                ItemInstance primaryItem = activePlayerInventory.GetWeaponInEquipmentSlot(PlayerItemInventory.PrimaryWeaponSlotIndex);
                ItemInstance secondaryItem = activePlayerInventory.GetWeaponInEquipmentSlot(PlayerItemInventory.SecondaryWeaponSlotIndex);
                string slotBadge = primaryItem != null && primaryItem.InstanceId == item.InstanceId
                    ? "PRI"
                    : secondaryItem != null && secondaryItem.InstanceId == item.InstanceId ? "SEC" : string.Empty;
                if (!string.IsNullOrEmpty(slotBadge))
                {
                    Text slotText = CreateLabel("Weapon Slot Badge", tile.transform, slotBadge, 11, TextAnchor.MiddleRight, new Vector2(0.62f, 0.78f), new Vector2(0.97f, 0.98f));
                    slotText.fontStyle = FontStyle.Bold;
                    slotText.color = new Color(0.7f, 0.83f, 1f, 1f);
                }
            }

            itemTiles.Add(tile);
        }
    }


    private void RefreshDetailPanel()
    {
        ItemInstance item = GetSelectedStashItem();
        bool selectedFromStash = item != null;
        if (item == null)
        {
            item = GetFocusedCarryItem() ?? GetSelectedCarryItem();
        }

        if (item == null)
        {
            selectedItemNameText.text = "Select an item";
            selectedItemCategoryText.text = string.Empty;
            selectedItemDescriptionText.text = isStashMode
                ? "Select a carry or stash item to inspect it. Store and withdraw actions move ownership without creating duplicates."
                : "Choose an item to view its description and relevant stats.";
            selectedItemStatsText.text = string.Empty;
            comparisonPanel.gameObject.SetActive(false);
            if (isActionPopupOpen)
            {
                CloseItemActionPopup();
            }

            RefreshControlsLegend();
            return;
        }

        ItemDefinition definition = item.Definition;
        selectedItemNameText.text = definition.DisplayName + (selectedFromStash ? "  [STASH]" : string.Empty);
        selectedItemCategoryText.text = definition.Category.ToString().ToUpperInvariant();
        string description = string.IsNullOrWhiteSpace(definition.Description)
            ? "No additional item description."
            : definition.Description;
        if (isStashMode && !definition.CanBeStored)
        {
            description += "\n\nThis item is protected from stash transfers and cannot be stored or withdrawn.";
        }
        else if (isStashMode && !selectedFromStash && item.IsEquipped)
        {
            description += "\n\nUnequip this item before storing it.";
        }

        if (!definition.CanBeDiscarded)
        {
            description += "\n\nProtected item — it cannot be discarded.";
        }

        if (definition is HealingItemDefinition)
        {
            description += "\n\nHealing use is not available in this build because player health is not implemented.";
        }
        else if (definition is WeaponAttachmentDefinition)
        {
            description += "\n\nInstall or remove this attachment at a Workbench.";
        }
        else if (definition.Category == ItemCategory.CraftingMaterial || definition.Category == ItemCategory.CraftingTool)
        {
            description += "\n\nCrafting is available only through an authored Workbench recipe; none is configured at the Sandbox Workbench.";
        }
        else if (definition.Category == ItemCategory.UtilityTool || definition.Category == ItemCategory.Deployable ||
                 definition.Category == ItemCategory.Valuable || definition.Category == ItemCategory.Consumable)
        {
            description += "\n\nThis item action is not available in this build.";
        }
        else if (definition.Category == ItemCategory.Weapon && !(definition is WeaponDefinition))
        {
            description += "\n\nThis weapon type is not available in this build.";
        }

        selectedItemDescriptionText.text = description;
        selectedItemStatsText.text = BuildItemStats(item);
        RefreshWeaponComparison(item, selectedFromStash);
        if (isActionPopupOpen)
        {
            RefreshActionPopup();
        }

        RefreshControlsLegend();
    }

    private void RefreshStashView()
    {
        if (stashPanel == null || !isStashMode || activePlayerInventory == null)
        {
            return;
        }

        for (int index = 0; index < stashRows.Count; index++)
        {
            if (stashRows[index] != null)
            {
                Destroy(stashRows[index]);
            }
        }

        stashRows.Clear();
        IReadOnlyList<ItemInstance> items = activePlayerInventory.StashItems;
        Text heading = stashPanel.Find("Stash Heading")?.GetComponent<Text>();
        if (heading != null)
        {
            heading.text = $"STASH   {items.Count}";
        }

        float rowHeight = 48f;
        stashContentTransform.sizeDelta = new Vector2(0f, Mathf.Max(1f, items.Count * rowHeight));
        if (items.Count == 0)
        {
            Text emptyText = CreateLabel("Empty Stash", stashContentTransform, "Stash is empty.", 16, TextAnchor.MiddleCenter, new Vector2(0.05f, 0f), new Vector2(0.95f, 1f));
            stashRows.Add(emptyText.gameObject);
            return;
        }

        for (int index = 0; index < items.Count; index++)
        {
            ItemInstance item = items[index];
            float top = 1f - index * rowHeight / Mathf.Max(rowHeight, items.Count * rowHeight);
            float bottom = 1f - (index + 1) * rowHeight / Mathf.Max(rowHeight, items.Count * rowHeight);
            GameObject row = CreateButtonObject(
                $"Stash Item {index + 1}",
                stashContentTransform,
                new Vector2(0.02f, bottom),
                new Vector2(0.98f, top),
                GetCategoryColor(item.Definition.Category, item.InstanceId == selectedStashItemInstanceId),
                15,
                $"{item.Definition.DisplayName}  ×{item.Quantity}",
                () => SelectStashItem(item));
            stashRows.Add(row);
        }

        if (GetSelectedStashItem() == null)
        {
            selectedStashItemInstanceId = null;
        }


    }

    private void SetStashLayout()
    {
        if (carryGridPanel == null || selectedItemPanel == null || stashPanel == null)
        {
            return;
        }

        if (isStashMode)
        {
            SetStretch(carryGridPanel, new Vector2(0.01f, 0.015f), new Vector2(0.43f, 0.985f), Vector2.zero);
            SetStretch(stashPanel, new Vector2(0.44f, 0.015f), new Vector2(0.62f, 0.985f), Vector2.zero);
            SetStretch(selectedItemPanel, new Vector2(0.63f, 0.015f), new Vector2(0.99f, 0.985f), Vector2.zero);
            stashPanel.gameObject.SetActive(activeTabIndex == 1);
        }
        else
        {
            SetStretch(carryGridPanel, new Vector2(0.01f, 0.015f), new Vector2(0.42f, 0.985f), Vector2.zero);
            SetStretch(stashPanel, new Vector2(0.43f, 0.015f), new Vector2(0.62f, 0.985f), Vector2.zero);
            SetStretch(selectedItemPanel, new Vector2(0.44f, 0.015f), new Vector2(0.99f, 0.985f), Vector2.zero);
            stashPanel.gameObject.SetActive(false);
        }
    }

    private void AssignSelectedWeapon(int slotIndex)
    {
        ItemInstance item = GetSelectedCarryItem();
        if (item == null || !(item.Definition is WeaponDefinition))
        {
            SetStatus("Select a carried ranged weapon to assign an equipment slot.");
            return;
        }

        InventoryOperationResult result = activePlayerInventory.AssignWeaponEquipmentSlot(item.InstanceId, slotIndex);
        SetOperationStatus(result, $"{item.Definition.DisplayName} assigned to {activePlayerInventory.GetEquipmentSlotLabel(item.InstanceId)}.");
        if (result.Succeeded)
        {
            RefreshInventoryView();
        }
    }

    private void ApplySelectedCapacityUpgrade()
    {
        ItemInstance item = GetSelectedCarryItem();
        if (item == null || !(item.Definition is InventoryCapacityUpgradeDefinition))
        {
            SetStatus("Select a carried capacity upgrade to apply it.");
            return;
        }

        InventoryOperationResult result = activePlayerInventory.TryApplyCapacityUpgrade(item.InstanceId);
        if (result.Failure == InventoryOperationFailure.MaximumCapacity)
        {
            SetStatus("The carry case is already at maximum capacity.");
            return;
        }

        SetOperationStatus(result, $"Applied {item.Definition.DisplayName}; carry capacity increased.");
        if (result.Succeeded)
        {
            selectedItemInstanceId = null;
            RefreshInventoryView();
        }
    }

    private void DiscardSelectedCarryItem()
    {
        ItemInstance item = GetSelectedCarryItem();
        if (item == null)
        {
            SetStatus("Select a carried item before discarding.");
            return;
        }

        if (item.Definition.Category == ItemCategory.Weapon)
        {
            SetStatus("Weapons cannot be discarded.");
            return;
        }

        if (!item.Definition.CanBeDiscarded)
        {
            SetStatus($"{item.Definition.DisplayName} is protected and cannot be discarded.");
            return;
        }

        InventoryOperationResult result = activePlayerInventory.TryDiscardCarryItem(item.InstanceId);
        SetOperationStatus(result, $"Discarded {item.Definition.DisplayName}.");
        if (result.Succeeded)
        {
            selectedItemInstanceId = null;
            RefreshInventoryView();
        }
        else if (result.Failure == InventoryOperationFailure.ItemEquipped)
        {
            SetStatus($"Unequip {item.Definition.DisplayName} before discarding it.");
        }
    }

    private void StoreSelectedCarryItem()
    {
        ItemInstance item = GetSelectedCarryItem();
        if (item == null)
        {
            SetStatus("Select a carry item before storing it.");
            return;
        }

        InventoryOperationResult result = activePlayerInventory.TryStoreCarryItem(item.InstanceId);
        SetOperationStatus(result, $"Stored {item.Definition.DisplayName}.");
        if (result.Succeeded)
        {
            selectedItemInstanceId = null;
            RefreshInventoryView();
        }
        else if (result.Failure == InventoryOperationFailure.NotStorable)
        {
            SetStatus($"{item.Definition.DisplayName} is protected from stash transfers and cannot be stored.");
        }
        else if (result.Failure == InventoryOperationFailure.ItemEquipped)
        {
            SetStatus($"Unequip {item.Definition.DisplayName} before storing it.");
        }
    }

    private void WithdrawSelectedStashItem()
    {
        ItemInstance item = GetSelectedStashItem();
        if (item == null)
        {
            SetStatus("Select a stashed item before withdrawing it.");
            return;
        }

        InventoryOperationResult result = activePlayerInventory.TryWithdrawStashItem(item.InstanceId);
        SetOperationStatus(result, $"Withdrew {item.Definition.DisplayName} to the carry grid.");
        if (result.Succeeded)
        {
            selectedStashItemInstanceId = null;
            selectedItemInstanceId = item.InstanceId;
            RefreshInventoryView();
        }
        else if (result.Failure == InventoryOperationFailure.NotStorable)
        {
            SetStatus($"{item.Definition.DisplayName} is protected from stash transfers and cannot be withdrawn.");
        }
        else if (result.Failure == InventoryOperationFailure.NoSpace)
        {
            SetStatus("There is not enough free carry-grid space to withdraw this item.");
        }
    }

    private void SelectStashItem(ItemInstance item)
    {
        if (item == null)
        {
            return;
        }

        if (selectedStashItemInstanceId == item.InstanceId)
        {
            OpenItemActionPopup();
            return;
        }

        selectedStashItemInstanceId = item.InstanceId;
        selectedItemInstanceId = null;
        isMergeTargetSelectionActive = false;
        RefreshStashRowColors();
        RefreshDetailPanel();
    }

    private void RefreshStashRowColors()
    {
        if (activePlayerInventory == null)
        {
            return;
        }

        IReadOnlyList<ItemInstance> items = activePlayerInventory.StashItems;
        for (int index = 0; index < stashRows.Count && index < items.Count; index++)
        {
            Button rowButton = stashRows[index] != null ? stashRows[index].GetComponent<Button>() : null;
            if (rowButton != null && items[index] != null)
            {
                rowButton.image.color = GetCategoryColor(items[index].Definition.Category, items[index].InstanceId == selectedStashItemInstanceId);
            }
        }
    }

    private ItemInstance GetSelectedStashItem()
    {
        return activePlayerInventory != null && !string.IsNullOrWhiteSpace(selectedStashItemInstanceId)
            ? activePlayerInventory.FindStashItem(selectedStashItemInstanceId)
            : null;
    }

    private void RefreshKeyItemsView()
    {
        for (int index = 0; index < keyItemRows.Count; index++)
        {
            if (keyItemRows[index] != null)
            {
                Destroy(keyItemRows[index]);
            }
        }

        keyItemRows.Clear();
        if (activePlayerInventory == null)
        {
            keyItemDetailText.text = "No player inventory is available.";
            return;
        }

        IReadOnlyList<ItemInstance> keyItems = activePlayerInventory.KeyItems;
        keyItemsHeadingText.text = $"KEY ITEMS   {keyItems.Count}";
        RectTransform listRoot = transform.Find("Inventory Menu/Window/Tab Content/Key Items Content/Key Item List") as RectTransform;
        if (listRoot == null)
        {
            return;
        }

        if (keyItems.Count == 0)
        {
            Text emptyText = CreateLabel("Empty Key Items", listRoot, "No key items collected.", 20, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.6f));
            keyItemRows.Add(emptyText.gameObject);
            keyItemDetailText.text = "Select a key item to inspect it. These items cannot be sold or discarded.";
            return;
        }

        for (int index = 0; index < keyItems.Count; index++)
        {
            ItemInstance item = keyItems[index];
            float top = 0.94f - index * 0.12f;
            GameObject row = CreateButtonObject(
                $"Key Item {index + 1}",
                listRoot,
                new Vector2(0.04f, top - 0.105f),
                new Vector2(0.96f, top),
                new Color(0.12f, 0.14f, 0.17f, 1f),
                18,
                $"{item.Definition.DisplayName}   ×{item.Quantity}",
                () => SelectKeyItem(item));
            keyItemRows.Add(row);
        }



        ItemInstance selectedKeyItem = GetSelectedKeyItem();
        if (selectedKeyItem == null && keyItems.Count > 0)
        {
            selectedKeyItemInstanceId = keyItems[0].InstanceId;
            selectedKeyItem = keyItems[0];
        }

        keyItemDetailText.text = selectedKeyItem != null
            ? $"{selectedKeyItem.Definition.DisplayName}\n\n{selectedKeyItem.Definition.Category}\nQuantity: {selectedKeyItem.Quantity}\n\n{selectedKeyItem.Definition.Description}\n\nProtected item — it does not occupy carry-grid cells and cannot be sold or discarded."
            : "Select a key item to inspect it. These items cannot be sold or discarded.";
    }

    private void UpdateMoveSelection()
    {
        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject == null || activeInventory == null)
        {
            return;
        }

        for (int index = 0; index < gridCellButtons.Count; index++)
        {
            Button cellButton = gridCellButtons[index];
            if (cellButton == null || cellButton.gameObject != selectedObject)
            {
                continue;
            }

            Vector2Int cell = new Vector2Int(index % activeInventory.GridWidth, index / activeInventory.GridWidth);
            if (cell != movePreviewPosition)
            {
                ApplyMovePreview(cell);
            }

            return;
        }
    }

    private void FocusGridCell(Vector2Int cell)
    {
        if (EventSystem.current == null || activeInventory == null)
        {
            return;
        }

        int index = cell.y * activeInventory.GridWidth + cell.x;
        if (index >= 0 && index < gridCellButtons.Count && gridCellButtons[index] != null)
        {
            EventSystem.current.SetSelectedGameObject(gridCellButtons[index].gameObject);
        }
    }



    private void HandleGridCellClicked(Vector2Int cell)
    {
        if (isMoveMode)
        {
            TryPlaceSelectedItem(cell);
            return;
        }

        ItemInstance occupant = activeInventory.GetItemAt(cell);
        if (occupant != null)
        {
            SelectOrMergeItem(occupant);
            return;
        }

        SetStatus(GetSelectedCarryItem() == null
            ? "Select an item first, then click it again to pick it up."
            : "Click the selected item again to pick it up before choosing a destination.");
    }

    private void StartMoveMode()
    {
        ItemInstance item = isActionPopupOpen
            ? GetSelectedCarryItem()
            : GetFocusedCarryItem() ?? GetSelectedCarryItem();
        if (item == null)
        {
            SetStatus("Select a carried item before moving it.");
            return;
        }

        selectedItemInstanceId = item.InstanceId;
        selectedStashItemInstanceId = null;
        selectedKeyItemInstanceId = null;
        focusedItemInstanceId = item.InstanceId;

        if (isActionPopupOpen)
        {
            CloseItemActionPopup();
        }

        isMoveMode = true;
        ClearPickupExchange();
        movingItemInstanceId = item.InstanceId;
        movePreviewPosition = item.GridPosition;
        movePreviewQuarterTurns = item.RotationQuarterTurns;
        movingItemTile = GetSelectedItemObject();
        ApplyMovingItemVisuals();
        ApplyMovePreview(movePreviewPosition);
        FocusGridCell(movePreviewPosition);
        SetStatus($"{item.Definition.DisplayName} picked up. Choose a cell to place it.");
        RefreshControlsLegend();
    }

    private void CancelMoveMode()
    {
        if (!isMoveMode)
        {
            return;
        }

        isMoveMode = false;
        if (!RollbackPendingPickupExchange())
        {
            isMoveMode = true;
            return;
        }

        RestoreMovingItemVisuals();
        movingItemInstanceId = null;
        RefreshInventoryView();
        SetStatus("Move cancelled. The item remains in its original cell.");
        RefreshControlsLegend();
        OpenItemActionPopup();
    }

    private bool RollbackPendingPickupExchange()
    {
        if (!hasPickupExchange)
        {
            return true;
        }

        InventoryOperationResult result = activeInventory.TryRollbackPickupExchange(
            exchangePlacedItemInstanceId,
            exchangePlacedItemOriginalPosition,
            exchangePlacedItemOriginalRotationQuarterTurns,
            exchangePickedUpItemInstanceId,
            exchangePickedUpItemOriginalPosition,
            exchangePickedUpItemOriginalRotationQuarterTurns);
        if (!result.Succeeded)
        {
            SetOperationStatus(result, "Pickup exchange cancelled.");
            return false;
        }

        selectedItemInstanceId = exchangePlacedItemInstanceId;
        ClearPickupExchange();
        return true;
    }

    private void ClearPickupExchange()
    {
        hasPickupExchange = false;
        exchangePlacedItemInstanceId = null;
        exchangePlacedItemOriginalPosition = Vector2Int.zero;
        exchangePlacedItemOriginalRotationQuarterTurns = 0;
        exchangePickedUpItemInstanceId = null;
        exchangePickedUpItemOriginalPosition = Vector2Int.zero;
        exchangePickedUpItemOriginalRotationQuarterTurns = 0;
    }

    private void ApplyMovingItemVisuals()
    {
        if (movingItemTile == null)
        {
            return;
        }

        movingItemCanvasGroup = movingItemTile.GetComponent<CanvasGroup>();
        if (movingItemCanvasGroup == null)
        {
            movingItemCanvasGroup = movingItemTile.AddComponent<CanvasGroup>();
        }

        movingItemCanvasGroup.alpha = 1f;
        movingItemCanvasGroup.blocksRaycasts = false;
        movingItemCanvasGroup.interactable = false;
        movingItemTile.transform.localScale = Vector3.one;
        movingItemTile.transform.SetAsLastSibling();
        movingItemTile.SetActive(true);

        Image movingItemImage = movingItemTile.GetComponent<Image>();
        if (movingItemImage != null)
        {
            movingItemImage.color = new Color(1f, 1f, 1f, 0f);
            movingItemImage.raycastTarget = false;
        }

        Outline movingItemOutline = movingItemTile.GetComponent<Outline>();
        if (movingItemOutline != null)
        {
            movingItemOutline.enabled = false;
        }

        foreach (KeyValuePair<GameObject, string> tileEntry in itemTileInstanceIds)
        {
            if (tileEntry.Key != null)
            {
                Image tileImage = tileEntry.Key.GetComponent<Image>();
                if (tileImage != null)
                {
                    tileImage.raycastTarget = false;
                }
            }
        }
    }

    private void RestoreMovingItemVisuals()
    {
        if (movingItemTile != null)
        {
            movingItemTile.SetActive(true);
        }

        if (movingItemCanvasGroup != null)
        {
            movingItemCanvasGroup.alpha = 1f;
            movingItemCanvasGroup.blocksRaycasts = true;
            movingItemCanvasGroup.interactable = true;
        }

        foreach (KeyValuePair<GameObject, string> tileEntry in itemTileInstanceIds)
        {
            if (tileEntry.Key != null)
            {
                tileEntry.Key.transform.localScale = Vector3.one;
                Image tileImage = tileEntry.Key.GetComponent<Image>();
                if (tileImage != null)
                {
                    tileImage.raycastTarget = true;
                    ItemInstance item = activeInventory != null ? activeInventory.FindItem(tileEntry.Value) : null;
                    if (item != null)
                    {
                        tileImage.color = GetCategoryColor(item.Definition.Category, item.InstanceId == selectedItemInstanceId);
                    }
                }
            }
        }

        movingItemTile = null;
        movingItemCanvasGroup = null;
    }

    private void ApplyMovePreview(Vector2Int cell)
    {
        if (!isMoveMode || activeInventory == null)
        {
            return;
        }

        ItemInstance item = GetMovingCarryItem();
        if (item == null)
        {
            CancelMoveMode();
            return;
        }

        movePreviewPosition = cell;
        if (movingItemTile != null)
        {
            movingItemTile.transform.SetAsLastSibling();
        }

        int footprintWidth = movePreviewQuarterTurns % 2 != 0 ? item.Definition.InventoryHeight : item.Definition.InventoryWidth;
        int footprintHeight = movePreviewQuarterTurns % 2 != 0 ? item.Definition.InventoryWidth : item.Definition.InventoryHeight;
        if (movingItemTile != null)
        {
            RectTransform tileRect = movingItemTile.GetComponent<RectTransform>();
            if (tileRect != null)
            {
                tileRect.sizeDelta = new Vector2(
                    footprintWidth * gridCellSize + Mathf.Max(0, footprintWidth - 1) * GridCellSpacing - 6f,
                    footprintHeight * gridCellSize + Mathf.Max(0, footprintHeight - 1) * GridCellSpacing - 6f);
                tileRect.anchoredPosition = new Vector2(
                    GridPadding + cell.x * (gridCellSize + GridCellSpacing) + 3f,
                    -GridPadding - cell.y * (gridCellSize + GridCellSpacing) - 3f);
            }

            Transform iconTransform = movingItemTile.transform.Find("Icon");
            if (iconTransform is RectTransform iconRect && tileRect != null)
            {
                SetItemIconRotation(iconRect, tileRect, movePreviewQuarterTurns);
            }
        }

        ResetCellPreview();
    }

    private void TryPlaceSelectedItem(Vector2Int cell)
    {
        ItemInstance item = GetMovingCarryItem();
        if (item == null)
        {
            CancelMoveMode();
            return;
        }

        ApplyMovePreview(cell);
        string blockingItemInstanceId = null;
        InventoryOperationFailure failure = hasPickupExchange
            ? activeInventory.GetMoveItemFailure(item.InstanceId, cell, movePreviewQuarterTurns)
            : activeInventory.GetMoveItemFailureWithSingleBlockerPickup(
                item.InstanceId,
                cell,
                movePreviewQuarterTurns,
                out blockingItemInstanceId);
        if (failure != InventoryOperationFailure.None)
        {
            SetStatus($"Cannot place item here: {failure}. Move to a valid cell or cancel.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(blockingItemInstanceId))
        {
            ItemInstance blockingItem = activeInventory.FindItem(blockingItemInstanceId);
            if (blockingItem == null)
            {
                SetStatus("The blocking item is no longer available. Choose another cell.");
                return;
            }

            Vector2Int placedItemOriginalPosition = item.GridPosition;
            int placedItemOriginalRotation = item.RotationQuarterTurns;
            Vector2Int pickedItemOriginalPosition = blockingItem.GridPosition;
            int pickedItemOriginalRotation = blockingItem.RotationQuarterTurns;

            isMoveMode = false;
            RestoreMovingItemVisuals();
            selectedItemInstanceId = blockingItem.InstanceId;
            selectedStashItemInstanceId = null;
            InventoryOperationResult exchangeResult = activeInventory.TryMoveItemAndPickUpSingleBlocker(
                item.InstanceId,
                cell,
                movePreviewQuarterTurns,
                out string pickedUpItemInstanceId);
            if (!exchangeResult.Succeeded || string.IsNullOrWhiteSpace(pickedUpItemInstanceId))
            {
                selectedItemInstanceId = item.InstanceId;
                movingItemInstanceId = item.InstanceId;
                isMoveMode = true;
                movingItemTile = GetSelectedItemObject();
                ApplyMovingItemVisuals();
                ApplyMovePreview(movePreviewPosition);
                SetOperationStatus(exchangeResult, "Item placed and blocking item picked up.");
                return;
            }

            hasPickupExchange = true;
            exchangePlacedItemInstanceId = item.InstanceId;
            exchangePlacedItemOriginalPosition = placedItemOriginalPosition;
            exchangePlacedItemOriginalRotationQuarterTurns = placedItemOriginalRotation;
            exchangePickedUpItemInstanceId = pickedUpItemInstanceId;
            exchangePickedUpItemOriginalPosition = pickedItemOriginalPosition;
            exchangePickedUpItemOriginalRotationQuarterTurns = pickedItemOriginalRotation;
            selectedItemInstanceId = pickedUpItemInstanceId;
            movingItemInstanceId = null;
            RefreshInventoryView();

            ItemInstance pickedUpItem = activeInventory.FindItem(pickedUpItemInstanceId);
            if (pickedUpItem == null)
            {
                SetStatus("The blocking item could not be picked up.");
                return;
            }

            movePreviewPosition = placedItemOriginalPosition;
            movePreviewQuarterTurns = pickedItemOriginalRotation;
            movingItemInstanceId = pickedUpItemInstanceId;
            isMoveMode = true;
            movingItemTile = GetSelectedItemObject();
            ApplyMovingItemVisuals();
            ApplyMovePreview(movePreviewPosition);
            FocusGridCell(movePreviewPosition);
            SetStatus($"Placed {item.Definition.DisplayName}; picked up {pickedUpItem.Definition.DisplayName}. Choose a cell.");
            RefreshControlsLegend();
            return;
        }

        isMoveMode = false;
        RestoreMovingItemVisuals();
        InventoryOperationResult result = activeInventory.TryMoveItem(item.InstanceId, cell, movePreviewQuarterTurns);
        SetOperationStatus(result, "Item placed.");
        if (result.Succeeded)
        {
            ClearPickupExchange();
            movingItemInstanceId = null;
            RefreshInventoryView();
            OpenItemActionPopup();
            return;
        }

        isMoveMode = true;
        movingItemTile = GetSelectedItemObject();
        ApplyMovingItemVisuals();
        ApplyMovePreview(movePreviewPosition);
    }

    private void RefreshWeaponComparison(ItemInstance selectedItem, bool selectedFromStash)
    {
        comparisonPanel.gameObject.SetActive(true);
        if (selectedFromStash)
        {
            comparisonText.text = "Weapon comparison is available for carried ranged weapons.";
            UpdateComparisonContentHeight();
            return;
        }

        if (selectedItem == null || !(selectedItem.Definition is WeaponDefinition selectedWeapon))
        {
            comparisonText.text = "Select a carried ranged weapon to compare it with your active weapon.";
            UpdateComparisonContentHeight();
            return;
        }

        ItemInstance activeWeaponItem = GetActiveWeaponItem();
        if (activeWeaponItem == null || !(activeWeaponItem.Definition is WeaponDefinition activeWeapon))
        {
            comparisonText.text = "No active or equipped ranged weapon is available to compare.";
            UpdateComparisonContentHeight();
            return;
        }

        if (activeWeaponItem.InstanceId == selectedItem.InstanceId)
        {
            comparisonText.text = "This weapon is currently active/equipped. Select a different carried ranged weapon to compare.";
            UpdateComparisonContentHeight();
            return;
        }

        int selectedReserve = activePlayerInventory.GetReserveRounds(selectedWeapon.AmmoType);
        int activeReserve = activePlayerInventory.GetReserveRounds(activeWeapon.AmmoType);
        IReadOnlyList<ItemInstance> selectedAttachments = activePlayerInventory.GetInstalledAttachmentsForWeapon(selectedItem.InstanceId);
        IReadOnlyList<ItemInstance> activeAttachments = activePlayerInventory.GetInstalledAttachmentsForWeapon(activeWeaponItem.InstanceId);
        WeaponRuntimeStats selectedRuntimeStats = WeaponRuntimeStats.Resolve(selectedWeapon, selectedItem, selectedAttachments);
        WeaponRuntimeStats activeRuntimeStats = WeaponRuntimeStats.Resolve(activeWeapon, activeWeaponItem, activeAttachments);
        comparisonText.text =
            $"SELECTED: {selectedItem.Definition.Category} — {selectedItem.Definition.DisplayName}\n" +
            $"ACTIVE: {activeWeaponItem.Definition.Category} — {activeWeaponItem.Definition.DisplayName}\n" +
            $"Loaded / clip: {selectedItem.LoadedRounds} / {selectedWeapon.MagazineCapacity}  |  {activeWeaponItem.LoadedRounds} / {activeWeapon.MagazineCapacity}\n" +
            $"Quantity: {selectedItem.Quantity}  |  {activeWeaponItem.Quantity}\n" +
            $"Ammo / reserve: {selectedWeapon.AmmoType} / {selectedReserve}  |  {activeWeapon.AmmoType} / {activeReserve}\n" +
            $"Damage: {selectedWeapon.Damage:0.##}  |  {activeWeapon.Damage:0.##}\n" +
            $"Fire rate (base → current): {selectedWeapon.ShotsPerSecond:0.##} → {selectedRuntimeStats.ShotsPerSecond:0.##}/s  |  {activeWeapon.ShotsPerSecond:0.##} → {activeRuntimeStats.ShotsPerSecond:0.##}/s\n" +
            $"Hip spread (base → current): {selectedWeapon.HipFireSpread:0.##}° → {selectedRuntimeStats.HipFireSpread:0.##}°  |  {activeWeapon.HipFireSpread:0.##}° → {activeRuntimeStats.HipFireSpread:0.##}°\n" +
            $"Aimed spread (base → current): {selectedWeapon.AimedSpread:0.##}° → {selectedRuntimeStats.AimedSpread:0.##}°  |  {activeWeapon.AimedSpread:0.##}° → {activeRuntimeStats.AimedSpread:0.##}°\n" +
            $"Pierce: {selectedWeapon.Pierce}  |  {activeWeapon.Pierce}\n" +
            $"Shot power: {selectedWeapon.ShotPower:0.##}  |  {activeWeapon.ShotPower:0.##}\n" +
            $"Footprint / cell: {GetFootprintLabel(selectedItem)} at ({selectedItem.GridPosition.x}, {selectedItem.GridPosition.y})  |  " +
            $"{GetFootprintLabel(activeWeaponItem)} at ({activeWeaponItem.GridPosition.x}, {activeWeaponItem.GridPosition.y}); grid {activeInventory.GridWidth} × {activeInventory.GridHeight}\n" +
            "Upgrade levels (selected | active):";

        for (int index = 0; index < WeaponUpgradeOrder.Length; index++)
        {
            WeaponUpgradeType upgradeType = WeaponUpgradeOrder[index];
            bool selectedSupports = SupportsUpgrade(selectedWeapon, upgradeType);
            bool activeSupports = SupportsUpgrade(activeWeapon, upgradeType);
            if (!selectedSupports && !activeSupports)
            {
                continue;
            }

            int selectedLevel = selectedItem.GetUpgradeLevel(upgradeType);
            int activeLevel = activeWeaponItem.GetUpgradeLevel(upgradeType);
            string selectedLevelText = selectedSupports ? selectedLevel.ToString() : "—";
            string activeLevelText = activeSupports ? activeLevel.ToString() : "—";
            string difference = selectedLevel != activeLevel || selectedSupports != activeSupports ? "  (different)" : string.Empty;
            comparisonText.text += $"\n{upgradeType}: {selectedLevelText} | {activeLevelText}{difference}";
        }

        UpdateComparisonContentHeight();
    }

    private ItemInstance GetActiveWeaponItem()
    {
        if (activePlayerInventory == null)
        {
            return null;
        }

        WeaponHoldController weaponHoldController = activePlayerInventory.GetComponentInChildren<WeaponHoldController>(true);
        ItemInstance activeWeapon = weaponHoldController != null ? weaponHoldController.CurrentWeaponItem : null;
        if (activeWeapon != null)
        {
            return activeWeapon;
        }

        string equippedWeaponId = activePlayerInventory.EquippedWeaponInstanceId;
        if (!string.IsNullOrWhiteSpace(equippedWeaponId) && activeInventory != null)
        {
            activeWeapon = activeInventory.FindItem(equippedWeaponId);
            if (activeWeapon != null)
            {
                return activeWeapon;
            }
        }

        ItemInstance primaryWeapon = activePlayerInventory.GetWeaponInEquipmentSlot(PlayerItemInventory.PrimaryWeaponSlotIndex);
        if (primaryWeapon != null && primaryWeapon.IsEquipped)
        {
            return primaryWeapon;
        }

        ItemInstance secondaryWeapon = activePlayerInventory.GetWeaponInEquipmentSlot(PlayerItemInventory.SecondaryWeaponSlotIndex);
        return secondaryWeapon != null && secondaryWeapon.IsEquipped ? secondaryWeapon : null;
    }

    private static bool SupportsUpgrade(WeaponDefinition weaponDefinition, WeaponUpgradeType upgradeType)
    {
        if (weaponDefinition.upgradeOptions == null)
        {
            return false;
        }

        for (int index = 0; index < weaponDefinition.upgradeOptions.Length; index++)
        {
            if (weaponDefinition.upgradeOptions[index].upgradeType == upgradeType)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateComparisonContentHeight()
    {
        if (comparisonText == null || comparisonScrollRect == null)
        {
            return;
        }

        float minimumHeight = comparisonScrollRect.viewport.rect.height;
        comparisonContentTransform.sizeDelta = new Vector2(0f, Mathf.Max(minimumHeight, comparisonText.preferredHeight + 8f));
        comparisonScrollRect.verticalNormalizedPosition = 1f;
    }


    private void SelectOrMergeItem(ItemInstance item)
    {
        if (item == null || isMoveMode)
        {
            return;
        }

        focusedItemInstanceId = item.InstanceId;

        if (!isMergeTargetSelectionActive && item.InstanceId == selectedItemInstanceId)
        {
            if (isActionPopupOpen)
            {
                CloseItemActionPopup();
            }

            StartMoveMode();
            return;
        }

        ItemInstance selectedItem = GetSelectedCarryItem();
        if (isMergeTargetSelectionActive && selectedItem != null && selectedItem.InstanceId != item.InstanceId)
        {
            InventoryOperationResult mergeResult = activeInventory.TryMergeStacks(item.InstanceId, selectedItem.InstanceId);
            SetOperationStatus(mergeResult, "Compatible stacks merged.");
            if (mergeResult.Succeeded)
            {
                selectedItemInstanceId = activeInventory.FindItem(item.InstanceId) != null
                    ? item.InstanceId
                    : null;
                isMergeTargetSelectionActive = false;
                RefreshInventoryView();
            }

            return;
        }

        if (isActionPopupOpen)
        {
            CloseItemActionPopup();
        }

        selectedItemInstanceId = item.InstanceId;
        selectedStashItemInstanceId = null;
        selectedKeyItemInstanceId = null;
        isMergeTargetSelectionActive = false;
        RefreshDetailPanel();
        if (isStashMode)
        {
            RefreshStashRowColors();
        }

        RefreshItemTileColors();
        OpenItemActionPopup();
    }

    private void SelectKeyItem(ItemInstance item)
    {
        if (item == null)
        {
            return;
        }

        selectedKeyItemInstanceId = item.InstanceId;
        keyItemDetailText.text = $"{item.Definition.DisplayName}\n\n{item.Definition.Category}\nQuantity: {item.Quantity}\n\n{item.Definition.Description}\n\nProtected item — it does not occupy carry-grid cells and cannot be sold or discarded.";
        RefreshControlsLegend();
    }

    private void PreviewPlacement(Vector2Int cell)
    {
        if (isMoveMode)
        {
            ApplyMovePreview(cell);
            FocusGridCell(cell);
            return;
        }

        ResetCellPreview();
    }

    private void ResetCellPreview()
    {
        if (activeInventory == null)
        {
            return;
        }

        for (int index = 0; index < gridCellImages.Count; index++)
        {
            Vector2Int cell = new Vector2Int(index % activeInventory.GridWidth, index / activeInventory.GridWidth);
            ItemInstance occupant = activeInventory.GetItemAt(cell);
            bool isCarriedItem = occupant != null && isMoveMode && occupant.InstanceId == movingItemInstanceId;
            gridCellImages[index].color = occupant != null && !isCarriedItem
                ? new Color(0.2f, 0.24f, 0.29f, 1f)
                : CellColor;
        }

        if (!isMoveMode)
        {
            return;
        }

        ItemInstance item = GetMovingCarryItem();
        if (item == null)
        {
            return;
        }

        int footprintWidth = movePreviewQuarterTurns % 2 != 0 ? item.Definition.InventoryHeight : item.Definition.InventoryWidth;
        int footprintHeight = movePreviewQuarterTurns % 2 != 0 ? item.Definition.InventoryWidth : item.Definition.InventoryHeight;
        InventoryOperationFailure failure;
        if (hasPickupExchange)
        {
            failure = activeInventory.GetMoveItemFailure(item.InstanceId, movePreviewPosition, movePreviewQuarterTurns);
        }
        else
        {
            failure = activeInventory.GetMoveItemFailureWithSingleBlockerPickup(
                item.InstanceId,
                movePreviewPosition,
                movePreviewQuarterTurns,
                out _);
        }

        Color previewColor = failure == InventoryOperationFailure.None ? ValidPlacementColor : InvalidPlacementColor;
        for (int y = 0; y < footprintHeight; y++)
        {
            for (int x = 0; x < footprintWidth; x++)
            {
                Vector2Int cell = new Vector2Int(movePreviewPosition.x + x, movePreviewPosition.y + y);
                if (cell.x < 0 || cell.x >= activeInventory.GridWidth || cell.y < 0 || cell.y >= activeInventory.GridHeight)
                {
                    continue;
                }

                int cellIndex = cell.y * activeInventory.GridWidth + cell.x;
                if (cellIndex >= 0 && cellIndex < gridCellImages.Count)
                {
                    gridCellImages[cellIndex].color = previewColor;
                }
            }
        }
    }

    private void RefreshItemTileColors()
    {
        for (int index = 0; index < itemTiles.Count; index++)
        {
            GameObject tile = itemTiles[index];
            if (tile == null)
            {
                continue;
            }

            Button button = tile.GetComponent<Button>();
            ItemInstance item = activeInventory.FindItem(itemTileInstanceIds[tile]);
            if (item != null)
            {
                bool isSelectedItem = item.InstanceId == selectedItemInstanceId;
                button.image.color = GetCategoryColor(item.Definition.Category, isSelectedItem);
                Outline selectionOutline = tile.GetComponent<Outline>();
                if (selectionOutline != null)
                {
                    selectionOutline.effectColor = SelectedOutlineColor;
                    selectionOutline.effectDistance = new Vector2(4f, -4f);
                    selectionOutline.useGraphicAlpha = false;
                    selectionOutline.enabled = isSelectedItem;
                }
            }
        }

        ResetCellPreview();
    }

    private string BuildItemStats(ItemInstance item)
    {
        if (item.Definition is WeaponDefinition weaponDefinition)
        {
            int reserveRounds = activePlayerInventory.GetReserveRounds(weaponDefinition.AmmoType);
            string equipmentSlot = activePlayerInventory.GetEquipmentSlotLabel(item.InstanceId);
            string slotLabel = string.IsNullOrWhiteSpace(equipmentSlot) ? "Unassigned" : equipmentSlot;
            return $"Equipment slot: {slotLabel}\nMagazine: {item.LoadedRounds} / {weaponDefinition.MagazineCapacity}\nReserve: {reserveRounds} rounds ({weaponDefinition.AmmoType})\nDamage: {weaponDefinition.Damage:0.##}   Fire rate: {weaponDefinition.ShotsPerSecond:0.##}/s";
        }

        if (item.Definition is AmmoDefinition ammoDefinition)
        {
            return $"Ammo type: {ammoDefinition.AmmunitionType}\nStack compatibility: {ammoDefinition.StackCompatibilityKey}\nMaximum per stack: {item.Definition.MaximumStackSize}\nFootprint: {GetFootprintLabel(item)}";
        }

        return $"Footprint: {GetFootprintLabel(item)}\nMaximum stack: {item.Definition.MaximumStackSize}\nState: {(item.IsEquipped ? "Equipped" : "Carried")}";
    }

    private string GetFootprintLabel(ItemInstance item)
    {
        int width = item.RotationQuarterTurns % 2 != 0 ? item.Definition.InventoryHeight : item.Definition.InventoryWidth;
        int height = item.RotationQuarterTurns % 2 != 0 ? item.Definition.InventoryWidth : item.Definition.InventoryHeight;
        return $"{width} × {height}";
    }

    private ItemInstance GetFocusedCarryItem()
    {
        return activeInventory != null && !string.IsNullOrWhiteSpace(focusedItemInstanceId)
            ? activeInventory.FindItem(focusedItemInstanceId)
            : null;
    }

    private ItemInstance GetSelectedCarryItem()
    {
        return activeInventory != null && !string.IsNullOrWhiteSpace(selectedItemInstanceId)
            ? activeInventory.FindItem(selectedItemInstanceId)
            : null;
    }

    private ItemInstance GetSelectedKeyItem()
    {
        return activePlayerInventory != null && !string.IsNullOrWhiteSpace(selectedKeyItemInstanceId)
            ? activePlayerInventory.FindKeyItem(selectedKeyItemInstanceId)
            : null;
    }

    private void HandleInventoryChanged(PlayerItemInventory changedInventory)
    {
        if (isOpen)
        {
            RefreshInventoryView();
            RefreshKeyItemsView();
        }
    }

    private void UnsubscribeFromInventory()
    {
        if (activePlayerInventory != null)
        {
            activePlayerInventory.Changed -= HandleInventoryChanged;
        }

        activePlayerInventory = null;
        activeInventory = null;
    }

    private void SetOperationStatus(InventoryOperationResult result, string successMessage)
    {
        SetStatus(result.Succeeded ? successMessage : $"Could not complete action: {result.Failure}.");
    }

    private void SetStatus(string message)
    {
        statusMessage = message;
        if (inventoryStatusText != null)
        {
            inventoryStatusText.text = message;
        }
    }
    private ItemInstance GetMovingCarryItem()
    {
        return activeInventory != null && !string.IsNullOrWhiteSpace(movingItemInstanceId)
            ? activeInventory.FindItem(movingItemInstanceId)
            : null;
    }



    private RectTransform CreatePanel(string objectName, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        SetStretch(panelRect, anchorMin, anchorMax, Vector2.zero);
        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = color.a > 0f;
        return panelRect;
    }

    private RectTransform CreateRect(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        RectTransform rect = rectObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return rect;
    }

    private Text CreateLabel(string objectName, Transform parent, string value, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax)
    {
        RectTransform rect = CreateRect(objectName, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        GameObject labelObject = rect.gameObject;
        labelObject.AddComponent<CanvasRenderer>();
        Text label = labelObject.AddComponent<Text>();
        label.font = uiFont;
        label.text = value;
        label.fontSize = fontSize;
        label.color = TextColor;
        label.alignment = alignment;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        return label;
    }

    private Button CreateButton(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color, int fontSize, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        SetStretch(buttonObject.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero);
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic, wrapAround = false };
        button.onClick.AddListener(onClick);
        Outline focusOutline = buttonObject.AddComponent<Outline>();
        focusOutline.effectColor = SelectedOutlineColor;
        focusOutline.effectDistance = new Vector2(4f, -4f);
        focusOutline.useGraphicAlpha = false;
        focusOutline.enabled = false;
        EventTrigger buttonEvents = buttonObject.AddComponent<EventTrigger>();
        AddPointerEvent(buttonEvents, EventTriggerType.Cancel, () => OnCancel(null));
        AddPointerEvent(buttonEvents, EventTriggerType.Select, () => focusOutline.enabled = true);
        AddPointerEvent(buttonEvents, EventTriggerType.Deselect, () => focusOutline.enabled = false);
        CreateLabel("Label", buttonObject.transform, objectName, fontSize, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f));
        return button;
    }

    private GameObject CreateButtonObject(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color, int fontSize, string labelText, UnityEngine.Events.UnityAction onClick)
    {
        Button button = CreateButton(objectName, parent, anchorMin, anchorMax, color, fontSize, onClick);
        button.GetComponentInChildren<Text>().text = labelText;
        return button.gameObject;
    }

    private static void SetStretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offset)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = Vector2.zero;
    }

    private static Color GetCategoryColor(ItemCategory category, bool selected)
    {
        return ItemTileColor;
    }
    private static void SetItemIconRotation(RectTransform iconRect, RectTransform tileRect, int quarterTurns)
    {
        if (iconRect == null || tileRect == null)
        {
            return;
        }

        int normalizedTurns = ((quarterTurns % 4) + 4) % 4;
        Vector2 tileSize = tileRect.sizeDelta;
        Vector2 iconSize = normalizedTurns % 2 == 0
            ? tileSize * 0.92f
            : new Vector2(tileSize.y, tileSize.x) * 0.92f;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = iconSize;
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.localRotation = Quaternion.Euler(0f, 0f, -90f * normalizedTurns);
    }


    private static void AddPointerEvent(EventTrigger eventTrigger, EventTriggerType eventType, Action callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(_ => callback());
        eventTrigger.triggers.Add(entry);
    }
}
