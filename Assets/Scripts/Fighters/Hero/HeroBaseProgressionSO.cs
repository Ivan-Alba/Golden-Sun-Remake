using System;
using System.Collections.Generic;
using UnityEngine;

#region Data Structures

/// <summary>
/// Defines a single innate psynergy unlock requirement based on the hero's current level.
/// </summary>
[Serializable]
public class PsynergyLearnEntry
{
    [Tooltip("Level required for the hero to learn this psynergy innately.")]
    public int levelRequirement = 1;

    [Tooltip("The psynergy granted at this level requirement.")]
    public PsynergySO psynergy;
}

#endregion

/// <summary>
/// ScriptableObject defining the innate progression tree (psynergies learned naturally by level) for a specific hero.
/// </summary>
[CreateAssetMenu(fileName = "HeroBaseProgression_", menuName = "GoldenSun/Hero/Base Progression")]
public class HeroBaseProgressionSO : ScriptableObject
{
    #region Serialized Fields

    [Header("Base Psynergy Progression")]
    [Tooltip("List of innate psynergies the hero learns naturally as they level up.")]
    [SerializeField] private List<PsynergyLearnEntry> learnablePsynergies = new List<PsynergyLearnEntry>();

    #endregion

    #region Properties

    /// <summary>
    /// Read-only access to the innate progression table.
    /// </summary>
    public IReadOnlyList<PsynergyLearnEntry> LearnablePsynergies => learnablePsynergies;

    #endregion

    #region Public Methods

    /// <summary>
    /// Returns all unique innate psynergies unlocked up to the specified hero level.
    /// </summary>
    /// <param name="currentLevel">The current level of the hero.</param>
    /// <returns>List of unlocked innate PsynergySO assets.</returns>
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
    /// Returns newly learned psynergies unlocked specifically at a given level milestone (e.g. for Level-Up UI popups).
    /// </summary>
    /// <param name="level">The newly reached level.</param>
    /// <returns>List of psynergies learned strictly at this level.</returns>
    public List<PsynergySO> GetPsynergiesUnlockedAtLevel(int level)
    {
        List<PsynergySO> newlyLearned = new List<PsynergySO>();
        if (learnablePsynergies == null) return newlyLearned;

        for (int i = 0; i < learnablePsynergies.Count; i++)
        {
            var entry = learnablePsynergies[i];
            if (entry != null && entry.psynergy != null && entry.levelRequirement == level)
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
        if (learnablePsynergies == null) return;

        // Auto-sort entries in the Inspector by level requirement ascending for clear editing
        learnablePsynergies.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return -1;
            if (b == null) return 1;
            return a.levelRequirement.CompareTo(b.levelRequirement);
        });
    }
#endif

    #endregion
}