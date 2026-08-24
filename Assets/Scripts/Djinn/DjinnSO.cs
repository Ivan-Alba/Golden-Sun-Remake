using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Djinn_", menuName = "GoldenSun/Djinn/Djinn Asset")]
public class DjinnSO : ScriptableObject
{
    #region Basic Identity Properties

    [Header("Basic Identity")]
    [SerializeField] private string djinnName = "Flint";
    [SerializeField] private string djinnSlug = "flint";
    [SerializeField] private ElementalType element = ElementalType.Venus;
    [SerializeField] private Sprite icon;
    [TextArea(2, 4)]
    [SerializeField] private string description;

    #endregion

    #region Base Stat Bonuses (Applied when in SET state)

    [Header("Set State Stat Modifiers")]
    [Tooltip("Base stat bonuses granted to the assigned hero while this Djinn is in SET state.")]
    [SerializeField] private List<StatBonus> setStatModifiers = new List<StatBonus>();

    #endregion

    #region Battle Unleash Properties

    [Header("Battle Unleash Properties")]
    [Tooltip("Reference to the battle unleash effect asset executed when this Djinn is unleashed in combat.")]
    [SerializeField] private DjinnUnleashSO unleashEffect;

    [Tooltip("Base individual turns required to recover to SET state after being consumed in a Summon.")]
    [SerializeField] private int baseRecoveryTurns = 2;

    #endregion

    #region Leader Djinn Passive Perk

    [Header("Leader Djinn Passive Perk")]
    [Tooltip("Unique passive status effect applied to the hero ONLY when this Djinn is chosen as Leader AND is in SET state.")]
    [SerializeField] private StatusEffectSO leaderPassiveEffect;

    [TextArea(2, 3)]
    [SerializeField] private string leaderPassiveDescription;

    #endregion

    #region Public Properties

    public string DjinnName => djinnName;
    public string DjinnSlug => djinnSlug;
    public ElementalType Element => element;
    public Sprite Icon => icon;
    public string Description => description;

    public IReadOnlyList<StatBonus> SetStatModifiers => setStatModifiers;

    public int BaseRecoveryTurns => baseRecoveryTurns;

    public StatusEffectSO LeaderPassiveEffect => leaderPassiveEffect;
    public string LeaderPassiveDescription => leaderPassiveDescription;

    public DjinnUnleashSO UnleashEffect => unleashEffect;

    #endregion

    #region Helper Methods

    /// <summary>
    /// Devuelve la lista de StatModifiers generados por este Djinn para inyectar en Stat.cs o StatCalculator.
    /// </summary>
    public List<(StatType statType, StatModifier modifier)> GetStatModifiers()
    {
        var result = new List<(StatType, StatModifier)>();
        if (setStatModifiers == null) return result;

        for (int i = 0; i < setStatModifiers.Count; i++)
        {
            if (setStatModifiers[i] != null)
            {
                result.Add((setStatModifiers[i].statType, setStatModifiers[i].ToStatModifier(this, isTemporary: false)));
            }
        }
        return result;
    }

    #endregion
}