using System;
using UnityEngine;

#region Enums

/// <summary>
/// Broad classification of status effects used for filtering, UI display, and removal abilities (e.g., Antidote, Purify, Dispel).
/// </summary>
public enum StatusEffectCategory
{
    Buff,           // Beneficial temporary effect (e.g., +25% ATK, HP/PP Regen, Shields)
    Debuff,         // Harmful stat drop or weakness (e.g., -20% DEF, Elemental Resistance reduction)
    Ailment         // Severe physical or mental condition (e.g., Poison, Sleep, Stun, Doom)
}

/// <summary>
/// Functional mechanics that a status effect feature can execute.
/// </summary>
public enum StatusEffectType
{
    StatModifier,           // Modifies primary attributes (ATK, DEF, AGI, LUCK, MaxHP, MaxPP)
    ElementalResist,        // Modifies elemental power or resistance (Venus, Mars, Jupiter, Mercury)
    ResourceRegenOverTime,  // Restores or drains HP/PP every turn
    DamageOverTime,         // Deals physical or elemental damage at start/end of turn (e.g., Poison)
    Incapacitate,           // Restricts actions (e.g., Stun, Sleep, Freeze)
    DamageMitigation,       // Reduces incoming damage by percentage (e.g., Granite, Protection)
    DoomCountdown,          // Decrements a counter each turn; causes instant K.O. when reaching 0
    Delusion,               // Accuracy penalty causing physical or Psynergy attacks to miss
    CommandSeal             // Blocks specific action commands (Psynergy, Basic Attacks, Items, Djinn)
}

/// <summary>
/// Target resource for regeneration or drain over time.
/// </summary>
public enum ResourceTarget
{
    HP,
    PP
}

/// <summary>
/// Defines specific crowd control restrictions when Incapacitated.
/// </summary>
public enum IncapacitateType
{
    None,
    Stun,       // Cannot perform any action
    Sleep,      // Cannot act; wakes up when taking physical damage or via chance
    Freeze,     // Cannot act; frozen in ice
    Silence,    // Can attack or use items, but cannot cast Psynergy
    Paralysis   // Cannot perform any action; periodic chance to break per turn
}

#endregion

#region Serializable Feature Structures

/// <summary>
/// Encapsulates a status effect application payload with an explicit trigger probability.
/// Embedded in Psynergies, Weapon Unleashes, Djinn abilities, and Consumables.
/// </summary>
[Serializable]
public struct StatusEffectApplication
{
    [Tooltip("Target status effect template to apply.")]
    [SerializeField] private StatusEffectSO statusEffect;

    [Range(0f, 1f)]
    [Tooltip("Probability of successfully applying this status effect on hit (0.0 = 0%, 1.0 = 100%).")]
    [SerializeField] private float applicationChance;

    public StatusEffectSO StatusEffect => statusEffect;
    public float ApplicationChance => applicationChance;

    public StatusEffectApplication(StatusEffectSO statusEffect, float applicationChance = 1.0f)
    {
        this.statusEffect = statusEffect;
        this.applicationChance = Mathf.Clamp01(applicationChance);
    }
}

/// <summary>
/// Configuration for resource regeneration or drain over time (HP/PP per turn).
/// </summary>
[Serializable]
public class StatusResourceRegen
{
    [Tooltip("Target resource (HP or PP).")]
    public ResourceTarget targetResource = ResourceTarget.HP;

    [Tooltip("Calculation type: Flat points or Percentage of Max Resource.")]
    public StatModifierType valueType = StatModifierType.Flat;

    [Tooltip("Amount per turn (positive to restore, negative to drain).")]
    public float amountPerTurn = 10f;

    public float NormalizedAmount => (valueType == StatModifierType.Flat) ? amountPerTurn : (amountPerTurn / 100f);
}

/// <summary>
/// Defines periodic damage over time values with separated battle (per turn) and overworld (per steps tick) configurations.
/// </summary>
[Serializable]
public struct StatusDamageOverTime
{
    [Header("Elemental Alignment")]
    [Tooltip("Elemental type of the damage tick.")]
    [SerializeField] private ElementalType damageElement;

    [Header("Battle Damage Settings (Per Turn)")]
    [Tooltip("Base damage value dealt at the start/end of each turn in combat.")]
    [SerializeField] private float battleDamage;

    [Tooltip("Calculation type for combat: Flat points or Percentage of Max HP.")]
    [SerializeField] private StatModifierType battleDamageType;

    [Header("Overworld Damage Settings (Per Step Tick)")]
    [Tooltip("Base damage value dealt every X steps in the overworld.")]
    [SerializeField] private float overworldDamage;

    [Tooltip("Calculation type for overworld: Flat points or Percentage of Max HP.")]
    [SerializeField] private StatModifierType overworldDamageType;

    #region Properties

    public ElementalType DamageElement => damageElement;
    public float BattleDamage => battleDamage;
    public StatModifierType BattleDamageType => battleDamageType;
    public float OverworldDamage => overworldDamage;
    public StatModifierType OverworldDamageType => overworldDamageType;

    public float NormalizedBattleDamage => (battleDamageType == StatModifierType.Flat) ? battleDamage : (battleDamage / 100f);
    public float NormalizedOverworldDamage => (overworldDamageType == StatModifierType.Flat) ? overworldDamage : (overworldDamage / 100f);

    #endregion

    public StatusDamageOverTime(
        float battleDamage,
        StatModifierType battleDamageType,
        float overworldDamage,
        StatModifierType overworldDamageType,
        ElementalType damageElement = ElementalType.None)
    {
        this.battleDamage = battleDamage;
        this.battleDamageType = battleDamageType;
        this.overworldDamage = overworldDamage;
        this.overworldDamageType = overworldDamageType;
        this.damageElement = damageElement;
    }
}

#endregion