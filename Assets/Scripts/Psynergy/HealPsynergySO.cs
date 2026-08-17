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

    public override void ExecuteBattleEffect(FighterStats caster, List<FighterStats> targets)
    {
        base.ExecuteBattleEffect(caster, targets);

        if (targets == null || targets.Count == 0) return;

        foreach (FighterStats target in targets)
        {
            if (target == null) continue;

            // 1. Revive Logic
            if (target.IsDead)
            {
                if (canRevive)
                {
                    int reviveHp = isPercentage ? Mathf.RoundToInt(target.MaxHp * (healValue / 100f)) : healValue;
                    target.Revive(reviveHp);
                    Debug.Log($"[Psynergy] '{PsynergyName}' revived {target.Name} with {reviveHp} HP.");
                }
                else
                {
                    Debug.LogWarning($"[Psynergy] '{PsynergyName}' cannot target dead ally {target.Name}.");
                    continue;
                }
            }
            // 2. HP Restoration Logic for Living Targets
            else if (restoresHp)
            {
                int amountToHeal = isPercentage ? Mathf.RoundToInt(target.MaxHp * (healValue / 100f)) : healValue;
                target.RestoreHP(amountToHeal);
                Debug.Log($"[Psynergy] '{PsynergyName}' restored {amountToHeal} HP to {target.Name}.");
            }

            // 3. PP Restoration Logic
            if (restoresPp && !target.IsDead)
            {
                target.RestorePP(ppHealValue);
                Debug.Log($"[Psynergy] '{PsynergyName}' restored {ppHealValue} PP to {target.Name}.");
            }

            // 4. Status Effects Cleansing
            if (curesStatusEffects && !target.IsDead)
            {
                Debug.Log($"[Psynergy] '{PsynergyName}' cleansed status effects from {target.Name}.");
                // TODO: Call target.CureStatusEffects() when StatusEffect system is implemented
            }

            // 5. Debuff Cleansing (Negative Stat Modifiers)
            if (removesStatDebuffs && !target.IsDead)
            {
                Debug.Log($"[Psynergy] '{PsynergyName}' removed negative stat debuffs from {target.Name}.");
                // TODO: Implement logic to clear negative StatModifiers from target stats
            }
        }
    }
}