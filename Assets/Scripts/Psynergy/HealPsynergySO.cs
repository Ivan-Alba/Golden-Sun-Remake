using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject defining healing, restoration, revival, and cleansing Psynergies (e.g., Cure, Ply, Aura, Restore).
/// Supports HP/PP restoration, revival, category-based status purges, specific status removals,
/// and optional bonus status applications (shields/buffs) exclusively during battle.
/// Triggers floating UI numbers directly via global Battle Event Bus.
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
    [Range(0f, 1f)]
    [Tooltip("Probability of successfully reviving a fallen ally (0.0 = 0%, 1.0 = 100%).")]
    [SerializeField] private float reviveSuccessChance = 1.0f;

    [Header("Status Effect Application (Battle Only Buffs / Shields)")]
    [Tooltip("Optional status effects or shields potentially applied to targets alongside healing during combat.")]
    [SerializeField] private List<StatusEffectApplication> statusEffectsToApply = new List<StatusEffectApplication>();

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
    public IReadOnlyList<StatusEffectApplication> StatusEffectsToApply => statusEffectsToApply;
    public IReadOnlyList<StatusEffectCategory> CategoriesToCure => categoriesToCure;
    public IReadOnlyList<StatusEffectSO> SpecificStatusToCure => specificStatusToCure;

    #endregion

    #region Editor Validation

    private void OnValidate()
    {
        if (healValue < 0) healValue = 0;
        if (ppHealValue < 0) ppHealValue = 0;
    }

    #endregion

    #region Execution Methods Override

    /// <summary>
    /// Executes healing, restoration, revival, and status cleansing logic in combat.
    /// Emits UI floating numbers for restored HP, restored PP, or Miss directly via GameEvents.
    /// </summary>
    public override bool ExecuteBattleEffect(FighterStats caster, IReadOnlyList<FighterStats> targets)
    {
        if (!IsBattlePsynergy)
        {
            Debug.LogWarning($"[Psynergy] '{PsynergyName}' is marked as a non-battle Psynergy and cannot be used in combat.");
            return false;
        }

        if (targets == null || targets.Count == 0 || caster == null) return false;

        bool anyEffectApplied = false;

        for (int i = 0; i < targets.Count; i++)
        {
            FighterStats target = targets[i];
            if (target == null) continue;

            // 1. Snapshot target stats before applying effects
            int hpBefore = target.CurrHp;
            int ppBefore = target.CurrPp;

            // 2. Execute restoration & cleansing logic
            bool targetAffected = ApplyHealEffectToTarget(caster, target, applyBuffs: true);

            int restoredHp = target.CurrHp - hpBefore;
            int restoredPp = target.CurrPp - ppBefore;

            if (targetAffected)
            {
                anyEffectApplied = true;

                // 3. Trigger HP restoration number (Handles Revival or Standard Healing)
                if (restoredHp > 0)
                {
                    GameEvents.Battle.TriggerDamageNumber(
                        target.gameObject,
                        restoredHp.ToString(),
                        DamageNumbersPresenter.DamageTextType.Healing
                    );
                }

                // 4. Trigger PP restoration number (Blue text)
                if (restoredPp > 0)
                {
                    GameEvents.Battle.TriggerDamageNumber(
                        target.gameObject,
                        restoredPp.ToString(),
                        DamageNumbersPresenter.DamageTextType.PPRecovery
                    );
                }
            }
            else
            {
                // 5. Trigger Miss text if no effect succeeded (e.g. revive roll failed or target was already at max stats)
                GameEvents.Battle.TriggerDamageNumber(
                    target.gameObject,
                    "0",
                    DamageNumbersPresenter.DamageTextType.Miss
                );
            }
        }

        return anyEffectApplied;
    }

    public override bool ExecuteMenuEffect(FighterStats caster, IReadOnlyList<FighterStats> targets)
    {
        if (!MenuData.CanBeUsedInMenu)
        {
            Debug.LogWarning($"[Psynergy] '{PsynergyName}' cannot be executed from the party menu.");
            return false;
        }

        if (targets == null || targets.Count == 0) return false;

        bool anyEffectApplied = false;

        for (int i = 0; i < targets.Count; i++)
        {
            FighterStats target = targets[i];
            if (target == null) continue;

            // In menu execution, we set applyBuffs to false to avoid triggering combat status effects/shields out of battle
            if (ApplyHealEffectToTarget(caster, target, applyBuffs: false))
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
    /// <param name="applyBuffs">If true, iterates and applies statusEffectsToApply (battle context only).</param>
    /// <returns>True if at least one effect succeeded on the target; otherwise false.</returns>
    private bool ApplyHealEffectToTarget(FighterStats caster, FighterStats target, bool applyBuffs)
    {
        bool effectApplied = false;

        // 1. Handle Revival for Dead Targets FIRST
        if (target.IsDead)
        {
            if (canRevive)
            {
                if (Random.value <= reviveSuccessChance)
                {
                    int reviveHp = isPercentage
                        ? Mathf.RoundToInt(target.MaxHp * (healValue / 100f))
                        : healValue;

                    if (target.Revive(reviveHp))
                    {
                        effectApplied = true;
                        Debug.Log($"[Psynergy] '{PsynergyName}' revived {target.Name} with {reviveHp} HP.");
                    }
                }
                else
                {
                    Debug.Log($"[Psynergy] '{PsynergyName}' failed to revive {target.Name}.");
                }
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
                Debug.Log($"[Psynergy] '{PsynergyName}' restored {amountToHeal} HP to {target.Name}.");
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
                Debug.Log($"[Psynergy] '{PsynergyName}' restored {amountToRestore} PP to {target.Name}.");
            }
        }

        // 4. Handle Status Cures & Cleansing
        if (!target.IsDead)
        {
            if (cureAllStatusEffects && target.ActiveStatusEffects.Count > 0)
            {
                target.ClearAllStatusEffects();
                effectApplied = true;
                Debug.Log($"[Psynergy] '{PsynergyName}' cleared ALL active status effects on {target.Name}.");
            }
            else
            {
                // Purge by category using non-alloc index loops
                for (int c = 0; c < categoriesToCure.Count; c++)
                {
                    StatusEffectCategory category = categoriesToCure[c];
                    int purgedCount = target.PurgeCategory(category);
                    if (purgedCount > 0)
                    {
                        effectApplied = true;
                        Debug.Log($"[Psynergy] '{PsynergyName}' purged {purgedCount} effect(s) of category '{category}' on {target.Name}.");
                    }
                }

                // Purge specific status effects
                for (int s = 0; s < specificStatusToCure.Count; s++)
                {
                    StatusEffectSO statusSO = specificStatusToCure[s];
                    if (statusSO != null && target.RemoveStatusEffect(statusSO))
                    {
                        effectApplied = true;
                        Debug.Log($"[Psynergy] '{PsynergyName}' cured status '{statusSO.EffectName}' on {target.Name}.");
                    }
                }
            }
        }

        // 5. Apply Bonus Status Effects / Shields (Combat Context Only)
        if (applyBuffs && !target.IsDead && statusEffectsToApply != null && statusEffectsToApply.Count > 0)
        {
            for (int e = 0; e < statusEffectsToApply.Count; e++)
            {
                StatusEffectApplication app = statusEffectsToApply[e];
                if (app.StatusEffect == null) continue;

                if (Random.value <= app.ApplicationChance)
                {
                    if (target.ApplyStatusEffect(app.StatusEffect, caster))
                    {
                        effectApplied = true;
                        Debug.Log($"[Psynergy] '{PsynergyName}' applied bonus status '{app.StatusEffect.EffectName}' to {target.Name}.");
                    }
                }
            }
        }

        return effectApplied;
    }

    #endregion
}