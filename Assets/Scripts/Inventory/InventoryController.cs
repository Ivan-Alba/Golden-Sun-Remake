using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls item routing, backpack distribution, and global bag operations for the active party.
/// Listens exclusively to GameEvents (Inventory & Party) for decoupled transactional messaging with zero GC allocations.
/// </summary>
public class InventoryController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Active Party References")]
    [Tooltip("List of registered hero inventories corresponding to the active party members.")]
    [SerializeField] private List<HeroInventory> partyInventories = new List<HeroInventory>();

    #endregion

    #region Private Local Cache State

    private GameObject _currentActiveHero;

    #endregion

    #region Properties

    public IReadOnlyList<HeroInventory> PartyInventories => partyInventories;

    #endregion

    #region Unity Lifecycle & Event Subscriptions

    private void OnEnable()
    {
        // Inventory Requests
        GameEvents.Inventory.OnRequestAddItem += HandleRequestAddItem;
        GameEvents.Inventory.OnRequestRemoveItem += HandleRequestRemoveItem;
        GameEvents.Inventory.OnCheckHasItem += HandleCheckHasItem;
        GameEvents.Inventory.OnClearAllInventoriesRequested += ClearAllInventories;
        GameEvents.Inventory.OnAllItemsRequested += AllItems;

        // Party Domain Subscriptions
        GameEvents.Party.OnPartyChanged += SyncWithParty;
        GameEvents.Party.OnActiveHeroChanged += HandleActiveHeroChanged;
    }

    private void OnDisable()
    {
        GameEvents.Inventory.OnRequestAddItem -= HandleRequestAddItem;
        GameEvents.Inventory.OnRequestRemoveItem -= HandleRequestRemoveItem;
        GameEvents.Inventory.OnCheckHasItem -= HandleCheckHasItem;
        GameEvents.Inventory.OnClearAllInventoriesRequested -= ClearAllInventories;
        GameEvents.Inventory.OnAllItemsRequested -= AllItems;

        GameEvents.Party.OnPartyChanged -= SyncWithParty;
        GameEvents.Party.OnActiveHeroChanged -= HandleActiveHeroChanged;
    }

    #endregion

    #region Event Handlers

    private void HandleActiveHeroChanged(GameObject newActiveHero)
    {
        _currentActiveHero = newActiveHero;
    }

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
    /// Automatically rebuilds the active party inventory list when GameEvents.Party.OnPartyChanged fires.
    /// </summary>
    private void SyncWithParty(IReadOnlyList<GameObject> activeParty)
    {
        partyInventories.Clear();

        if (activeParty == null || activeParty.Count == 0) return;

        for (int i = 0; i < activeParty.Count; i++)
        {
            GameObject heroObj = activeParty[i];
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
    /// Adds an item by slug via GameEvents.Database. Automatically targets active hero first (or specific targetHeroIndex if provided).
    /// </summary>
    private int AddItem(string itemSlug, int quantity = 1, int targetHeroIndex = -1)
    {
        ItemSO itemToAdd = null;

        GameEvents.Database.RequestItemBySlug(itemSlug, (item) =>
        {
            itemToAdd = item;
        });

        if (itemToAdd == null)
        {
            Debug.LogWarning($"[InventoryController] Cannot add item. Slug '{itemSlug}' not found.", this);
            return quantity;
        }

        return AddItem(itemToAdd, quantity, targetHeroIndex);
    }

    /// <summary>
    /// Adds an ItemSO instance. Routes KeyItems to GlobalBag via decoupled events, 
    /// or places standard items in the target hero's backpack (overflowing to party members).
    /// </summary>
    private int AddItem(ItemSO item, int quantity = 1, int targetHeroIndex = -1)
    {
        if (item == null || quantity <= 0) return quantity;

        // 1. Route KeyItems / QuestItems to GlobalBag via Decoupled Event Callback
        if (item is KeyItemSO keyItem)
        {
            int remainingOverflow = quantity;
            GameEvents.Inventory.RequestAddKeyItem(keyItem, quantity, (int remaining) =>
            {
                remainingOverflow = remaining;
            });

            if (remainingOverflow == 0)
            {
                Debug.Log($"<color=cyan>[GlobalBag] Added {quantity}x {keyItem.ItemName}</color>");
            }
            else
            {
                Debug.LogWarning($"[GlobalBag] Could not fully fit {remainingOverflow}x {keyItem.ItemName}.");
            }

            return remainingOverflow;
        }

        // 2. Route standard items to Hero Backpacks
        if (partyInventories == null || partyInventories.Count == 0)
        {
            Debug.LogError("[InventoryController] No party inventories registered!", this);
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

        // Step 2b: If primary hero was full, attempt to route remaining quantity to other party members
        if (remaining > 0)
        {
            for (int i = 0; i < partyInventories.Count; i++)
            {
                if (i == startingIndex) continue; // Skip primary, already processed

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
    /// Helper method to find the list index of the currently active hero's inventory without calling GameManager.
    /// </summary>
    private int GetActiveHeroInventoryIndex()
    {
        if (_currentActiveHero != null && _currentActiveHero.TryGetComponent<HeroInventory>(out var activeInv))
        {
            int index = partyInventories.IndexOf(activeInv);
            if (index != -1) return index;
        }

        return 0; // Default to first hero in party if active hero is not found or null
    }

    /// <summary>
    /// Removes a quantity of a specific item from the party (routes KeyItems to GlobalBag, otherwise checks hero backpacks).
    /// </summary>
    private bool RemoveItem(ItemSO itemData, int quantity = 1)
    {
        if (itemData == null || quantity <= 0) return false;

        // 1. KeyItem removal via Decoupled Event Callback
        if (itemData is KeyItemSO keyItem)
        {
            bool removalSuccess = false;
            GameEvents.Inventory.RequestRemoveKeyItem(keyItem, quantity, (bool success) =>
            {
                removalSuccess = success;
            });
            return removalSuccess;
        }

        // 2. Standard item backpack removal using Non-Alloc index loops
        int remainingToRemove = quantity;

        for (int i = 0; i < partyInventories.Count; i++)
        {
            HeroInventory heroInv = partyInventories[i];
            if (heroInv == null || heroInv.Slots == null) continue;

            for (int j = 0; j < heroInv.Slots.Count; j++)
            {
                var slot = heroInv.Slots[j];
                if (slot != null && !slot.IsEmpty && slot.Item == itemData)
                {
                    int qtyInSlot = slot.Quantity;
                    if (qtyInSlot <= remainingToRemove)
                    {
                        remainingToRemove -= qtyInSlot;
                        heroInv.RemoveFromSlot(j, qtyInSlot);
                    }
                    else
                    {
                        heroInv.RemoveFromSlot(j, remainingToRemove);
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
    /// Checks if any party member's backpack (or the GlobalBag via events) possesses a given item quantity.
    /// </summary>
    private bool HasItem(ItemSO itemData, int amount = 1)
    {
        if (itemData == null || amount <= 0) return false;

        // 1. Query KeyItem possession from GlobalBag via Decoupled Event Callback
        if (itemData is KeyItemSO keyItem)
        {
            bool hasKeyItem = false;
            GameEvents.Inventory.CheckHasKeyItem(keyItem, amount, (bool result) =>
            {
                hasKeyItem = result;
            });

            if (hasKeyItem) return true;
        }

        // 2. Query standard item possession across hero backpacks using Non-Alloc index loops
        int totalFound = 0;
        for (int i = 0; i < partyInventories.Count; i++)
        {
            HeroInventory heroInv = partyInventories[i];
            if (heroInv == null || heroInv.Slots == null) continue;

            for (int j = 0; j < heroInv.Slots.Count; j++)
            {
                var slot = heroInv.Slots[j];
                if (slot != null && !slot.IsEmpty && slot.Item == itemData)
                {
                    totalFound += slot.Quantity;
                    if (totalFound >= amount) return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Debug utility: Adds 1 instance of every item in ItemDatabase to the party via GameEvents.Database.
    /// </summary>
    private void AllItems()
    {
        IReadOnlyList<ItemSO> allItems = null;

        GameEvents.Database.RequestAllItems((items) =>
        {
            allItems = items;
        });

        if (allItems == null) return;

        for (int i = 0; i < allItems.Count; i++)
        {
            AddItem(allItems[i], 1);
        }
    }

    /// <summary>
    /// Clears all registered hero inventories and requests global bag wipe via events.
    /// </summary>
    private void ClearAllInventories()
    {
        for (int i = 0; i < partyInventories.Count; i++)
        {
            HeroInventory heroInv = partyInventories[i];
            if (heroInv != null && heroInv.Slots != null)
            {
                for (int j = 0; j < heroInv.Slots.Count; j++)
                {
                    if (heroInv.Slots[j] != null)
                    {
                        heroInv.RemoveFromSlot(j, heroInv.Slots[j].Quantity);
                    }
                }
            }
        }
    }

    #endregion
}