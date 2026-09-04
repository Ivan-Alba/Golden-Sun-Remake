using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the shared global bag for key/quest items (e.g. Dungeon Keys, Elemental Stars, Quest Relics).
/// Fully decoupled via GameEvents.Inventory, zero-alloc loop iterations, and Save System ready.
/// </summary>
public class GlobalBag : MonoBehaviour
{
    #region Serialized Fields

    [Header("Global Key/Quest Items State")]
    [Tooltip("List of inventory slots holding key items shared by the entire party.")]
    [SerializeField] private List<InventorySlot> keyItemSlots = new List<InventorySlot>();

    #endregion

    #region Public Properties

    public IReadOnlyList<InventorySlot> KeyItemSlots => keyItemSlots;

    #endregion

    #region Unity Lifecycle & Event Subscriptions

    private void Awake()
    {
        if (keyItemSlots == null)
        {
            keyItemSlots = new List<InventorySlot>();
        }
    }

    private void OnEnable()
    {
        GameEvents.Inventory.OnRequestAddKeyItem += HandleRequestAddKeyItem;
        GameEvents.Inventory.OnRequestRemoveKeyItem += HandleRequestRemoveKeyItem;
        GameEvents.Inventory.OnCheckHasKeyItem += HandleCheckHasKeyItem;
        GameEvents.Inventory.OnRequestKeyItemQuantity += HandleRequestKeyItemQuantity;
        GameEvents.Inventory.OnRequestKeyItemSlots += HandleRequestKeyItemSlots;
        GameEvents.Inventory.OnClearAllInventoriesRequested += HandleClearAllInventories;
    }

    private void OnDisable()
    {
        GameEvents.Inventory.OnRequestAddKeyItem -= HandleRequestAddKeyItem;
        GameEvents.Inventory.OnRequestRemoveKeyItem -= HandleRequestRemoveKeyItem;
        GameEvents.Inventory.OnCheckHasKeyItem -= HandleCheckHasKeyItem;
        GameEvents.Inventory.OnRequestKeyItemQuantity -= HandleRequestKeyItemQuantity;
        GameEvents.Inventory.OnRequestKeyItemSlots -= HandleRequestKeyItemSlots;
        GameEvents.Inventory.OnClearAllInventoriesRequested -= HandleClearAllInventories;
    }

    #endregion

    #region Event Handlers

    private void HandleRequestAddKeyItem(ItemSO item, int amount, System.Action<int> onResult)
    {
        int remaining = AddKeyItem(item, amount);
        onResult?.Invoke(remaining);
    }

    private void HandleRequestRemoveKeyItem(ItemSO item, int amount, System.Action<bool> onResult)
    {
        bool success = RemoveKeyItem(item, amount);
        onResult?.Invoke(success);
    }

    private void HandleCheckHasKeyItem(ItemSO item, int amount, System.Action<bool> onResult)
    {
        bool hasItem = HasKeyItem(item, amount);
        onResult?.Invoke(hasItem);
    }

    private void HandleRequestKeyItemQuantity(ItemSO item, System.Action<int> onResult)
    {
        int quantity = GetKeyItemQuantity(item);
        onResult?.Invoke(quantity);
    }

    private void HandleRequestKeyItemSlots(System.Action<IReadOnlyList<InventorySlot>> onResult)
    {
        onResult?.Invoke(keyItemSlots);
    }

    private void HandleClearAllInventories()
    {
        keyItemSlots.Clear();
        GameEvents.Inventory.TriggerGlobalBagUpdated();
    }

    #endregion

    #region Add, Remove & Query Operations (Non-Alloc)

    /// <summary>
    /// Adds a KeyItem to the global bag using Non-Alloc index loops.
    /// Auto-stacks if MaxStack > 1 or creates a new slot entry.
    /// </summary>
    /// <returns>Remaining quantity that could not fit (0 if fully added).</returns>
    public int AddKeyItem(ItemSO item, int amount = 1)
    {
        if (item == null || item is not KeyItemSO keyItem || amount <= 0) return amount;

        int remaining = amount;

        // Step 1: Try stacking into existing non-full slots (Non-Alloc for loop)
        if (keyItem.MaxStack > 1)
        {
            for (int i = 0; i < keyItemSlots.Count; i++)
            {
                InventorySlot slot = keyItemSlots[i];
                if (slot != null && !slot.IsEmpty && slot.Item == keyItem && !slot.IsFull)
                {
                    remaining = slot.AddQuantity(remaining);
                    if (remaining <= 0) break;
                }
            }
        }

        // Step 2: Create new slot for remaining items (Unlimited capacity for Key Items)
        while (remaining > 0)
        {
            int toAdd = Mathf.Min(remaining, keyItem.MaxStack);
            keyItemSlots.Add(new InventorySlot(keyItem, toAdd));
            remaining -= toAdd;
        }

        GameEvents.Inventory.TriggerGlobalBagUpdated();
        return 0; // Key items have unlimited slot capacity
    }

    /// <summary>
    /// Removes a specific quantity of a KeyItem using Non-Alloc reverse index loops.
    /// Auto-cleans slots when quantity reaches 0.
    /// </summary>
    public bool RemoveKeyItem(ItemSO item, int amount = 1)
    {
        if (item == null || item is not KeyItemSO keyItem || amount <= 0) return false;

        if (!HasKeyItem(keyItem, amount)) return false;

        int remainingToRemove = amount;

        for (int i = keyItemSlots.Count - 1; i >= 0; i--)
        {
            InventorySlot slot = keyItemSlots[i];
            if (slot != null && !slot.IsEmpty && slot.Item == keyItem)
            {
                int currentQty = slot.Quantity;
                if (currentQty <= remainingToRemove)
                {
                    remainingToRemove -= currentQty;
                    keyItemSlots.RemoveAt(i);
                }
                else
                {
                    slot.RemoveQuantity(remainingToRemove);
                    remainingToRemove = 0;
                }

                if (remainingToRemove <= 0) break;
            }
        }

        GameEvents.Inventory.TriggerGlobalBagUpdated();
        return true;
    }

    /// <summary>
    /// Checks if the party possesses a specific key item and quantity using Non-Alloc loops.
    /// </summary>
    public bool HasKeyItem(ItemSO item, int requiredAmount = 1)
    {
        if (item == null || item is not KeyItemSO keyItem) return false;

        int totalCount = 0;
        for (int i = 0; i < keyItemSlots.Count; i++)
        {
            InventorySlot slot = keyItemSlots[i];
            if (slot != null && !slot.IsEmpty && slot.Item == keyItem)
            {
                totalCount += slot.Quantity;
                if (totalCount >= requiredAmount) return true;
            }
        }

        return totalCount >= requiredAmount;
    }

    /// <summary>
    /// Returns total quantity owned of a specific key item using Non-Alloc loops.
    /// </summary>
    public int GetKeyItemQuantity(ItemSO item)
    {
        if (item == null || item is not KeyItemSO keyItem) return 0;

        int total = 0;
        for (int i = 0; i < keyItemSlots.Count; i++)
        {
            InventorySlot slot = keyItemSlots[i];
            if (slot != null && !slot.IsEmpty && slot.Item == keyItem)
            {
                total += slot.Quantity;
            }
        }
        return total;
    }

    /// <summary>
    /// Retrieves a KeyItem slot by index safely. Returns null if index is out of bounds.
    /// </summary>
    public InventorySlot GetKeyItemSlot(int index)
    {
        if (index >= 0 && index < keyItemSlots.Count)
        {
            return keyItemSlots[index];
        }
        return null;
    }

    #endregion

    #region Save System & Persistence Preparation

    /// <summary>
    /// Gets the current state of key item slots for save system serialization.
    /// </summary>
    public List<InventorySlot> GetSaveData()
    {
        return keyItemSlots;
    }

    /// <summary>
    /// Restores the key item slots from saved game data.
    /// </summary>
    public void LoadSaveData(List<InventorySlot> savedSlots)
    {
        if (savedSlots == null) return;

        keyItemSlots = new List<InventorySlot>(savedSlots);
        GameEvents.Inventory.TriggerGlobalBagUpdated();
    }

    #endregion
}