using UnityEngine;

/// <summary>
/// ScriptableObject defining quest items and story artifacts (e.g., Elemental Stars, Dungeon Keys).
/// Strictly holds quest/story identification data. Automatically enforces non-salable 
/// and single-stack economy rules in the editor.
/// </summary>
[CreateAssetMenu(fileName = "KeyItem_", menuName = "GoldenSun/Items/Key Item")]
public class KeyItemSO : ItemSO
{
    #region Quest & Story Context

    [Header("Quest & Story Context")]
    [Tooltip("Unique Identifier linking this item to a specific quest or story progression flag.")]
    [SerializeField] private string questId;

    [Tooltip("If true, this item is strictly required for main story progression and cannot be discarded/dropped.")]
    [SerializeField] private bool isStoryMandatory = true;

    #endregion

    #region Public Properties

    public string QuestId => questId;
    public bool IsStoryMandatory => isStoryMandatory;

    #endregion

    #region Unity Editor Validation

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Enforce Non-Salable and Single-Stack rules for Key Items
        var canSellProp = typeof(ItemSO).GetField("canSell", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (canSellProp != null) canSellProp.SetValue(this, false);

        var buyPriceProp = typeof(ItemSO).GetField("buyPrice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (buyPriceProp != null) buyPriceProp.SetValue(this, 0);

        var sellPriceProp = typeof(ItemSO).GetField("sellPrice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (sellPriceProp != null) sellPriceProp.SetValue(this, 0);

        var maxStackProp = typeof(ItemSO).GetField("maxStack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (maxStackProp != null) maxStackProp.SetValue(this, 1);

        var itemTypeProp = typeof(ItemSO).GetField("itemType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (itemTypeProp != null) itemTypeProp.SetValue(this, ItemType.KeyItem);
    }
#endif

    #endregion
}