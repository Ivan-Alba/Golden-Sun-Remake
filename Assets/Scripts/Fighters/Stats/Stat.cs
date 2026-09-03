using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Definición modular de un bono de estadística para inspeccionadores y ScriptableObjects.
/// Mapea de forma limpia a un StatModifier del motor de stats.
/// </summary>
[Serializable]
public class StatBonus
{
    [Tooltip("Estadística o resistencia/poder elemental a modificar.")]
    public StatType statType = StatType.Attack;

    [Tooltip("Tipo de cálculo canónico: Flat, PercentBase o PercentTotal.")]
    public StatModifierType modifierType = StatModifierType.Flat;

    [Tooltip("Valor numérico. Para porcentajes, introduce el valor directo (ej: 20 para +20%, -15 para -15%).")]
    public float value = 10f;

    /// <summary>
    /// Devuelve el valor numérico ajustado a la matemática de punto flotante.
    /// Si es un porcentaje, convierte 20 en 0.20f.
    /// </summary>
    public float NormalizedValue
    {
        get
        {
            if (modifierType == StatModifierType.Flat)
            {
                return value;
            }
            // Convierte valores de inspector (ej. 20%) a multiplicadores flotantes (0.20f)
            return value / 100f;
        }
    }

    /// <summary>
    /// Instancia un StatModifier válido para inyectar en Stat.cs o StatCalculator.
    /// </summary>
    public StatModifier ToStatModifier(object source = null, bool isTemporary = false, int durationInTurns = -1)
    {
        return new StatModifier(
            NormalizedValue,
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

    private float classMultiplier = 1f;
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
    /// Multiplicador de clase activo para este stat (1.0 = 100%).
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
    /// Pipeline canónico de cálculo de 5 niveles (Golden Sun Spec).
    /// </summary>
    private int CalculateFinalValue()
    {
        float sumFlat = 0f;
        float sumPercentBase = 0f;
        float sumPercentTotal = 0f;

        for (int i = 0; i < statModifiers.Count; i++)
        {
            StatModifier mod = statModifiers[i];
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

        // 1. Nivel Base Modificada (% Base sobre BaseValue puro)
        float baseModificada = BaseValue + (BaseValue * sumPercentBase);

        // 2. Nivel Suma Plana (BaseMod + Equipos/Djinns/Pasivas planas)
        float subtotalPlano = baseModificada + sumFlat;

        // 3. Nivel Multiplicador de Clase (Escalado canónico GS)
        float subtotalClase = subtotalPlano * classMultiplier;

        // 4. Nivel % Total (Bufos/Debuffs globales de combate o pasivas de % total)
        float valorFlotanteFinal = subtotalClase * (1f + sumPercentTotal);

        // 5. Truncamiento único con FloorToInt (Evita Banker's Rounding)
        return Mathf.Max(0, Mathf.FloorToInt(valorFlotanteFinal));
    }
}