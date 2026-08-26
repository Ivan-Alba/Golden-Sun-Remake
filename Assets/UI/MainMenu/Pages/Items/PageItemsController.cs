using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

#region Drag & Drop Data Structures

/// <summary>
/// Identifies the UI container source where a drag operation started.
/// </summary>
public enum DragSourceType
{
    PersonalInventory,
    Equipment,
    Relic
}

/// <summary>
/// Payload container holding all metadata required during an active item drag operation.
/// </summary>
public class DragContext
{
    public DragSourceType SourceType { get; private set; }
    public int SourceIndex { get; private set; }
    public ItemSO Item { get; private set; }
    public int Quantity { get; private set; }
    public VisualElement SourceSlotUI { get; private set; }

    public DragContext(DragSourceType sourceType, int sourceIndex, ItemSO item, int quantity, VisualElement sourceSlotUI)
    {
        SourceType = sourceType;
        SourceIndex = sourceIndex;
        Item = item;
        Quantity = quantity;
        SourceSlotUI = sourceSlotUI;
    }
}

/// <summary>
/// Categorizes the target container where an item is dropped.
/// </summary>
public enum DropTargetType
{
    None,
    PersonalInventory,
    Equipment,
    Relic,
    HeroCard
}

/// <summary>
/// Holds drop destination metadata detected under the pointer.
/// </summary>
public struct DropTargetInfo
{
    public DropTargetType TargetType;
    public int Index;
    public VisualElement ElementUI;

    public DropTargetInfo(DropTargetType type, int index, VisualElement element)
    {
        TargetType = type;
        Index = index;
        ElementUI = element;
    }
}

/// <summary>
/// Holds metadata for a pending transaction that requires quantity selection in the split modal.
/// </summary>
public struct PendingSplitTransaction
{
    public DragSourceType SourceType;
    public int SourceIndex;
    public DropTargetType TargetType;
    public int TargetIndex;
    public HeroInventory TargetHeroInventory;
    public ItemSO Item;
    public int MaxQuantity;
}

#endregion

public class PageItemsController
{
    #region Events & Properties

    /// <summary>
    /// Event triggered whenever a consumable item is used or equipment stats change.
    /// Used by MainMenuController to refresh top header hero cards.
    /// </summary>
    public event Action OnItemUsedOnHero;

    /// <summary>
    /// Indicates if an item is currently active in targeting mode for usage on a hero.
    /// </summary>
    public bool IsTargetingItem => isTargetingItem;

    #endregion

    #region Private Fields

    private VisualElement rootElement;
    private VisualElement inventoryGrid;
    private VisualElement globalBagGrid;

    // Header Elements
    private VisualElement heroInventoryIcon;
    private Label labelInventoryTitle;

    // Info Panel Elements
    private Label labelInfoTitle;
    private VisualElement itemIconPreview;
    private Label labelItemName;
    private Label labelItemType;
    private Label labelItemSellPrice;
    private Label labelItemMaxStack;
    private Label labelItemDesc;

    // Container holding modular dynamic UXML templates for item extra information
    private VisualElement extraInfoContainer;

    // Modular UXML Template Assets for Detailed Item Info Views
    private VisualTreeAsset templateInfoConsumable;
    private VisualTreeAsset templateInfoEquipable;
    private VisualTreeAsset templateInfoWeapon;
    private VisualTreeAsset templateInfoPsynergyRelic;
    private VisualTreeAsset templateInfoKeyItem;

    // Stack Splitter Overlay Popup Elements
    private VisualElement splitPopupOverlay;
    private VisualElement splitItemIcon;
    private Label labelSplitItemName;
    private Button btnSplitMinus;
    private SliderInt sliderSplitAmount;
    private Button btnSplitPlus;
    private Label labelSplitAmount;
    private Button btnSplitConfirm;
    private Button btnSplitCancel;

    // Pending split transaction data
    private PendingSplitTransaction pendingSplit;

    // Fixed Equipment Slots (7 Slots)
    private VisualElement[] gearSlots = new VisualElement[7];

    // Fixed Psynergy Relic Slots (4 Slots)
    private VisualElement[] relicSlots = new VisualElement[4];

    // Inventory Slots Cache
    private List<VisualElement> spawnedInventorySlots = new List<VisualElement>();
    private List<VisualElement> spawnedGlobalBagSlots = new List<VisualElement>();

    // Callback Memory tracking to prevent duplicate listener accumulation
    private Dictionary<VisualElement, EventCallback<PointerDownEvent>> registeredPointerDownCallbacks = new Dictionary<VisualElement, EventCallback<PointerDownEvent>>();

    // Party Data Source
    private List<HeroStats> partyHeroes = new List<HeroStats>();

    // Current Focused Data Sources
    private HeroStats currentHero;
    private HeroInventory currentInventory;
    private HeroEquipment currentEquipment;

    // Drag & Drop State Tracking
    private DragContext currentDragContext;
    private VisualElement ghostElement;
    private bool isDragging = false;

    // Pointer Tracking & Drag Threshold
    private Vector2 pointerStartPosition;
    private int activePointerId = -1;
    private const float DRAG_THRESHOLD = 5f;

    // Manual Double-Click Tracking
    private float lastClickTime = 0f;
    private int lastClickSlotIndex = -1;
    private DragSourceType lastClickSourceType;
    private const float DOUBLE_CLICK_TIME_THRESHOLD = 0.3f; // 300 ms double-click window

    // Item Targeting State Tracking
    private bool isTargetingItem = false;
    private int targetingSourceIndex = -1;
    private ItemSO targetingItem;
    private VisualElement targetingGhostElement;

    // Item Selected (Info Panel) State Tracking
    private ItemSO selectedItem;

    // Equipment slot type mapping matching gearSlots[0..6] indices
    private readonly EquipmentSlot[] gearSlotTypes = new EquipmentSlot[]
    {
        EquipmentSlot.Weapon,    // gearSlots[0] -> slot_weapon
        EquipmentSlot.Shield,    // gearSlots[1] -> slot_shield
        EquipmentSlot.Armor,     // gearSlots[2] -> slot_armor
        EquipmentSlot.Helmet,    // gearSlots[3] -> slot_helm
        EquipmentSlot.Hands,     // gearSlots[4] -> slot_hands
        EquipmentSlot.Boots,     // gearSlots[5] -> slot_boots
        EquipmentSlot.Accessory  // gearSlots[6] -> slot_acc
    };

    // Slot Dimension Constants for Layout Calculations
    private const float SLOT_SIZE = 80f;
    private const float SLOT_MARGIN_TOTAL = 6f; // 3px left + 3px right (matches .slot-box margin: 3px)

    #endregion

    #region Constructor

    public PageItemsController(VisualElement root)
    {
        rootElement = root;
        BindUIElements();
        BindSplitPopupEvents();
    }

    #endregion

    #region Helper Methods for Global Panel Access

    /// <summary>
    /// Binds the modular VisualTreeAsset templates required for dynamic item details rendering.
    /// </summary>
    public void SetExtraInfoTemplates(
        VisualTreeAsset consumableTpl,
        VisualTreeAsset equipableTpl,
        VisualTreeAsset weaponTpl,
        VisualTreeAsset relicTpl,
        VisualTreeAsset keyItemTpl)
    {
        templateInfoConsumable = consumableTpl;
        templateInfoEquipable = equipableTpl;
        templateInfoWeapon = weaponTpl;
        templateInfoPsynergyRelic = relicTpl;
        templateInfoKeyItem = keyItemTpl;
    }

    /// <summary>
    /// Retrieves the top-level root visual element of the entire UI Document panel
    /// to ensure ghosts and global events bypass sub-page boundaries.
    /// </summary>
    private VisualElement GetTopRootElement()
    {
        return rootElement?.panel?.visualTree ?? rootElement;
    }

    #endregion

    #region UI Binding

    private void BindUIElements()
    {
        if (rootElement == null) return;

        // Header Elements
        heroInventoryIcon = rootElement.Q<VisualElement>("hero_inventory_icon");
        labelInventoryTitle = rootElement.Q<Label>("label_inventory_title");

        // Grid Containers
        inventoryGrid = rootElement.Q<VisualElement>("inventory_grid");
        globalBagGrid = rootElement.Q<VisualElement>("global_bag_grid");

        // Info Panel
        labelInfoTitle = rootElement.Q<Label>("label_info_title");
        itemIconPreview = rootElement.Q<VisualElement>("item_icon_preview");
        labelItemName = rootElement.Q<Label>("label_item_name");
        labelItemType = rootElement.Q<Label>("label_item_type");
        labelItemSellPrice = rootElement.Q<Label>("label_item_sell_price");
        labelItemMaxStack = rootElement.Q<Label>("label_item_max_stack");
        labelItemDesc = rootElement.Q<Label>("label_item_desc");

        // Container holding extra item details
        extraInfoContainer = rootElement.Q<VisualElement>("extra_info_container") ?? rootElement.Q<VisualElement>("stat_changes_box");

        // Split Modal Overlay Elements
        splitPopupOverlay = rootElement.Q<VisualElement>("split_popup_overlay");
        splitItemIcon = rootElement.Q<VisualElement>("split_item_icon");
        labelSplitItemName = rootElement.Q<Label>("label_split_item_name");
        btnSplitMinus = rootElement.Q<Button>("btn_split_minus");
        sliderSplitAmount = rootElement.Q<SliderInt>("slider_split_amount");
        btnSplitPlus = rootElement.Q<Button>("btn_split_plus");
        labelSplitAmount = rootElement.Q<Label>("label_split_amount");
        btnSplitConfirm = rootElement.Q<Button>("btn_split_confirm");
        btnSplitCancel = rootElement.Q<Button>("btn_split_cancel");

        if (splitPopupOverlay != null)
        {
            splitPopupOverlay.style.display = DisplayStyle.None;
        }

        // Bind Fixed Equipment Slots
        gearSlots[0] = rootElement.Q<VisualElement>("slot_weapon");
        gearSlots[1] = rootElement.Q<VisualElement>("slot_shield");
        gearSlots[2] = rootElement.Q<VisualElement>("slot_armor");
        gearSlots[3] = rootElement.Q<VisualElement>("slot_helm");
        gearSlots[4] = rootElement.Q<VisualElement>("slot_hands");
        gearSlots[5] = rootElement.Q<VisualElement>("slot_boots");
        gearSlots[6] = rootElement.Q<VisualElement>("slot_acc");

        // Bind Fixed Relic Slots
        relicSlots[0] = rootElement.Q<VisualElement>("slot_relic_0");
        relicSlots[1] = rootElement.Q<VisualElement>("slot_relic_1");
        relicSlots[2] = rootElement.Q<VisualElement>("slot_relic_2");
        relicSlots[3] = rootElement.Q<VisualElement>("slot_relic_3");

        // Ensure equipment and relic slots register pointer hit tests properly
        for (int i = 0; i < gearSlots.Length; i++)
        {
            if (gearSlots[i] != null) gearSlots[i].pickingMode = PickingMode.Position;
        }

        for (int i = 0; i < relicSlots.Length; i++)
        {
            if (relicSlots[i] != null) relicSlots[i].pickingMode = PickingMode.Position;
        }
    }

    private void BindSplitPopupEvents()
    {
        if (btnSplitMinus != null) btnSplitMinus.clicked += () => StepSplitAmount(-1);
        if (btnSplitPlus != null) btnSplitPlus.clicked += () => StepSplitAmount(1);

        if (sliderSplitAmount != null)
        {
            sliderSplitAmount.RegisterValueChangedCallback(evt => UpdateSplitAmountLabel(evt.newValue));
        }

        if (btnSplitConfirm != null) btnSplitConfirm.clicked += OnConfirmSplit;
        if (btnSplitCancel != null) btnSplitCancel.clicked += CloseSplitModal;
    }

    #endregion

    #region Data Population & Initialization

    public void SetParty(List<HeroStats> party)
    {
        partyHeroes = party ?? new List<HeroStats>();
    }

    public void PopulateData(HeroStats hero, HeroInventory inventory, HeroEquipment equipment, VisualTreeAsset itemSlotAsset)
    {
        if (currentEquipment != null)
        {
            currentEquipment.OnEquipmentChanged -= RefreshAllData;
        }

        currentHero = hero;
        currentInventory = inventory;
        currentEquipment = equipment;

        if (currentEquipment != null)
        {
            currentEquipment.OnEquipmentChanged += RefreshAllData;
        }

        UpdateHeaderHeroInfo();

        int personalCapacity = (currentInventory != null) ? currentInventory.MaxBackpackSlots : 18;
        int columns = Mathf.CeilToInt(personalCapacity / 3.0f);
        int globalCapacity = columns * 2;

        PopulateInventoryGrid(personalCapacity, columns, itemSlotAsset);
        PopulateGlobalBagGrid(globalCapacity, columns, itemSlotAsset);

        RefreshAllData();
        ClearInfoPanel();
    }

    private void UpdateHeaderHeroInfo()
    {
        if (currentHero != null)
        {
            if (labelInventoryTitle != null)
            {
                labelInventoryTitle.text = $"INVENTARIO DE {currentHero.Name.ToUpper()}";
            }

            if (heroInventoryIcon != null && currentHero.TurnBarImage != null)
            {
                heroInventoryIcon.style.backgroundImage = new StyleBackground(currentHero.TurnBarImage);
            }
        }
    }

    private void PopulateInventoryGrid(int capacity, int columns, VisualTreeAsset itemSlotAsset)
    {
        if (inventoryGrid == null || itemSlotAsset == null) return;

        inventoryGrid.Clear();
        spawnedInventorySlots.Clear();

        float calculatedWidth = columns * (SLOT_SIZE + SLOT_MARGIN_TOTAL);
        inventoryGrid.style.maxWidth = calculatedWidth;

        for (int i = 0; i < capacity; i++)
        {
            TemplateContainer slotInstance = itemSlotAsset.Instantiate();
            VisualElement slotRoot = slotInstance.Q<VisualElement>("slot_root") ?? slotInstance;

            if (slotRoot != null)
            {
                slotRoot.pickingMode = PickingMode.Position;
                inventoryGrid.Add(slotRoot);
                spawnedInventorySlots.Add(slotRoot);
            }
        }
    }

    private void PopulateGlobalBagGrid(int capacity, int columns, VisualTreeAsset itemSlotAsset)
    {
        if (globalBagGrid == null || itemSlotAsset == null) return;

        globalBagGrid.Clear();
        spawnedGlobalBagSlots.Clear();

        float calculatedWidth = columns * (SLOT_SIZE + SLOT_MARGIN_TOTAL);
        globalBagGrid.style.maxWidth = calculatedWidth;

        for (int i = 0; i < capacity; i++)
        {
            TemplateContainer slotInstance = itemSlotAsset.Instantiate();
            VisualElement slotRoot = slotInstance.Q<VisualElement>("slot_root") ?? slotInstance;

            if (slotRoot != null)
            {
                int slotIndex = i;
                slotRoot.pickingMode = PickingMode.Position;
                slotRoot.RegisterCallback<ClickEvent>(evt => SelectGlobalBagSlot(slotIndex));

                globalBagGrid.Add(slotRoot);
                spawnedGlobalBagSlots.Add(slotRoot);
            }
        }
    }

    #endregion

    #region Data Refresh & Event Binding

    public void RefreshAllData()
    {
        RefreshInventorySlots();
        RefreshEquipmentSlots();
        RefreshGlobalBagSlots();

        if (selectedItem != null)
        {
            UpdateInfoPanel(selectedItem);
        }

        // Notify MainMenuController to update top header hero cards (HP/PP/Stats)
        OnItemUsedOnHero?.Invoke();
    }

    private void RefreshInventorySlots()
    {
        if (currentInventory == null) return;

        for (int i = 0; i < spawnedInventorySlots.Count; i++)
        {
            int slotIndex = i;
            VisualElement slotUI = spawnedInventorySlots[i];
            if (slotUI == null) continue;

            VisualElement iconElem = slotUI.Q<VisualElement>("item_icon");
            VisualElement quantityBox = slotUI.Q<VisualElement>("quantity_box");
            Label quantityLabel = slotUI.Q<Label>("label_quantity");
            VisualElement placeholderElem = slotUI.Q<VisualElement>("slot_placeholder");
            VisualElement equippedBadge = slotUI.Q<VisualElement>("badge_equipped");

            if (iconElem != null) iconElem.pickingMode = PickingMode.Ignore;
            if (quantityBox != null) quantityBox.pickingMode = PickingMode.Ignore;
            if (quantityLabel != null) quantityLabel.pickingMode = PickingMode.Ignore;
            if (placeholderElem != null) placeholderElem.pickingMode = PickingMode.Ignore;
            if (equippedBadge != null) equippedBadge.pickingMode = PickingMode.Ignore;

            InventorySlot slot = currentInventory.GetSlot(slotIndex);

            UnregisterPointerCallbacks(slotUI);

            if (slot != null && !slot.IsEmpty && slot.Item != null)
            {
                if (iconElem != null)
                {
                    iconElem.style.backgroundImage = new StyleBackground(slot.Item.Icon);
                    iconElem.style.display = DisplayStyle.Flex;
                }

                if (quantityLabel != null)
                {
                    quantityLabel.text = (slot.Item.MaxStack > 1 && slot.Quantity > 1) ? slot.Quantity.ToString() : string.Empty;
                    quantityBox.style.display = (slot.Quantity > 1) ? DisplayStyle.Flex : DisplayStyle.None;
                }

                if (placeholderElem != null) placeholderElem.style.display = DisplayStyle.None;
                if (equippedBadge != null) equippedBadge.style.display = slot.IsEquipped ? DisplayStyle.Flex : DisplayStyle.None;

                RegisterPointerCallbacks(slotUI, DragSourceType.PersonalInventory, slotIndex, slot.Item, slot.Quantity);
            }
            else
            {
                if (iconElem != null)
                {
                    iconElem.style.backgroundImage = null;
                    iconElem.style.display = DisplayStyle.None;
                }

                if (quantityBox != null)
                {
                    quantityLabel.text = string.Empty;
                    quantityBox.style.display = DisplayStyle.None;
                }

                if (placeholderElem != null) placeholderElem.style.display = DisplayStyle.Flex;
                if (equippedBadge != null) equippedBadge.style.display = DisplayStyle.None;
            }
        }
    }

    private void RefreshEquipmentSlots()
    {
        if (currentEquipment == null) return;

        // 1. Refresh Standard Gear Slots (gearSlots[0..6])
        for (int i = 0; i < gearSlots.Length; i++)
        {
            int slotIndex = i;
            VisualElement slotUI = gearSlots[slotIndex];
            if (slotUI == null) continue;

            EquipmentSlot slotType = gearSlotTypes[slotIndex];
            EquipableSO equippedItem = currentEquipment.GetEquippedItem(slotType);

            UnregisterPointerCallbacks(slotUI);

            if (equippedItem != null)
            {
                slotUI.style.backgroundImage = new StyleBackground(equippedItem.Icon);
                RegisterPointerCallbacks(slotUI, DragSourceType.Equipment, slotIndex, equippedItem, 1);
            }
            else
            {
                slotUI.style.backgroundImage = null;
            }
        }

        // 2. Refresh Psynergy Relic Slots (relicSlots[0..3])
        for (int i = 0; i < relicSlots.Length; i++)
        {
            int slotIndex = i;
            VisualElement slotUI = relicSlots[slotIndex];
            if (slotUI == null) continue;

            PsynergyRelicSO equippedRelic = currentEquipment.GetEquippedRelic(slotIndex);

            UnregisterPointerCallbacks(slotUI);

            if (equippedRelic != null)
            {
                slotUI.style.backgroundImage = new StyleBackground(equippedRelic.Icon);
                RegisterPointerCallbacks(slotUI, DragSourceType.Relic, slotIndex, equippedRelic, 1);
            }
            else
            {
                slotUI.style.backgroundImage = null;
            }
        }
    }

    private void RefreshGlobalBagSlots()
    {
        if (GlobalBag.Instance == null) return;

        IReadOnlyList<InventorySlot> keySlots = GlobalBag.Instance.KeyItemSlots;

        for (int i = 0; i < spawnedGlobalBagSlots.Count; i++)
        {
            VisualElement slotUI = spawnedGlobalBagSlots[i];
            if (slotUI == null) continue;

            VisualElement iconElem = slotUI.Q<VisualElement>("item_icon");
            Label quantityLabel = slotUI.Q<Label>("label_quantity");
            VisualElement placeholderElem = slotUI.Q<VisualElement>("slot_placeholder");

            if (iconElem != null) iconElem.pickingMode = PickingMode.Ignore;
            if (quantityLabel != null) quantityLabel.pickingMode = PickingMode.Ignore;
            if (placeholderElem != null) placeholderElem.pickingMode = PickingMode.Ignore;

            InventorySlot slot = (i < keySlots.Count) ? keySlots[i] : null;

            if (slot != null && !slot.IsEmpty && slot.Item != null)
            {
                if (iconElem != null)
                {
                    iconElem.style.backgroundImage = new StyleBackground(slot.Item.Icon);
                    iconElem.style.display = DisplayStyle.Flex;
                }

                if (quantityLabel != null)
                {
                    quantityLabel.text = (slot.Item.MaxStack > 1 && slot.Quantity > 1) ? slot.Quantity.ToString() : string.Empty;
                    quantityLabel.style.display = (slot.Quantity > 1) ? DisplayStyle.Flex : DisplayStyle.None;
                }

                if (placeholderElem != null) placeholderElem.style.display = DisplayStyle.None;
            }
            else
            {
                if (iconElem != null)
                {
                    iconElem.style.backgroundImage = null;
                    iconElem.style.display = DisplayStyle.None;
                }

                if (quantityLabel != null)
                {
                    quantityLabel.text = string.Empty;
                    quantityLabel.style.display = DisplayStyle.None;
                }

                if (placeholderElem != null) placeholderElem.style.display = DisplayStyle.Flex;
            }
        }
    }

    #endregion

    #region Pointer & Drag Event Callbacks

    private void RegisterPointerCallbacks(VisualElement slotUI, DragSourceType sourceType, int slotIndex, ItemSO item, int quantity)
    {
        UnregisterPointerCallbacks(slotUI);

        EventCallback<PointerDownEvent> pointerDownCallback = evt => OnSlotPointerDown(evt, sourceType, slotIndex, slotUI, item, quantity);
        registeredPointerDownCallbacks[slotUI] = pointerDownCallback;

        slotUI.RegisterCallback(pointerDownCallback);
        slotUI.RegisterCallback<PointerMoveEvent>(OnSlotPointerMove);
        slotUI.RegisterCallback<PointerUpEvent>(OnSlotPointerUp);
        slotUI.RegisterCallback<PointerCaptureOutEvent>(OnSlotPointerCancel);
    }

    private void UnregisterPointerCallbacks(VisualElement slotUI)
    {
        if (slotUI == null) return;

        if (registeredPointerDownCallbacks.TryGetValue(slotUI, out var existingCallback))
        {
            slotUI.UnregisterCallback(existingCallback);
            registeredPointerDownCallbacks.Remove(slotUI);
        }

        slotUI.UnregisterCallback<PointerMoveEvent>(OnSlotPointerMove);
        slotUI.UnregisterCallback<PointerUpEvent>(OnSlotPointerUp);
        slotUI.UnregisterCallback<PointerCaptureOutEvent>(OnSlotPointerCancel);
    }

    private ItemSO GetLiveItemFromSource(DragSourceType sourceType, int index)
    {
        switch (sourceType)
        {
            case DragSourceType.PersonalInventory:
                if (currentInventory == null) return null;
                InventorySlot invSlot = currentInventory.GetSlot(index);
                return (invSlot != null && !invSlot.IsEmpty) ? invSlot.Item : null;

            case DragSourceType.Equipment:
                if (currentEquipment == null || index < 0 || index >= gearSlotTypes.Length) return null;
                return currentEquipment.GetEquippedItem(gearSlotTypes[index]);

            case DragSourceType.Relic:
                if (currentEquipment == null) return null;
                return currentEquipment.GetEquippedRelic(index);

            default:
                return null;
        }
    }

    private void OnSlotPointerDown(PointerDownEvent evt, DragSourceType sourceType, int slotIndex, VisualElement slotUI, ItemSO item, int quantity)
    {
        if (evt.button != 0) return;

        ItemSO liveItem = GetLiveItemFromSource(sourceType, slotIndex);
        if (liveItem == null) return;

        float currentTime = Time.unscaledTime;
        bool isDoubleClick = (currentTime - lastClickTime <= DOUBLE_CLICK_TIME_THRESHOLD)
                             && (lastClickSlotIndex == slotIndex)
                             && (lastClickSourceType == sourceType);

        if (isDoubleClick)
        {
            lastClickTime = 0f;
            lastClickSlotIndex = -1;

            RemoveGhostElement();
            currentDragContext = null;
            activePointerId = -1;
            isDragging = false;

            HandleSlotDoubleClick(sourceType, slotIndex, liveItem);
            return;
        }

        lastClickTime = currentTime;
        lastClickSlotIndex = slotIndex;
        lastClickSourceType = sourceType;

        if (isTargetingItem)
        {
            CancelItemTargeting();
        }

        pointerStartPosition = evt.position;
        activePointerId = evt.pointerId;

        currentDragContext = new DragContext(sourceType, slotIndex, liveItem, quantity, slotUI);
        isDragging = false;

        UpdateInfoPanel(liveItem);
    }

    private void OnSlotPointerMove(PointerMoveEvent evt)
    {
        if (currentDragContext == null || evt.pointerId != activePointerId) return;

        Vector2 currentPosition = evt.position;

        if (!isDragging)
        {
            float distance = Vector2.Distance(pointerStartPosition, currentPosition);
            if (distance >= DRAG_THRESHOLD)
            {
                isDragging = true;

                if (currentDragContext.SourceSlotUI != null)
                {
                    currentDragContext.SourceSlotUI.style.opacity = 0.3f;
                }

                HighlightCompatibleSlots(currentDragContext.Item, true);
                CreateGhostElement(currentDragContext.Item.Icon, currentPosition);
                currentDragContext.SourceSlotUI.CapturePointer(activePointerId);
            }
        }
        else
        {
            UpdateGhostPosition(currentPosition);
        }
    }

    private void OnSlotPointerUp(PointerUpEvent evt)
    {
        if (currentDragContext == null || evt.pointerId != activePointerId) return;

        if (currentDragContext.SourceSlotUI != null && currentDragContext.SourceSlotUI.HasPointerCapture(activePointerId))
        {
            currentDragContext.SourceSlotUI.ReleasePointer(activePointerId);
        }

        if (isDragging)
        {
            ProcessDrop(evt.position);
            RemoveGhostElement();
            RefreshAllData();
        }
        else
        {
            currentDragContext = null;
            activePointerId = -1;
        }
    }

    private void OnSlotPointerCancel(PointerCaptureOutEvent evt)
    {
        if (isDragging)
        {
            RemoveGhostElement();
        }
    }

    #endregion

    #region Double-Click Quick Actions & Chained Targeting Mode

    private void HandleSlotDoubleClick(DragSourceType sourceType, int slotIndex, ItemSO item)
    {
        if (item == null) return;

        // 1. Actions from Personal Inventory
        if (sourceType == DragSourceType.PersonalInventory)
        {
            if (item is EquipableSO equipable)
            {
                Debug.Log("Double-clicked equipable item in inventory: " + item.name);
                if (equipable.EquipmentSlot == EquipmentSlot.PsynergyRelic)
                {
                    Debug.Log("Equipping relic: " + item.name);
                    if (currentEquipment != null)
                    {
                        int emptyRelicIndex = GetFirstEmptyRelicIndex();
                        int targetRelicIndex = (emptyRelicIndex != -1) ? emptyRelicIndex : 0;

                        currentEquipment.EquipFromBackpack(slotIndex, targetRelicIndex);
                        RefreshAllData();
                    }
                }
                else
                {
                    currentEquipment?.EquipFromBackpack(slotIndex);
                    RefreshAllData();
                }
            }
            else if (item is ConsumableSO)
            {
                StartItemTargeting(slotIndex, item);
            }
        }
        // 2. Actions from Standard Gear Slots
        else if (sourceType == DragSourceType.Equipment)
        {
            if (currentEquipment == null || currentInventory == null) return;
            if (slotIndex < 0 || slotIndex >= gearSlotTypes.Length) return;

            if (GetFirstEmptyInventoryIndex() == -1)
            {
                Debug.LogWarning("[PageItems] Cannot unequip: Personal inventory is full.");
                return;
            }

            EquipmentSlot slotType = gearSlotTypes[slotIndex];
            if (currentEquipment.UnequipStandardSlot(slotType))
            {
                RefreshAllData();
            }
        }
        // 3. Actions from Relic Slots
        else if (sourceType == DragSourceType.Relic)
        {
            if (currentEquipment == null || currentInventory == null) return;

            if (GetFirstEmptyInventoryIndex() == -1)
            {
                Debug.LogWarning("[PageItems] Cannot unequip relic: Personal inventory is full.");
                return;
            }

            if (currentEquipment.UnequipRelicSlot(slotIndex))
            {
                RefreshAllData();
            }
        }
    }

    private int GetFirstEmptyRelicIndex()
    {
        if (currentEquipment == null) return -1;

        for (int i = 0; i < 4; i++)
        {
            if (currentEquipment.GetEquippedRelic(i) == null)
            {
                return i;
            }
        }
        return -1;
    }

    private void StartItemTargeting(int sourceIndex, ItemSO item)
    {
        CancelItemTargeting();

        isTargetingItem = true;
        targetingSourceIndex = sourceIndex;
        targetingItem = item;

        CreateTargetingGhost(item.Icon);

        VisualElement topRoot = GetTopRootElement();
        if (topRoot != null)
        {
            topRoot.RegisterCallback<PointerDownEvent>(OnGlobalPointerDownTargeting, TrickleDown.TrickleDown);
            topRoot.RegisterCallback<PointerMoveEvent>(OnGlobalPointerMoveTargeting, TrickleDown.TrickleDown);
        }
    }

    private void OnGlobalPointerDownTargeting(PointerDownEvent evt)
    {
        if (!isTargetingItem) return;

        if (evt.button == 1)
        {
            CancelItemTargeting();
            evt.StopPropagation();
            return;
        }

        if (evt.button == 0)
        {
            if (TryGetDropTarget(evt.position, out DropTargetInfo targetInfo) && targetInfo.TargetType == DropTargetType.HeroCard)
            {
                evt.StopImmediatePropagation();
                ExecuteConsumableUsageOnHero(targetInfo.Index);
            }
            else
            {
                CancelItemTargeting();
            }
        }
    }

    private void OnGlobalPointerMoveTargeting(PointerMoveEvent evt)
    {
        VisualElement topRoot = GetTopRootElement();
        if (!isTargetingItem || targetingGhostElement == null || topRoot == null) return;

        Vector2 localPos = topRoot.WorldToLocal(evt.position);
        targetingGhostElement.style.left = localPos.x - (SLOT_SIZE / 2f);
        targetingGhostElement.style.top = localPos.y - (SLOT_SIZE / 2f);
    }

    private void ExecuteConsumableUsageOnHero(int targetHeroIdx)
    {
        if (currentInventory == null || targetingSourceIndex < 0)
        {
            CancelItemTargeting();
            return;
        }

        InventorySlot sourceSlot = currentInventory.GetSlot(targetingSourceIndex);
        if (sourceSlot == null || sourceSlot.IsEmpty || !(sourceSlot.Item is ConsumableSO consumable))
        {
            CancelItemTargeting();
            return;
        }

        HeroStats targetHero = GetHeroStatsByIdx(targetHeroIdx);
        if (targetHero == null) return;

        Debug.Log($"[Consumable] Using '{consumable.ItemName}' (Slot {targetingSourceIndex}) on {targetHero.Name}...");

        bool effectApplied = consumable.ExecuteMenuEffect(targetHero);

        if (effectApplied)
        {
            currentInventory.RemoveFromSlot(targetingSourceIndex, 1);
            RefreshAllData();

            InventorySlot updatedSlot = currentInventory.GetSlot(targetingSourceIndex);
            if (updatedSlot == null || updatedSlot.IsEmpty || updatedSlot.Quantity <= 0)
            {
                CancelItemTargeting();
            }
        }
        else
        {
            Debug.LogWarning($"[Consumable] '{consumable.ItemName}' had no effect on {targetHero.Name}. Item was not consumed.");
        }
    }

    private HeroStats GetHeroStatsByIdx(int index)
    {
        if (partyHeroes != null && index >= 0 && index < partyHeroes.Count)
        {
            return partyHeroes[index];
        }
        return null;
    }

    private void CreateTargetingGhost(Sprite icon)
    {
        DestroyTargetingGhost();

        targetingGhostElement = new VisualElement
        {
            name = "targeting_ghost_element",
            pickingMode = PickingMode.Ignore
        };

        targetingGhostElement.style.position = Position.Absolute;
        targetingGhostElement.style.width = SLOT_SIZE;
        targetingGhostElement.style.height = SLOT_SIZE;
        if (icon != null)
        {
            targetingGhostElement.style.backgroundImage = new StyleBackground(icon);
        }
        targetingGhostElement.style.opacity = 0.85f;
        targetingGhostElement.style.left = -9999f;
        targetingGhostElement.style.top = -9999f;

        VisualElement topRoot = GetTopRootElement();
        topRoot?.Add(targetingGhostElement);
    }

    private void DestroyTargetingGhost()
    {
        if (targetingGhostElement != null)
        {
            targetingGhostElement.RemoveFromHierarchy();
            targetingGhostElement = null;
        }
    }

    public void CancelItemTargeting()
    {
        VisualElement topRoot = GetTopRootElement();
        if (topRoot != null)
        {
            topRoot.UnregisterCallback<PointerDownEvent>(OnGlobalPointerDownTargeting, TrickleDown.TrickleDown);
            topRoot.UnregisterCallback<PointerMoveEvent>(OnGlobalPointerMoveTargeting, TrickleDown.TrickleDown);
        }

        DestroyTargetingGhost();

        isTargetingItem = false;
        targetingSourceIndex = -1;
        targetingItem = null;
    }

    #endregion

    #region Slot Highlighting & Drop Target Detection

    private void HighlightCompatibleSlots(ItemSO item, bool enable)
    {
        if (item is EquipableSO equipable)
        {
            if (equipable.EquipmentSlot == EquipmentSlot.PsynergyRelic)
            {
                for (int i = 0; i < relicSlots.Length; i++)
                {
                    SetSlotHighlight(relicSlots[i], enable);
                }
            }
            else
            {
                EquipmentSlot slotType = equipable.EquipmentSlot;

                for (int i = 0; i < gearSlotTypes.Length; i++)
                {
                    if (gearSlotTypes[i] == slotType && gearSlots[i] != null)
                    {
                        SetSlotHighlight(gearSlots[i], enable);
                    }
                }
            }
        }
    }

    private void SetSlotHighlight(VisualElement slotUI, bool enable)
    {
        if (slotUI == null) return;

        if (enable)
        {
            slotUI.style.borderTopColor = new StyleColor(new Color(1f, 0.84f, 0f, 0.9f));
            slotUI.style.borderRightColor = new StyleColor(new Color(1f, 0.84f, 0f, 0.9f));
            slotUI.style.borderBottomColor = new StyleColor(new Color(1f, 0.84f, 0f, 0.9f));
            slotUI.style.borderLeftColor = new StyleColor(new Color(1f, 0.84f, 0f, 0.9f));
        }
        else
        {
            slotUI.style.borderTopColor = StyleKeyword.Null;
            slotUI.style.borderRightColor = StyleKeyword.Null;
            slotUI.style.borderBottomColor = StyleKeyword.Null;
            slotUI.style.borderLeftColor = StyleKeyword.Null;
        }
    }

    private bool TryGetDropTarget(Vector2 pointerPosition, out DropTargetInfo targetInfo)
    {
        targetInfo = new DropTargetInfo(DropTargetType.None, -1, null);
        if (rootElement?.panel == null) return false;

        VisualElement picked = rootElement.panel.Pick(pointerPosition);
        if (picked == null) return false;

        // 1. Check Personal Inventory Slots
        for (int i = 0; i < spawnedInventorySlots.Count; i++)
        {
            if (spawnedInventorySlots[i] != null && IsElementOrChildOf(picked, spawnedInventorySlots[i]))
            {
                targetInfo = new DropTargetInfo(DropTargetType.PersonalInventory, i, spawnedInventorySlots[i]);
                return true;
            }
        }

        // 2. Check Equipment Slots
        for (int i = 0; i < gearSlots.Length; i++)
        {
            if (gearSlots[i] != null && IsElementOrChildOf(picked, gearSlots[i]))
            {
                targetInfo = new DropTargetInfo(DropTargetType.Equipment, i, gearSlots[i]);
                return true;
            }
        }

        // 3. Check Relic Slots
        for (int i = 0; i < relicSlots.Length; i++)
        {
            if (relicSlots[i] != null && IsElementOrChildOf(picked, relicSlots[i]))
            {
                targetInfo = new DropTargetInfo(DropTargetType.Relic, i, relicSlots[i]);
                return true;
            }
        }

        // 4. Check Hero Header Cards in Main Menu
        VisualElement heroCard = FindParentMatching(picked, elem => elem.name != null && elem.name.StartsWith("btn_hero"));
        if (heroCard != null)
        {
            string nameStr = heroCard.name.Replace("btn_hero", "");
            if (int.TryParse(nameStr, out int heroIdx))
            {
                targetInfo = new DropTargetInfo(DropTargetType.HeroCard, heroIdx, heroCard);
                return true;
            }
        }

        return false;
    }

    private bool IsElementOrChildOf(VisualElement child, VisualElement parent)
    {
        VisualElement current = child;
        while (current != null)
        {
            if (current == parent) return true;
            current = current.parent;
        }
        return false;
    }

    private VisualElement FindParentMatching(VisualElement element, System.Predicate<VisualElement> predicate)
    {
        VisualElement current = element;
        while (current != null)
        {
            if (predicate(current)) return current;
            current = current.parent;
        }
        return null;
    }

    #endregion

    #region Drop Processing & Transactions

    private void ProcessDrop(Vector2 pointerPosition)
    {
        if (currentDragContext == null || currentInventory == null || currentEquipment == null) return;

        if (TryGetDropTarget(pointerPosition, out DropTargetInfo targetInfo))
        {
            switch (targetInfo.TargetType)
            {
                case DropTargetType.PersonalInventory:
                    ExecutePersonalInventoryDrop(targetInfo.Index);
                    break;

                case DropTargetType.Equipment:
                    ExecuteEquipmentDrop(targetInfo.Index);
                    break;

                case DropTargetType.Relic:
                    ExecuteRelicDrop(targetInfo.Index);
                    break;

                case DropTargetType.HeroCard:
                    ExecuteHeroCardDrop(targetInfo.Index);
                    break;
            }
        }
    }

    private void ExecutePersonalInventoryDrop(int targetIndex)
    {
        if (currentDragContext.SourceType == DragSourceType.PersonalInventory)
        {
            int sourceIndex = currentDragContext.SourceIndex;
            if (sourceIndex == targetIndex || targetIndex < 0 || targetIndex >= currentInventory.MaxBackpackSlots) return;

            InventorySlot targetSlot = currentInventory.GetSlot(targetIndex);
            bool isTargetEmpty = (targetSlot == null || targetSlot.IsEmpty);
            bool isSameStackableItem = (!isTargetEmpty && targetSlot.Item.ItemSlug == currentDragContext.Item.ItemSlug && targetSlot.Item.MaxStack > 1);

            if (currentDragContext.Quantity > 1 && (isTargetEmpty || isSameStackableItem))
            {
                OpenSplitModal(
                    sourceType: DragSourceType.PersonalInventory,
                    sourceIndex: sourceIndex,
                    targetType: DropTargetType.PersonalInventory,
                    targetIndex: targetIndex,
                    targetHeroInv: null,
                    item: currentDragContext.Item,
                    maxQty: currentDragContext.Quantity
                );
            }
            else
            {
                currentInventory.SwapOrMergeSlots(sourceIndex, targetIndex);
            }
        }
        else if (currentDragContext.SourceType == DragSourceType.Equipment || currentDragContext.SourceType == DragSourceType.Relic)
        {
            ExecuteUnequipToInventory(targetIndex);
        }
    }

    private void ExecuteEquipmentDrop(int targetIndex)
    {
        if (currentDragContext.SourceType == DragSourceType.PersonalInventory)
        {
            if (targetIndex < 0 || targetIndex >= gearSlotTypes.Length) return;

            EquipmentSlot targetSlotType = gearSlotTypes[targetIndex];

            if (currentDragContext.Item is EquipableSO equipable && equipable.EquipmentSlot == targetSlotType)
            {
                currentEquipment.EquipFromBackpack(currentDragContext.SourceIndex);
            }
        }
    }

    private void ExecuteRelicDrop(int targetIndex)
    {
        if (currentDragContext.SourceType == DragSourceType.PersonalInventory)
        {
            if (currentDragContext.Item is EquipableSO equipable && equipable.EquipmentSlot == EquipmentSlot.PsynergyRelic)
            {
                currentEquipment.EquipFromBackpack(currentDragContext.SourceIndex, targetIndex);
            }
        }
    }

    private void ExecuteUnequipToInventory(int targetInventoryIndex)
    {
        if (currentInventory == null || currentEquipment == null) return;

        InventorySlot targetSlot = currentInventory.GetSlot(targetInventoryIndex);
        EquipmentSlot sourceGearSlot = (currentDragContext.SourceType == DragSourceType.Equipment) ? gearSlotTypes[currentDragContext.SourceIndex] : EquipmentSlot.None;

        bool canDirectSwap = false;
        if (targetSlot != null && !targetSlot.IsEmpty && targetSlot.Item is EquipableSO equipableTarget)
        {
            if (currentDragContext.SourceType == DragSourceType.Equipment && equipableTarget.EquipmentSlot == sourceGearSlot)
            {
                canDirectSwap = true;
            }
            else if (currentDragContext.SourceType == DragSourceType.Relic && equipableTarget.EquipmentSlot == EquipmentSlot.PsynergyRelic)
            {
                canDirectSwap = true;
            }
        }

        if (canDirectSwap)
        {
            if (currentDragContext.SourceType == DragSourceType.Equipment)
            {
                currentEquipment.EquipFromBackpack(targetInventoryIndex);
            }
            else if (currentDragContext.SourceType == DragSourceType.Relic)
            {
                currentEquipment.EquipFromBackpack(targetInventoryIndex, currentDragContext.SourceIndex);
            }
        }
        else if (targetSlot != null && targetSlot.IsEmpty)
        {
            int firstEmpty = GetFirstEmptyInventoryIndex();

            if (currentDragContext.SourceType == DragSourceType.Equipment)
            {
                if (currentEquipment.UnequipStandardSlot(sourceGearSlot))
                {
                    if (firstEmpty != -1 && firstEmpty != targetInventoryIndex)
                    {
                        currentInventory.SwapOrMergeSlots(firstEmpty, targetInventoryIndex);
                    }
                }
            }
            else if (currentDragContext.SourceType == DragSourceType.Relic)
            {
                if (currentEquipment.UnequipRelicSlot(currentDragContext.SourceIndex))
                {
                    if (firstEmpty != -1 && firstEmpty != targetInventoryIndex)
                    {
                        currentInventory.SwapOrMergeSlots(firstEmpty, targetInventoryIndex);
                    }
                }
            }
        }
        else
        {
            if (currentDragContext.SourceType == DragSourceType.Equipment)
            {
                currentEquipment.UnequipStandardSlot(sourceGearSlot);
            }
            else if (currentDragContext.SourceType == DragSourceType.Relic)
            {
                currentEquipment.UnequipRelicSlot(currentDragContext.SourceIndex);
            }
        }
    }

    private void ExecuteHeroCardDrop(int targetHeroIdx)
    {
        HeroInventory targetHeroInv = GetHeroInventoryByIdx(targetHeroIdx);
        if (targetHeroInv == null || targetHeroInv == currentInventory) return;

        if (currentDragContext.SourceType == DragSourceType.PersonalInventory)
        {
            if (!targetHeroInv.CanAddItem(currentDragContext.Item, 1))
            {
                Debug.LogWarning($"[PageItems] Cannot transfer item to {targetHeroInv.gameObject.name}. Backpack is full!");
                return;
            }

            if (currentDragContext.Quantity > 1)
            {
                OpenSplitModal(
                    sourceType: DragSourceType.PersonalInventory,
                    sourceIndex: currentDragContext.SourceIndex,
                    targetType: DropTargetType.HeroCard,
                    targetIndex: targetHeroIdx,
                    targetHeroInv: targetHeroInv,
                    item: currentDragContext.Item,
                    maxQty: currentDragContext.Quantity
                );
            }
            else
            {
                currentInventory.TransferAmountToHero(currentDragContext.SourceIndex, 1, targetHeroInv);
            }
        }
        else if (currentDragContext.SourceType == DragSourceType.Equipment || currentDragContext.SourceType == DragSourceType.Relic)
        {
            if (!targetHeroInv.CanAddItem(currentDragContext.Item, 1))
            {
                Debug.LogWarning($"[PageItems] Cannot transfer equipped item to {targetHeroInv.gameObject.name}. Target backpack full!");
                return;
            }

            EquipmentSlot sourceGearSlot = (currentDragContext.SourceType == DragSourceType.Equipment) ? gearSlotTypes[currentDragContext.SourceIndex] : EquipmentSlot.None;

            if (currentDragContext.SourceType == DragSourceType.Equipment)
            {
                if (currentEquipment.UnequipStandardSlot(sourceGearSlot))
                {
                    currentInventory.TransferItemToHero(GetFirstEmptyInventoryIndex(), targetHeroInv);
                }
            }
            else if (currentDragContext.SourceType == DragSourceType.Relic)
            {
                if (currentEquipment.UnequipRelicSlot(currentDragContext.SourceIndex))
                {
                    currentInventory.TransferItemToHero(GetFirstEmptyInventoryIndex(), targetHeroInv);
                }
            }
        }
    }

    private HeroInventory GetHeroInventoryByIdx(int index)
    {
        if (partyHeroes != null && index >= 0 && index < partyHeroes.Count)
        {
            return partyHeroes[index]?.GetComponent<HeroInventory>();
        }
        return null;
    }

    private int GetFirstEmptyInventoryIndex()
    {
        if (currentInventory == null) return -1;

        for (int i = 0; i < currentInventory.MaxBackpackSlots; i++)
        {
            InventorySlot slot = currentInventory.GetSlot(i);
            if (slot == null || slot.IsEmpty)
            {
                return i;
            }
        }
        return -1;
    }

    #endregion

    #region Stack Splitter Popup Controls

    private void OpenSplitModal(DragSourceType sourceType, int sourceIndex, DropTargetType targetType, int targetIndex, HeroInventory targetHeroInv, ItemSO item, int maxQty)
    {
        if (splitPopupOverlay == null) return;

        pendingSplit = new PendingSplitTransaction
        {
            SourceType = sourceType,
            SourceIndex = sourceIndex,
            TargetType = targetType,
            TargetIndex = targetIndex,
            TargetHeroInventory = targetHeroInv,
            Item = item,
            MaxQuantity = maxQty
        };

        if (splitItemIcon != null && item != null && item.Icon != null)
        {
            splitItemIcon.style.backgroundImage = new StyleBackground(item.Icon);
        }

        if (labelSplitItemName != null && item != null)
        {
            labelSplitItemName.text = item.ItemName;
        }

        if (sliderSplitAmount != null)
        {
            sliderSplitAmount.lowValue = 1;
            sliderSplitAmount.highValue = maxQty;
            sliderSplitAmount.value = maxQty;
        }

        UpdateSplitAmountLabel(maxQty);
        splitPopupOverlay.style.display = DisplayStyle.Flex;
    }

    private void StepSplitAmount(int delta)
    {
        if (sliderSplitAmount == null) return;
        int newValue = Mathf.Clamp(sliderSplitAmount.value + delta, sliderSplitAmount.lowValue, sliderSplitAmount.highValue);
        sliderSplitAmount.value = newValue;
    }

    private void UpdateSplitAmountLabel(int value)
    {
        if (labelSplitAmount != null)
        {
            labelSplitAmount.text = value.ToString();
        }
    }

    private void OnConfirmSplit()
    {
        int amountToMove = (sliderSplitAmount != null) ? sliderSplitAmount.value : 1;

        if (pendingSplit.TargetType == DropTargetType.PersonalInventory)
        {
            if (pendingSplit.SourceType == DragSourceType.PersonalInventory)
            {
                currentInventory.SplitOrMoveSlotAmount(pendingSplit.SourceIndex, pendingSplit.TargetIndex, amountToMove);
            }
        }
        else if (pendingSplit.TargetType == DropTargetType.HeroCard && pendingSplit.TargetHeroInventory != null)
        {
            if (pendingSplit.SourceType == DragSourceType.PersonalInventory)
            {
                currentInventory.TransferAmountToHero(pendingSplit.SourceIndex, amountToMove, pendingSplit.TargetHeroInventory);
            }
        }

        CloseSplitModal();
        RefreshAllData();
    }

    private void CloseSplitModal()
    {
        if (splitPopupOverlay != null)
        {
            splitPopupOverlay.style.display = DisplayStyle.None;
        }
    }

    #endregion

    #region Ghost Element Management

    private void CreateGhostElement(Sprite icon, Vector2 startPosition)
    {
        DestroyGhostVisual();

        ghostElement = new VisualElement
        {
            name = "drag_ghost_element",
            pickingMode = PickingMode.Ignore
        };

        ghostElement.style.position = Position.Absolute;
        ghostElement.style.width = SLOT_SIZE;
        ghostElement.style.height = SLOT_SIZE;
        if (icon != null)
        {
            ghostElement.style.backgroundImage = new StyleBackground(icon);
        }
        ghostElement.style.opacity = 0.85f;

        VisualElement topRoot = GetTopRootElement();
        if (topRoot != null)
        {
            Vector2 localPos = topRoot.WorldToLocal(startPosition);
            ghostElement.style.left = localPos.x - (SLOT_SIZE / 2f);
            ghostElement.style.top = localPos.y - (SLOT_SIZE / 2f);

            topRoot.Add(ghostElement);
        }
    }

    private void UpdateGhostPosition(Vector2 currentPosition)
    {
        VisualElement topRoot = GetTopRootElement();
        if (ghostElement == null || topRoot == null) return;

        Vector2 localPos = topRoot.WorldToLocal(currentPosition);

        ghostElement.style.left = localPos.x - (SLOT_SIZE / 2f);
        ghostElement.style.top = localPos.y - (SLOT_SIZE / 2f);
    }

    private void DestroyGhostVisual()
    {
        if (ghostElement != null)
        {
            ghostElement.RemoveFromHierarchy();
            ghostElement = null;
        }
    }

    private void RemoveGhostElement()
    {
        if (currentDragContext?.Item != null)
        {
            HighlightCompatibleSlots(currentDragContext.Item, false);
        }

        DestroyGhostVisual();

        if (currentDragContext?.SourceSlotUI != null)
        {
            currentDragContext.SourceSlotUI.style.opacity = 1f;
        }

        isDragging = false;
        currentDragContext = null;
        activePointerId = -1;
    }

    #endregion

    #region Info Panel & Dynamic Extra Details Display

    private void UpdateInfoPanel(ItemSO item)
    {
        selectedItem = item;

        if (item == null)
        {
            ClearInfoPanel();
            return;
        }

        if (labelItemName != null) labelItemName.text = item.ItemName;
        if (labelItemType != null) labelItemType.text = item.ItemType.ToString();

        if (labelItemSellPrice != null)
        {
            labelItemSellPrice.text = item.CanSell
                ? $"Se vende por:  {item.SellPrice} monedas"
                : "No se puede vender";
        }

        if (labelItemMaxStack != null) labelItemMaxStack.text = $"Máx. stack:  {item.MaxStack}";
        if (labelItemDesc != null) labelItemDesc.text = item.ItemDescription;

        if (itemIconPreview != null && item.Icon != null)
        {
            itemIconPreview.style.backgroundImage = new StyleBackground(item.Icon);
        }

        UpdateInfoExtraPanel(item);
    }

    /// <summary>
    /// Instantiates and binds the matching UXML template into extraInfoContainer based on the selected ItemSO type.
    /// Dynamically renders equipment stat modifiers and composite consumable status/resource effects.
    /// </summary>
    private void UpdateInfoExtraPanel(ItemSO item)
    {
        if (extraInfoContainer == null) return;

        extraInfoContainer.Clear();

        if (item == null) return;

        // 1. Weapon Artifacts / Standard Weapons
        if (item is WeaponSO weapon)
        {
            if (templateInfoWeapon == null) return;

            TemplateContainer instance = templateInfoWeapon.Instantiate();
            extraInfoContainer.Add(instance);

            // Element Badge
            VisualElement badgeElement = instance.Q<VisualElement>("badge_element");
            Label labelElementName = instance.Q<Label>("label_element_name");
            VisualElement elementIcon = instance.Q<VisualElement>("element_icon");
            ApplyElementBadgeTheme(badgeElement, labelElementName, elementIcon, weapon.Element);

            // Hit Type Badge
            Label labelHitType = instance.Q<Label>("label_hit_type");
            if (labelHitType != null) labelHitType.text = weapon.HitFXType.ToString().ToUpper();

            // Battle Use Badge
            VisualElement badgeBattleUse = instance.Q<VisualElement>("badge_battle_use");
            if (badgeBattleUse != null)
            {
                badgeBattleUse.RemoveFromClassList("badge-active");
                badgeBattleUse.RemoveFromClassList("badge-inactive");
                badgeBattleUse.AddToClassList(weapon.UseInBattle ? "badge-active" : "badge-inactive");
            }

            // Weapon Unleash Box
            VisualElement unleashBox = instance.Q<VisualElement>("unleash_info_box");
            if (unleashBox != null)
            {
                if (weapon.HasUnleash && weapon.UnleashEffect != null)
                {
                    unleashBox.style.display = DisplayStyle.Flex;

                    Label labelUnleashRate = instance.Q<Label>("label_unleash_rate");
                    if (labelUnleashRate != null) labelUnleashRate.text = $"{Mathf.RoundToInt(weapon.UnleashProcRate * 100f)}% Proc";

                    VisualElement unleashIcon = instance.Q<VisualElement>("unleash_icon");
                    if (unleashIcon != null && weapon.UnleashEffect.Icon != null)
                    {
                        unleashIcon.style.backgroundImage = new StyleBackground(weapon.UnleashEffect.Icon);
                    }

                    // Unleash Elemental Pip
                    VisualElement unleashElementIcon = instance.Q<VisualElement>("unleash_element_icon");
                    if (unleashElementIcon != null)
                    {
                        switch (weapon.UnleashEffect.Element)
                        {
                            case ElementalType.None:
                                unleashElementIcon.style.display = DisplayStyle.None;
                                break;
                            case ElementalType.Venus:
                                unleashElementIcon.AddToClassList("icon-venus");
                                break;
                            case ElementalType.Mars:
                                unleashElementIcon.AddToClassList("icon-mars");
                                break;
                            case ElementalType.Jupiter:
                                unleashElementIcon.AddToClassList("icon-jupiter");
                                break;
                            case ElementalType.Mercury:
                                unleashElementIcon.AddToClassList("icon-mercury");
                                break;
                        }
                    }

                    Label labelUnleashName = instance.Q<Label>("label_unleash_name");
                    if (labelUnleashName != null) labelUnleashName.text = weapon.UnleashEffect.UnleashName;

                    Label labelUnleashDesc = instance.Q<Label>("label_unleash_desc");
                    if (labelUnleashDesc != null) labelUnleashDesc.text = weapon.UnleashEffect.Description;
                }
                else
                {
                    unleashBox.style.display = DisplayStyle.None;
                }
            }

            // Render Stat Modifiers
            RenderEquipableStatModifiers(instance, weapon);

            // Stat Delta Comparison
            PopulateStatDeltaComparison(instance, weapon);

            // Flavor Text
            Label labelFlavor = instance.Q<Label>("label_flavor_text");
            if (labelFlavor != null) labelFlavor.text = weapon.FlavorText;
        }
        // 2. Psynergy Relics
        else if (item is PsynergyRelicSO relic)
        {
            if (templateInfoPsynergyRelic == null) return;

            TemplateContainer instance = templateInfoPsynergyRelic.Instantiate();
            extraInfoContainer.Add(instance);

            VisualElement cardIcon = instance.Q<VisualElement>("psynergy_card_icon");
            Label labelPsynergyName = instance.Q<Label>("label_psynergy_name");
            Label badgeUsageType = instance.Q<Label>("badge_usage_type");
            VisualElement elemMiniIcon = instance.Q<VisualElement>("element_mini_icon");
            Label labelPsynergyPp = instance.Q<Label>("label_psynergy_pp");

            if (relic.GrantedPsynergy != null)
            {
                if (labelPsynergyName != null) labelPsynergyName.text = relic.GrantedPsynergy.PsynergyName;
                if (labelPsynergyPp != null) labelPsynergyPp.text = $"{relic.GrantedPsynergy.PpCost} PP";

                if (cardIcon != null && relic.GrantedPsynergy.Icon != null)
                {
                    cardIcon.style.backgroundImage = new StyleBackground(relic.GrantedPsynergy.Icon);
                }

                if (elemMiniIcon != null && relic.GrantedPsynergy.ElemIcon != null)
                {
                    elemMiniIcon.style.backgroundImage = new StyleBackground(relic.GrantedPsynergy.ElemIcon);
                }

                if (badgeUsageType != null)
                {
                    badgeUsageType.RemoveFromClassList("badge-world");
                    badgeUsageType.RemoveFromClassList("badge-battle");
                    badgeUsageType.RemoveFromClassList("badge-menu");

                    if (relic.GrantedPsynergy.WorldData.CanBeUsedInWorld)
                    {
                        badgeUsageType.text = "Mundo";
                        badgeUsageType.AddToClassList("badge-world");
                    }
                    else if (relic.GrantedPsynergy.IsBattlePsynergy)
                    {
                        badgeUsageType.text = "Combate";
                        badgeUsageType.AddToClassList("badge-battle");
                    }
                    else
                    {
                        badgeUsageType.text = "Menú";
                        badgeUsageType.AddToClassList("badge-menu");
                    }
                }
            }

            RenderEquipableStatModifiers(instance, relic);

            Label labelFlavor = instance.Q<Label>("label_flavor_text");
            if (labelFlavor != null) labelFlavor.text = relic.FlavorText;
        }
        // 3. Standard Equipables (Armors, Helmets, Accessories, etc.)
        else if (item is EquipableSO equipable)
        {
            if (templateInfoEquipable == null) return;

            TemplateContainer instance = templateInfoEquipable.Instantiate();
            extraInfoContainer.Add(instance);

            // Element Badge
            VisualElement badgeElement = instance.Q<VisualElement>("badge_element");
            Label labelElementName = instance.Q<Label>("label_element_name");
            VisualElement elementIcon = instance.Q<VisualElement>("element_icon");
            ApplyElementBadgeTheme(badgeElement, labelElementName, elementIcon, equipable.Element);

            // Battle Use Badge & Effect Description
            VisualElement badgeBattleUse = instance.Q<VisualElement>("badge_battle_use");
            VisualElement battleEffectBox = instance.Q<VisualElement>("battle_effect_info_box");
            Label labelBattleEffectName = instance.Q<Label>("label_battle_effect_name");

            if (badgeBattleUse != null)
            {
                badgeBattleUse.RemoveFromClassList("badge-active");
                badgeBattleUse.RemoveFromClassList("badge-inactive");
                badgeBattleUse.AddToClassList(equipable.UseInBattle ? "badge-active" : "badge-inactive");
            }

            if (battleEffectBox != null)
            {
                if (equipable.UseInBattle && equipable.BattleUseEffect != null)
                {
                    battleEffectBox.style.display = DisplayStyle.Flex;
                    if (labelBattleEffectName != null) labelBattleEffectName.text = equipable.BattleUseEffect.name;
                }
                else
                {
                    battleEffectBox.style.display = DisplayStyle.None;
                }
            }

            // Render Stat Modifiers
            RenderEquipableStatModifiers(instance, equipable);

            // Stat Delta Comparison
            PopulateStatDeltaComparison(instance, equipable);

            // Flavor Text
            Label labelFlavor = instance.Q<Label>("label_flavor_text");
            if (labelFlavor != null) labelFlavor.text = equipable.FlavorText;
        }
        // 4. Consumables (Composite Effects System)
        else if (item is ConsumableSO consumable)
        {
            if (templateInfoConsumable == null) return;

            TemplateContainer instance = templateInfoConsumable.Instantiate();
            extraInfoContainer.Add(instance);

            VisualElement badgeMenuUse = instance.Q<VisualElement>("badge_menu_use");
            if (badgeMenuUse != null)
            {
                badgeBattleUseClassToggle(badgeMenuUse, consumable.UseInMenu);
            }

            VisualElement badgeBattleUse = instance.Q<VisualElement>("badge_battle_use");
            if (badgeBattleUse != null)
            {
                badgeBattleUseClassToggle(badgeBattleUse, consumable.UseInBattle);
            }

            // Target the dynamic effects list container in UXML
            VisualElement effectsContainer = instance.Q<VisualElement>("effects_list_container");

            if (effectsContainer != null)
            {
                effectsContainer.Clear();

                // 1. HP Restoration
                if (consumable.RestoresHp)
                {
                    string hpText = consumable.IsHpPercentage ? $"+{consumable.HpAmount}% PV" : $"+{consumable.HpAmount} PV";
                    AddEffectRow(effectsContainer, "Restaura HP", hpText);
                }

                // 2. PP Restoration
                if (consumable.RestoresPp)
                {
                    string ppText = consumable.IsPpPercentage ? $"+{consumable.PpAmount}% PP" : $"+{consumable.PpAmount} PP";
                    AddEffectRow(effectsContainer, "Restaura PP", ppText);
                }

                // 3. Revival Settings
                if (consumable.CanRevive)
                {
                    string reviveText = consumable.IsRevivePercentage ? $"+{consumable.ReviveHpAmount}% PV" : $"+{consumable.ReviveHpAmount} PV";
                    AddEffectRow(effectsContainer, "Resurrección", reviveText);
                }

                // 4. Status Removal & Cures
                if (consumable.CureAllStatusEffects)
                {
                    AddEffectRow(effectsContainer, "Curar Estados", "Todos");
                }
                else
                {
                    if (consumable.CategoriesToCure != null)
                    {
                        foreach (var category in consumable.CategoriesToCure)
                        {
                            AddEffectRow(effectsContainer, "Curar Categoría", category.ToString());
                        }
                    }

                    if (consumable.SpecificStatusToCure != null)
                    {
                        foreach (var statusSO in consumable.SpecificStatusToCure)
                        {
                            if (statusSO != null)
                            {
                                AddEffectRow(effectsContainer, "Curar Estado", statusSO.EffectName);
                            }
                        }
                    }
                }

                // 5. Applied Status Effects / Buffs
                if (consumable.StatusEffectsToApply != null)
                {
                    foreach (var statusSO in consumable.StatusEffectsToApply)
                    {
                        if (statusSO != null)
                        {
                            AddEffectRow(effectsContainer, "Aplica Estado", statusSO.EffectName);
                        }
                    }
                }

                // 6. Permanent Stat Boost
                if (consumable.IsPermanentStatBoost)
                {
                    string statName = GetStatDisplayName(consumable.StatToBonus);
                    AddEffectRow(effectsContainer, "Mejora Permanente", $"+{consumable.StatBonusAmount} {statName}");
                }
            }

            Label labelFlavor = instance.Q<Label>("label_flavor_text");
            if (labelFlavor != null) labelFlavor.text = consumable.FlavorText;
        }
        // 5. Key / Story Items
        else if (item is KeyItemSO keyItem)
        {
            if (templateInfoKeyItem == null) return;

            TemplateContainer instance = templateInfoKeyItem.Instantiate();
            extraInfoContainer.Add(instance);

            Label labelFlavor = instance.Q<Label>("label_lore_text") ?? instance.Q<Label>("label_flavor_text");
            if (labelFlavor != null) labelFlavor.text = keyItem.FlavorText;
        }
    }

    /// <summary>
    /// Helper method to dynamically instantiate an effect row using ItemInfo_Consumable.uss CSS classes.
    /// </summary>
    private void AddEffectRow(VisualElement container, string typeText, string valueText)
    {
        if (container == null) return;

        VisualElement row = new VisualElement();
        row.AddToClassList("effect-value-row");

        Label labelType = new Label(typeText);
        labelType.AddToClassList("label-effect-type");

        Label labelValue = new Label(valueText);
        labelValue.AddToClassList("label-effect-value");

        row.Add(labelType);
        row.Add(labelValue);

        container.Add(row);
    }

    private void badgeBattleUseClassToggle(VisualElement elem, bool active)
    {
        elem.RemoveFromClassList("badge-active");
        elem.RemoveFromClassList("badge-inactive");
        elem.AddToClassList(active ? "badge-active" : "badge-inactive");
    }

    /// <summary>
    /// Applies CSS class themes to the elemental badge according to ElementalType.
    /// </summary>
    private void ApplyElementBadgeTheme(VisualElement badgeElement, Label labelElementName, VisualElement elementIcon, ElementalType element)
    {
        if (badgeElement == null) return;

        badgeElement.RemoveFromClassList("badge-element-none");
        badgeElement.RemoveFromClassList("badge-element-venus");
        badgeElement.RemoveFromClassList("badge-element-mars");
        badgeElement.RemoveFromClassList("badge-element-jupiter");
        badgeElement.RemoveFromClassList("badge-element-mercury");

        string elemString = element.ToString().ToLower();
        badgeElement.AddToClassList($"badge-element-{elemString}");

        // ElementIcon
        if (elementIcon != null)
        {
            elementIcon.RemoveFromClassList("icon-none");
            elementIcon.RemoveFromClassList("icon-venus");
            elementIcon.RemoveFromClassList("icon-mars");
            elementIcon.RemoveFromClassList("icon-jupiter");
            elementIcon.RemoveFromClassList("icon-mercury");

            switch (element)
            {
                case ElementalType.None:
                    elementIcon.AddToClassList("icon-none");
                    break;
                case ElementalType.Venus:
                    elementIcon.AddToClassList("icon-venus");
                    break;
                case ElementalType.Mars:
                    elementIcon.AddToClassList("icon-mars");
                    break;
                case ElementalType.Jupiter:
                    elementIcon.AddToClassList("icon-jupiter");
                    break;
                case ElementalType.Mercury:
                    elementIcon.AddToClassList("icon-mercury");
                    break;
                default:
                    elementIcon.AddToClassList("icon-none");
                    break;
            }
        }

        if (labelElementName != null)
        {
            labelElementName.text = element.ToString().ToUpper();
        }
    }

    /// <summary>
    /// Renders all Stat Modifiers belonging strictly to the equipable item into generic containers.
    /// </summary>
    private void RenderEquipableStatModifiers(VisualElement rootInstance, EquipableSO equipable)
    {
        if (equipable == null) return;

        // Query generic grid containers across weapon, equipable, or relic views
        VisualElement statsContainer = rootInstance.Q<VisualElement>("stats_grid") ??
                                       rootInstance.Q<VisualElement>("stats_list_container") ??
                                       rootInstance.Q<VisualElement>("stat_modifiers_box");

        IReadOnlyList<StatBonus> modifiers = equipable.StatModifiers;
        if (modifiers == null || modifiers.Count == 0 || statsContainer == null) return;

        statsContainer.Clear();

        foreach (var mod in modifiers)
        {
            if (mod.value == 0) continue;

            VisualElement row = new VisualElement();
            row.AddToClassList("item-stat-row");

            Label nameLabel = new Label(GetStatDisplayName(mod.statType));
            nameLabel.AddToClassList("item-stat-name");

            string percentSuffix = (mod.modifierType != StatModifierType.Flat) ? "%" : string.Empty;
            string valuePrefix = (mod.value > 0) ? "+" : string.Empty;

            Label valueLabel = new Label($"{valuePrefix}{mod.value}{percentSuffix}");
            valueLabel.AddToClassList("item-stat-value");

            row.Add(nameLabel);
            row.Add(valueLabel);

            statsContainer.Add(row);
        }
    }

    /// <summary>
    /// Calculates current vs new stat comparison delta values using the centralized StatCalculator engine.
    /// Evaluates exact predicted stats if newEquipable is equipped, replacing current gear in the same slot.
    /// </summary>
    private void PopulateStatDeltaComparison(VisualElement rootInstance, EquipableSO newEquipable)
    {
        if (currentHero == null || currentEquipment == null || newEquipable == null) return;

        // Query generic comparison containers across UXML views
        VisualElement deltaContainer = rootInstance.Q<VisualElement>("comparison_rows_container") ??
                                       rootInstance.Q<VisualElement>("delta_comparison_container") ??
                                       rootInstance.Q<VisualElement>("stat_delta_box");

        if (deltaContainer == null) return;

        deltaContainer.Clear();

        // 1. Build Base Context Shared Setup (Base Stats + Class Multipliers + Djinns + Leader Passives)
        CharacterClassSO currentClass = currentHero.CurrentClass;
        HeroDjinnController djinnCtrl = currentHero.DjinnController;

        // Helper action to populate base parameters onto any context instance
        void PopulateBaseContextData(StatEvaluationContext context)
        {
            // A. Assign Base Stats
            context.SetBaseStat(StatType.MaxHP, currentHero.GetStat(StatType.MaxHP)?.BaseValue ?? 0);
            context.SetBaseStat(StatType.MaxPP, currentHero.GetStat(StatType.MaxPP)?.BaseValue ?? 0);
            context.SetBaseStat(StatType.Attack, currentHero.GetStat(StatType.Attack)?.BaseValue ?? 0);
            context.SetBaseStat(StatType.Defense, currentHero.GetStat(StatType.Defense)?.BaseValue ?? 0);
            context.SetBaseStat(StatType.Agility, currentHero.GetStat(StatType.Agility)?.BaseValue ?? 0);
            context.SetBaseStat(StatType.Luck, currentHero.GetStat(StatType.Luck)?.BaseValue ?? 0);

            // B. Assign Class Multipliers
            if (currentClass != null)
            {
                context.SetClassMultiplier(StatType.MaxHP, currentClass.GetStatMultiplier(StatType.MaxHP));
                context.SetClassMultiplier(StatType.MaxPP, currentClass.GetStatMultiplier(StatType.MaxPP));
                context.SetClassMultiplier(StatType.Attack, currentClass.GetStatMultiplier(StatType.Attack));
                context.SetClassMultiplier(StatType.Defense, currentClass.GetStatMultiplier(StatType.Defense));
                context.SetClassMultiplier(StatType.Agility, currentClass.GetStatMultiplier(StatType.Agility));
                context.SetClassMultiplier(StatType.Luck, currentClass.GetStatMultiplier(StatType.Luck));
            }

            // C. Assign Djinn Set Modifiers
            if (djinnCtrl != null && djinnCtrl.AssignedDjinns != null)
            {
                foreach (var djinn in djinnCtrl.AssignedDjinns)
                {
                    if (djinn != null && djinn.CurrentState == DjinnState.Set && djinn.DjinnData != null && djinn.DjinnData.SetStatModifiers != null)
                    {
                        foreach (var mod in djinn.DjinnData.SetStatModifiers)
                        {
                            if (mod == null) continue;
                            context.AddModifier(mod.statType, new StatModifier(mod.value, mod.modifierType, djinn));
                        }
                    }
                }

                // D. Assign Leader Passive Modifiers directly from active leader
                DjinnInstance leader = djinnCtrl.LeaderDjinn;
                if (leader != null && leader.CurrentState == DjinnState.Set && leader.DjinnData != null && leader.DjinnData.LeaderPassiveEffect != null)
                {
                    var leaderModifiers = leader.DjinnData.LeaderPassiveEffect.StatModifiers;
                    if (leaderModifiers != null)
                    {
                        foreach (var bonus in leaderModifiers)
                        {
                            if (bonus == null) continue;
                            context.AddModifier(bonus.statType, new StatModifier(bonus.value, bonus.modifierType));
                        }
                    }
                }
            }
        }

        // 2. Build CURRENT Context (with currently equipped gear)
        StatEvaluationContext currentContext = new StatEvaluationContext();
        PopulateBaseContextData(currentContext);

        if (currentEquipment.EquippedItems != null)
        {
            foreach (var item in currentEquipment.EquippedItems.Values)
            {
                if (item != null && item.StatModifiers != null)
                {
                    foreach (var mod in item.StatModifiers)
                    {
                        if (mod == null) continue;
                        currentContext.AddModifier(mod.statType, new StatModifier(mod.value, mod.modifierType, item));
                    }
                }
            }
        }

        // 3. Build HYPOTHETICAL Context (replacing item in target equipment slot)
        StatEvaluationContext hypotheticalContext = new StatEvaluationContext();
        PopulateBaseContextData(hypotheticalContext);

        if (currentEquipment.EquippedItems != null)
        {
            foreach (var kvp in currentEquipment.EquippedItems)
            {
                EquipmentSlot slot = kvp.Key;
                EquipableSO item = kvp.Value;

                // Exclude currently equipped item if it occupies the exact same slot as newEquipable
                if (slot == newEquipable.EquipmentSlot) continue;

                if (item != null && item.StatModifiers != null)
                {
                    foreach (var mod in item.StatModifiers)
                    {
                        if (mod == null) continue;
                        hypotheticalContext.AddModifier(mod.statType, new StatModifier(mod.value, mod.modifierType, item));
                    }
                }
            }
        }

        // Add target newEquipable modifiers to hypothetical context
        if (newEquipable.StatModifiers != null)
        {
            foreach (var mod in newEquipable.StatModifiers)
            {
                if (mod == null) continue;
                hypotheticalContext.AddModifier(mod.statType, new StatModifier(mod.value, mod.modifierType, newEquipable));
            }
        }

        // 4. Evaluate both contexts via StatCalculator engine
        StatBlock currentEvaluatedStats = StatCalculator.Evaluate(currentContext);
        StatBlock hypotheticalEvaluatedStats = StatCalculator.Evaluate(hypotheticalContext);

        // 5. Identify relevant stats modified by either current slot item or new item
        EquipableSO currentlyEquippedInSlot = currentEquipment.GetEquippedItem(newEquipable.EquipmentSlot);
        HashSet<StatType> relevantStats = new HashSet<StatType>();

        if (newEquipable.StatModifiers != null)
        {
            foreach (var mod in newEquipable.StatModifiers)
            {
                if (mod != null && mod.value != 0) relevantStats.Add(mod.statType);
            }
        }

        if (currentlyEquippedInSlot != null && currentlyEquippedInSlot.StatModifiers != null)
        {
            foreach (var mod in currentlyEquippedInSlot.StatModifiers)
            {
                if (mod != null && mod.value != 0) relevantStats.Add(mod.statType);
            }
        }

        // 6. Render UI Rows for Delta Comparisons
        foreach (StatType stat in relevantStats)
        {
            float currentStatVal = currentEvaluatedStats.Get(stat);
            float newStatVal = hypotheticalEvaluatedStats.Get(stat);
            float delta = newStatVal - currentStatVal;

            VisualElement row = new VisualElement();
            row.AddToClassList("item-comparison-row");

            Label nameLabel = new Label(GetStatDisplayName(stat));
            nameLabel.AddToClassList("stat-name");

            VisualElement deltaGroup = new VisualElement();
            deltaGroup.AddToClassList("item-delta-container");

            Label currentValLabel = new Label(Mathf.RoundToInt(currentStatVal).ToString());
            currentValLabel.AddToClassList("item-current-value");

            Label arrowLabel = new Label("→");
            arrowLabel.AddToClassList("arrow-separator");

            Label newValLabel = new Label(Mathf.RoundToInt(newStatVal).ToString());
            newValLabel.AddToClassList("item-new-value");

            Label deltaTagLabel = new Label($"({(delta >= 0 ? "+" : "")}{Mathf.RoundToInt(delta)})");
            deltaTagLabel.AddToClassList("item-delta-tag");

            if (delta > 0) deltaTagLabel.AddToClassList("delta-positive");
            else if (delta < 0) deltaTagLabel.AddToClassList("delta-negative");
            else deltaTagLabel.AddToClassList("delta-neutral");

            deltaGroup.Add(currentValLabel);
            deltaGroup.Add(arrowLabel);
            deltaGroup.Add(newValLabel);
            deltaGroup.Add(deltaTagLabel);

            row.Add(nameLabel);
            row.Add(deltaGroup);

            deltaContainer.Add(row);
        }
    }
    /// <summary>
    /// Friendly name resolver for StatType enums.
    /// </summary>
    private string GetStatDisplayName(StatType statType)
    {
        switch (statType)
        {
            case StatType.MaxHP: return "Puntos de Vida";
            case StatType.MaxPP: return "Puntos de Psinergia";
            case StatType.Attack: return "Ataque";
            case StatType.Defense: return "Defensa";
            case StatType.Agility: return "Agilidad";
            case StatType.Luck: return "Suerte";
            case StatType.PowerVenus: return "Poder Venus";
            case StatType.PowerMars: return "Poder Marte";
            case StatType.PowerJupiter: return "Poder Júpiter";
            case StatType.PowerMercury: return "Poder Mercurio";
            case StatType.ResistVenus: return "Resist. Venus";
            case StatType.ResistMars: return "Resist. Marte";
            case StatType.ResistJupiter: return "Resist. Júpiter";
            case StatType.ResistMercury: return "Resist. Mercurio";
            default: return statType.ToString();
        }
    }

    private void SelectGlobalBagSlot(int index)
    {
        if (GlobalBag.Instance == null) return;

        IReadOnlyList<InventorySlot> keySlots = GlobalBag.Instance.KeyItemSlots;
        if (index >= 0 && index < keySlots.Count)
        {
            InventorySlot slot = keySlots[index];
            if (slot != null && slot.Item != null)
            {
                UpdateInfoPanel(slot.Item);
            }
        }
    }

    public void ClearInfoPanel()
    {
        selectedItem = null;

        if (labelItemName != null) labelItemName.text = "Seleccionar Objeto";
        if (labelItemType != null) labelItemType.text = "---";
        if (labelItemSellPrice != null) labelItemSellPrice.text = "Se vende por: --- monedas";
        if (labelItemMaxStack != null) labelItemMaxStack.text = "Máx. stack: ---";
        if (labelItemDesc != null) labelItemDesc.text = "Selecciona un objeto del inventario o del equipamiento para ver sus detalles.";
        if (itemIconPreview != null) itemIconPreview.style.backgroundImage = null;

        if (extraInfoContainer != null)
        {
            extraInfoContainer.Clear();
        }
    }

    public void HandleConfirm()
    {
        Debug.Log("[PageItems] Confirm action triggered.");
    }

    #endregion
}