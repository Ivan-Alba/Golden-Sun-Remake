using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the individual backpack inventory of a party hero.
/// Handles item stacking, capacity limits, slot transfers, drag-and-drop merging, and battle queries.
/// </summary>
public class HeroInventory : MonoBehaviour
{
    #region Serialized Fields

    [Header("Inventory Capacity Settings")]
    [SerializeField] private int maxBackpackSlots = 18;

    [Header("Current Backpack State")]
    [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();

    #endregion

    #region Events & Properties

    /// <summary>
    /// Fired whenever inventory structure or item quantities change to auto-refresh listening UI components.
    /// </summary>
    public event Action OnInventoryUpdated;

    public IReadOnlyList<InventorySlot> Slots => slots;
    public int MaxBackpackSlots => maxBackpackSlots;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeInventory();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes empty slots if inventory list is empty or smaller than maxBackpackSlots.
    /// </summary>
    public void InitializeInventory()
    {
        if (slots == null)
        {
            slots = new List<InventorySlot>();
        }

        while (slots.Count < MaxBackpackSlots)
        {
            slots.Add(new InventorySlot());
        }
    }

    #endregion

    #region Add & Remove Operations

    /// <summary>
    /// Attempts to add an item to the backpack. Auto-stacks with existing items first, then uses empty slots.
    /// Returns the remaining count if backpack becomes completely full.
    /// </summary>
    public int AddItem(ItemSO item, int amount = 1)
    {
        if (item == null || amount <= 0) return amount;

        int remaining = amount;

        // Step 1: Try stacking into existing non-full slots of the same item
        if (item.MaxStack > 1)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty && slots[i].Item.ItemSlug == item.ItemSlug && !slots[i].IsFull)
                {
                    remaining = slots[i].AddQuantity(remaining);
                    if (remaining <= 0) break;
                }
            }
        }

        // Step 2: Put remaining quantity into first empty slots
        if (remaining > 0)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty)
                {
                    int toAdd = Mathf.Min(remaining, item.MaxStack);
                    slots[i].SetSlot(item, toAdd);
                    remaining -= toAdd;

                    if (remaining <= 0) break;
                }
            }
        }

        OnInventoryUpdated?.Invoke();
        return remaining; // 0 if everything fit into backpack
    }

    /// <summary>
    /// Checks if there is enough space in backpack to hold a given item and quantity.
    /// </summary>
    public bool CanAddItem(ItemSO item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;

        int needed = amount;

        // Check space in stackable non-full slots
        if (item.MaxStack > 1)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty && slots[i].Item.ItemSlug == item.ItemSlug && !slots[i].IsFull)
                {
                    int spaceLeft = item.MaxStack - slots[i].Quantity;
                    needed -= spaceLeft;
                    if (needed <= 0) return true;
                }
            }
        }

        // Check empty slots
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty)
            {
                needed -= item.MaxStack;
                if (needed <= 0) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes a specified quantity from a specific slot index.
    /// </summary>
    public void RemoveFromSlot(int slotIndex, int amount = 1)
    {
        if (!IsValidIndex(slotIndex)) return;

        slots[slotIndex].RemoveQuantity(amount);
        OnInventoryUpdated?.Invoke();
    }

    /// <summary>
    /// Forces an item assignment directly into a specific slot index.
    /// Essential for precise save data restoration and deterministic slot placement.
    /// </summary>
    public void SetSlot(int slotIndex, ItemSO item, int amount = 1, bool isEquipped = false)
    {
        if (!IsValidIndex(slotIndex))
        {
            Debug.LogWarning($"[HeroInventory] Cannot set slot. Index {slotIndex} is out of range.", this);
            return;
        }

        slots[slotIndex].SetSlot(item, amount, isEquipped);
        OnInventoryUpdated?.Invoke();
    }

    #endregion

    #region Drag & Drop / Rearrange Operations

    /// <summary>
    /// Swaps or merges contents between two backpack slots. Perfect for UI Drag & Drop.
    /// </summary>
    public void SwapOrMergeSlots(int indexA, int indexB)
    {
        if (!IsValidIndex(indexA) || !IsValidIndex(indexB) || indexA == indexB) return;

        InventorySlot slotA = slots[indexA];
        InventorySlot slotB = slots[indexB];

        // Case 1: Merge identical stackable items
        if (!slotA.IsEmpty && !slotB.IsEmpty && slotA.Item.ItemSlug == slotB.Item.ItemSlug && slotA.Item.MaxStack > 1)
        {
            int remaining = slotB.AddQuantity(slotA.Quantity);
            if (remaining == 0)
            {
                slotA.ClearSlot();
            }
            else
            {
                slotA.SetSlot(slotA.Item, remaining);
            }
        }
        // Case 2: Swap slots completely
        else
        {
            ItemSO tempItem = slotA.Item;
            int tempQty = slotA.Quantity;
            bool tempEquipped = slotA.IsEquipped;

            slotA.SetSlot(slotB.Item, slotB.Quantity, slotB.IsEquipped);
            slotB.SetSlot(tempItem, tempQty, tempEquipped);
        }

        OnInventoryUpdated?.Invoke();
    }

    /// <summary>
    /// Transfers an item from a slot in this inventory to another hero's inventory.
    /// </summary>
    public bool TransferItemToHero(int sourceSlotIndex, HeroInventory targetHero)
    {
        if (!IsValidIndex(sourceSlotIndex) || targetHero == null || targetHero == this) return false;

        InventorySlot sourceSlot = slots[sourceSlotIndex];
        if (sourceSlot.IsEmpty || sourceSlot.IsEquipped) return false; // Cannot transfer equipped items directly

        ItemSO itemToTransfer = sourceSlot.Item;
        int qtyToTransfer = sourceSlot.Quantity;

        if (targetHero.CanAddItem(itemToTransfer, qtyToTransfer))
        {
            int remaining = targetHero.AddItem(itemToTransfer, qtyToTransfer);
            if (remaining == 0)
            {
                sourceSlot.ClearSlot();
            }
            else
            {
                sourceSlot.SetSlot(itemToTransfer, remaining);
            }

            OnInventoryUpdated?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Splits or moves a specific amount from one slot to another within the same inventory.
    /// </summary>
    public void SplitOrMoveSlotAmount(int sourceIndex, int targetIndex, int amount)
    {
        if (!IsValidIndex(sourceIndex) || !IsValidIndex(targetIndex) || sourceIndex == targetIndex || amount <= 0) return;

        InventorySlot sourceSlot = slots[sourceIndex];
        InventorySlot targetSlot = slots[targetIndex];

        if (sourceSlot.IsEmpty) return;

        int actualAmountToMove = Mathf.Min(amount, sourceSlot.Quantity);
        ItemSO itemToMove = sourceSlot.Item;

        // Target is empty: assign amount to target, subtract from source
        if (targetSlot.IsEmpty)
        {
            targetSlot.SetSlot(itemToMove, actualAmountToMove);
            sourceSlot.RemoveQuantity(actualAmountToMove);
        }
        // Target has identical item: stack into target, subtract from source
        else if (targetSlot.Item.ItemSlug == itemToMove.ItemSlug && targetSlot.Item.MaxStack > 1)
        {
            int remaining = targetSlot.AddQuantity(actualAmountToMove);
            int movedCount = actualAmountToMove - remaining;
            sourceSlot.RemoveQuantity(movedCount);
        }

        OnInventoryUpdated?.Invoke();
    }

    /// <summary>
    /// Transfers a specific amount of an item from a slot in this inventory to another hero's inventory.
    /// </summary>
    public bool TransferAmountToHero(int sourceSlotIndex, int amount, HeroInventory targetHero)
    {
        if (!IsValidIndex(sourceSlotIndex) || targetHero == null || targetHero == this || amount <= 0) return false;

        InventorySlot sourceSlot = slots[sourceSlotIndex];
        if (sourceSlot.IsEmpty || sourceSlot.IsEquipped) return false;

        ItemSO itemToTransfer = sourceSlot.Item;
        int amountToTransfer = Mathf.Min(amount, sourceSlot.Quantity);

        if (targetHero.CanAddItem(itemToTransfer, amountToTransfer))
        {
            int remaining = targetHero.AddItem(itemToTransfer, amountToTransfer);
            int movedCount = amountToTransfer - remaining;

            sourceSlot.RemoveQuantity(movedCount);

            OnInventoryUpdated?.Invoke();
            return true;
        }

        return false;
    }

    #endregion

    #region Query Helpers for UI & Battle

    /// <summary>
    /// Retrieves a slot by its index safely.
    /// </summary>
    public InventorySlot GetSlot(int index)
    {
        if (!IsValidIndex(index)) return null;
        return slots[index];
    }

    /// <summary>
    /// Finds the index of the first empty slot available. Returns -1 if full.
    /// </summary>
    public int GetFirstEmptySlotIndex()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty) return i;
        }
        return -1;
    }

    public bool HasUsableBattleItems()
    {
        if (Slots == null) return false;

        foreach (var slot in Slots)
        {
            if (slot?.Item == null) continue;

            if ((slot.Item is ConsumableSO consumable && consumable.UseInBattle) ||
                (slot.Item is EquipableSO equipable && equipable.UseInBattle))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < slots.Count;
    }

    #endregion
}