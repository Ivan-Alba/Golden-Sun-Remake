using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls item routing, backpack distribution, and global bag operations for the active party.
/// Listens exclusively to GameEvents.Inventory for decoupled transactional messaging.
/// </summary>
public class InventoryController : MonoBehaviour
{
    [Header("Active Party References")]
    [SerializeField] private List<HeroInventory> partyInventories = new List<HeroInventory>();

    #region Properties

    public IReadOnlyList<HeroInventory> PartyInventories => partyInventories;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        GameEvents.Inventory.OnRequestAddItem += HandleRequestAddItem;
        GameEvents.Inventory.OnSyncWithPartyRequested += SyncWithParty;
        GameEvents.Inventory.OnClearAllInventoriesRequested += ClearAllInventories;
        GameEvents.Inventory.OnCheckHasItem += HandleCheckHasItem;
        GameEvents.Inventory.OnAllItemsRequested += AllItems;
        GameEvents.Inventory.OnRequestRemoveItem += HandleRequestRemoveItem;
    }

    private void OnDisable()
    {
        GameEvents.Inventory.OnRequestAddItem -= HandleRequestAddItem;
        GameEvents.Inventory.OnSyncWithPartyRequested -= SyncWithParty;
        GameEvents.Inventory.OnClearAllInventoriesRequested -= ClearAllInventories;
        GameEvents.Inventory.OnCheckHasItem -= HandleCheckHasItem;
        GameEvents.Inventory.OnAllItemsRequested -= AllItems;
        GameEvents.Inventory.OnRequestRemoveItem -= HandleRequestRemoveItem;
    }

    #endregion

    #region Event Handlers

    private void HandleRequestAddItem(ItemSO itemData, int quantity, Action<int> onResult)
    {
        int remaining = AddItem(itemData, quantity);
        onResult?.Invoke(remaining);
    }

    private void HandleRequestRemoveItem(ItemSO itemData, int quantity, Action<bool> onResult)
    {
        bool success = RemoveItem(itemData, quantity);
        onResult?.Invoke(success);
    }

    private void HandleCheckHasItem(ItemSO itemData, int amount, Action<bool> onResult)
    {
        bool hasIt = HasItem(itemData, amount);
        onResult?.Invoke(hasIt);
    }

    #endregion

    #region Party Registration

    /// <summary>
    /// Rebuilds the active party inventory list based on the current active heroes in GameManager.
    /// </summary>
    private void SyncWithParty()
    {
        partyInventories.Clear();

        if (GameManager.Instance == null) return;

        foreach (GameObject heroObj in GameManager.Instance.GetParty())
        {
            if (heroObj != null && heroObj.TryGetComponent<HeroInventory>(out var heroInv))
            {
                RegisterHeroInventory(heroInv);
            }
        }
    }

    /// <summary>
    /// Registers a hero's inventory into the active party list.
    /// </summary>
    private void RegisterHeroInventory(HeroInventory heroInv)
    {
        if (heroInv != null && !partyInventories.Contains(heroInv))
        {
            partyInventories.Add(heroInv);
        }
    }

    /// <summary>
    /// Unregisters a hero inventory (e.g., when a hero leaves the active party).
    /// </summary>
    private void UnregisterHeroInventory(HeroInventory heroInv)
    {
        if (partyInventories.Contains(heroInv))
        {
            partyInventories.Remove(heroInv);
        }
    }

    #endregion

    #region Smart Item Routing (AddItem & RemoveItem)

    /// <summary>
    /// Adds an item by slug. Automatically targets active hero first (or specific targetHeroIndex if provided).
    /// </summary>
    private int AddItem(string itemSlug, int quantity = 1, int targetHeroIndex = -1)
    {
        ItemSO itemToAdd = ItemDatabase.Instance.GetItem(itemSlug);
        if (itemToAdd == null)
        {
            Debug.LogWarning($"[InventoryController] Cannot add item. Slug '{itemSlug}' not found.");
            return quantity;
        }

        return AddItem(itemToAdd, quantity, targetHeroIndex);
    }

    /// <summary>
    /// Adds an ItemSO instance. Routes KeyItems to GlobalBag, or places in active hero's backpack (overflowing to party).
    /// </summary>
    private int AddItem(ItemSO item, int quantity = 1, int targetHeroIndex = -1)
    {
        if (item == null || quantity <= 0) return quantity;

        // 1. Route KeyItems / QuestItems to the Infinite Global Bag
        if (item is KeyItemSO keyItem)
        {
            GlobalBag.Instance.AddKeyItem(keyItem, quantity);
            Debug.Log($"<color=cyan>[GlobalBag] Added {quantity}x {keyItem.ItemName}</color>");
            return 0; // Always successful
        }

        // 2. Route standard items to Hero Backpacks
        if (partyInventories == null || partyInventories.Count == 0)
        {
            Debug.LogError("[InventoryController] No party inventories registered!");
            return quantity;
        }

        // Determine starting hero index (active hero by default if targetHeroIndex is -1)
        int startingIndex = targetHeroIndex;
        if (startingIndex < 0 || startingIndex >= partyInventories.Count)
        {
            startingIndex = GetActiveHeroInventoryIndex();
        }

        int remaining = quantity;

        // Step 2a: Try putting in starting hero's inventory first
        HeroInventory primaryHero = partyInventories[startingIndex];
        remaining = primaryHero.AddItem(item, remaining);

        // Step 2b: If primary hero was full, attempt to route remaining quantity to other heroes
        if (remaining > 0)
        {
            for (int i = 0; i < partyInventories.Count; i++)
            {
                if (i == startingIndex) continue; // Skip primary, already tried

                remaining = partyInventories[i].AddItem(item, remaining);
                if (remaining <= 0) break;
            }
        }

        if (remaining == 0)
        {
            Debug.Log($"<color=green>[Inventory] Added {quantity}x {item.ItemName} (Started at {primaryHero.gameObject.name}).</color>");
        }
        else
        {
            Debug.LogWarning($"[Inventory] Backpacks full! Could not fit {remaining}x {item.ItemName}.");
        }

        return remaining;
    }

    /// <summary>
    /// Helper method to find the list index of the currently active hero's inventory.
    /// </summary>
    private int GetActiveHeroInventoryIndex()
    {
        if (GameManager.Instance != null && GameManager.Instance.ActiveHero != null)
        {
            if (GameManager.Instance.ActiveHero.TryGetComponent<HeroInventory>(out var activeInv))
            {
                int index = partyInventories.IndexOf(activeInv);
                if (index != -1) return index;
            }
        }
        return 0; // Default to first hero if active hero is not found
    }

    /// <summary>
    /// Removes a quantity of a specific item from the party (checks GlobalBag first, then hero backpacks).
    /// </summary>
    private bool RemoveItem(ItemSO itemData, int quantity = 1)
    {
        // KeyItem removal
        if (itemData is KeyItemSO)
        {
            return GlobalBag.Instance.RemoveKeyItem(itemData, quantity);
        }

        // Backpack removal
        int remainingToRemove = quantity;

        foreach (var heroInv in partyInventories)
        {
            for (int i = 0; i < heroInv.Slots.Count; i++)
            {
                var slot = heroInv.Slots[i];
                if (!slot.IsEmpty && slot.Item == itemData)
                {
                    int qtyInSlot = slot.Quantity;
                    if (qtyInSlot <= remainingToRemove)
                    {
                        remainingToRemove -= qtyInSlot;
                        heroInv.RemoveFromSlot(i, qtyInSlot);
                    }
                    else
                    {
                        heroInv.RemoveFromSlot(i, remainingToRemove);
                        remainingToRemove = 0;
                    }

                    if (remainingToRemove <= 0) break;
                }
            }

            if (remainingToRemove <= 0) break;
        }

        return remainingToRemove <= 0;
    }

    #endregion

    #region Queries & Debug Helpers

    /// <summary>
    /// Checks if any party member (or GlobalBag) possesses a given item slug.
    /// </summary>
    private bool HasItem(ItemSO itemData, int amount = 1)
    {
        if (GlobalBag.Instance != null && GlobalBag.Instance.HasKeyItem(itemData, amount))
        {
            return true;
        }

        int totalFound = 0;
        foreach (var heroInv in partyInventories)
        {
            foreach (var slot in heroInv.Slots)
            {
                if (!slot.IsEmpty && slot.Item == itemData)
                {
                    totalFound += slot.Quantity;
                    if (totalFound >= amount) return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Debug utility: Adds 1 instance of every item in ItemDatabase to the party.
    /// </summary>
    private void AllItems()
    {
        if (ItemDatabase.Instance == null) return;

        foreach (ItemSO item in ItemDatabase.Instance.GetAllItems())
        {
            AddItem(item, 1);
        }
    }

    /// <summary>
    /// Clears all hero inventories and global bag.
    /// </summary>
    private void ClearAllInventories()
    {
        foreach (var heroInv in partyInventories)
        {
            if (heroInv != null)
            {
                for (int i = 0; i < heroInv.Slots.Count; i++)
                {
                    heroInv.RemoveFromSlot(i, heroInv.Slots[i].Quantity);
                }
            }
        }
    }

    #endregion
}