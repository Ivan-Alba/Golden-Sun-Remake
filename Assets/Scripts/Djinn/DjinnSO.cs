using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// ScriptableObject defining an individual Djinn asset in Golden Sun.
/// Holds elemental attributes, base stat modifiers granted in SET state,
/// combat unleash effects, and leader passive status effects.
/// </summary>
[CreateAssetMenu(fileName = "Djinn_", menuName = "GoldenSun/Djinn/Djinn Asset")]
public class DjinnSO : ScriptableObject
{
    #region Basic Identity Properties

    [Header("Basic Identity")]
    [Tooltip("Localized name of the Djinn displayed in menus and combat.")]
    [SerializeField] private LocalizedString djinnName;

    [Tooltip("Unique string identifier used for database lookups and save data transactions.")]
    [SerializeField] private string djinnSlug = "flint";

    [Tooltip("Elemental alignment of this Djinn (Venus, Mars, Jupiter, Mercury).")]
    [SerializeField] private ElementalType element = ElementalType.Venus;

    [Tooltip("UI Icon displayed in party status, Djinn management, and battle menus.")]
    [SerializeField] private Sprite icon;

    [Tooltip("Localized detailed lore/description of this Djinn.")]
    [SerializeField] private LocalizedString description;

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

    #endregion

    #region Public Properties

    public LocalizedString DjinnNameLocalizedString => djinnName;
    public string DjinnName => djinnName != null ? djinnName.GetLocalizedString() : string.Empty;

    public string DjinnSlug => djinnSlug;
    public ElementalType Element => element;
    public Sprite Icon => icon;

    public LocalizedString DescriptionLocalizedString => description;
    public string Description => description != null ? description.GetLocalizedString() : string.Empty;

    public IReadOnlyList<StatBonus> SetStatModifiers => setStatModifiers;

    public int BaseRecoveryTurns => baseRecoveryTurns;

    public StatusEffectSO LeaderPassiveEffect => leaderPassiveEffect;

    public DjinnUnleashSO UnleashEffect => unleashEffect;

    #endregion

    #region Unity Editor Validation

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Enforce minimum recovery turn threshold
        if (baseRecoveryTurns < 1)
        {
            baseRecoveryTurns = 1;
        }

        // Auto-generate fallback slug if empty
        if (string.IsNullOrWhiteSpace(djinnSlug) && djinnName != null)
        {
            djinnSlug = name.Replace("Djinn_", string.Empty).ToLowerInvariant();
        }
    }
#endif

    #endregion

    #region Helper & Calculation Methods

    /// <summary>
    /// Generates a list of tuples containing StatType and instantiated StatModifier for injection into StatCalculator.
    /// </summary>
    public List<(StatType statType, StatModifier modifier)> GetStatModifiers()
    {
        var result = new List<(StatType, StatModifier)>();
        GetStatModifiersNonAlloc(result);
        return result;
    }

    /// <summary>
    /// Zero-allocation variant to populate an existing container with generated StatModifiers.
    /// </summary>
    public void GetStatModifiersNonAlloc(List<(StatType statType, StatModifier modifier)> resultsContainer)
    {
        if (resultsContainer == null) return;
        resultsContainer.Clear();

        if (setStatModifiers == null) return;

        for (int i = 0; i < setStatModifiers.Count; i++)
        {
            var bonus = setStatModifiers[i];
            if (bonus != null)
            {
                resultsContainer.Add((bonus.statType, bonus.ToStatModifier(this, isTemporary: false)));
            }
        }
    }

    #endregion
}