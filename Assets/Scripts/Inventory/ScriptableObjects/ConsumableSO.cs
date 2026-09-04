using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Flexible ScriptableObject defining consumable items (Potions, Antidotes, Stat Boosters, Elixirs).
/// Supports composite actions combining resource restoration, status cures, status applications, 
/// and permanent stat boosts with zero-allocation loops and custom SFX playback.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_", menuName = "GoldenSun/Items/Consumable")]
public class ConsumableSO : ItemSO
{
    #region Usage Restrictions

    [Header("Usage Context Restrictions")]
    [Tooltip("Can this item be used from the main party menu out of combat?")]
    [SerializeField] private bool useInMenu = true;

    [Tooltip("Can this item be used during turn-based combat?")]
    [SerializeField] private bool useInBattle = true;

    #endregion

    #region Health & Psynergy Restoration Settings

    [Header("Resource Restoration")]
    [Tooltip("Restores health points (HP) to the target.")]
    [SerializeField] private bool restoresHp = false;

    [Tooltip("Amount of HP to restore. If percentage is true, 100 restores 100% Max HP.")]
    [SerializeField] private int hpAmount = 50;

    [Tooltip("If true, hpAmount is treated as a percentage of target's Max HP.")]
    [SerializeField] private bool isHpPercentage = false;

    [Space(5)]
    [Tooltip("Restores psynergy points (PP) to the target.")]
    [SerializeField] private bool restoresPp = false;

    [Tooltip("Amount of PP to restore. If percentage is true, 100 restores 100% Max PP.")]
    [SerializeField] private int ppAmount = 20;

    [Tooltip("If true, ppAmount is treated as a percentage of target's Max PP.")]
    [SerializeField] private bool isPpPercentage = false;

    #endregion

    #region Revive Settings

    [Header("Revive Settings")]
    [Tooltip("Allows this item to revive defeated/downed targets (0 HP).")]
    [SerializeField] private bool canRevive = false;

    [Tooltip("Amount of HP granted upon revival.")]
    [SerializeField] private int reviveHpAmount = 100;

    [Tooltip("If true, reviveHpAmount is treated as a percentage of target's Max HP.")]
    [SerializeField] private bool isRevivePercentage = true;

    #endregion

    #region Status Removal & Cure Settings

    [Header("Status Removal & Cures")]
    [Tooltip("If true, cures ALL active status effects unconditionally.")]
    [SerializeField] private bool cureAllStatusEffects = false;

    [Tooltip("Categories of status effects cured by this item (e.g. Ailments for Elixir, Debuffs for Dispel).")]
    [SerializeField] private List<StatusEffectCategory> categoriesToCure = new List<StatusEffectCategory>();

    [Tooltip("Specific status effect templates cured by this item (e.g. PoisonSO for Antidote).")]
    [SerializeField] private List<StatusEffectSO> specificStatusToCure = new List<StatusEffectSO>();

    #endregion

    #region Status Effect Application Settings

    [Header("Status Effect Application (Buffs / Tonics)")]
    [Tooltip("Status effects or temporary buffs applied to the target upon consuming this item.")]
    [SerializeField] private List<StatusEffectSO> statusEffectsToApply = new List<StatusEffectSO>();

    #endregion

    #region Permanent Stat Boost Settings

    [Header("Permanent Stat Boosts")]
    [Tooltip("If true, permanently increases the base value of a core stat.")]
    [SerializeField] private bool isPermanentStatBoost = false;

    [Tooltip("The specific stat modified permanently (e.g. MaxHP for Apple, Atk for Hard Nut).")]
    [SerializeField] private StatType statToBonus = StatType.Attack;

    [Tooltip("The amount added permanently to the base stat.")]
    [SerializeField] private int statBonusAmount = 1;

    #endregion

    #region Audio Settings

    [Header("Audio Settings")]
    [Tooltip("Optional custom audio clip played upon successful usage. If unassigned, defaults to fallback SFX.")]
    [SerializeField] private AudioClip customUseSFX;

    [Tooltip("Fallback system SFX triggered if no custom AudioClip is provided.")]
    [SerializeField] private SFXType fallbackUseSFX = SFXType.UI_Cure;

    #endregion

    #region Public Properties

    public bool UseInMenu => useInMenu;
    public bool UseInBattle => useInBattle;

    public bool RestoresHp => restoresHp;
    public int HpAmount => hpAmount;
    public bool IsHpPercentage => isHpPercentage;

    public bool RestoresPp => restoresPp;
    public int PpAmount => ppAmount;
    public bool IsPpPercentage => isPpPercentage;

    public bool CanRevive => canRevive;
    public int ReviveHpAmount => reviveHpAmount;
    public bool IsRevivePercentage => isRevivePercentage;

    public bool CureAllStatusEffects => cureAllStatusEffects;
    public IReadOnlyList<StatusEffectCategory> CategoriesToCure => categoriesToCure;
    public IReadOnlyList<StatusEffectSO> SpecificStatusToCure => specificStatusToCure;

    public IReadOnlyList<StatusEffectSO> StatusEffectsToApply => statusEffectsToApply;

    public bool IsPermanentStatBoost => isPermanentStatBoost;
    public StatType StatToBonus => statToBonus;
    public int StatBonusAmount => statBonusAmount;

    public AudioClip CustomUseSFX => customUseSFX;
    public SFXType FallbackUseSFX => fallbackUseSFX;

    #endregion

    #region Execution API

    /// <summary>
    /// Executes consumable logic out of combat from the main menu.
    /// </summary>
    public virtual bool ExecuteMenuEffect(HeroStats target)
    {
        if (!useInMenu)
        {
            Debug.LogWarning($"[Consumable] '{ItemName}' is not marked for menu usage.", this);
            return false;
        }

        return ExecuteEffectOnFighter(target);
    }

    /// <summary>
    /// Executes consumable logic during turn-based combat.
    /// </summary>
    public virtual bool ExecuteBattleEffect(FighterStats target)
    {
        if (!useInBattle)
        {
            Debug.LogWarning($"[Consumable] '{ItemName}' cannot be used in battle.", this);
            return false;
        }

        return ExecuteEffectOnFighter(target);
    }

    #endregion

    #region Internal Unified Execution Logic

    /// <summary>
    /// Core processing method executing all composite effects defined in this consumable asset with zero allocation overhead.
    /// </summary>
    /// <returns>True if at least one effect succeeded on the target; otherwise false.</returns>
    protected virtual bool ExecuteEffectOnFighter(FighterStats target)
    {
        if (target == null) return false;

        bool anyEffectApplied = false;

        // 1. Handle Dead Target / Revival FIRST
        if (target.IsDead)
        {
            if (canRevive)
            {
                int healthToGrant = isRevivePercentage
                    ? Mathf.RoundToInt(target.MaxHp * (reviveHpAmount / 100f))
                    : reviveHpAmount;

                anyEffectApplied = target.Revive(healthToGrant);
            }
            else
            {
                // Cannot use standard consumables on defeated fighters
                GameEvents.Audio.RequestPlaySFX(SFXType.UI_Cancel);
                return false;
            }
        }

        // 2. Handle Resource Restoration (HP)
        if (restoresHp && !target.IsDead && target.CurrHp < target.MaxHp)
        {
            int hpToRestore = isHpPercentage
                ? Mathf.RoundToInt(target.MaxHp * (hpAmount / 100f))
                : hpAmount;

            if (target.RestoreHP(hpToRestore))
            {
                anyEffectApplied = true;
            }
        }

        // 3. Handle Resource Restoration (PP)
        if (restoresPp && !target.IsDead && target.CurrPp < target.MaxPp)
        {
            int ppToRestore = isPpPercentage
                ? Mathf.RoundToInt(target.MaxPp * (ppAmount / 100f))
                : ppAmount;

            if (target.RestorePP(ppToRestore))
            {
                anyEffectApplied = true;
            }
        }

        // 4. Handle Status Cures & Removal (Non-Alloc index-based loops)
        if (!target.IsDead)
        {
            if (cureAllStatusEffects && target.ActiveStatusEffects.Count > 0)
            {
                target.ClearAllStatusEffects();
                anyEffectApplied = true;
            }
            else
            {
                // Cure by categories (e.g. Elixir clearing Ailments)
                for (int i = 0; i < categoriesToCure.Count; i++)
                {
                    int purgedCount = target.PurgeCategory(categoriesToCure[i]);
                    if (purgedCount > 0) anyEffectApplied = true;
                }

                // Cure specific statuses (e.g. Antidote clearing Poison)
                for (int i = 0; i < specificStatusToCure.Count; i++)
                {
                    StatusEffectSO statusSO = specificStatusToCure[i];
                    if (statusSO != null && target.RemoveStatusEffect(statusSO))
                    {
                        anyEffectApplied = true;
                    }
                }
            }
        }

        // 5. Apply New Status Effects / Buffs (Non-Alloc index-based loop)
        if (!target.IsDead && statusEffectsToApply != null)
        {
            for (int i = 0; i < statusEffectsToApply.Count; i++)
            {
                StatusEffectSO statusSO = statusEffectsToApply[i];
                if (statusSO != null)
                {
                    bool applied = target.ApplyStatusEffect(statusSO);
                    if (applied) anyEffectApplied = true;
                }
            }
        }

        // 6. Apply Permanent Stat Boosts
        if (!target.IsDead && isPermanentStatBoost)
        {
            target.AddPermanentStatBoost(statToBonus, statBonusAmount);
            anyEffectApplied = true;
        }

        // 7. Decoupled Audio Resolution
        if (anyEffectApplied)
        {
            if (customUseSFX != null)
            {
                GameEvents.Audio.RequestPlaySFXClip(customUseSFX);
            }
            else
            {
                GameEvents.Audio.RequestPlaySFX(fallbackUseSFX);
            }
        }
        else
        {
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Cancel);
        }

        return anyEffectApplied;
    }

    #endregion

    #region Unity Editor Validation

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (hpAmount < 0) hpAmount = 0;
        if (ppAmount < 0) ppAmount = 0;
        if (reviveHpAmount < 0) reviveHpAmount = 0;
        if (statBonusAmount < 0) statBonusAmount = 0;

        if (isHpPercentage) hpAmount = Mathf.Clamp(hpAmount, 0, 100);
        if (isPpPercentage) ppAmount = Mathf.Clamp(ppAmount, 0, 100);
        if (isRevivePercentage) reviveHpAmount = Mathf.Clamp(reviveHpAmount, 0, 100);
    }
#endif

    #endregion
}