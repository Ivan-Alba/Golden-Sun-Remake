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
    DoomCountdown           // Decrements a counter each turn; causes instant K.O. when reaching 0
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
/// Determines how numerical values are calculated (Flat value vs. Percentage of base/max stat).
/// </summary>
public enum StatusValueType
{
    Flat,
    Percentage
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
    Silence     // Can attack or use items, but cannot cast Psynergy
}

#endregion

#region Serializable Feature Structures

/// <summary>
/// Modular feature configuration block used inside StatusEffectSO.
/// Allows a single status effect to combine multiple behaviors (e.g., Poison dealing DoT AND dropping ATK).
/// </summary>

[Serializable]
public class StatusStatBonus
{
    [Tooltip("Stat to modify.")]
    public StatType statType = StatType.Attack;

    [Tooltip("Calculation method: Flat value or Percentage bonus/malus.")]
    public StatusValueType valueType = StatusValueType.Percentage;

    [Tooltip("Amount to modify. Positive for Buffs (+20%), negative for Debuffs (-15%).")]
    public float value = 10f;
}

[Serializable]
public class StatusElementalBonus
{
    [Tooltip("Elemental affinity to modify.")]
    public ElementalType element = ElementalType.Venus;

    [Tooltip("Amount of elemental power or resistance bonus/penalty.")]
    public float value = 20f;
}

[Serializable]
public class StatusResourceRegen
{
    [Tooltip("Target resource (HP or PP).")]
    public ResourceTarget targetResource = ResourceTarget.HP;

    [Tooltip("Flat amount or Percentage of Max HP/PP to restore (positive) or drain (negative) per turn.")]
    public StatusValueType valueType = StatusValueType.Flat;

    public float amountPerTurn = 10f;
}

[Serializable]
public class StatusDamageOverTime
{
    [Tooltip("Elemental alignment of the damage over time.")]
    public ElementalType element = ElementalType.None;

    [Tooltip("Flat damage or Percentage of Max HP dealt each turn.")]
    public StatusValueType damageType = StatusValueType.Percentage;

    public float damagePerTurn = 10f;
}

#endregion