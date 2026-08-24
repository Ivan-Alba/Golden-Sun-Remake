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
/// Configuración para regeneración o drenaje de recursos (HP/PP) por turno.
/// </summary>
[Serializable]
public class StatusResourceRegen
{
    [Tooltip("Recurso objetivo (HP o PP).")]
    public ResourceTarget targetResource = ResourceTarget.HP;

    [Tooltip("Tipo de cálculo: Flat (puntos fijos) o PercentTotal (% de MaxHP/MaxPP).")]
    public StatModifierType valueType = StatModifierType.Flat;

    [Tooltip("Cantidad por turno (positivo para regenerar, negativo para drenar).")]
    public float amountPerTurn = 10f;

    public float NormalizedAmount => (valueType == StatModifierType.Flat) ? amountPerTurn : (amountPerTurn / 100f);
}

/// <summary>
/// Configuración para daño periódico por turno (Poison, Haunt, etc.).
/// </summary>
[Serializable]
public class StatusDamageOverTime
{
    [Tooltip("Alineación elemental del daño por turno.")]
    public ElementalType element = ElementalType.None;

    [Tooltip("Tipo de cálculo: Flat (puntos fijos) o PercentTotal (% de MaxHP).")]
    public StatModifierType damageType = StatModifierType.PercentTotal;

    [Tooltip("Cantidad de daño recibido por turno.")]
    public float damagePerTurn = 10f;

    public float NormalizedDamage => (damageType == StatModifierType.Flat) ? damagePerTurn : (damagePerTurn / 100f);
}

#endregion