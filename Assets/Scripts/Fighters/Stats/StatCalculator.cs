using System;
using System.Collections.Generic;
using UnityEngine;

#region Data Transfer Objects (DTOs) for Simulation

/// <summary>
/// Input container to simulate and evaluate stat calculations
/// without altering the actual character state.
/// </summary>
public class StatEvaluationContext
{
    public Dictionary<StatType, int> BaseStats { get; set; } = new Dictionary<StatType, int>();
    public Dictionary<StatType, float> ClassMultipliers { get; set; } = new Dictionary<StatType, float>();
    public Dictionary<StatType, List<StatModifier>> Modifiers { get; set; } = new Dictionary<StatType, List<StatModifier>>();

    public StatEvaluationContext() { }

    /// <summary>
    /// Sets the base value for a specific StatType.
    /// </summary>
    public void SetBaseStat(StatType type, int value)
    {
        BaseStats[type] = value;
    }

    /// <summary>
    /// Sets the class multiplier for a StatType (1.0 = 100%, 1.1 = 110%).
    /// </summary>
    public void SetClassMultiplier(StatType type, float multiplier)
    {
        ClassMultipliers[type] = Mathf.Max(0.01f, multiplier);
    }

    /// <summary>
    /// Sets class multipliers for all Core Stats simultaneously.
    /// </summary>
    public void SetUniformClassMultipliers(float hp, float pp, float atk, float def, float agi, float lck)
    {
        ClassMultipliers[StatType.MaxHP] = Mathf.Max(0.01f, hp);
        ClassMultipliers[StatType.MaxPP] = Mathf.Max(0.01f, pp);
        ClassMultipliers[StatType.Attack] = Mathf.Max(0.01f, atk);
        ClassMultipliers[StatType.Defense] = Mathf.Max(0.01f, def);
        ClassMultipliers[StatType.Agility] = Mathf.Max(0.01f, agi);
        ClassMultipliers[StatType.Luck] = Mathf.Max(0.01f, lck);
    }

    /// <summary>
    /// Adds a modifier associated with a target StatType.
    /// </summary>
    public void AddModifier(StatType statType, StatModifier modifier)
    {
        if (modifier == null) return;

        if (!Modifiers.TryGetValue(statType, out var list))
        {
            list = new List<StatModifier>();
            Modifiers[statType] = list;
        }
        list.Add(modifier);
    }

    /// <summary>
    /// Adds a list of modifiers associated with a target StatType.
    /// </summary>
    public void AddModifiers(StatType statType, IEnumerable<StatModifier> modifiers)
    {
        if (modifiers == null) return;

        foreach (var mod in modifiers)
        {
            AddModifier(statType, mod);
        }
    }
}

/// <summary>
/// Represents the consolidated and evaluated set of all stats for a character.
/// </summary>
public class StatBlock
{
    private readonly Dictionary<StatType, int> evaluatedStats = new Dictionary<StatType, int>();

    public int MaxHP => Get(StatType.MaxHP);
    public int MaxPP => Get(StatType.MaxPP);
    public int Attack => Get(StatType.Attack);
    public int Defense => Get(StatType.Defense);
    public int Agility => Get(StatType.Agility);
    public int Luck => Get(StatType.Luck);

    public int PowerVenus => Get(StatType.PowerVenus);
    public int PowerMars => Get(StatType.PowerMars);
    public int PowerJupiter => Get(StatType.PowerJupiter);
    public int PowerMercury => Get(StatType.PowerMercury);

    public int ResistVenus => Get(StatType.ResistVenus);
    public int ResistMars => Get(StatType.ResistMars);
    public int ResistJupiter => Get(StatType.ResistJupiter);
    public int ResistMercury => Get(StatType.ResistMercury);

    public void Set(StatType type, int value)
    {
        evaluatedStats[type] = value;
    }

    public int Get(StatType type)
    {
        return evaluatedStats.TryGetValue(type, out int val) ? val : 0;
    }
}

#endregion

#region Stat Calculator Core Engine

/// <summary>
/// Pure and independent evaluator for centralized stat calculations.
/// Strictly applies the canonical 5-level pipeline (Golden Sun Spec) without side effects.
/// </summary>
public static class StatCalculator
{
    /// <summary>
    /// Evaluates a full context and returns a StatBlock with all calculated stats.
    /// </summary>
    public static StatBlock Evaluate(StatEvaluationContext context)
    {
        StatBlock block = new StatBlock();

        if (context == null) return block;

        foreach (StatType statType in Enum.GetValues(typeof(StatType)))
        {
            int calculatedValue = EvaluateSingleStat(statType, context);
            block.Set(statType, calculatedValue);
        }

        return block;
    }

    /// <summary>
    /// Evaluates an individual StatType strictly following the 5-level formula.
    /// </summary>
    public static int EvaluateSingleStat(StatType statType, StatEvaluationContext context)
    {
        if (context == null) return 0;

        int baseValue = context.BaseStats.TryGetValue(statType, out int bVal) ? bVal : 0;
        float classMultiplier = context.ClassMultipliers.TryGetValue(statType, out float cMult) ? cMult : 1f;

        float sumFlat = 0f;
        float sumPercentBase = 0f;
        float sumPercentTotal = 0f;

        if (context.Modifiers.TryGetValue(statType, out var modifierList) && modifierList != null)
        {
            for (int i = 0; i < modifierList.Count; i++)
            {
                StatModifier mod = modifierList[i];
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
        }

        return CalculateCanonicalFormula(baseValue, classMultiplier, sumFlat, sumPercentBase, sumPercentTotal);
    }

    /// <summary>
    /// Canonical 5-Level Calculation Pipeline (Golden Sun Spec).
    /// </summary>
    public static int CalculateCanonicalFormula(
        int baseValue,
        float classMultiplier,
        float sumFlat,
        float sumPercentBase,
        float sumPercentTotal)
    {
        // 1. Modified Base Level (% Base over pure BaseValue)
        float baseModificada = baseValue + (baseValue * sumPercentBase);

        // 2. Flat Addition Level (BaseMod + Equipment / Djinns / Flat Passives)
        float subtotalPlano = baseModificada + sumFlat;

        // 3. Class Multiplier Level (Direct GS Spec scaling, e.g., 1.1f for Knight)
        float subtotalClase = subtotalPlano * classMultiplier;

        // 4. % Total Level (Global combat buffs/debuffs or % total passives, e.g., 0.10f for +10%)
        float valorFlotanteFinal = subtotalClase * (1f + sumPercentTotal);

        Debug.Log($"[Virtual Stat] Base: {baseValue}, Mult: {classMultiplier}, Flat: {sumFlat}, %Base: {sumPercentBase}, %Tot: {sumPercentTotal} => Final: {valorFlotanteFinal}");

        // 5. Single truncation using FloorToInt + Epsilon tolerance
        return Mathf.Max(0, Mathf.FloorToInt(valorFlotanteFinal + 0.0001f));
    }
}

#endregion