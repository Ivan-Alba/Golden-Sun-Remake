using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject defining tactical support Psynergies (e.g., Impact, High Impact, Guard, Resistance, Break, Delude).
/// Utilizes the modular StatusEffect system with application probabilities to apply buffs, debuffs, crowd control,
/// shields, or purge target effect categories in combat.
/// </summary>
[CreateAssetMenu(fileName = "NewSupportPsynergy", menuName = "Psynergy/Support Psynergy")]
public class SupportPsynergySO : PsynergySO
{
    #region Serialized Fields

    [Header("Status Effect Application (Buffs / Debuffs / CC)")]
    [Tooltip("Status effect templates with individual application probabilities applied to targets.")]
    [SerializeField] private List<StatusEffectApplication> statusEffectsToApply = new List<StatusEffectApplication>();

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

    public IReadOnlyList<StatusEffectApplication> StatusEffectsToApply => statusEffectsToApply;
    public int OverrideDurationTurns => overrideDurationTurns;
    public bool DispelsEnemyBuffs => dispelsEnemyBuffs;
    public IReadOnlyList<StatusEffectCategory> CategoriesToPurge => categoriesToPurge;
    public IReadOnlyList<StatusEffectSO> SpecificStatusToPurge => specificStatusToPurge;

    #endregion

    #region Editor Validation

    private void OnValidate()
    {
        if (overrideDurationTurns < -2) overrideDurationTurns = -2;
    }

    #endregion

    #region Execution Methods Override

    public override bool ExecuteBattleEffect(FighterStats caster, IReadOnlyList<FighterStats> targets)
    {
        if (!IsBattlePsynergy)
        {
            Debug.LogWarning($"[Psynergy] '{PsynergyName}' is marked as a non-battle Psynergy and cannot be used in combat.");
            return false;
        }

        if (targets == null || targets.Count == 0) return false;

        bool anyEffectApplied = false;

        for (int i = 0; i < targets.Count; i++)
        {
            FighterStats target = targets[i];
            if (target == null || target.IsDead) continue;

            // 1. Dispel Enemy Buffs (Break / Anular logic)
            if (dispelsEnemyBuffs)
            {
                int purgedBuffs = target.PurgeCategory(StatusEffectCategory.Buff);
                if (purgedBuffs > 0)
                {
                    anyEffectApplied = true;
                    Debug.Log($"[Psynergy] '{PsynergyName}' dispelled {purgedBuffs} buff(s) on {target.Name}.");
                }
            }

            // 2. Purge Additional Categories (e.g., Debuffs on allies)
            for (int c = 0; c < categoriesToPurge.Count; c++)
            {
                StatusEffectCategory category = categoriesToPurge[c];
                int purged = target.PurgeCategory(category);
                if (purged > 0)
                {
                    anyEffectApplied = true;
                    Debug.Log($"[Psynergy] '{PsynergyName}' purged {purged} effect(s) of category '{category}' on {target.Name}.");
                }
            }

            // 3. Purge Specific Status Effects
            for (int s = 0; s < specificStatusToPurge.Count; s++)
            {
                StatusEffectSO statusSO = specificStatusToPurge[s];
                if (statusSO != null && target.RemoveStatusEffect(statusSO))
                {
                    anyEffectApplied = true;
                    Debug.Log($"[Psynergy] '{PsynergyName}' purged status '{statusSO.EffectName}' on {target.Name}.");
                }
            }

            // 4. Apply Status Effects / Buffs / Debuffs using non-alloc probability rolls
            for (int e = 0; e < statusEffectsToApply.Count; e++)
            {
                StatusEffectApplication app = statusEffectsToApply[e];
                if (app.StatusEffect == null) continue;

                if (Random.value <= app.ApplicationChance)
                {
                    bool applied = target.ApplyStatusEffect(app.StatusEffect, caster, overrideDurationTurns);
                    if (applied)
                    {
                        anyEffectApplied = true;
                        Debug.Log($"[Psynergy] '{PsynergyName}' applied status '{app.StatusEffect.EffectName}' to {target.Name}.");
                    }
                }
            }
        }

        return anyEffectApplied;
    }

    public override bool ExecuteMenuEffect(FighterStats caster, IReadOnlyList<FighterStats> targets)
    {
        Debug.LogWarning($"[Psynergy] '{PsynergyName}' is a tactical combat support Psynergy and cannot be used from the pause menu.");
        return false;
    }

    #endregion
}