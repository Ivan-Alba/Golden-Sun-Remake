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
    public void AddKeyItem(KeyItemSO keyItem, int amount = 1)
    {
        if (keyItem == null || amount <= 0) return;

        int remaining = amount;

        // Step 1: Try stacking into existing non-full slots
        if (keyItem.MaxStack > 1)
        {
            foreach (var slot in keyItemSlots)
            {
                if (!slot.IsEmpty && slot.Item.ItemSlug == keyItem.ItemSlug && !slot.IsFull)
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
    public bool RemoveKeyItem(string itemSlug, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemSlug) || amount <= 0) return false;

        if (!HasKeyItem(itemSlug, amount)) return false;

        int remainingToRemove = amount;

        for (int i = keyItemSlots.Count - 1; i >= 0; i--)
        {
            if (!keyItemSlots[i].IsEmpty && keyItemSlots[i].Item.ItemSlug == itemSlug)
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
    public bool HasKeyItem(string itemSlug, int requiredAmount = 1)
    {
        if (string.IsNullOrEmpty(itemSlug)) return false;

        int totalCount = 0;
        foreach (var slot in keyItemSlots)
        {
            if (!slot.IsEmpty && slot.Item.ItemSlug == itemSlug)
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
    public int GetKeyItemQuantity(string itemSlug)
    {
        if (string.IsNullOrEmpty(itemSlug)) return 0;

        int total = 0;
        foreach (var slot in keyItemSlots)
        {
            if (!slot.IsEmpty && slot.Item.ItemSlug == itemSlug)
            {
                total += slot.Quantity;
            }
        }
        return total;
    }

    #endregion
}