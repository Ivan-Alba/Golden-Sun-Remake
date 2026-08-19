using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Requisite entry for mapping a specific combination of Djinns in SET state to a CharacterClassSO.
/// </summary>
[Serializable]
public struct ClassRequirementEntry
{
    [Tooltip("Target base element of the hero (e.g. Venus for Isaac, Mars for Garet).")]
    public ElementalType baseElement;

    [Tooltip("Required number of Venus Djinns in SET state.")]
    public int venusCount;

    [Tooltip("Required number of Mars Djinns in SET state.")]
    public int marsCount;

    [Tooltip("Required number of Jupiter Djinns in SET state.")]
    public int jupiterCount;

    [Tooltip("Required number of Mercury Djinns in SET state.")]
    public int mercuryCount;

    [Tooltip("Priority for matching. Higher numbers are evaluated first (useful for advanced multi-element classes like Ninja/Samurai).")]
    public int priority;

    [Tooltip("The class awarded when this exact elemental combination is met.")]
    public CharacterClassSO targetClass;
}

/// <summary>
/// Database asset containing all class requirements for resolving hero classes based on active SET Djinns.
/// </summary>
[CreateAssetMenu(fileName = "ClassDatabase", menuName = "GoldenSun/Djinn/Class Database")]
public class ClassDatabaseSO : ScriptableObject
{
    [Header("Default Base Classes")]
    [Tooltip("Fallback base classes when 0 Djinns are equipped (Venus -> Squire, Mars -> Guard, Jupiter -> Wind Seer, Mercury -> Water Seer).")]
    [SerializeField] private CharacterClassSO defaultVenusClass;
    [SerializeField] private CharacterClassSO defaultMarsClass;
    [SerializeField] private CharacterClassSO defaultJupiterClass;
    [SerializeField] private CharacterClassSO defaultMercuryClass;

    [Header("Class Requirement Table")]
    [Tooltip("List of all class mappings ordered or evaluated by priority.")]
    [SerializeField] private List<ClassRequirementEntry> classEntries = new List<ClassRequirementEntry>();

    #region Public Evaluation Methods

    /// <summary>
    /// Evaluates the hero's base element and active SET Djinn counts to resolve the matching CharacterClassSO.
    /// </summary>
    public CharacterClassSO EvaluateClass(ElementalType baseElement, int venusSet, int marsSet, int jupiterSet, int mercurySet)
    {
        CharacterClassSO bestMatch = null;
        int highestPriority = -1;

        for (int i = 0; i < classEntries.Count; i++)
        {
            var entry = classEntries[i];

            if (entry.baseElement == baseElement &&
                entry.venusCount == venusSet &&
                entry.marsCount == marsSet &&
                entry.jupiterCount == jupiterSet &&
                entry.mercuryCount == mercurySet)
            {
                if (entry.priority > highestPriority)
                {
                    highestPriority = entry.priority;
                    bestMatch = entry.targetClass;
                }
            }
        }

        // Fallback to elemental default class if no specific entry matched
        if (bestMatch == null)
        {
            bestMatch = GetDefaultClassForElement(baseElement);
        }

        return bestMatch;
    }

    /// <summary>
    /// Returns the default 0-Djinn base class for a hero's element.
    /// </summary>
    public CharacterClassSO GetDefaultClassForElement(ElementalType element)
    {
        switch (element)
        {
            case ElementalType.Venus: return defaultVenusClass;
            case ElementalType.Mars: return defaultMarsClass;
            case ElementalType.Jupiter: return defaultJupiterClass;
            case ElementalType.Mercury: return defaultMercuryClass;
            default: return defaultVenusClass;
        }
    }

    #endregion
}