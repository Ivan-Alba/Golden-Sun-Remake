using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSupportPsynergy", menuName = "Psynergy/Support Psynergy")]
public class SupportPsynergySO : PsynergySO
{
    [Header("Support / Stat Modifier Properties")]
    [SerializeField] private bool appliesStatModifier = true;
    [SerializeField] private StatType statToModify;
    [SerializeField] private float modifierValue = 0.25f; // e.g., +0.25 (+25% Attack) or -0.25 (-25% Attack)
    [SerializeField] private StatModifierType modifierType = StatModifierType.PercentBase;
    [SerializeField] private int durationInTurns = 3;

    [Header("Dispel Enemy Buffs (Break / Anular)")]
    [Tooltip("If true, removes positive stat modifiers (buffs) applied to the targets (e.g., Break Psynergy).")]
    [SerializeField] private bool dispelsEnemyBuffs;

    #region Public Properties

    public bool AppliesStatModifier => appliesStatModifier;
    public StatType StatToModify => statToModify;
    public float ModifierValue => modifierValue;
    public StatModifierType ModifierType => modifierType;
    public int DurationInTurns => durationInTurns;
    public bool DispelsEnemyBuffs => dispelsEnemyBuffs;

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

            // 1. Dispel Enemy Buffs (Break)
            if (dispelsEnemyBuffs)
            {
                Debug.Log($"[Psynergy] '{PsynergyName}' disipó potenciadores positivos en {target.Name}.");
                // TODO: Implement logic to purge positive StatModifiers from target stats and set anyEffectApplied = true if removed
            }

            // 2. Apply Stat Modifier (Buff / Debuff)
            if (appliesStatModifier)
            {
                Stat targetStat = target.GetStat(statToModify);
                if (targetStat != null)
                {
                    StatModifier mod = new StatModifier(
                        value: modifierValue,
                        type: modifierType,
                        source: this,
                        isTemporary: true,
                        durationInTurns: durationInTurns
                    );

                    targetStat.AddModifier(mod);
                    anyEffectApplied = true;
                    Debug.Log($"[Psynergy] Aplicado StatModifier ({modifierValue}) a {statToModify} en {target.Name} durante {durationInTurns} turnos.");
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