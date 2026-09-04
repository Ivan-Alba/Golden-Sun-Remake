using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages standard equipment slots and dedicated Psynergy Relic slots for a hero.
/// Applies stat modifiers to FighterStats and notifies system events upon equipment changes.
/// </summary>
public class HeroEquipment : MonoBehaviour
{
    #region Serialized Fields

    [Header("Relic Slot Configuration")]
    [SerializeField] private int maxRelicSlots = 4;

    [Header("Component References")]
    [SerializeField] private FighterStats fighterStats;
    [SerializeField] private HeroInventory heroInventory;

    #endregion

    #region Private Fields

    // Standard Equipment Slots Map
    private readonly Dictionary<EquipmentSlot, EquipableSO> equippedItems = new Dictionary<EquipmentSlot, EquipableSO>();

    // Dedicated Psynergy Relic Slots Array
    private PsynergyRelicSO[] equippedRelics;

    #endregion

    #region Properties

    public int MaxRelicSlots => maxRelicSlots;
    public IReadOnlyDictionary<EquipmentSlot, EquipableSO> EquippedItems => equippedItems;
    public IReadOnlyList<PsynergyRelicSO> EquippedRelics => equippedRelics;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (fighterStats == null) fighterStats = GetComponent<FighterStats>();
        if (heroInventory == null) heroInventory = GetComponent<HeroInventory>();

        InitializeEquipmentSlots();
    }

    #endregion

    #region Initialization

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
    /// Handles unequipping and swapping if the target slot is currently occupied.
    /// </summary>
    /// <param name="backpackSlotIndex">Target backpack slot index.</param>
    /// <param name="targetRelicIndex">Target relic array index if equipping a Psynergy Relic.</param>
    /// <returns>True if successfully equipped; false otherwise.</returns>
    public bool EquipFromBackpack(int backpackSlotIndex, int targetRelicIndex = 0)
    {
        if (heroInventory == null) return false;

        InventorySlot slot = heroInventory.GetSlot(backpackSlotIndex);
        if (slot == null || slot.IsEmpty) return false;

        ItemSO itemToEquip = slot.Item;

        // 1. Handle Psynergy Relics FIRST
        if (itemToEquip is PsynergyRelicSO relic)
        {
            return EquipRelicItem(relic, backpackSlotIndex, targetRelicIndex);
        }
        // 2. Handle Standard Equipment SECOND
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

        // 1. Remove stat bonuses of previously equipped item
        if (currentEquipped != null)
        {
            RemoveItemModifiers(currentEquipped);
        }

        // 2. Apply stat bonuses of the new item
        ApplyItemModifiers(newEquipable);

        // 3. Update equipped slot map
        equippedItems[slotType] = newEquipable;

        // 4. Update backpack slot
        if (currentEquipped != null)
        {
            heroInventory.GetSlot(backpackSlotIndex).SetSlot(currentEquipped, 1, false);
        }
        else
        {
            heroInventory.RemoveFromSlot(backpackSlotIndex, 1);
        }

        GameEvents.Hero.TriggerEquipmentChanged(gameObject);
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

        // 4. Update backpack slot
        if (currentRelic != null)
        {
            heroInventory.GetSlot(backpackSlotIndex).SetSlot(currentRelic, 1, false);
        }
        else
        {
            heroInventory.RemoveFromSlot(backpackSlotIndex, 1);
        }

        GameEvents.Hero.TriggerEquipmentChanged(gameObject);
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

        if (!heroInventory.CanAddItem(itemToUnequip, 1))
        {
            Debug.LogWarning("[HeroEquipment] Cannot unequip. Backpack is full!");
            return false;
        }

        RemoveItemModifiers(itemToUnequip);
        equippedItems[slotType] = null;
        heroInventory.AddItem(itemToUnequip, 1);

        GameEvents.Hero.TriggerEquipmentChanged(gameObject);
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

        if (!heroInventory.CanAddItem(relicToUnequip, 1))
        {
            Debug.LogWarning("[HeroEquipment] Cannot unequip relic. Backpack is full!");
            return false;
        }

        RemoveItemModifiers(relicToUnequip);
        equippedRelics[relicSlotIndex] = null;
        heroInventory.AddItem(relicToUnequip, 1);

        GameEvents.Hero.TriggerEquipmentChanged(gameObject);
        Debug.Log($"<color=yellow>[Equipment] Unequipped Relic {relicToUnequip.ItemName} from {gameObject.name}</color>");
        return true;
    }

    #endregion

    #region Modifier Helpers

    private void ApplyItemModifiers(ItemSO item)
    {
        if (fighterStats == null || item == null) return;

        if (item is EquipableSO equipable)
        {
            var modifiers = equipable.GetStatModifiers();
            int count = modifiers.Count;
            for (int i = 0; i < count; i++)
            {
                fighterStats.AddStatModifier(modifiers[i].statType, modifiers[i].modifier);
            }
        }
    }

    private void RemoveItemModifiers(ItemSO item)
    {
        if (fighterStats == null || item == null) return;

        fighterStats.RemoveModifiersFromSource(item);
    }

    #endregion

    #region Public Queries & Load Helpers

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
    /// Gets the item currently equipped in a weapon slot safely.
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
    /// Returns a list of all psynergies currently granted by equipped relics.
    /// </summary>
    public List<PsynergySO> GetEquippedRelicPsynergies()
    {
        List<PsynergySO> relicPsynergies = new List<PsynergySO>();
        if (equippedRelics == null) return relicPsynergies;

        for (int i = 0; i < equippedRelics.Length; i++)
        {
            if (equippedRelics[i] != null && equippedRelics[i].GrantedPsynergy != null)
            {
                relicPsynergies.Add(equippedRelics[i].GrantedPsynergy);
            }
        }
        return relicPsynergies;
    }

    /// <summary>
    /// Directly assigns an equipable item during save data loading, applying its modifiers.
    /// Allows silent assignment to avoid multiple event triggers during loading sequences.
    /// </summary>
    public void LoadEquippedItem(EquipableSO item, bool notifyEvent = false)
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

            if (notifyEvent)
            {
                GameEvents.Hero.TriggerEquipmentChanged(gameObject);
            }
        }
    }

    /// <summary>
    /// Directly assigns a relic item during save data loading, applying its modifiers.
    /// Allows silent assignment to avoid multiple event triggers during loading sequences.
    /// </summary>
    public void LoadEquippedRelic(PsynergyRelicSO relic, int slotIndex, bool notifyEvent = false)
    {
        if (relic == null || slotIndex < 0 || slotIndex >= maxRelicSlots) return;

        if (equippedRelics[slotIndex] != null)
        {
            RemoveItemModifiers(equippedRelics[slotIndex]);
        }

        equippedRelics[slotIndex] = relic;
        ApplyItemModifiers(relic);

        if (notifyEvent)
        {
            GameEvents.Hero.TriggerEquipmentChanged(gameObject);
        }
    }

    #endregion
}