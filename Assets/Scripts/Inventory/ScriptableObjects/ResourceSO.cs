using UnityEngine;

[CreateAssetMenu(fileName = "New_Resource", menuName = "Inventory/Items/Resource")]
public class ResourceSO : ItemSO
{
    [Header("Crafting Context")]
    [SerializeField] private string craftingCategory;

    #region Properties

    public string CraftingCategory => craftingCategory;

    #endregion
}