using UnityEngine;

[CreateAssetMenu(fileName = "New_Item", menuName = "Inventory/Items/Base Item")]
public class ItemSO : ScriptableObject
{
    [Header("Base Identification")]
    [SerializeField] private string itemSlug;
    [SerializeField] private string itemName;
    [TextArea(2, 4)]
    [SerializeField] private string itemDescription;
    [SerializeField] private Sprite icon;
    [SerializeField] private ItemType itemType;

    [Header("Economy & Rules")]
    [SerializeField] private int buyPrice = 10;
    [SerializeField] private int sellPrice = 5;
    [SerializeField] private bool canSell = true;
    [SerializeField] private int maxStack = 1;

    #region Properties

    public string ItemSlug => itemSlug;
    public string ItemName => itemName;
    public string ItemDescription => itemDescription;
    public Sprite Icon => icon;
    public ItemType ItemType => itemType;
    public int BuyPrice => buyPrice;
    public int SellPrice => sellPrice;
    public bool CanSell => canSell;
    public int MaxStack => maxStack;

    #endregion
}