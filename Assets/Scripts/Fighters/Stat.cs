using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Stat
{
    [SerializeField] private StatType statType;
    [SerializeField] private int baseValue;

    private readonly List<StatModifier> statModifiers = new List<StatModifier>();

    private bool isDirty = true;
    private int cachedValue;

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

    public int Value
    {
        get
        {
            if (isDirty)
            {
                cachedValue = CalculateFinalValue();
                isDirty = false;
            }
            return cachedValue;
        }
    }

    public Stat(StatType statType, int baseValue)
    {
        this.statType = statType;
        this.baseValue = baseValue;
    }

    public void AddModifier(StatModifier mod)
    {
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

    /// <summary>
    /// Removes all modifiers originating from a specific source object (e.g., equipped weapon, specific status effect).
    /// </summary>
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

    /// <summary>
    /// Decrements the turn counter for temporary modifiers and removes expired ones.
    /// </summary>
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

    /// <summary>
    /// Clears all temporary battle-related modifiers when a combat ends.
    /// </summary>
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

    private int CalculateFinalValue()
    {
        float sumFlat = 0f;
        float sumPercentBase = 0f;
        float sumPercentTotal = 0f;

        foreach (StatModifier mod in statModifiers)
        {
            switch (mod.Type)
            {
                case StatModifierType.Flat:
                    sumFlat += mod.Value;
                    break;
                case StatModifierType.PercentBase:
                    sumPercentBase += mod.Value;
                    break;
                case StatModifierType.PercentTotal:
                    sumPercentTotal += mod.Value;
                    break;
            }
        }

        // Subtotal = Base + (Base * % Base Modifiers) + Flat Modifiers
        float subtotal = BaseValue + (BaseValue * sumPercentBase) + sumFlat;

        // Final Value = Subtotal * (1 + % Total Modifiers)
        float finalValue = subtotal * (1f + sumPercentTotal);

        // Prevent stats from going below 0
        return Mathf.Max(0, Mathf.RoundToInt(finalValue));
    }
}