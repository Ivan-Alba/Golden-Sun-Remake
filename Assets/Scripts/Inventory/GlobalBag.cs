using System;
using System.Collections.Generic;
using UnityEngine;

public class GlobalBag : MonoBehaviour
{
    public static GlobalBag Instance { get; private set; }

    [Header("Global Key/Quest Items State")]
    [SerializeField] private List<InventorySlot> keyItemSlots = new List<InventorySlot>();

    // Fired whenever key items are added, removed, or updated
    public event Action OnGlobalBagUpdated;

    #region Properties

    public IReadOnlyList<InventorySlot> KeyItemSlots => keyItemSlots;

    #endregion

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            if (keyItemSlots == null)
            {
                keyItemSlots = new List<InventorySlot>();
            }
        }
    }

    #region Add, Remove & Query Operations

    /// <summary>
    /// Adds a KeyItem/QuestItem to the global bag. Auto-stacks if MaxStack > 1 or creates a new entry.
    /// </summary>
    public void AddKeyItem(ItemSO item, int amount = 1)
    {
        if (item == null || item is not KeyItemSO keyItem || amount <= 0) return;

        int remaining = amount;

        // Step 1: Try stacking into existing non-full slots
        if (keyItem.MaxStack > 1)
        {
            foreach (var slot in keyItemSlots)
            {
                if (!slot.IsEmpty && slot.Item == keyItem && !slot.IsFull)
                {
                    remaining = slot.AddQuantity(remaining);
                    if (remaining <= 0) break;
                }
            }
        }

        // Step 2: Create new slot for remaining items (Unlimited capacity)
        while (remaining > 0)
        {
            int toAdd = Mathf.Min(remaining, keyItem.MaxStack);
            keyItemSlots.Add(new InventorySlot(keyItem, toAdd));
            remaining -= toAdd;
        }

        OnGlobalBagUpdated?.Invoke();
    }

    /// <summary>
    /// Removes a quantity of a specific KeyItem by slug. Auto-removes slot when quantity hits 0.
    /// </summary>
    public bool RemoveKeyItem(ItemSO item, int amount = 1)
    {
        if (item == null || item is not KeyItemSO keyItem || amount <= 0) return false;

        if (!HasKeyItem(keyItem, amount)) return false;

        int remainingToRemove = amount;

        for (int i = keyItemSlots.Count - 1; i >= 0; i--)
        {
            if (!keyItemSlots[i].IsEmpty && keyItemSlots[i].Item == keyItem)
            {
                int currentQty = keyItemSlots[i].Quantity;
                if (currentQty <= remainingToRemove)
                {
                    remainingToRemove -= currentQty;
                    keyItemSlots.RemoveAt(i);
                }
                else
                {
                    keyItemSlots[i].RemoveQuantity(remainingToRemove);
                    remainingToRemove = 0;
                }

                if (remainingToRemove <= 0) break;
            }
        }

        OnGlobalBagUpdated?.Invoke();
        return true;
    }

    /// <summary>
    /// Checks if the group possesses a specific key item and quantity.
    /// </summary>
    public bool HasKeyItem(ItemSO item, int requiredAmount = 1)
    {
        if (item == null || item is not KeyItemSO keyItem) return false;

        int totalCount = 0;
        foreach (var slot in keyItemSlots)
        {
            if (!slot.IsEmpty && slot.Item == keyItem)
            {
                totalCount += slot.Quantity;
                if (totalCount >= requiredAmount) return true;
            }
        }

        return totalCount >= requiredAmount;
    }

    /// <summary>
    /// Returns total quantity owned of a specific key item.
    /// </summary>
    public int GetKeyItemQuantity(ItemSO item)
    {
        if (item == null || item is not KeyItemSO keyItem) return 0;

        int total = 0;
        foreach (var slot in keyItemSlots)
        {
            if (!slot.IsEmpty && slot.Item == keyItem)
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
}