using UnityEngine;
using UnityEngine.Localization;


public enum ItemType
{
    Consumable,      // Health/PP restoratives, cure items, stat boosts (Herbs, Nuts, Apples)
    Equipment,       // Standard gear (Weapons, Shields, Armors, etc.)
    PsynergyRelic,   // Special artifacts granting field/battle Psynergies
    KeyItem,         // Quest items and story keys (Mars Star, Palace Key) -> Infinite Global Bag
    Resource,        // Crafting materials and trade goods (Spiderweb, Mithril Ore)
    Misc             // Event or minigame items (Game Tickets, Lucky Medals, Special gifts)
}

/// <summary>
/// Base ScriptableObject defining inventory items in the game.
/// Contains localized identity strings, UI icon, category taxonomy, economy parameters, 
/// quest deliverable flags, and stacking rules.
/// </summary>
[CreateAssetMenu(fileName = "New_Item", menuName = "GoldenSun/Inventory/Base Item")]
public class ItemSO : ScriptableObject
{
    #region Base Identification

    [Header("Base Identification")]
    [Tooltip("Unique string identifier used for database lookups, save data, and inventory transactions.")]
    [SerializeField] private string itemSlug;

    [Tooltip("Localized display name of the item.")]
    [SerializeField] private LocalizedString itemName;

    [Tooltip("Localized mechanical description of the item's effects or usage.")]
    [SerializeField] private LocalizedString itemDescription;

    [Tooltip("Localized lore or flavor text providing narrative depth.")]
    [SerializeField] private LocalizedString flavorText;

    [Tooltip("UI Icon displayed in inventory grids, shops, and notifications.")]
    [SerializeField] private Sprite icon;

    [Tooltip("Taxonomy classification of the item (e.g. Consumable, Equipment, KeyItem).")]
    [SerializeField] private ItemType itemType;

    #endregion

    #region Quest & NPC Interaction Rules

    [Header("Quest & NPC Interaction Rules")]
    [Tooltip("Can this item be selected and given/shown to an NPC during dialogue or quest prompts?")]
    [SerializeField] private bool isQuestDeliverable = false;

    #endregion

    #region Economy & Stacking Rules

    [Header("Economy & Rules")]
    [Tooltip("Base purchase price at vendors in Coins.")]
    [SerializeField] private int buyPrice = 10;

    [Tooltip("Base resale value at vendors in Coins.")]
    [SerializeField] private int sellPrice = 5;

    [Tooltip("Can this item be sold to vendors?")]
    [SerializeField] private bool canSell = true;

    [Tooltip("Maximum allowed quantity in a single inventory slot.")]
    [SerializeField] private int maxStack = 1;

    #endregion

    #region Public Properties

    public string ItemSlug => itemSlug;

    public LocalizedString ItemNameLocalizedString => itemName;
    public string ItemName => itemName != null ? itemName.GetLocalizedString() : string.Empty;

    public LocalizedString ItemDescriptionLocalizedString => itemDescription;
    public string ItemDescription => itemDescription != null ? itemDescription.GetLocalizedString() : string.Empty;

    public LocalizedString FlavorTextLocalizedString => flavorText;
    public string FlavorText => flavorText != null ? flavorText.GetLocalizedString() : string.Empty;

    public Sprite Icon => icon;
    public ItemType ItemType => itemType;

    public bool IsQuestDeliverable => isQuestDeliverable;

    public int BuyPrice => buyPrice;
    public int SellPrice => sellPrice;
    public bool CanSell => canSell;
    public int MaxStack => maxStack;

    #endregion

    #region Unity Editor Validation

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (buyPrice < 0) buyPrice = 0;
        if (sellPrice < 0) sellPrice = 0;
        if (maxStack < 1) maxStack = 1;

        if (string.IsNullOrWhiteSpace(itemSlug))
        {
            itemSlug = name.Replace("New_Item", string.Empty).Replace("Item_", string.Empty).ToLowerInvariant();
        }
    }
#endif

    #endregion
}