using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject defining static data, stat bonuses, battle unleash effects, 
/// and Leader Passive perks for a Golden Sun Djinn.
/// </summary>
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
    [SerializeField] private List<EquipmentStatBonus> setStatModifiers = new List<EquipmentStatBonus>();

    #endregion

    #region Battle Unleash Properties

    [Header("Battle Unleash Properties")]
    [Tooltip("Reference to the battle unleash effect asset executed when this Djinn is unleashed in combat.")]
    [SerializeField] private DjinnUnleashSO unleashEffect;

    [Tooltip("Base individual turns required to recover to SET state after being consumed in a Summon.")]
    [SerializeField] private int baseRecoveryTurns = 2;

    #endregion

    #region New Feature: Leader Djinn Passive Perk

    [Header("Leader Djinn Passive Perk (New Feature)")]
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

    public IReadOnlyList<EquipmentStatBonus> SetStatModifiers => setStatModifiers;

    public int BaseRecoveryTurns => baseRecoveryTurns;

    public StatusEffectSO LeaderPassiveEffect => leaderPassiveEffect;
    public string LeaderPassiveDescription => leaderPassiveDescription;

    public DjinnUnleashSO UnleashEffect { get => unleashEffect; }

    #endregion
}