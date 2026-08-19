using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject defining support and tactical Psynergies (e.g., Impact, High Impact, Guard, Resistance, Break, Delude).
/// Utilizes the central StatusEffect system to apply buffs, debuffs, crowd control, shields, or purge target categories.
/// </summary>
[CreateAssetMenu(fileName = "NewSupportPsynergy", menuName = "Psynergy/Support Psynergy")]
public class SupportPsynergySO : PsynergySO
{
    #region Serialized Fields

    [Header("Status Effect Application (Buffs / Debuffs / CC)")]
    [Tooltip("Status effect templates applied to targets upon executing this psynergy.")]
    [SerializeField] private List<StatusEffectSO> statusEffectsToApply = new List<StatusEffectSO>();

    [Tooltip("Optional custom duration override in turns. Set to -2 to use default duration defined inside each StatusEffectSO.")]
    [SerializeField] private int overrideDurationTurns = -2;

    [Header("Status Dispel & Purge Properties")]
    [Tooltip("If true, purges ALL positive Buff category status effects from targets (e.g., Break / Anular).")]
    [SerializeField] private bool dispelsEnemyBuffs = false;

    [Tooltip("Broad categories of status effects to purge from targets (e.g., Buffs, Debuffs).")]
    [SerializeField] private List<StatusEffectCategory> categoriesToPurge = new List<StatusEffectCategory>();

    [Tooltip("Specific status effect templates to remove from targets.")]
    [SerializeField] private List<StatusEffectSO> specificStatusToPurge = new List<StatusEffectSO>();

    #endregion

    #region Public Properties

    public IReadOnlyList<StatusEffectSO> StatusEffectsToApply => statusEffectsToApply;
    public int OverrideDurationTurns => overrideDurationTurns;
    public bool DispelsEnemyBuffs => dispelsEnemyBuffs;
    public IReadOnlyList<StatusEffectCategory> CategoriesToPurge => categoriesToPurge;
    public IReadOnlyList<StatusEffectSO> SpecificStatusToPurge => specificStatusToPurge;

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
            if (target == null || target.IsDead) continue;

            // 1. Dispel Enemy Buffs (Break / Anular logic)
            if (dispelsEnemyBuffs)
            {
                int purgedBuffs = target.PurgeCategory(StatusEffectCategory.Buff);
                if (purgedBuffs > 0)
                {
                    anyEffectApplied = true;
                    Debug.Log($"[Psynergy] '{PsynergyName}' disipó {purgedBuffs} potenciador(es) en {target.Name}.");
                }
            }

            // 2. Purge Additional Categories (e.g., Debuffs on allies)
            foreach (var category in categoriesToPurge)
            {
                int purged = target.PurgeCategory(category);
                if (purged > 0)
                {
                    anyEffectApplied = true;
                    Debug.Log($"[Psynergy] '{PsynergyName}' eliminó {purged} efecto(s) de categoría '{category}' en {target.Name}.");
                }
            }

            // 3. Purge Specific Status Effects
            foreach (var statusSO in specificStatusToPurge)
            {
                if (statusSO != null && target.RemoveStatusEffect(statusSO))
                {
                    anyEffectApplied = true;
                    Debug.Log($"[Psynergy] '{PsynergyName}' eliminó el estado '{statusSO.EffectName}' en {target.Name}.");
                }
            }

            // 4. Apply Status Effects / Buffs / Debuffs
            if (statusEffectsToApply != null)
            {
                foreach (var statusSO in statusEffectsToApply)
                {
                    if (statusSO != null)
                    {
                        bool applied = target.ApplyStatusEffect(statusSO, caster, overrideDurationTurns);
                        if (applied)
                        {
                            anyEffectApplied = true;
                            Debug.Log($"[Psynergy] '{PsynergyName}' aplicó el estado '{statusSO.EffectName}' en {target.Name}.");
                        }
                    }
                }
            }
        }

        return anyEffectApplied;
    }

    public override bool ExecuteMenuEffect(FighterStats caster, List<FighterStats> targets)
    {
        Debug.LogWarning($"[Psynergy] '{PsynergyName}' es una psinergía de soporte de combate y no puede usarse desde el menú.");
        return false;
    }

    #endregion
}