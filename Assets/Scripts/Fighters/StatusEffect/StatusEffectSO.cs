using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject defining the static template and parameters for a status effect, buff, debuff, or ailment.
/// </summary>
[CreateAssetMenu(fileName = "StatusEffect_", menuName = "GoldenSun/Status Effects/Status Effect")]
public class StatusEffectSO : ScriptableObject
{
    #region Identity & Metadata

    [Header("Identity & Display")]
    [Tooltip("Unique technical slug identifier (e.g., 'poison', 'atk_buff', 'sleep').")]
    [SerializeField] private string effectSlug;

    [Tooltip("In-game display name.")]
    [SerializeField] private string effectName;

    [Tooltip("UI Icon displayed on character status bar or combat HUD.")]
    [SerializeField] private Sprite icon;

    [TextArea(2, 4)]
    [Tooltip("Detailed description of what this effect does.")]
    [SerializeField] private string description;

    [Tooltip("Broad category used for filtering, UI display, and cure/purge abilities.")]
    [SerializeField] private StatusEffectCategory category = StatusEffectCategory.Ailment;

    [Tooltip("Primary functional mechanics executed by this effect.")]
    [SerializeField] private StatusEffectType effectType = StatusEffectType.StatModifier;

    #endregion

    #region Persistence & Overworld Behavior

    [Header("Combat & Overworld Persistence")]
    [Tooltip("If true, this status effect persists after combat ends (e.g., Poison, Haunt). If false, it is automatically cleared when battle finishes.")]
    [SerializeField] private bool persistsAfterBattle = false;

    [Tooltip("Does this effect execute periodic ticks while walking in the overworld? (e.g., Poison damage every X steps).")]
    [SerializeField] private bool ticksInOverworld = false;

    [Tooltip("Number of overworld steps required to trigger one tick of this effect (e.g., 4 steps for poison damage).")]
    [SerializeField] private int stepsPerOverworldTick = 4;

    #endregion

    #region Duration & Stacking Rules

    [Header("Duration & Stacking")]
    [Tooltip("Default duration in turns. Set to -1 for permanent until cured or end of battle.")]
    [SerializeField] private int baseDurationTurns = 3;

    [Tooltip("Can multiple applications of this same effect stack on a single fighter?")]
    [SerializeField] private bool isStackable = false;

    [Tooltip("Maximum allowed stacks if stackable is enabled.")]
    [SerializeField] private int maxStacks = 1;

    #endregion

    #region Recovery & Removal Conditions

    [Header("Natural Recovery & Removal")]
    [Range(0f, 100f)]
    [Tooltip("Percentage chance (0-100%) to automatically cure at the start of the turn.")]
    [SerializeField] private float autoCureChancePerTurn = 0f;

    [Tooltip("Does receiving physical damage immediately break/remove this effect? (e.g., Sleep).")]
    [SerializeField] private bool cureOnPhysicalDamage = false;

    #endregion

    #region Specialized Effect Configurations

    [Header("Incapacitation / Crowd Control Settings")]
    [Tooltip("Type of crowd control applied if EffectType is Incapacitate.")]
    [SerializeField] private IncapacitateType incapacitateType = IncapacitateType.None;

    [Header("Damage Mitigation Settings")]
    [Range(0f, 100f)]
    [Tooltip("Percentage of incoming damage reduced (e.g., 50% for shields or Granite).")]
    [SerializeField] private float damageMitigationPercentage = 0f;

    [Header("Doom Settings")]
    [Tooltip("Number of turns until instant K.O. when EffectType is DoomCountdown.")]
    [SerializeField] private int doomTurnCount = 3;

    #endregion

    #region Modular Feature Lists

    [Header("Stat Modifiers")]
    [SerializeField] private List<StatusStatBonus> statModifiers = new List<StatusStatBonus>();

    [Header("Elemental Power & Resistance Modifiers")]
    [SerializeField] private List<StatusElementalBonus> elementalModifiers = new List<StatusElementalBonus>();

    [Header("Resource Regeneration / Drain Over Time")]
    [SerializeField] private List<StatusResourceRegen> resourceRegens = new List<StatusResourceRegen>();

    [Header("Damage Over Time")]
    [SerializeField] private List<StatusDamageOverTime> damagesOverTime = new List<StatusDamageOverTime>();

    #endregion

    #region Properties

    public string EffectSlug => effectSlug;
    public string EffectName => effectName;
    public Sprite Icon => icon;
    public string Description => description;
    public StatusEffectCategory Category => category;
    public StatusEffectType EffectType => effectType;

    public bool PersistsAfterBattle => persistsAfterBattle;
    public bool TicksInOverworld => ticksInOverworld;
    public int StepsPerOverworldTick => stepsPerOverworldTick;

    public int BaseDurationTurns => baseDurationTurns;
    public bool IsStackable => isStackable;
    public int MaxStacks => maxStacks;

    public float AutoCureChancePerTurn => autoCureChancePerTurn;
    public bool CureOnPhysicalDamage => cureOnPhysicalDamage;

    public IncapacitateType IncapacitateType => incapacitateType;
    public float DamageMitigationPercentage => damageMitigationPercentage;
    public int DoomTurnCount => doomTurnCount;

    public IReadOnlyList<StatusStatBonus> StatModifiers => statModifiers;
    public IReadOnlyList<StatusElementalBonus> ElementalModifiers => elementalModifiers;
    public IReadOnlyList<StatusResourceRegen> ResourceRegens => resourceRegens;
    public IReadOnlyList<StatusDamageOverTime> DamagesOverTime => damagesOverTime;

    #endregion
}