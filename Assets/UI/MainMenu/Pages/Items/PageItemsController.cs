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
    private Label labelItemDesc;
    private VisualElement statChangesBox;

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
    private const float SLOT_SIZE = 70f;
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
        labelItemDesc = rootElement.Q<Label>("label_item_desc");
        statChangesBox = rootElement.Q<VisualElement>("stat_changes_box");

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
            Label quantityLabel = slotUI.Q<Label>("label_quantity");
            VisualElement placeholderElem = slotUI.Q<VisualElement>("slot_placeholder");
            VisualElement equippedBadge = slotUI.Q<VisualElement>("badge_equipped");

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
                    quantityLabel.style.display = (slot.Quantity > 1) ? DisplayStyle.Flex : DisplayStyle.None;
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

                if (quantityLabel != null)
                {
                    quantityLabel.text = string.Empty;
                    quantityLabel.style.display = DisplayStyle.None;
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
        slotUI.RegisterCallback<PointerDownEvent>(evt => OnSlotPointerDown(evt, sourceType, slotIndex, slotUI, item, quantity));
        slotUI.RegisterCallback<PointerMoveEvent>(OnSlotPointerMove);
        slotUI.RegisterCallback<PointerUpEvent>(OnSlotPointerUp);
        slotUI.RegisterCallback<PointerCaptureOutEvent>(OnSlotPointerCancel);
    }

    private void UnregisterPointerCallbacks(VisualElement slotUI)
    {
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

    #region Slot Highlighting & Drop Target Detection

    private void HighlightCompatibleSlots(ItemSO item, bool enable)
    {
        if (item is EquipableSO equipable)
        {
            EquipmentSlot slotType = equipable.EquipmentSlot;

            if (slotType == EquipmentSlot.PsynergyRelic)
            {
                for (int i = 0; i < relicSlots.Length; i++)
                {
                    SetSlotHighlight(relicSlots[i], enable);
                }
            }
            else
            {
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

            // Open Stack Splitter Modal if Quantity > 1 and dropping into empty or same stackable slot
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
            if (currentDragContext.Item is PsynergyRelicSO || (currentDragContext.Item is EquipableSO equipable && equipable.EquipmentSlot == EquipmentSlot.PsynergyRelic))
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

        // Transfer from Personal Inventory
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
        // Transfer equipped gear or relic directly to target hero's backpack
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
            sliderSplitAmount.value = maxQty; // Default to max quantity
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

        rootElement.Add(ghostElement);
        UpdateGhostPosition(startPosition);
    }

    private void UpdateGhostPosition(Vector2 currentPosition)
    {
        if (ghostElement == null || rootElement == null) return;

        Vector2 localPos = rootElement.WorldToLocal(currentPosition);

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

    #region Info Panel & Details Display

    private void UpdateInfoPanel(ItemSO item)
    {
        if (item == null)
        {
            ClearInfoPanel();
            return;
        }

        if (labelItemName != null) labelItemName.text = item.ItemName;
        if (labelItemType != null) labelItemType.text = item.ItemType.ToString();
        if (labelItemDesc != null) labelItemDesc.text = item.ItemDescription;

        if (itemIconPreview != null && item.Icon != null)
        {
            itemIconPreview.style.backgroundImage = new StyleBackground(item.Icon);
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
        if (labelItemName != null) labelItemName.text = "Seleccionar Objeto";
        if (labelItemType != null) labelItemType.text = "---";
        if (labelItemDesc != null) labelItemDesc.text = "Selecciona un objeto del inventario o del equipamiento para ver sus detalles.";
        if (itemIconPreview != null) itemIconPreview.style.backgroundImage = null;
    }

    public void HandleConfirm()
    {
        Debug.Log("[PageItems] Confirm action triggered.");
    }

    #endregion
}