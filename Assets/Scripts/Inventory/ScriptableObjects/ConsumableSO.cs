using UnityEngine;

public enum ConsumableEffectType
{
    RestoreHp,          // Herbs, Nuts, Potions
    RestorePp,          // Psynergy Stones, Elixirs
    RestoreHpAndPp,     // Full restoratives
    Revive,             // Water of Life
    CureStatus,         // Antidote
    PermanentStatBoost  // Apple, Mint, Hard Nut
}

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

    #region Execution Methods

    /// <summary>
    /// Executes the consumable logic when used from the party menu out of combat.
    /// </summary>
    /// <returns>True if the item had a real effect on the target hero; otherwise false.</returns>
    public virtual bool ExecuteMenuEffect(HeroStats target)
    {
        if (!useInMenu)
        {
            Debug.LogWarning($"[Consumable] '{ItemName}' is not marked for menu usage (UseInMenu = false).");
            return false;
        }

        if (target == null) return false;

        bool effectApplied = false;

        switch (effectType)
        {
            case ConsumableEffectType.RestoreHp:
                effectApplied = target.RestoreHP(bonusValue);
                break;

            case ConsumableEffectType.RestorePp:
                effectApplied = target.RestorePP(bonusValue);
                break;

            case ConsumableEffectType.RestoreHpAndPp:
                bool hpRestored = target.RestoreHP(bonusValue);
                bool ppRestored = target.RestorePP(bonusValue);
                effectApplied = hpRestored || ppRestored;
                break;

            case ConsumableEffectType.Revive:
                effectApplied = target.Revive(bonusValue);
                break;

            case ConsumableEffectType.CureStatus:
                effectApplied = target.CureStatusEffect();
                break;

            case ConsumableEffectType.PermanentStatBoost:
                if (!target.IsDead)
                {
                    target.AddPermanentStatBoost(statToBonus, bonusValue);
                    effectApplied = true;
                }
                break;

            default:
                Debug.LogWarning($"[Consumable] EffectType '{effectType}' is unhandled in ExecuteMenuEffect.");
                break;
        }

        if (effectApplied)
        {
            Debug.Log($"[Consumable] Successfully used '{ItemName}' on {target.Name}.");
            // TODO: Play item's specific usage sound if configured (e.g., AudioManager.Instance.PlaySFX(itemSoundEffect))
        }

        return effectApplied;
    }

    /// <summary>
    /// Executes the consumable logic during combat.
    /// </summary>
    /// <returns>True if the battle item action succeeded; otherwise false.</returns>
    public virtual bool ExecuteBattleEffect(FighterStats target)
    {
        if (!useInBattle)
        {
            Debug.LogWarning($"[Consumable] '{ItemName}' cannot be used in battle (UseInBattle = false).");
            return false;
        }

        if (target == null) return false;

        bool effectApplied = false;

        switch (effectType)
        {
            case ConsumableEffectType.RestoreHp:
                effectApplied = target.RestoreHP(bonusValue);
                break;

            case ConsumableEffectType.RestorePp:
                effectApplied = target.RestorePP(bonusValue);
                break;

            case ConsumableEffectType.RestoreHpAndPp:
                bool hpRestored = target.RestoreHP(bonusValue);
                bool ppRestored = target.RestorePP(bonusValue);
                effectApplied = hpRestored || ppRestored;
                break;

            case ConsumableEffectType.Revive:
                effectApplied = target.Revive(bonusValue);
                break;

            case ConsumableEffectType.CureStatus:
                effectApplied = target.CureStatusEffect();
                break;

            case ConsumableEffectType.PermanentStatBoost:
                if (!target.IsDead)
                {
                    target.AddPermanentStatBoost(statToBonus, bonusValue);
                    effectApplied = true;
                }
                break;
        }

        return effectApplied;
    }

    #endregion
}