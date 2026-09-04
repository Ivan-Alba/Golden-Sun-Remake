using UnityEngine;

/// <summary>
/// Defines specific target minigames or interactive events where tokens/vouchers are consumed.
/// </summary>
public enum MinigameTargetType
{
    None,
    TolbiFountain,      // Used for Lucky Medals thrown into the wishing well/fountain
    LuckyTicketsGame,   // Used for Game Tickets / Lucky Tickets at the Tolbi Slot & Scratch games
    GeneralExchange     // Used for general prize tokens or carnival events
}

/// <summary>
/// ScriptableObject defining miscellaneous utility items, event tokens, and minigame tickets 
/// (e.g., Lucky Medals, Game Tickets, Festival Vouchers).
/// Automatically enforces classification rules in the editor.
/// </summary>
[CreateAssetMenu(fileName = "Misc_", menuName = "GoldenSun/Items/Misc Item")]
public class MiscSO : ItemSO
{
    #region Minigame & Event Context

    [Header("Minigame & Event Settings")]
    [Tooltip("Is this item a currency token or voucher consumed in minigames (e.g. Lucky Medals, Game Tickets)?")]
    [SerializeField] private bool isMinigameToken = true;

    [Tooltip("Target minigame or interactive location where this item can be redeemed.")]
    [SerializeField] private MinigameTargetType minigameTarget = MinigameTargetType.None;

    #endregion

    #region Public Properties

    public bool IsMinigameToken => isMinigameToken;
    public MinigameTargetType MinigameTarget => minigameTarget;

    #endregion

    #region Unity Editor Validation

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Enforce Misc ItemType classification
        var itemTypeProp = typeof(ItemSO).GetField("itemType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (itemTypeProp != null)
        {
            itemTypeProp.SetValue(this, ItemType.Misc);
        }
    }
#endif

    #endregion
}