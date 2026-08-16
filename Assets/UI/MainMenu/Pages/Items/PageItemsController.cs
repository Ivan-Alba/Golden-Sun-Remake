using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

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

    // Fixed Equipment Slots (7 Slots)
    private VisualElement[] gearSlots = new VisualElement[7];

    // Fixed Psynergy Relic Slots (4 Slots)
    private VisualElement[] relicSlots = new VisualElement[4];

    // Inventory Slots Cache
    private List<VisualElement> spawnedInventorySlots = new List<VisualElement>();
    private List<VisualElement> spawnedGlobalBagSlots = new List<VisualElement>();

    // Current Focused Data Sources
    private HeroStats currentHero;
    private HeroInventory currentInventory;
    private HeroEquipment currentEquipment;

    // Slot Dimension Constants for Layout Calculations
    private const float SLOT_SIZE = 70f;
    private const float SLOT_MARGIN_TOTAL = 6f; // 3px left + 3px right (matches .slot-box margin: 3px)

    #endregion

    #region Constructor

    public PageItemsController(VisualElement root)
    {
        rootElement = root;
        BindUIElements();
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

        // Bind Fixed Equipment Slots
        gearSlots[0] = rootElement.Q<VisualElement>("slot_weapon");
        gearSlots[1] = rootElement.Q<VisualElement>("slot_shield");
        gearSlots[2] = rootElement.Q<VisualElement>("slot_armor");
        gearSlots[3] = rootElement.Q<VisualElement>("slot_helm");
        gearSlots[4] = rootElement.Q<VisualElement>("slot_hands");
        gearSlots[5] = rootElement.Q<VisualElement>("slot_boots");
        gearSlots[6] = rootElement.Q<VisualElement>("slot_acc");

        // Bind Fixed Relic Slots (Updated to 4 Relics)
        relicSlots[0] = rootElement.Q<VisualElement>("slot_relic_0");
        relicSlots[1] = rootElement.Q<VisualElement>("slot_relic_1");
        relicSlots[2] = rootElement.Q<VisualElement>("slot_relic_2");
        relicSlots[3] = rootElement.Q<VisualElement>("slot_relic_3");
    }

    #endregion

    #region Data Population & Initialization

    /// <summary>
    /// Populates inventory grids and equipment slots with data from the focused hero, inventory, equipment, and global bag.
    /// </summary>
    /// <param name="hero">Hero stats data source.</param>
    /// <param name="inventory">Hero inventory data source.</param>
    /// <param name="equipment">Hero equipment data source.</param>
    /// <param name="itemSlotAsset">UXML Template asset for individual inventory slots.</param>
    public void PopulateData(HeroStats hero, HeroInventory inventory, HeroEquipment equipment, VisualTreeAsset itemSlotAsset)
    {
        currentHero = hero;
        currentInventory = inventory;
        currentEquipment = equipment;

        UpdateHeaderHeroInfo();

        // Fetch inventory capacity from HeroInventory or fallback to 18 slots (3 rows x 6 columns)
        int personalCapacity = (currentInventory != null) ? currentInventory.MaxBackpackSlots : 18;
        int columns = Mathf.CeilToInt(personalCapacity / 3.0f);

        // Global Bag capacity = 2 rows matching the same column width
        int globalCapacity = columns * 2;

        // Instantiate slot elements
        PopulateInventoryGrid(personalCapacity, columns, itemSlotAsset);
        PopulateGlobalBagGrid(globalCapacity, columns, itemSlotAsset);
        BindEquipmentCallbacks();

        // Load item data into all slots
        RefreshAllData();

        // Default info panel display
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

        // Constrain grid width so flexbox automatically wraps into exactly 3 rows
        float calculatedWidth = columns * (SLOT_SIZE + SLOT_MARGIN_TOTAL);
        inventoryGrid.style.maxWidth = calculatedWidth;

        for (int i = 0; i < capacity; i++)
        {
            TemplateContainer slotInstance = itemSlotAsset.Instantiate();
            VisualElement slotRoot = slotInstance.Q<VisualElement>("slot_root") ?? slotInstance;

            if (slotRoot != null)
            {
                int slotIndex = i;
                slotRoot.pickingMode = PickingMode.Position;
                slotRoot.RegisterCallback<ClickEvent>(evt => SelectInventorySlot(slotIndex));

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

        // Match the same maximum width as the main personal inventory grid
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

    private void BindEquipmentCallbacks()
    {
        // Register callbacks on fixed gear slots
        for (int i = 0; i < gearSlots.Length; i++)
        {
            int index = i;
            if (gearSlots[i] != null)
            {
                gearSlots[i].RegisterCallback<ClickEvent>(evt => SelectGearSlot(index));
            }
        }

        // Register callbacks on fixed relic slots
        for (int i = 0; i < relicSlots.Length; i++)
        {
            int index = i;
            if (relicSlots[i] != null)
            {
                relicSlots[i].RegisterCallback<ClickEvent>(evt => SelectRelicSlot(index));
            }
        }
    }

    #endregion

    #region Data Refresh & Sync

    /// <summary>
    /// Refreshes all UI items across personal inventory, equipment, relics, and global bag.
    /// </summary>
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
            VisualElement slotUI = spawnedInventorySlots[i];
            if (slotUI == null) continue;

            // Query internal slot elements
            VisualElement iconElem = slotUI.Q<VisualElement>("item_icon");
            Label quantityLabel = slotUI.Q<Label>("label_quantity");
            VisualElement placeholderElem = slotUI.Q<VisualElement>("slot_placeholder");
            VisualElement equippedBadge = slotUI.Q<VisualElement>("badge_equipped"); // Optional visual badge for equipped status

            // Safely fetch inventory slot by index
            InventorySlot slot = currentInventory.GetSlot(i);

            if (slot != null && !slot.IsEmpty && slot.Item != null)
            {
                // Set item icon
                if (iconElem != null)
                {
                    // Adjust property name if ItemSO uses Icon, ItemIcon, or Sprite
                    iconElem.style.backgroundImage = new StyleBackground(slot.Item.Icon);
                    iconElem.style.display = DisplayStyle.Flex;
                }

                // Set quantity label (only visible for stackable items or quantities > 1)
                if (quantityLabel != null)
                {
                    quantityLabel.text = (slot.Item.MaxStack > 1 && slot.Quantity > 1) ? slot.Quantity.ToString() : string.Empty;
                    quantityLabel.style.display = (slot.Quantity > 1) ? DisplayStyle.Flex : DisplayStyle.None;
                }

                // Hide empty slot placeholder background if present
                if (placeholderElem != null)
                {
                    placeholderElem.style.display = DisplayStyle.None;
                }

                // Optional: Toggle equipped badge status
                if (equippedBadge != null)
                {
                    equippedBadge.style.display = slot.IsEquipped ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
            else
            {
                // Clear slot UI if empty
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

                if (placeholderElem != null)
                {
                    placeholderElem.style.display = DisplayStyle.Flex;
                }

                if (equippedBadge != null)
                {
                    equippedBadge.style.display = DisplayStyle.None;
                }
            }
        }
    }

    private void RefreshEquipmentSlots()
    {
        if (currentEquipment == null) return;

        // TODO: Populate gearSlots[0..6] with currentEquipment equipped weapons, armor, etc.
        // TODO: Populate relicSlots[0..3] with currentEquipment equipped psynergy relics.
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

            // Safely retrieve slot if within current keyItemSlots count
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

                if (placeholderElem != null)
                {
                    placeholderElem.style.display = DisplayStyle.None;
                }
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

                if (placeholderElem != null)
                {
                    placeholderElem.style.display = DisplayStyle.Flex;
                }
            }
        }
    }

    #endregion

    #region Selection & Interaction Handlers

    private void SelectInventorySlot(int index)
    {
        Debug.Log($"[PageItems] Selected Personal Inventory Slot {index}");
        // TODO: Update Info Panel with Item Details
    }

    private void SelectGlobalBagSlot(int index)
    {
        Debug.Log($"[PageItems] Selected Global Bag Key Item Slot {index}");
        // TODO: Update Info Panel with Key Item Details
    }

    private void SelectGearSlot(int index)
    {
        Debug.Log($"[PageItems] Selected Equipment Slot {index}");
        // TODO: Update Info Panel with Gear Details and Stat Comparisons
    }

    private void SelectRelicSlot(int index)
    {
        Debug.Log($"[PageItems] Selected Psynergy Relic Slot {index}");
        // TODO: Update Info Panel with Relic Details
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