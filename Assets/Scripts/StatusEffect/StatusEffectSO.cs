using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Core ScriptableObject template defining static rules, modifiers, and mechanics for status effects,
/// buffs, debuffs, ailments, and crowd control conditions in Golden Sun.
/// </summary>
[CreateAssetMenu(fileName = "StatusEffect_", menuName = "GoldenSun/Status Effects/Status Effect")]
public class StatusEffectSO : ScriptableObject
{
    #region Identity & Metadata

    [Header("Identity & Display")]
    [Tooltip("Unique technical slug identifier (e.g., 'poison', 'atk_buff', 'delusion', 'psynergy_seal').")]
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
    [Tooltip("If true, this status effect persists after combat ends (e.g., Poison, Haunt). If false, cleared when battle ends.")]
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

    #region Natural Recovery & Removal Conditions

    [Header("Natural Recovery & Removal")]
    [Range(0f, 1f)]
    [Tooltip("Normalized probability (0.0 - 1.0) to automatically cure at the start of the turn.")]
    [SerializeField] private float autoCureChancePerTurn = 0f;

    [Tooltip("Does receiving physical damage immediately break/remove this effect? (e.g., Sleep).")]
    [SerializeField] private bool cureOnPhysicalDamage = false;

    #endregion

    #region Specialized Mechanics (Golden Sun Classic)

    [Header("Delusion / Illusion Settings (Accuracy Penalty)")]
    [Range(0f, 1f)]
    [Tooltip("Chance (0.0 to 1.0) that physical attacks miss while affected by Delusion.")]
    [SerializeField] private float missChancePhysical = 0f;

    [Range(0f, 1f)]
    [Tooltip("Chance (0.0 to 1.0) that Psynergy spells fail or miss while affected by Delusion.")]
    [SerializeField] private float missChancePsynergy = 0f;

    [Header("Command Seal / Silence Settings")]
    [Tooltip("Prevents casting any Psynergy spells while active.")]
    [SerializeField] private bool sealPsynergy = false;

    [Tooltip("Prevents performing basic physical attacks while active.")]
    [SerializeField] private bool sealPhysicalAttacks = false;

    [Tooltip("Prevents using items from inventory during battle.")]
    [SerializeField] private bool sealItemUsage = false;

    [Tooltip("Prevents unleashing, setting, or swapping Djinn during battle.")]
    [SerializeField] private bool sealDjinnUsage = false;

    [Header("Incapacitation / Crowd Control Settings")]
    [Tooltip("Type of crowd control applied if EffectType is Incapacitate.")]
    [SerializeField] private IncapacitateType incapacitateType = IncapacitateType.None;

    [Header("Damage Mitigation & Aggro Settings")]
    [Range(0f, 1f)]
    [Tooltip("Normalized percentage of incoming damage reduced (e.g., 0.5 = 50% reduction for Granite/Shields).")]
    [SerializeField] private float damageMitigationPercentage = 0f;

    [Tooltip("Threat multiplier applied to target targeting weight (e.g., 2.0 = Taunt / double target priority).")]
    [SerializeField] private float aggroMultiplier = 1.0f;

    [Header("Reflection & Elemental Absorption Settings")]
    [Range(0f, 1f)]
    [Tooltip("Chance (0.0 to 1.0) to reflect targeted offensive Psynergies back to the caster.")]
    [SerializeField] private float reflectChance = 0f;

    [Tooltip("If true, incoming damage matching character's innate element converts to HP healing.")]
    [SerializeField] private bool elementalAbsorb = false;

    [Header("Doom Settings")]
    [Tooltip("Number of turns until instant K.O. when EffectType is DoomCountdown.")]
    [SerializeField] private int doomTurnCount = 3;

    #endregion

    #region Modular Feature Lists

    [Header("Stat & Elemental Modifiers")]
    [Tooltip("Unified list of stat modifiers, elemental power boosts, and elemental resistances.")]
    [SerializeField] private List<StatBonus> statModifiers = new List<StatBonus>();

    [Header("Resource Regeneration / Drain Over Time")]
    [SerializeField] private List<StatusResourceRegen> resourceRegens = new List<StatusResourceRegen>();

    [Header("Damage Over Time")]
    [SerializeField] private List<StatusDamageOverTime> damagesOverTime = new List<StatusDamageOverTime>();

    #endregion

    #region Public Properties

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

    // Delusion & Accuracy
    public float MissChancePhysical => missChancePhysical;
    public float MissChancePsynergy => missChancePsynergy;

    // Command Seals
    public bool SealPsynergy => sealPsynergy;
    public bool SealPhysicalAttacks => sealPhysicalAttacks;
    public bool SealItemUsage => sealItemUsage;
    public bool SealDjinnUsage => sealDjinnUsage;

    // Combat Defense & Threat
    public IncapacitateType IncapacitateType => incapacitateType;
    public float DamageMitigationPercentage => damageMitigationPercentage;
    public float AggroMultiplier => aggroMultiplier;

    // Reflection & Absorb
    public float ReflectChance => reflectChance;
    public bool ElementalAbsorb => elementalAbsorb;

    // Doom
    public int DoomTurnCount => doomTurnCount;

    // Read-Only Feature Collections
    public IReadOnlyList<StatBonus> StatModifiers => statModifiers;
    public IReadOnlyList<StatusResourceRegen> ResourceRegens => resourceRegens;
    public IReadOnlyList<StatusDamageOverTime> DamagesOverTime => damagesOverTime;

    #endregion

    #region Editor Validation

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(effectSlug) && !string.IsNullOrWhiteSpace(effectName))
        {
            effectSlug = effectName.ToLowerInvariant().Replace(" ", "_");
        }

        if (stepsPerOverworldTick < 1) stepsPerOverworldTick = 1;
        if (maxStacks < 1) maxStacks = 1;
        if (aggroMultiplier < 0f) aggroMultiplier = 0f;
    }

    #endregion
}