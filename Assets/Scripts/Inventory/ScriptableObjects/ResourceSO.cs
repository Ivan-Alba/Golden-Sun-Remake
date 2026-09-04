using System.Reflection;
using UnityEngine;

/// <summary>
/// Defines specific crafting material tiers for forging special gear (Sunshine Forge system in Golden Sun).
/// </summary>
public enum CraftingMaterialType
{
    General,
    Mithril,        // Mithril Silver / Ore
    Orihalcon,      // Divine Metal
    DarkMatter,     // Cursed / Dark Forge
    DragonSkin,     // Scale / Leather Forge
    TearStone,      // Gem / Jewelry Forge
    StarDust        // Rare Cosmic Powder
}

/// <summary>
/// ScriptableObject defining raw crafting resources and forging materials 
/// (e.g., Mithril Silver, Dark Matter, Orihalcon).
/// Automatically enforces Resource item classification and stack settings in the editor.
/// </summary>
[CreateAssetMenu(fileName = "Resource_", menuName = "GoldenSun/Items/Resource")]
public class ResourceSO : ItemSO
{
    #region Crafting Context

    [Header("Crafting Context")]
    [Tooltip("Text category describing material usage (e.g., Ores, Pelts, Gems).")]
    [SerializeField] private string craftingCategory = "Ores";

    [Tooltip("Specific material classification used by blacksmiths/forge systems to evaluate craftable recipes.")]
    [SerializeField] private CraftingMaterialType materialType = CraftingMaterialType.General;

    #endregion

    #region Public Properties

    public string CraftingCategory => craftingCategory;
    public CraftingMaterialType MaterialType => materialType;

    #endregion

    #region Unity Editor Validation

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Enforce Resource ItemType classification in ItemSO
        FieldInfo itemTypeProp = typeof(ItemSO).GetField("itemType", BindingFlags.NonPublic | BindingFlags.Instance);
        if (itemTypeProp != null)
        {
            itemTypeProp.SetValue(this, ItemType.Resource);
        }
    }
#endif

    #endregion
}