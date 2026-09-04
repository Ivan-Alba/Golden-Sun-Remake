using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central database repository for all ItemSO assets loaded from Resources.
/// Resolves slug-to-object deserialization for Save Systems and provides zero-alloc database queries.
/// Fully decoupled via GameEvents.Database and GameEvents.Inventory.
/// </summary>
public class ItemDatabase : MonoBehaviour
{
    #region Serialized Fields

    [Header("Resource Path Settings")]
    [Tooltip("Folder path inside Resources where ItemSO assets are stored.")]
    [SerializeField] private string itemsResourcePath = "ScriptableObjects/Items";

    #endregion

    #region Private Fields

    private readonly List<ItemSO> itemList = new List<ItemSO>();
    private readonly Dictionary<string, ItemSO> itemDictionary = new Dictionary<string, ItemSO>();

    #endregion

    #region Public Properties

    public IReadOnlyList<ItemSO> ItemList => itemList;

    #endregion

    #region Unity Lifecycle & Event Subscriptions

    private void Awake()
    {
        BuildDatabase();
    }

    private void OnEnable()
    {
        // Database bus subscriptions
        GameEvents.Database.OnRequestItemBySlug += HandleRequestItemBySlug;
        GameEvents.Database.OnRequestAllItems += HandleRequestAllItems;
        GameEvents.Database.OnRequestItemsByType += HandleRequestItemsByType;
    }

    private void OnDisable()
    {
        // Database bus unsubscriptions
        GameEvents.Database.OnRequestItemBySlug -= HandleRequestItemBySlug;
        GameEvents.Database.OnRequestAllItems -= HandleRequestAllItems;
        GameEvents.Database.OnRequestItemsByType -= HandleRequestItemsByType;
    }

    #endregion

    #region Initialization Logic

    /// <summary>
    /// Clears existing caches and populates the database from Resources using non-alloc loops.
    /// </summary>
    public void BuildDatabase()
    {
        itemList.Clear();
        itemDictionary.Clear();

        // Load all ItemSO assets (including derived ScriptableObjects) from Resources
        ItemSO[] loadedItems = Resources.LoadAll<ItemSO>(itemsResourcePath);

        if (loadedItems == null || loadedItems.Length == 0)
        {
            Debug.LogWarning($"[ItemDatabase] No items found at path Resources/{itemsResourcePath}.", this);
            return;
        }

        for (int i = 0; i < loadedItems.Length; i++)
        {
            ItemSO item = loadedItems[i];
            if (item == null) continue;

            if (string.IsNullOrWhiteSpace(item.ItemSlug))
            {
                Debug.LogWarning($"[ItemDatabase] Item '{item.name}' has an empty or null ItemSlug.", this);
                continue;
            }

            if (itemDictionary.ContainsKey(item.ItemSlug))
            {
                Debug.LogError($"[ItemDatabase] Duplicate ItemSlug detected: '{item.ItemSlug}'. Item '{item.name}' skipped.", this);
                continue;
            }

            itemList.Add(item);
            itemDictionary.Add(item.ItemSlug, item);
        }

        Debug.Log($"[ItemDatabase] Successfully loaded {itemList.Count} items from Resources/{itemsResourcePath}.");
    }

    #endregion

    #region Event Handlers

    private void HandleRequestItemBySlug(string slug, Action<ItemSO> onResult)
    {
        ItemSO item = GetItem(slug);
        onResult?.Invoke(item);
    }

    private void HandleRequestAllItems(Action<IReadOnlyList<ItemSO>> onResult)
    {
        onResult?.Invoke(itemList);
    }

    private void HandleRequestItemsByType(ItemType itemType, List<ItemSO> resultsBuffer)
    {
        GetItemsByType(itemType, resultsBuffer);
    }

    private void HandleAllItemsRequested()
    {
        // Debug handler: Pushes 1 instance of every item in database to party inventories
        for (int i = 0; i < itemList.Count; i++)
        {
            GameEvents.Inventory.RequestAddItem(itemList[i], 1, null);
        }
    }

    #endregion

    #region Database Query Methods (Non-Alloc)

    /// <summary>
    /// Retrieves a base ItemSO by its unique ItemSlug. Essential for save game deserialization.
    /// </summary>
    public ItemSO GetItem(string itemSlug)
    {
        if (string.IsNullOrEmpty(itemSlug)) return null;

        if (itemDictionary.TryGetValue(itemSlug, out ItemSO item))
        {
            return item;
        }

        Debug.LogWarning($"[ItemDatabase] Item with slug '{itemSlug}' not found in database.");
        return null;
    }

    /// <summary>
    /// Generic lookup to retrieve an item directly cast to its derived type (e.g., ConsumableSO, EquipableSO).
    /// </summary>
    public T GetItem<T>(string itemSlug) where T : ItemSO
    {
        ItemSO item = GetItem(itemSlug);
        if (item is T typedItem)
        {
            return typedItem;
        }

        if (item != null)
        {
            Debug.LogWarning($"[ItemDatabase] Item '{itemSlug}' exists but is not of type '{typeof(T).Name}'.");
        }

        return null;
    }

    /// <summary>
    /// Zero-allocation query: Fills a provided buffer list with items matching the specified ItemType.
    /// </summary>
    public void GetItemsByType(ItemType type, List<ItemSO> resultsBuffer)
    {
        if (resultsBuffer == null) return;
        resultsBuffer.Clear();

        for (int i = 0; i < itemList.Count; i++)
        {
            ItemSO item = itemList[i];
            if (item != null && item.ItemType == type)
            {
                resultsBuffer.Add(item);
            }
        }
    }

    /// <summary>
    /// Returns a read-only list of all registered items in the database for zero-alloc index iteration.
    /// </summary>
    public IReadOnlyList<ItemSO> GetAllItems()
    {
        return itemList;
    }

    #endregion
}