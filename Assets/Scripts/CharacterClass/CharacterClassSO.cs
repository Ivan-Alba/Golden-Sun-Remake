using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Container for Psynergy skills unlocked at specific level thresholds within a class.
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

    #region Helper Methods

    /// <summary>
    /// Gets the class multiplier for a specific StatType.
    /// </summary>
    public float GetStatMultiplier(StatType statType)
    {
        switch (statType)
        {
            case StatType.MaxHP: return hpMultiplier;
            case StatType.MaxPP: return ppMultiplier;
            case StatType.Attack: return atkMultiplier;
            case StatType.Defense: return defMultiplier;
            case StatType.Agility: return agiMultiplier;
            case StatType.Luck: return lckMultiplier;
            default: return 1.0f;
        }
    }

    public List<ClassPsynergyEntry> GetFuturePsynergies(int currentLevel)
    {
        List<ClassPsynergyEntry> futurePsynergies = new List<ClassPsynergyEntry>();
        foreach (var entry in grantedPsynergies)
        {
            if (entry.requiredLevel > currentLevel)
            {
                futurePsynergies.Add(entry);
            }
        }
        return futurePsynergies;
    }

    #endregion
}