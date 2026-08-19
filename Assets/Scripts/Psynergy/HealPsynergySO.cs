using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject defining healing and cleansing Psynergies (e.g., Cure, Ply, Aura, Restore).
/// Supports HP/PP restoration, revival, category-based status purges, specific status removals,
/// and optional bonus status applications (shields/buffs).
/// </summary>
[CreateAssetMenu(fileName = "NewHealPsynergy", menuName = "Psynergy/Heal Psynergy")]
public class HealPsynergySO : PsynergySO
{
    #region Serialized Fields

    [Header("Heal HP Properties")]
    [SerializeField] private bool restoresHp = true;
    [SerializeField] private int healValue = 50;
    [SerializeField] private bool isPercentage = false;

    [Header("Heal PP Properties")]
    [SerializeField] private bool restoresPp = false;
    [SerializeField] private int ppHealValue = 10;
    [SerializeField] private bool isPpPercentage = false;

    [Header("Revive Properties")]
    [SerializeField] private bool canRevive = false;

    [Header("Status Effect Application (Buffs / Shields)")]
    [Tooltip("Optional status effects or shields applied to targets alongside healing.")]
    [SerializeField] private List<StatusEffectSO> statusEffectsToApply = new List<StatusEffectSO>();

    [Header("Status & Debuff Cleansing")]
    [Tooltip("If true, clears ALL active status effects on the target unconditionally.")]
    [SerializeField] private bool cureAllStatusEffects = false;

    [Tooltip("Broad categories of status effects to purge (e.g., Ailments for Cure, Debuffs for Dispel).")]
    [SerializeField] private List<StatusEffectCategory> categoriesToCure = new List<StatusEffectCategory>();

    [Tooltip("Specific status effect templates to remove (e.g., PoisonSO, ParalysisSO).")]
    [SerializeField] private List<StatusEffectSO> specificStatusToCure = new List<StatusEffectSO>();

    #endregion

    #region Public Properties

    public bool RestoresHp => restoresHp;
    public int HealValue => healValue;
    public bool IsPercentage => isPercentage;
    public bool RestoresPp => restoresPp;
    public int PpHealValue => ppHealValue;
    public bool IsPpPercentage => isPpPercentage;
    public bool CanRevive => canRevive;
    public bool CureAllStatusEffects => cureAllStatusEffects;
    public IReadOnlyList<StatusEffectSO> StatusEffectsToApply => statusEffectsToApply;
    public IReadOnlyList<StatusEffectCategory> CategoriesToCure => categoriesToCure;
    public IReadOnlyList<StatusEffectSO> SpecificStatusToCure => specificStatusToCure;

    #endregion

    #region Execution Methods Override

    public override bool ExecuteBattleEffect(FighterStats caster, List<FighterStats> targets)
    {
        if (!IsBattlePsynergy)
        {
            Debug.LogWarning($"[Psynergy] '{PsynergyName}' no es una psinergía de combate.");
            return false;
        }

        if (targets == null || targets.Count == 0) return false;

        bool anyEffectApplied = false;

        foreach (FighterStats target in targets)
        {
            if (target == null) continue;

            if (ApplyHealEffectToTarget(caster, target))
            {
                anyEffectApplied = true;
            }
        }

        return anyEffectApplied;
    }

    public override bool ExecuteMenuEffect(FighterStats caster, List<FighterStats> targets)
    {
        if (!MenuData.CanBeUsedInMenu)
        {
            Debug.LogWarning($"[Psynergy] '{PsynergyName}' no se puede usar desde el menú.");
            return false;
        }

        if (targets == null || targets.Count == 0) return false;

        bool anyEffectApplied = false;

        foreach (FighterStats target in targets)
        {
            if (target == null) continue;

            if (ApplyHealEffectToTarget(caster, target))
            {
                anyEffectApplied = true;
            }
        }

        return anyEffectApplied;
    }

    #endregion

    #region Private Helper Processing

    /// <summary>
    /// Evaluates and applies composite healing, revival, status cleansing, and optional status application logic onto a target.
    /// </summary>
    /// <returns>True if at least one effect succeeded on the target; otherwise false.</returns>
    private bool ApplyHealEffectToTarget(FighterStats caster, FighterStats target)
    {
        bool effectApplied = false;

        // 1. Handle Revival for Dead Targets FIRST
        if (target.IsDead)
        {
            if (canRevive)
            {
                int reviveHp = isPercentage
                    ? Mathf.RoundToInt(target.MaxHp * (healValue / 100f))
                    : healValue;

                if (target.Revive(reviveHp))
                {
                    effectApplied = true;
                    Debug.Log($"[Psynergy] '{PsynergyName}' resucitó a {target.Name} con {reviveHp} PV.");
                }
            }
            else
            {
                // Defeated targets cannot be healed by standard healing psynergies
                return false;
            }
        }

        // 2. Handle HP Restoration for Living Targets
        if (restoresHp && !target.IsDead && target.CurrHp < target.MaxHp)
        {
            int amountToHeal = isPercentage
                ? Mathf.RoundToInt(target.MaxHp * (healValue / 100f))
                : healValue;

            if (target.RestoreHP(amountToHeal))
            {
                effectApplied = true;
                Debug.Log($"[Psynergy] '{PsynergyName}' restauró {amountToHeal} PV a {target.Name}.");
            }
        }

        // 3. Handle PP Restoration for Living Targets
        if (restoresPp && !target.IsDead && target.CurrPp < target.MaxPp)
        {
            int amountToRestore = isPpPercentage
                ? Mathf.RoundToInt(target.MaxPp * (ppHealValue / 100f))
                : ppHealValue;

            if (target.RestorePP(amountToRestore))
            {
                effectApplied = true;
                Debug.Log($"[Psynergy] '{PsynergyName}' restauró {amountToRestore} PP a {target.Name}.");
            }
        }

        // 4. Handle Status Cures & Cleansing
        if (!target.IsDead)
        {
            if (cureAllStatusEffects && target.ActiveStatusEffects.Count > 0)
            {
                target.ClearAllStatusEffects();
                effectApplied = true;
                Debug.Log($"[Psynergy] '{PsynergyName}' limpió TODOS los estados alterados en {target.Name}.");
            }
            else
            {
                // Purge by category (e.g. Ailments for Cure, Debuffs for Dispel)
                foreach (var category in categoriesToCure)
                {
                    int purgedCount = target.PurgeCategory(category);
                    if (purgedCount > 0)
                    {
                        effectApplied = true;
                        Debug.Log($"[Psynergy] '{PsynergyName}' eliminó {purgedCount} efecto(s) de categoría '{category}' en {target.Name}.");
                    }
                }

                // Purge specific status effects
                foreach (var statusSO in specificStatusToCure)
                {
                    if (target.RemoveStatusEffect(statusSO))
                    {
                        effectApplied = true;
                        Debug.Log($"[Psynergy] '{PsynergyName}' curó '{statusSO.EffectName}' en {target.Name}.");
                    }
                }
            }
        }

        // 5. Apply Bonus Status Effects / Shields (Opción A)
        if (!target.IsDead && statusEffectsToApply != null)
        {
            foreach (var statusSO in statusEffectsToApply)
            {
                if (statusSO != null && target.ApplyStatusEffect(statusSO, caster))
                {
                    effectApplied = true;
                    Debug.Log($"[Psynergy] '{PsynergyName}' aplicó el estado bonus '{statusSO.EffectName}' en {target.Name}.");
                }
            }
        }

        return effectApplied;
    }

    #endregion
}