using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Container for Psynergy skills unlocked at specific hero level thresholds within a character class.
/// </summary>
[Serializable]
public struct ClassPsynergyEntry
{
    [Tooltip("The Psynergy asset unlocked by this class.")]
    public PsynergySO psynergy;

    [Tooltip("Minimum hero level required to learn this Psynergy while in this class.")]
    public int requiredLevel;
}

/// <summary>
/// ScriptableObject defining a character class in Golden Sun.
/// Holds percentage stat multipliers (e.g. 150% ATK, 120% HP) and granted Psynergies.
/// </summary>
[CreateAssetMenu(fileName = "Class_", menuName = "GoldenSun/Djinn/Character Class")]
public class CharacterClassSO : ScriptableObject
{
    #region Basic Identity

    [Header("Identity")]
    [SerializeField] private string className = "Squire";
    [SerializeField] private string classSlug = "squire";
    [TextArea(2, 4)]
    [SerializeField] private string description;

    #endregion

    #region Percentage Stat Multipliers (Golden Sun Original System)

    [Header("Percentage Stat Multipliers (1.0 = 100%)")]
    [Tooltip("HP multiplier (e.g., 1.10 = 110% Max HP).")]
    [SerializeField] private float hpMultiplier = 1.10f;

    [Tooltip("PP multiplier (e.g., 1.00 = 100% Max PP).")]
    [SerializeField] private float ppMultiplier = 1.00f;

    [Tooltip("Attack multiplier (e.g., 1.10 = 110% ATK).")]
    [SerializeField] private float atkMultiplier = 1.10f;

    [Tooltip("Defense multiplier (e.g., 1.10 = 110% DEF).")]
    [SerializeField] private float defMultiplier = 1.10f;

    [Tooltip("Agility multiplier (e.g., 1.05 = 105% AGI).")]
    [SerializeField] private float agiMultiplier = 1.05f;

    [Tooltip("Luck multiplier (e.g., 1.00 = 100% LCK).")]
    [SerializeField] private float lckMultiplier = 1.00f;

    #endregion

    #region Granted Psynergies

    [Header("Class Psynergies")]
    [Tooltip("List of Psynergies granted by this class along with their level requirements.")]
    [SerializeField] private List<ClassPsynergyEntry> grantedPsynergies = new List<ClassPsynergyEntry>();

    #endregion

    #region Public Properties

    public string ClassName => className;
    public string ClassSlug => classSlug;
    public string Description => description;

    public float HpMultiplier => hpMultiplier;
    public float PpMultiplier => ppMultiplier;
    public float AtkMultiplier => atkMultiplier;
    public float DefMultiplier => defMultiplier;
    public float AgiMultiplier => agiMultiplier;
    public float LckMultiplier => lckMultiplier;

    public IReadOnlyList<ClassPsynergyEntry> GrantedPsynergies => grantedPsynergies;

    #endregion

    #region Public Evaluation Methods

    /// <summary>
    /// Returns the class multiplier scale corresponding to a given StatType.
    /// </summary>
    public float GetStatMultiplier(StatType statType)
    {
        return statType switch
        {
            StatType.MaxHP => hpMultiplier,
            StatType.MaxPP => ppMultiplier,
            StatType.Attack => atkMultiplier,
            StatType.Defense => defMultiplier,
            StatType.Agility => agiMultiplier,
            StatType.Luck => lckMultiplier,
            _ => 1.0f
        };
    }

    /// <summary>
    /// Retrieves all Psynergies currently unlocked at or below the given level.
    /// </summary>
    public List<PsynergySO> GetLearnedPsynergies(int currentLevel)
    {
        var learned = new List<PsynergySO>();
        for (int i = 0; i < grantedPsynergies.Count; i++)
        {
            var entry = grantedPsynergies[i];
            if (entry.psynergy != null && entry.requiredLevel <= currentLevel)
            {
                learned.Add(entry.psynergy);
            }
        }
        return learned;
    }

    /// <summary>
    /// Retrieves upcoming Psynergies that will be learned above the current level.
    /// </summary>
    public List<ClassPsynergyEntry> GetFuturePsynergies(int currentLevel)
    {
        var futurePsynergies = new List<ClassPsynergyEntry>();
        for (int i = 0; i < grantedPsynergies.Count; i++)
        {
            var entry = grantedPsynergies[i];
            if (entry.requiredLevel > currentLevel)
            {
                futurePsynergies.Add(entry);
            }
        }
        return futurePsynergies;
    }

    /// <summary>
    /// Zero-allocation variant to populate an existing list with future unlockable Psynergies.
    /// </summary>
    public void GetFuturePsynergiesNonAlloc(int currentLevel, List<ClassPsynergyEntry> resultsContainer)
    {
        if (resultsContainer == null) return;
        resultsContainer.Clear();

        for (int i = 0; i < grantedPsynergies.Count; i++)
        {
            var entry = grantedPsynergies[i];
            if (entry.requiredLevel > currentLevel)
            {
                resultsContainer.Add(entry);
            }
        }
    }

    #endregion
}