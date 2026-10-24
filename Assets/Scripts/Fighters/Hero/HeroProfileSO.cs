using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#region Data Structures

/// <summary>
/// Defines innate psynergy unlock requirements based on the hero's level.
/// </summary>
[Serializable]
public class PsynergyLearnEntry
{
    [Tooltip("Level required for the hero to learn this psynergy innately.")]
    public int levelRequirement = 1;

    [Tooltip("The psynergy granted at this level requirement.")]
    public PsynergySO psynergy;
}

/// <summary>
/// Container structure mapping base stat growth gained strictly during a single level up.
/// </summary>
[Serializable]
public class LevelStatGrowth
{
    public int hpGain;
    public int ppGain;
    public int attackGain;
    public int defenseGain;
    public int agilityGain;
    public int luckGain;
}

/// <summary>
/// Defines curve configuration for a single base stat from Level 1 to Max Level.
/// </summary>
[Serializable]
public class StatCurveConfig
{
    [Tooltip("Stat value at Level 1.")]
    public int baseValueAtLevel1 = 30;

    [Tooltip("Target stat value at Max Level (e.g., Level 99).")]
    public int targetValueAtMaxLevel = 500;

    [Tooltip("Normalized growth curve (Time 0 = Level 1, Time 1 = Max Level). Controls early/late stat spikes.")]
    public AnimationCurve growthCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    /// <summary>
    /// Evaluates the absolute base stat value at a specific level step.
    /// </summary>
    public int EvaluateStatAtLevel(int currentLevel, int maxLevel)
    {
        if (currentLevel <= 1) return baseValueAtLevel1;
        if (currentLevel >= maxLevel) return targetValueAtMaxLevel;

        float t = (float)(currentLevel - 1) / (maxLevel - 1);
        float curveNormalized = Mathf.Clamp01(growthCurve.Evaluate(t));

        return Mathf.RoundToInt(Mathf.Lerp(baseValueAtLevel1, targetValueAtMaxLevel, curveNormalized));
    }
}

#endregion

/// <summary>
/// ScriptableObject representing the canonical data definition for a hero.
/// Encapsulates allowed equipment subtypes, level cap, innate psynergy progression, and base stat growth curves.
/// </summary>
[CreateAssetMenu(fileName = "HeroProfile_", menuName = "GoldenSun/Hero/Hero Profile")]
public class HeroProfileSO : ScriptableObject
{
    #region Serialized Fields

    [Header("Hero Profile Config")]
    [Tooltip("Display name identifier for this hero profile.")]
    [SerializeField] private string heroName = "New Hero";

    [Tooltip("Maximum achievable level for this hero.")]
    [SerializeField] private int maxLevel = 99;

    [Header("Equipment Compatibility")]
    [Tooltip("List of equipment subtypes this hero can equip (e.g., LightBlade, Longsword, Robe, Shield...).")]
    [SerializeField] private List<EquipmentSubtype> allowedEquipmentSubtypes = new List<EquipmentSubtype>();

    [Header("Stat Growth Curves")]
    [SerializeField] private StatCurveConfig hpCurve = new StatCurveConfig { baseValueAtLevel1 = 32, targetValueAtMaxLevel = 820 };
    [SerializeField] private StatCurveConfig ppCurve = new StatCurveConfig { baseValueAtLevel1 = 12, targetValueAtMaxLevel = 240 };
    [SerializeField] private StatCurveConfig attackCurve = new StatCurveConfig { baseValueAtLevel1 = 15, targetValueAtMaxLevel = 380 };
    [SerializeField] private StatCurveConfig defenseCurve = new StatCurveConfig { baseValueAtLevel1 = 8, targetValueAtMaxLevel = 210 };
    [SerializeField] private StatCurveConfig agilityCurve = new StatCurveConfig { baseValueAtLevel1 = 10, targetValueAtMaxLevel = 260 };
    [SerializeField] private StatCurveConfig luckCurve = new StatCurveConfig { baseValueAtLevel1 = 2, targetValueAtMaxLevel = 15 };

    [Header("Base Psynergy Progression")]
    [Tooltip("List of innate psynergies the hero learns naturally as they level up.")]
    [SerializeField] private List<PsynergyLearnEntry> learnablePsynergies = new List<PsynergyLearnEntry>();

    #endregion

    #region Properties

    public string HeroName => heroName;
    public int MaxLevel => maxLevel;
    public IReadOnlyList<EquipmentSubtype> AllowedEquipmentSubtypes => allowedEquipmentSubtypes;
    public IReadOnlyList<PsynergyLearnEntry> LearnablePsynergies => learnablePsynergies;

    #endregion

    #region Equipment Rules Validation

    /// <summary>
    /// Validates if this hero can equip a specific item based on its EquipmentSubtype.
    /// </summary>
    public bool CanEquipSubtype(EquipmentSubtype subtype)
    {
        // Items without restriction (None) or relics are allowed by default
        if (subtype == EquipmentSubtype.Relic) return true;

        // If no restrictions are configured, allow everything as fallback
        if (allowedEquipmentSubtypes == null || allowedEquipmentSubtypes.Count == 0) return true;

        return allowedEquipmentSubtypes.Contains(subtype);
    }

    /// <summary>
    /// Overload helper checking directly against an EquipableSO asset.
    /// </summary>
    public bool CanEquipItem(EquipableSO item)
    {
        if (item == null) return false;
        return CanEquipSubtype(item.EquipmentSubtype);
    }

    #endregion

    #region Public Methods - Stat Evaluation & Delta Calculation

    /// <summary>
    /// Evaluates the total base stats a hero should have at a given level.
    /// </summary>
    public (int hp, int pp, int atk, int def, int agi, int luck) GetBaseStatsForLevel(int level)
    {
        int clampedLevel = Mathf.Clamp(level, 1, maxLevel);

        return (
            hpCurve.EvaluateStatAtLevel(clampedLevel, maxLevel),
            ppCurve.EvaluateStatAtLevel(clampedLevel, maxLevel),
            attackCurve.EvaluateStatAtLevel(clampedLevel, maxLevel),
            defenseCurve.EvaluateStatAtLevel(clampedLevel, maxLevel),
            agilityCurve.EvaluateStatAtLevel(clampedLevel, maxLevel),
            luckCurve.EvaluateStatAtLevel(clampedLevel, maxLevel)
        );
    }

    /// <summary>
    /// Calculates exact stat delta gains earned when transitioning from oldLevel to newLevel.
    /// Perfect for Level-Up UI popups.
    /// </summary>
    public LevelStatGrowth GetStatGrowthDelta(int oldLevel, int newLevel)
    {
        var oldStats = GetBaseStatsForLevel(oldLevel);
        var newStats = GetBaseStatsForLevel(newLevel);

        return new LevelStatGrowth
        {
            hpGain = Mathf.Max(0, newStats.hp - oldStats.hp),
            ppGain = Mathf.Max(0, newStats.pp - oldStats.pp),
            attackGain = Mathf.Max(0, newStats.atk - oldStats.atk),
            defenseGain = Mathf.Max(0, newStats.def - oldStats.def),
            agilityGain = Mathf.Max(0, newStats.agi - oldStats.agi),
            luckGain = Mathf.Max(0, newStats.luck - oldStats.luck)
        };
    }

    #endregion

    #region Public Methods - Psynergy Unlock

    /// <summary>
    /// Returns all unique innate psynergies unlocked up to the specified hero level.
    /// </summary>
    public List<PsynergySO> GetInnatePsynergiesForLevel(int currentLevel)
    {
        List<PsynergySO> unlocked = new List<PsynergySO>();
        if (learnablePsynergies == null || learnablePsynergies.Count == 0) return unlocked;

        HashSet<PsynergySO> uniqueTracker = new HashSet<PsynergySO>();

        for (int i = 0; i < learnablePsynergies.Count; i++)
        {
            var entry = learnablePsynergies[i];
            if (entry != null && entry.psynergy != null && currentLevel >= entry.levelRequirement)
            {
                if (uniqueTracker.Add(entry.psynergy))
                {
                    unlocked.Add(entry.psynergy);
                }
            }
        }

        return unlocked;
    }

    /// <summary>
    /// Returns newly learned psynergies unlocked strictly between oldLevel and newLevel milestones.
    /// </summary>
    public List<PsynergySO> GetPsynergiesUnlockedBetweenLevels(int oldLevel, int newLevel)
    {
        List<PsynergySO> newlyLearned = new List<PsynergySO>();
        if (learnablePsynergies == null) return newlyLearned;

        for (int i = 0; i < learnablePsynergies.Count; i++)
        {
            var entry = learnablePsynergies[i];
            if (entry != null && entry.psynergy != null && entry.levelRequirement > oldLevel && entry.levelRequirement <= newLevel)
            {
                if (!newlyLearned.Contains(entry.psynergy))
                {
                    newlyLearned.Add(entry.psynergy);
                }
            }
        }

        return newlyLearned;
    }

    /// <summary>
    /// Checks if a specific psynergy should be unlocked innately at the target level.
    /// </summary>
    public bool HasPsynergyAtLevel(PsynergySO psynergy, int currentLevel)
    {
        if (psynergy == null || learnablePsynergies == null) return false;

        for (int i = 0; i < learnablePsynergies.Count; i++)
        {
            var entry = learnablePsynergies[i];
            if (entry != null && entry.psynergy == psynergy && currentLevel >= entry.levelRequirement)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Unity Editor Validation

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (learnablePsynergies != null)
        {
            learnablePsynergies.Sort((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return -1;
                if (b == null) return 1;
                return a.levelRequirement.CompareTo(b.levelRequirement);
            });
        }
    }
#endif

    #endregion
}