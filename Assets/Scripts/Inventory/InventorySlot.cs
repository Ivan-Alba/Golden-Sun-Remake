using System;
using UnityEngine;

[Serializable]
public class InventorySlot
{
    [SerializeField] private ItemSO item;
    [SerializeField] private int quantity;
    [SerializeField] private bool isEquipped;

    #region Constructors

    public InventorySlot()
    {
        ClearSlot();
    }

    public InventorySlot(ItemSO item, int quantity = 1, bool isEquipped = false)
    {
        this.item = item;
        this.quantity = quantity;
        this.isEquipped = isEquipped;
    }

    #endregion

    #region Properties

    public ItemSO Item => item;
    public int Quantity => quantity;
    public bool IsEquipped => isEquipped;

    /// <summary>
    /// Returns true if this slot is completely empty.
    /// </summary>
    public bool IsEmpty => item == null || quantity <= 0;

    /// <summary>
    /// Returns true if the slot can accept more units of the assigned item.
    /// </summary>
    public bool IsFull => !IsEmpty && quantity >= item.MaxStack;

    #endregion

    #region Public Slot Operations

    /// <summary>
    /// Sets the item and quantity for this slot.
    /// </summary>
    public void SetSlot(ItemSO newItem, int newQuantity, bool equippedStatus = false)
    {
        item = newItem;
        quantity = Mathf.Clamp(newQuantity, 0, newItem != null ? newItem.MaxStack : 0);
        isEquipped = equippedStatus;

        if (quantity == 0)
        {
            ClearSlot();
        }
    }

    /// <summary>
    /// Adds quantity to this slot. Returns the overflow amount if max stack limit is exceeded.
    /// </summary>
    public int AddQuantity(int amount)
    {
        if (IsEmpty || item == null) return amount;

        int spaceLeft = item.MaxStack - quantity;
        if (amount <= spaceLeft)
        {
            quantity += amount;
            return 0; // All added successfully
        }
        else
        {
            quantity = item.MaxStack;
            return amount - spaceLeft; // Return remaining overflow
        }
    }

    /// <summary>
    /// Removes quantity from this slot. Automatically clears slot if quantity reaches 0.
    /// </summary>
    public void RemoveQuantity(int amount)
    {
        if (IsEmpty) return;

        quantity -= amount;
        if (quantity <= 0)
        {
            ClearSlot();
        }
    }

    /// <summary>
    /// Toggles or sets the equipped status of this item.
    /// </summary>
    public void SetEquipped(bool equipped)
    {
        isEquipped = equipped;
    }

    /// <summary>
    /// Resets this slot to an empty state.
    /// </summary>
    public void ClearSlot()
    {
        item = null;
        quantity = 0;
        isEquipped = false;
    }

    #endregion
}