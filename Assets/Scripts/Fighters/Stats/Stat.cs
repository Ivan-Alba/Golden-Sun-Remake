using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Modular definition of a stat bonus for inspectors and ScriptableObjects.
/// Maps directly to a StatModifier in the stat engine using raw float values.
/// </summary>
[Serializable]
public class StatBonus
{
    [Tooltip("Stat or elemental power/resistance to modify.")]
    public StatType statType = StatType.Attack;

    [Tooltip("Canonical calculation type: Flat, PercentBase, or PercentTotal.")]
    public StatModifierType modifierType = StatModifierType.Flat;

    [Tooltip("Raw numeric value. Flat: direct integer (e.g., 15). Percentages: decimal scalar (e.g., 0.10 for +10%, 0.05 for +5%).")]
    public float value = 0.1f;

    /// <summary>
    /// Instantiates a valid StatModifier to inject into Stat.cs or StatCalculator.
    /// Passes the raw decimal value directly.
    /// </summary>
    public StatModifier ToStatModifier(object source = null, bool isTemporary = false, int durationInTurns = -1)
    {
        return new StatModifier(
            value,
            modifierType,
            source,
            isTemporary,
            durationInTurns
        );
    }
}

[Serializable]
public class Stat
{
    [SerializeField] private StatType statType;
    [SerializeField] private int baseValue;

    private readonly List<StatModifier> statModifiers = new List<StatModifier>();

    private float classMultiplier = 1f; // Direct scalar (1.0 = 100%, 1.1 = 110%, 1.3 = 130%)
    private bool isDirty = true;
    private int cachedValue;

    public event Action OnValueChanged;

    public IReadOnlyList<StatModifier> StatModifiers => statModifiers;
    public StatType StatType => statType;

    public int BaseValue
    {
        get => baseValue;
        set
        {
            if (baseValue != value)
            {
                baseValue = value;
                isDirty = true;
            }
        }
    }

    /// <summary>
    /// Active class multiplier for this stat (1.0 = 100%, 1.1 = 110%).
    /// </summary>
    public float ClassMultiplier
    {
        get => classMultiplier;
        set
        {
            float clampedValue = Mathf.Max(0.01f, value);
            if (!Mathf.Approximately(classMultiplier, clampedValue))
            {
                classMultiplier = clampedValue;
                isDirty = true;
            }
        }
    }

    public int Value
    {
        get
        {
            if (isDirty)
            {
                int previousValue = cachedValue;
                cachedValue = CalculateFinalValue();
                isDirty = false;

                if (previousValue != cachedValue)
                {
                    OnValueChanged?.Invoke();
                }
            }
            return cachedValue;
        }
    }

    public Stat(StatType statType, int baseValue, float initialClassMultiplier = 1f)
    {
        this.statType = statType;
        this.baseValue = baseValue;
        this.classMultiplier = Mathf.Max(0.01f, initialClassMultiplier);
    }

    public void AddModifier(StatModifier mod)
    {
        if (mod == null) return;
        statModifiers.Add(mod);
        isDirty = true;
    }

    public bool RemoveModifier(StatModifier mod)
    {
        if (statModifiers.Remove(mod))
        {
            isDirty = true;
            return true;
        }
        return false;
    }

    public bool RemoveAllModifiersFromSource(object source)
    {
        if (source == null) return false;

        bool removedAny = false;
        for (int i = statModifiers.Count - 1; i >= 0; i--)
        {
            if (statModifiers[i].Source == source)
            {
                statModifiers.RemoveAt(i);
                removedAny = true;
            }
        }

        if (removedAny)
        {
            isDirty = true;
        }

        return removedAny;
    }

    public void TickTurnModifiers()
    {
        bool removedAny = false;
        for (int i = statModifiers.Count - 1; i >= 0; i--)
        {
            if (statModifiers[i].TickTurn())
            {
                statModifiers.RemoveAt(i);
                removedAny = true;
            }
        }

        if (removedAny)
        {
            isDirty = true;
        }
    }

    public void RemoveAllTemporaryModifiers()
    {
        bool removedAny = false;
        for (int i = statModifiers.Count - 1; i >= 0; i--)
        {
            if (statModifiers[i].IsTemporary)
            {
                statModifiers.RemoveAt(i);
                removedAny = true;
            }
        }

        if (removedAny)
        {
            isDirty = true;
        }
    }

    /// <summary>
    /// Canonical 5-level calculation pipeline (Golden Sun Spec).
    /// </summary>
    private int CalculateFinalValue()
    {
        float sumFlat = 0f;
        float sumPercentBase = 0f;
        float sumPercentTotal = 0f;

        for (int i = 0; i < statModifiers.Count; i++)
        {
            StatModifier mod = statModifiers[i];
            if (mod == null) continue;

            switch (mod.Type)
            {
                case StatModifierType.Flat:
                    sumFlat += mod.Value;
                    break;
                case StatModifierType.PercentBase:
                    sumPercentBase += mod.Value; // Direct decimal scalar (e.g., 0.10f for +10%)
                    break;
                case StatModifierType.PercentTotal:
                    sumPercentTotal += mod.Value; // Direct decimal scalar (e.g., 0.10f for +10%)
                    break;
            }
        }

        // 1. Modified Base Level (% Base over pure BaseValue)
        float baseModificada = BaseValue + (BaseValue * sumPercentBase);

        // 2. Flat Addition Level (BaseMod + Flat Equipment/Djinns/Passives)
        float subtotalPlano = baseModificada + sumFlat;

        // 3. Class Multiplier Level (Direct GS Spec scaling, e.g., 1.1f for Knight)
        float subtotalClase = subtotalPlano * classMultiplier;

        // 4. % Total Level (Global combat buffs/debuffs or % total passives, e.g., 0.10f for +10%)
        float valorFlotanteFinal = subtotalClase * (1f + sumPercentTotal);

        // 5. Single truncation using FloorToInt + Epsilon tolerance
        return Mathf.Max(0, Mathf.FloorToInt(valorFlotanteFinal + 0.0001f));
    }
}