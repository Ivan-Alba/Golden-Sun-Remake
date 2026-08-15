using System.Collections.Generic;
using UnityEngine;

public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance { get; private set; }

    [Header("Resource Path Settings")]
    [Tooltip("Folder path inside Resources where ItemSO assets are stored.")]
    [SerializeField] private string itemsResourcePath = "ScriptableObjects/Items";

    private List<ItemSO> itemList = new List<ItemSO>();
    private Dictionary<string, ItemSO> itemDictionary = new Dictionary<string, ItemSO>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            BuildDatabase();
        }
    }

    private void BuildDatabase()
    {
        itemList.Clear();
        itemDictionary.Clear();

        // Load all ItemSO assets (including derived ScriptableObjects) from Resources
        ItemSO[] loadedItems = Resources.LoadAll<ItemSO>(itemsResourcePath);

        foreach (ItemSO item in loadedItems)
        {
            if (item == null) continue;

            if (string.IsNullOrEmpty(item.ItemSlug))
            {
                Debug.LogWarning($"[ItemDatabase] Item '{item.name}' has an empty or null ItemSlug.");
                continue;
            }

            if (itemDictionary.ContainsKey(item.ItemSlug))
            {
                Debug.LogError($"[ItemDatabase] Duplicate ItemSlug detected: '{item.ItemSlug}'. Item '{item.name}' skipped.");
                continue;
            }

            itemList.Add(item);
            itemDictionary.Add(item.ItemSlug, item);
        }

        Debug.Log($"[ItemDatabase] Successfully loaded {itemList.Count} items from Resources/{itemsResourcePath}.");
    }

    #region Database Query Methods

    /// <summary>
    /// Retrieves a base ItemSO by its unique ItemSlug.
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
    /// Returns all items belonging to a specific ItemType (Consumable, Equipment, KeyItem, etc.).
    /// </summary>
    public List<ItemSO> GetItemsByType(ItemType type)
    {
        List<ItemSO> filteredList = new List<ItemSO>();
        foreach (ItemSO item in itemList)
        {
            if (item.ItemType == type)
            {
                filteredList.Add(item);
            }
        }
        return filteredList;
    }

    /// <summary>
    /// Returns an enumerable list of all registered items in the database.
    /// </summary>
    public IEnumerable<ItemSO> GetAllItems()
    {
        return itemList;
    }

    #endregion
}