using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines a single psynergy unlock requirement based on hero level.
/// </summary>
[Serializable]
public class PsynergyLearnEntry
{
    [Tooltip("Level required for the hero to learn this psynergy innately.")]
    public int levelRequirement = 1;

    [Tooltip("The psynergy granted at this level.")]
    public PsynergySO psynergy;
}

/// <summary>
/// ScriptableObject defining the innate progression tree (psynergies learned by level) for a specific hero.
/// </summary>
[CreateAssetMenu(fileName = "HeroBaseProgression_", menuName = "GoldenSun/Hero/Base Progression")]
public class HeroBaseProgressionSO : ScriptableObject
{
    [Header("Base Psynergy Progression")]
    [Tooltip("List of innate psynergies the hero learns naturally as they level up.")]
    [SerializeField] private List<PsynergyLearnEntry> learnablePsynergies = new List<PsynergyLearnEntry>();

    public IReadOnlyList<PsynergyLearnEntry> LearnablePsynergies => learnablePsynergies;

    /// <summary>
    /// Returns all innate psynergies that the hero should have unlocked up to the specified level.
    /// </summary>
    public List<PsynergySO> GetInnatePsynergiesForLevel(int currentLevel)
    {
        List<PsynergySO> unlocked = new List<PsynergySO>();

        if (learnablePsynergies == null) return unlocked;

        foreach (var entry in learnablePsynergies)
        {
            if (entry != null && entry.psynergy != null && currentLevel >= entry.levelRequirement)
            {
                // Avoid duplicates if configured multiple times by mistake
                if (!unlocked.Contains(entry.psynergy))
                {
                    unlocked.Add(entry.psynergy);
                }
            }
        }

        return unlocked;
    }
}