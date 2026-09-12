using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject defining battle-use effects for non-consumable items, equipment, or artifacts (e.g., Hermes' Wand, Healing Ring).
/// Supports damage, healing, status cures, buff applications, VFX/SFX playback, and item degradation (break chance) upon use.
/// </summary>
[CreateAssetMenu(fileName = "BattleEffect_", menuName = "GoldenSun/Items/Item Battle Effect")]
public class ItemBattleEffectSO : ScriptableObject
{
    #region Usage Restrictions & Element

    [Header("Targeting & Element")]
    [SerializeField] private ElementalType element = ElementalType.None;

    [Tooltip("Does this effect target an ally or an enemy?")]
    [SerializeField] private bool targetsAlly = true;

    [Tooltip("Does this effect target all combatants in the target party?")]
    [SerializeField] private bool isAreaOfEffect = false;

    #endregion

    #region Resource Effects (Damage / Healing)

    [Header("Health & Psynergy Effects")]
    [Tooltip("Restores health points (HP) to the target.")]
    [SerializeField] private bool restoresHp = false;

    [Tooltip("Amount of HP restored. If percentage is true, calculated over target's Max HP.")]
    [SerializeField] private int hpAmount = 0;

    [SerializeField] private bool isHpPercentage = false;

    [Space(5)]
    [Tooltip("Restores psynergy points (PP) to the target.")]
    [SerializeField] private bool restoresPp = false;

    [Tooltip("Amount of PP restored. If percentage is true, calculated over target's Max PP.")]
    [SerializeField] private int ppAmount = 0;

    [SerializeField] private bool isPpPercentage = false;

    #endregion

    #region Status Effect Removal & Cures

    [Header("Status Removal & Cures")]
    [Tooltip("If true, clears ALL active status effects on target.")]
    [SerializeField] private bool cureAllStatusEffects = false;

    [Tooltip("Categories of status effects cured by this item effect (e.g., Ailments, Debuffs).")]
    [SerializeField] private List<StatusEffectCategory> categoriesToCure = new List<StatusEffectCategory>();

    [Tooltip("Specific status effect templates cured by this item effect.")]
    [SerializeField] private List<StatusEffectSO> specificStatusToCure = new List<StatusEffectSO>();

    #endregion

    #region Status Effect Application (Buffs / Ailments)

    [Header("Status Effect Application")]
    [Tooltip("Status effects with application probabilities applied to target upon activation.")]
    [SerializeField] private List<StatusEffectApplication> statusEffectsToApply = new List<StatusEffectApplication>();

    #endregion

    #region Golden Sun Break Mechanics

    [Header("Break / Degradation Mechanics")]
    [Tooltip("Can this item break or crack when used in battle?")]
    [SerializeField] private bool canBreakOnUse = false;

    [Range(0f, 1f)]
    [Tooltip("Normalized probability (0.0 to 1.0) that using this item breaks it in inventory.")]
    [SerializeField] private float breakChance = 0.1f;

    #endregion

    #region Battle Presentation FX

    [Header("Battle Presentation FX")]
    [Tooltip("Unified visual and audio FX configuration for combat execution.")]
    [SerializeField] private BattleActionFXConfig battleFXConfig;

    #endregion

    #region Public Properties

    public ElementalType Element => element;
    public bool TargetsAlly => targetsAlly;
    public bool IsAreaOfEffect => isAreaOfEffect;

    public bool RestoresHp => restoresHp;
    public int HpAmount => hpAmount;
    public bool IsHpPercentage => isHpPercentage;

    public bool RestoresPp => restoresPp;
    public int PpAmount => ppAmount;
    public bool IsPpPercentage => isPpPercentage;

    public bool CureAllStatusEffects => cureAllStatusEffects;
    public IReadOnlyList<StatusEffectCategory> CategoriesToCure => categoriesToCure;
    public IReadOnlyList<StatusEffectSO> SpecificStatusToCure => specificStatusToCure;
    public IReadOnlyList<StatusEffectApplication> StatusEffectsToApply => statusEffectsToApply;

    public bool CanBreakOnUse => canBreakOnUse;
    public float BreakChance => breakChance;

    public BattleActionFXConfig BattleFXConfig => battleFXConfig;

    #endregion

    #region Editor Validation

    private void OnValidate()
    {
        if (hpAmount < 0) hpAmount = 0;
        if (ppAmount < 0) ppAmount = 0;
    }

    #endregion

    #region Execution API

    /// <summary>
    /// Executes the special battle effect on a single target.
    /// </summary>
    /// <returns>True if at least one effect succeeded; otherwise false.</returns>
    public virtual bool ExecuteEffect(FighterStats user, FighterStats target, out bool itemBroke)
    {
        itemBroke = false;

        if (target == null || target.IsDead) return false;

        bool effectApplied = false;

        // 1. Handle Resource Restoration (HP)
        if (restoresHp && target.CurrHp < target.MaxHp)
        {
            int hpToRestore = isHpPercentage
                ? Mathf.RoundToInt(target.MaxHp * (hpAmount / 100f))
                : hpAmount;

            if (target.RestoreHP(hpToRestore))
            {
                effectApplied = true;
            }
        }

        // 2. Handle Resource Restoration (PP)
        if (restoresPp && target.CurrPp < target.MaxPp)
        {
            int ppToRestore = isPpPercentage
                ? Mathf.RoundToInt(target.MaxPp * (ppAmount / 100f))
                : ppAmount;

            if (target.RestorePP(ppToRestore))
            {
                effectApplied = true;
            }
        }

        // 3. Handle Status Cures
        if (cureAllStatusEffects && target.ActiveStatusEffects.Count > 0)
        {
            target.ClearAllStatusEffects();
            effectApplied = true;
        }
        else
        {
            for (int c = 0; c < categoriesToCure.Count; c++)
            {
                if (target.PurgeCategory(categoriesToCure[c]) > 0)
                {
                    effectApplied = true;
                }
            }

            for (int s = 0; s < specificStatusToCure.Count; s++)
            {
                StatusEffectSO statusSO = specificStatusToCure[s];
                if (statusSO != null && target.RemoveStatusEffect(statusSO))
                {
                    effectApplied = true;
                }
            }
        }

        // 4. Apply Status Effects or Buffs using non-alloc probability rolls
        if (statusEffectsToApply != null)
        {
            for (int e = 0; e < statusEffectsToApply.Count; e++)
            {
                StatusEffectApplication app = statusEffectsToApply[e];
                if (app.StatusEffect == null) continue;

                if (Random.value <= app.ApplicationChance)
                {
                    if (target.ApplyStatusEffect(app.StatusEffect, user))
                    {
                        effectApplied = true;
                    }
                }
            }
        }

        // 5. Evaluate Item Break Chance (Golden Sun mechanic)
        if (effectApplied && canBreakOnUse)
        {
            if (Random.value <= breakChance)
            {
                itemBroke = true;
                Debug.LogWarning($"[ItemBattleEffect] Item effect triggered and item BROKE!");
            }
        }

        return effectApplied;
    }

    #endregion
}