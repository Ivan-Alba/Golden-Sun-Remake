using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewHealPsynergy", menuName = "Psynergy/Heal Psynergy")]
public class HealPsynergySO : PsynergySO
{
    [Header("Heal HP Properties")]
    [SerializeField] private bool restoresHp = true;
    [SerializeField] private int healValue = 50;
    [SerializeField] private bool isPercentage;

    [Header("Heal PP Properties (Optional)")]
    [SerializeField] private bool restoresPp;
    [SerializeField] private int ppHealValue = 10;

    [Header("Revive Properties")]
    [SerializeField] private bool canRevive;

    [Header("Status & Debuff Cleansing")]
    [SerializeField] private bool curesStatusEffects;
    // TODO: Add specific status effect categories when StatusEffect system is built (e.g., Poison, Paralysis, Curse, All)
    [SerializeField] private bool removesStatDebuffs;

    #region Public Properties

    public bool RestoresHp => restoresHp;
    public int HealValue => healValue;
    public bool IsPercentage => isPercentage;
    public bool RestoresPp => restoresPp;
    public int PpHealValue => ppHealValue;
    public bool CanRevive => canRevive;
    public bool CuresStatusEffects => curesStatusEffects;
    public bool RemovesStatDebuffs => removesStatDebuffs;

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

            // 1. Revive Logic
            if (target.IsDead)
            {
                if (canRevive)
                {
                    int reviveHp = isPercentage ? Mathf.RoundToInt(target.MaxHp * (healValue / 100f)) : healValue;
                    if (target.Revive(reviveHp))
                    {
                        anyEffectApplied = true;
                        Debug.Log($"[Psynergy] '{PsynergyName}' resucitó a {target.Name} con {reviveHp} PV.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[Psynergy] '{PsynergyName}' no puede seleccionar al aliado caído {target.Name}.");
                    continue;
                }
            }
            // 2. HP Restoration Logic for Living Targets
            else if (restoresHp)
            {
                int amountToHeal = isPercentage ? Mathf.RoundToInt(target.MaxHp * (healValue / 100f)) : healValue;
                if (target.RestoreHP(amountToHeal))
                {
                    anyEffectApplied = true;
                    Debug.Log($"[Psynergy] '{PsynergyName}' restauró {amountToHeal} PV a {target.Name}.");
                }
            }

            // 3. PP Restoration Logic
            if (restoresPp && !target.IsDead)
            {
                if (target.RestorePP(ppHealValue))
                {
                    anyEffectApplied = true;
                    Debug.Log($"[Psynergy] '{PsynergyName}' restauró {ppHealValue} PP a {target.Name}.");
                }
            }

            // 4. Status Effects Cleansing
            if (curesStatusEffects && !target.IsDead)
            {
                if (target.CureStatusEffect())
                {
                    anyEffectApplied = true;
                    Debug.Log($"[Psynergy] '{PsynergyName}' curó estados alterados en {target.Name}.");
                }
            }

            // 5. Debuff Cleansing (Negative Stat Modifiers)
            if (removesStatDebuffs && !target.IsDead)
            {
                Debug.Log($"[Psynergy] '{PsynergyName}' eliminó debuffs de {target.Name}.");
                // TODO: Implement logic to clear negative StatModifiers from target stats and set anyEffectApplied = true if removed
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

            // 1. Revive Logic
            if (target.IsDead)
            {
                if (canRevive)
                {
                    int reviveHp = isPercentage ? Mathf.RoundToInt(target.MaxHp * (healValue / 100f)) : healValue;
                    if (target.Revive(reviveHp))
                    {
                        anyEffectApplied = true;
                        Debug.Log($"[Psynergy] '{PsynergyName}' resucitó a {target.Name} con {reviveHp} PV.");
                    }
                }
            }
            // 2. HP Restoration Logic
            else if (restoresHp)
            {
                int amountToHeal = isPercentage ? Mathf.RoundToInt(target.MaxHp * (healValue / 100f)) : healValue;
                if (target.RestoreHP(amountToHeal))
                {
                    anyEffectApplied = true;
                    Debug.Log($"[Psynergy] '{PsynergyName}' restauró {amountToHeal} PV a {target.Name}.");
                }
            }

            // 3. PP Restoration Logic
            if (restoresPp && !target.IsDead)
            {
                if (target.RestorePP(ppHealValue))
                {
                    anyEffectApplied = true;
                    Debug.Log($"[Psynergy] '{PsynergyName}' restauró {ppHealValue} PP a {target.Name}.");
                }
            }

            // 4. Status Effects Cleansing
            if (curesStatusEffects && !target.IsDead)
            {
                if (target.CureStatusEffect())
                {
                    anyEffectApplied = true;
                    Debug.Log($"[Psynergy] '{PsynergyName}' curó estados alterados en {target.Name}.");
                }
            }

            // 5. Debuff Cleansing
            if (removesStatDebuffs && !target.IsDead)
            {
                // TODO: Clear debuffs and set anyEffectApplied = true if any were removed
            }
        }

        return anyEffectApplied;
    }

    #endregion
}