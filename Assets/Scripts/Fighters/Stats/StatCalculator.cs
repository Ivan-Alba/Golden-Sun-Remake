using System;
using System.Collections.Generic;
using UnityEngine;

#region Data Transfer Objects (DTOs) for Simulation

/// <summary>
/// Contenedor de entrada para simular y evaluar el cálculo de estadísticas
/// sin alterar el estado real del personaje.
/// </summary>
public class StatEvaluationContext
{
    public Dictionary<StatType, int> BaseStats { get; set; } = new Dictionary<StatType, int>();
    public Dictionary<StatType, float> ClassMultipliers { get; set; } = new Dictionary<StatType, float>();
    public Dictionary<StatType, List<StatModifier>> Modifiers { get; set; } = new Dictionary<StatType, List<StatModifier>>();

    public StatEvaluationContext() { }

    /// <summary>
    /// Asigna el valor base de un StatType específico.
    /// </summary>
    public void SetBaseStat(StatType type, int value)
    {
        BaseStats[type] = value;
    }

    /// <summary>
    /// Asigna el multiplicador de clase para un StatType (1.0 = 100%).
    /// </summary>
    public void SetClassMultiplier(StatType type, float multiplier)
    {
        ClassMultipliers[type] = Mathf.Max(0.01f, multiplier);
    }

    /// <summary>
    /// Asigna multiplicadores de clase para todos los Core Stats a la vez.
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
    /// Agrega un modificador asociado a un StatType objetivo.
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
    /// Agrega una lista de modificadores asociados a un StatType objetivo.
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
/// Representa el conjunto consolidado y evaluado de todos los stats de un personaje.
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
/// Evaluador puro e independiente para el cálculo centralizado de estadísticas.
/// Aplica el pipeline canónico de 5 niveles (Golden Sun Spec) sin efectos secundarios.
/// </summary>
public static class StatCalculator
{
    /// <summary>
    /// Evalúa un contexto completo y devuelve un StatBlock con todos los stats calculados.
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
    /// Evalúa un StatType individual siguiendo de forma estricta la fórmula de 5 niveles.
    /// </summary>
    public static int EvaluateSingleStat(StatType statType, StatEvaluationContext context)
    {
        if (context == null) return 0;

        int baseValue = context.BaseStats.TryGetValue(statType, out int bVal) ? bVal : 0;
        float classMultiplier = context.ClassMultipliers.TryGetValue(statType, out float cMult) ? cMult : 1f;

        float sumFlat = 0f;
        float sumPercentBase = 0f;
        float sumPercentTotal = 0f;

        if (context.Modifiers.TryGetValue(StatType.Attack, out var attackMods))
        {
            //Debug.Log($"[StatCalculator Test] Attack Modifiers Count: {attackMods.Count}");
            foreach (var mod in attackMods)
            {
                //Debug.Log($"[StatCalculator Test] Source: {mod.Source}, Value: {mod.Value}, Type: {mod.Type}");
            }
        }
        else
        {
            //Debug.LogWarning("[StatCalculator Test] No Attack Modifiers found in context!");
        }


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
                        sumPercentBase += mod.Value;
                        break;
                    case StatModifierType.PercentTotal:
                        sumPercentTotal += mod.Value;
                        break;
                }
            }
        }

        return CalculateCanonicalFormula(baseValue, classMultiplier, sumFlat, sumPercentBase, sumPercentTotal);
    }

    /// <summary>
    /// Pipeline de Cálculo Canónico de 5 Niveles (Golden Sun Spec).
    /// </summary>
    public static int CalculateCanonicalFormula(
        int baseValue,
        float classMultiplier,
        float sumFlat,
        float sumPercentBase,
        float sumPercentTotal)
    {
        // 1. Nivel Base Modificada (% Base sobre el BaseValue puro)
        float baseModificada = baseValue + (baseValue * sumPercentBase);

        // 2. Nivel Suma Plana (BaseMod + Equipos / Djinns / Pasivas planas)
        float subtotalPlano = baseModificada + sumFlat;

        // 3. Nivel Multiplicador de Clase (Escalado canónico GS)
        float subtotalClase = subtotalPlano * classMultiplier;

        // 4. Nivel % Total (Bufos / Debuffs globales de combate o pasivas de % total)
        float valorFlotanteFinal = subtotalClase * (1f + sumPercentTotal);

        // 5. Truncamiento único con FloorToInt (Elimina Banker's Rounding)
        return Mathf.Max(0, Mathf.FloorToInt(valorFlotanteFinal));
    }
}

#endregion