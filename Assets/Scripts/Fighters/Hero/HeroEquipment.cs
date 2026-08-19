using System;
using System.Collections.Generic;
using UnityEngine;

public class HeroEquipment : MonoBehaviour
{
    [Header("Relic Slot Configuration")]
    [SerializeField] private int maxRelicSlots = 4;

    [Header("Component References")]
    [SerializeField] private FighterStats fighterStats;
    [SerializeField] private HeroInventory heroInventory;

    // Standard Equipment Slots Map
    private Dictionary<EquipmentSlot, EquipableSO> equippedItems = new Dictionary<EquipmentSlot, EquipableSO>();

    // Dedicated Psynergy Relic Slots Array
    private PsynergyRelicSO[] equippedRelics;

    // Event fired whenever equipment changes, allowing UI auto-refresh and psynergy re-evaluation
    public event Action OnEquipmentChanged;

    #region Properties

    public int MaxRelicSlots => maxRelicSlots;
    public IReadOnlyDictionary<EquipmentSlot, EquipableSO> EquippedItems => equippedItems;
    public IReadOnlyList<PsynergyRelicSO> EquippedRelics => equippedRelics;

    #endregion

    #region Unity Lifecycle Methods

    private void Awake()
    {
        if (fighterStats == null) fighterStats = GetComponent<FighterStats>();
        if (heroInventory == null) heroInventory = GetComponent<HeroInventory>();

        InitializeEquipmentSlots();
    }

    private void InitializeEquipmentSlots()
    {
        equippedItems.Clear();

        // Initialize standard slots
        equippedItems.Add(EquipmentSlot.Weapon, null);
        equippedItems.Add(EquipmentSlot.Shield, null);
        equippedItems.Add(EquipmentSlot.Helmet, null);
        equippedItems.Add(EquipmentSlot.Armor, null);
        equippedItems.Add(EquipmentSlot.Hands, null);
        equippedItems.Add(EquipmentSlot.Boots, null);
        equippedItems.Add(EquipmentSlot.Accessory, null);

        // Initialize dedicated relic slots
        equippedRelics = new PsynergyRelicSO[maxRelicSlots];
    }

    #endregion

    #region Equip Operations

    /// <summary>
    /// Attempts to equip an item directly from a HeroInventory backpack slot.
    /// Handles unequipping/swapping if the target slot is currently occupied.
    /// </summary>
    public bool EquipFromBackpack(int backpackSlotIndex, int targetRelicIndex = 0)
    {
        if (heroInventory == null) return false;

        InventorySlot slot = heroInventory.GetSlot(backpackSlotIndex);
        if (slot == null || slot.IsEmpty) return false;

        ItemSO itemToEquip = slot.Item;

        // 1. Handle Psynergy Relics FIRST (more specific derived type)
        if (itemToEquip is PsynergyRelicSO relic)
        {
            return EquipRelicItem(relic, backpackSlotIndex, targetRelicIndex);
        }
        // 2. Handle Standard Equipment SECOND (base type)
        else if (itemToEquip is EquipableSO equipable)
        {
            return EquipStandardItem(equipable, backpackSlotIndex);
        }

        Debug.LogWarning($"[HeroEquipment] Item '{itemToEquip.ItemName}' is neither an Equipable nor a PsynergyRelic.");
        return false;
    }

    private bool EquipStandardItem(EquipableSO newEquipable, int backpackSlotIndex)
    {
        EquipmentSlot slotType = newEquipable.EquipmentSlot;

        if (slotType == EquipmentSlot.None || slotType == EquipmentSlot.PsynergyRelic)
        {
            Debug.LogWarning($"[HeroEquipment] Invalid slot type '{slotType}' for standard equipable.");
            return false;
        }

        EquipableSO currentEquipped = equippedItems[slotType];

        // 1. Remove stat bonuses of previously equipped item (if any)
        if (currentEquipped != null)
        {
            RemoveItemModifiers(currentEquipped);
        }

        // 2. Apply stat bonuses of the new item
        ApplyItemModifiers(newEquipable);

        // 3. Update equipped slot map
        equippedItems[slotType] = newEquipable;

        // 4. Update backpack: Replace newly equipped item with old item (or clear slot if none)
        if (currentEquipped != null)
        {
            heroInventory.GetSlot(backpackSlotIndex).SetSlot(currentEquipped, 1, false);
        }
        else
        {
            heroInventory.RemoveFromSlot(backpackSlotIndex, 1);
        }

        OnEquipmentChanged?.Invoke();
        Debug.Log($"<color=green>[Equipment] Equipped {newEquipable.ItemName} on {gameObject.name}</color>");
        return true;
    }

    private bool EquipRelicItem(PsynergyRelicSO newRelic, int backpackSlotIndex, int relicSlotIndex)
    {
        if (relicSlotIndex < 0 || relicSlotIndex >= maxRelicSlots)
        {
            relicSlotIndex = 0;
        }

        PsynergyRelicSO currentRelic = equippedRelics[relicSlotIndex];

        // 1. Remove modifiers of previously equipped relic
        if (currentRelic != null)
        {
            RemoveItemModifiers(currentRelic);
        }

        // 2. Apply modifiers of new relic
        ApplyItemModifiers(newRelic);

        // 3. Assign new relic to dedicated array
        equippedRelics[relicSlotIndex] = newRelic;

        // 4. Update backpack: Replace or clear
        if (currentRelic != null)
        {
            heroInventory.GetSlot(backpackSlotIndex).SetSlot(currentRelic, 1, false);
        }
        else
        {
            heroInventory.RemoveFromSlot(backpackSlotIndex, 1);
        }

        OnEquipmentChanged?.Invoke();
        Debug.Log($"<color=cyan>[Equipment] Equipped Relic {newRelic.ItemName} in slot {relicSlotIndex} on {gameObject.name}</color>");
        return true;
    }

    #endregion

    #region Unequip Operations

    /// <summary>
    /// Unequips a standard equipment slot and returns the item to the backpack.
    /// </summary>
    public bool UnequipStandardSlot(EquipmentSlot slotType)
    {
        if (!equippedItems.ContainsKey(slotType) || equippedItems[slotType] == null) return false;

        EquipableSO itemToUnequip = equippedItems[slotType];

        // Check if backpack has space
        if (!heroInventory.CanAddItem(itemToUnequip, 1))
        {
            Debug.LogWarning("[HeroEquipment] Cannot unequip. Backpack is full!");
            return false;
        }

        // 1. Remove modifiers
        RemoveItemModifiers(itemToUnequip);

        // 2. Clear slot
        equippedItems[slotType] = null;

        // 3. Return item to backpack
        heroInventory.AddItem(itemToUnequip, 1);

        OnEquipmentChanged?.Invoke();
        Debug.Log($"<color=yellow>[Equipment] Unequipped {itemToUnequip.ItemName} from {gameObject.name}</color>");
        return true;
    }

    /// <summary>
    /// Unequips a dedicated Psynergy Relic slot by index and returns it to the backpack.
    /// </summary>
    public bool UnequipRelicSlot(int relicSlotIndex)
    {
        if (relicSlotIndex < 0 || relicSlotIndex >= maxRelicSlots) return false;

        PsynergyRelicSO relicToUnequip = equippedRelics[relicSlotIndex];
        if (relicToUnequip == null) return false;

        // Check if backpack has space
        if (!heroInventory.CanAddItem(relicToUnequip, 1))
        {
            Debug.LogWarning("[HeroEquipment] Cannot unequip relic. Backpack is full!");
            return false;
        }

        // 1. Remove modifiers
        RemoveItemModifiers(relicToUnequip);

        // 2. Clear slot
        equippedRelics[relicSlotIndex] = null;

        // 3. Return relic to backpack
        heroInventory.AddItem(relicToUnequip, 1);

        OnEquipmentChanged?.Invoke();
        Debug.Log($"<color=yellow>[Equipment] Unequipped Relic {relicToUnequip.ItemName} from {gameObject.name}</color>");
        return true;
    }

    #endregion

    #region Modifier Helpers

    private void ApplyItemModifiers(ItemSO item)
    {
        if (fighterStats == null || item == null) return;

        if (item is PsynergyRelicSO relic)
        {
            foreach (var bonus in relic.StatModifiers)
            {
                StatModifier modifier = new StatModifier(
                    value: bonus.value,
                    type: bonus.modifierType,
                    source: relic,
                    isTemporary: false
                );

                fighterStats.AddStatModifier(bonus.statType, modifier);
            }
        }
        else if (item is EquipableSO equipable)
        {
            foreach (var bonus in equipable.StatModifiers)
            {
                StatModifier modifier = new StatModifier(
                    value: bonus.value,
                    type: bonus.modifierType,
                    source: equipable,
                    isTemporary: false
                );

                fighterStats.AddStatModifier(bonus.statType, modifier);
            }
        }
    }

    private void RemoveItemModifiers(ItemSO item)
    {
        if (fighterStats == null || item == null) return;

        // Remove stat modifiers associated with this ScriptableObject instance as source
        fighterStats.RemoveModifiersFromSource(item);
    }

    #endregion

    #region Public Queries & Loading

    /// <summary>
    /// Gets the item currently equipped in a standard equipment slot.
    /// </summary>
    public EquipableSO GetEquippedItem(EquipmentSlot slotType)
    {
        if (equippedItems.TryGetValue(slotType, out var item))
        {
            return item;
        }
        return null;
    }

    /// <summary>
    /// Gets the item currently equipped in a weapon equipment slot safely.
    /// Returns null if the slot is empty or if the item is not a WeaponSO.
    /// </summary>
    public WeaponSO GetEquippedWeapon()
    {
        if (equippedItems.TryGetValue(EquipmentSlot.Weapon, out var item))
        {
            return item as WeaponSO;
        }
        return null;
    }

    /// <summary>
    /// Gets the relic currently equipped in a specific relic slot index.
    /// </summary>
    public PsynergyRelicSO GetEquippedRelic(int relicSlotIndex)
    {
        if (relicSlotIndex >= 0 && relicSlotIndex < maxRelicSlots)
        {
            return equippedRelics[relicSlotIndex];
        }
        return null;
    }

    /// <summary>
    /// Directly assigns an equipable item during save data loading, applying its modifiers.
    /// </summary>
    public void LoadEquippedItem(EquipableSO item)
    {
        if (item == null) return;

        EquipmentSlot slotType = item.EquipmentSlot;
        if (equippedItems.ContainsKey(slotType))
        {
            if (equippedItems[slotType] != null)
            {
                RemoveItemModifiers(equippedItems[slotType]);
            }

            equippedItems[slotType] = item;
            ApplyItemModifiers(item);
            OnEquipmentChanged?.Invoke();
        }
    }

    /// <summary>
    /// Directly assigns a relic item during save data loading, applying its modifiers.
    /// </summary>
    public void LoadEquippedRelic(PsynergyRelicSO relic, int slotIndex)
    {
        if (relic == null || slotIndex < 0 || slotIndex >= maxRelicSlots) return;

        if (equippedRelics[slotIndex] != null)
        {
            RemoveItemModifiers(equippedRelics[slotIndex]);
        }

        equippedRelics[slotIndex] = relic;
        ApplyItemModifiers(relic);
        OnEquipmentChanged?.Invoke();
    }

    #endregion
}