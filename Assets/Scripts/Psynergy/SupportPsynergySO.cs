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

    public override void ExecuteBattleEffect(FighterStats caster, List<FighterStats> targets)
    {
        base.ExecuteBattleEffect(caster, targets);

        if (targets == null || targets.Count == 0) return;

        foreach (FighterStats target in targets)
        {
            if (target == null || target.IsDead) continue;

            // 1. Dispel Enemy Buffs (Break)
            if (dispelsEnemyBuffs)
            {
                Debug.Log($"[Psynergy] '{PsynergyName}' dispelled positive stat buffs on {target.Name}.");
                // TODO: Implement logic to purge positive StatModifiers from target stats
            }

            // 2. Apply Stat Modifier (Buff / Debuff)
            if (appliesStatModifier)
            {
                Stat targetStat = target.GetStat(statToModify);
                if (targetStat != null)
                {
                    // Create temporary StatModifier referencing this Psynergy as Source
                    StatModifier mod = new StatModifier(
                        value: modifierValue,
                        type: modifierType,
                        source: this,
                        isTemporary: true,
                        durationInTurns: durationInTurns
                    );

                    targetStat.AddModifier(mod);
                    Debug.Log($"[Psynergy] Applied StatModifier ({modifierValue}) to {statToModify} on {target.Name} for {durationInTurns} turns.");
                }
            }
        }
    }
}