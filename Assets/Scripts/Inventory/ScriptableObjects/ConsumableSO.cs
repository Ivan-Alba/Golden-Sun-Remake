using UnityEngine;

[CreateAssetMenu(fileName = "New_Consumable", menuName = "Inventory/Items/Consumable")]
public class ConsumableSO : ItemSO
{
    [Header("Consumable Usage Conditions")]
    [SerializeField] private bool useInMenu = true;
    [SerializeField] private bool useInBattle = true;

    [Header("Effect Settings")]
    [SerializeField] private ConsumableEffectType effectType = ConsumableEffectType.RestoreHp;
    [SerializeField] private int bonusValue = 30; // Amount of HP/PP restored or stat points gained

    [Header("Stat Boost Settings (Only for PermanentStatBoost)")]
    [SerializeField] private StatType statToBonus = StatType.MaxHP;

    #region Properties

    public bool UseInMenu => useInMenu;
    public bool UseInBattle => useInBattle;
    public ConsumableEffectType EffectType => effectType;
    public int BonusValue => bonusValue;
    public StatType StatToBonus => statToBonus;

    #endregion
}

public enum ConsumableEffectType
{
    RestoreHp,          // Herbs, Nuts, Potions
    RestorePp,          // Psynergy Stones, Elixirs
    RestoreHpAndPp,     // Full restoratives
    Revive,             // Water of Life
    CureStatus,         // Antidote
    PermanentStatBoost  // Apple, Mint, Hard Nut
}