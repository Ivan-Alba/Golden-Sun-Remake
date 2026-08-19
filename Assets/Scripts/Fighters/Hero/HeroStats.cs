using System;
using System.Collections.Generic;
using UnityEngine;

public class HeroStats : FighterStats
{
    #region Events

    /// <summary>
    /// Event fired whenever a psynergy is completely lost from all active sources 
    /// (Innate/Story, Equipment, Class). Heard by PsynergyWorldController to clear shortcuts.
    /// </summary>
    public event Action<PsynergySO> OnPsynergyLost;

    #endregion

    #region Serialized Fields

    [Header("Hero Base Element")]
    [Tooltip("Base element of the hero (e.g., Venus for Isaac, Mars for Garet). Used for class calculation.")]
    [SerializeField] private ElementalType baseElement = ElementalType.Venus;

    [Header("Hero Progression Assets")]
    [Tooltip("Base progression tree defining innate psynergies learned by level.")]
    [SerializeField] private HeroBaseProgressionSO baseProgression;

    [Header("Hero Progression Data")]
    [SerializeField] private int level = 1;
    [SerializeField] private int currentXp = 0;

    [Header("Story Unlocked Psynergies")]
    [Tooltip("Psynergies granted permanently to this hero via story events (e.g., Mind Read, Reveal).")]
    [SerializeField] private List<PsynergySO> unlockedStoryPsynergies = new List<PsynergySO>();

    #endregion

    #region Private Fields

    private int nextLevelXp;
    private HeroDjinnController djinnController;
    private readonly object classStatSourceTag = new object();

    #endregion

    #region Properties

    public ElementalType BaseElement => baseElement;
    public int Level { get => level; private set => level = value; }
    public int CurrentXp { get => currentXp; private set => currentXp = value; }
    public int NextLevelXp { get => nextLevelXp; private set => nextLevelXp = value; }
    public HeroBaseProgressionSO BaseProgression => baseProgression;
    public IReadOnlyList<PsynergySO> UnlockedStoryPsynergies => unlockedStoryPsynergies;

    #endregion

    #region Unity Lifecycle Methods

    protected override void Awake()
    {
        base.Awake();
        djinnController = GetComponent<HeroDjinnController>();
        CalculateNextLevelXp();
        SubscribeToStatEvents();
        SubscribeToEquipmentEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromStatEvents();
        UnsubscribeFromEquipmentEvents();
    }

    #endregion

    #region Event Subscriptions

    private void SubscribeToStatEvents()
    {
        Stat maxHpStat = GetStat(StatType.MaxHP);
        if (maxHpStat != null) maxHpStat.OnValueChanged += ClampCurrentHp;

        Stat maxPpStat = GetStat(StatType.MaxPP);
        if (maxPpStat != null) maxPpStat.OnValueChanged += ClampCurrentPp;
    }

    private void UnsubscribeFromStatEvents()
    {
        Stat maxHpStat = GetStat(StatType.MaxHP);
        if (maxHpStat != null) maxHpStat.OnValueChanged -= ClampCurrentHp;

        Stat maxPpStat = GetStat(StatType.MaxPP);
        if (maxPpStat != null) maxPpStat.OnValueChanged -= ClampCurrentPp;
    }

    private void SubscribeToEquipmentEvents()
    {
        HeroEquipment equipment = GetComponent<HeroEquipment>();
        if (equipment != null)
        {
            equipment.OnEquipmentChanged += RecalculateStatsAndPsynergies;
        }
    }

    private void UnsubscribeFromEquipmentEvents()
    {
        HeroEquipment equipment = GetComponent<HeroEquipment>();
        if (equipment != null)
        {
            equipment.OnEquipmentChanged -= RecalculateStatsAndPsynergies;
        }
    }

    private void ClampCurrentHp()
    {
        if (CurrHp > MaxHp) CurrHp = MaxHp;
    }

    private void ClampCurrentPp()
    {
        if (CurrPp > MaxPp) CurrPp = MaxPp;
    }

    #endregion

    #region Progression & Experience Logic

    private void CalculateNextLevelXp()
    {
        nextLevelXp = level * 125 + (125 * level / 2);
    }

    public List<PsynergySO> AddExperience(int amount)
    {
        List<PsynergySO> newlyLearnedPsynergies = new List<PsynergySO>();
        currentXp += amount;

        while (currentXp >= nextLevelXp)
        {
            List<PsynergySO> levelUpPsynergies = LevelUp();
            if (levelUpPsynergies != null && levelUpPsynergies.Count > 0)
            {
                newlyLearnedPsynergies.AddRange(levelUpPsynergies);
            }
        }

        return newlyLearnedPsynergies;
    }

    public List<PsynergySO> LevelUp()
    {
        level++;
        currentXp -= nextLevelXp;
        CalculateNextLevelXp();

        List<PsynergySO> newPsynergies = GetNewInnatePsynergiesForCurrentLevel();
        RecalculateStatsAndPsynergies();

        return newPsynergies;
    }

    public void LoadProgression(int savedLevel, int savedXp, List<PsynergySO> savedStoryPsynergies = null)
    {
        level = savedLevel;
        currentXp = savedXp;

        if (savedStoryPsynergies != null)
        {
            unlockedStoryPsynergies = new List<PsynergySO>(savedStoryPsynergies);
        }

        CalculateNextLevelXp();
        RecalculateStatsAndPsynergies();
    }

    #endregion

    #region Story Psynergy Management

    public bool GrantStoryPsynergy(PsynergySO psynergy)
    {
        if (psynergy == null) return false;

        if (!unlockedStoryPsynergies.Contains(psynergy))
        {
            unlockedStoryPsynergies.Add(psynergy);
            Debug.Log($"[HeroStats] {fighterName} learned story psynergy: '{psynergy.PsynergyName}'.");

            RecalculateStatsAndPsynergies();
            return true;
        }

        return false;
    }

    public List<PsynergySO> GetInnateAndStoryPsynergies()
    {
        List<PsynergySO> consolidatedInnate = new List<PsynergySO>();

        if (baseProgression != null)
        {
            List<PsynergySO> levelPsynergies = baseProgression.GetInnatePsynergiesForLevel(level);
            foreach (var p in levelPsynergies)
            {
                if (!consolidatedInnate.Contains(p)) consolidatedInnate.Add(p);
            }
        }

        if (unlockedStoryPsynergies != null)
        {
            foreach (var p in unlockedStoryPsynergies)
            {
                if (p != null && !consolidatedInnate.Contains(p)) consolidatedInnate.Add(p);
            }
        }

        return consolidatedInnate;
    }

    private List<PsynergySO> GetNewInnatePsynergiesForCurrentLevel()
    {
        List<PsynergySO> newEntries = new List<PsynergySO>();
        if (baseProgression == null || baseProgression.LearnablePsynergies == null) return newEntries;

        foreach (var entry in baseProgression.LearnablePsynergies)
        {
            if (entry != null && entry.psynergy != null && entry.levelRequirement == level)
            {
                newEntries.Add(entry.psynergy);
                Debug.Log($"[HeroStats] {fighterName} learned innate psynergy '{entry.psynergy.PsynergyName}' at Level {level}.");
            }
        }

        return newEntries;
    }

    #endregion

    #region Central Recalculation Engine (Class Multipliers & Psynergies)

    /// <summary>
    /// Master refresh method triggered whenever equipment, Djinns, or classes change.
    /// Updates class multipliers and consolidates active psynergies.
    /// </summary>
    public void RecalculateStatsAndPsynergies()
    {
        UpdateClassStatModifiers();
        ReevaluatePsynergies();
    }

    /// <summary>
    /// Applies Class Percentage Multipliers as PercentTotal StatModifiers to statsMap.
    /// Formula executed inside Stat.cs: (Base + Flat Equipment + Flat Djinn) * ClassMultiplier
    /// </summary>
    private void UpdateClassStatModifiers()
    {
        if (statsMap == null) return;

        // 1. Clear previous class modifiers
        RemoveModifiersFromSource(classStatSourceTag);

        // 2. Apply active Class Percentage Multipliers
        if (djinnController == null) djinnController = GetComponent<HeroDjinnController>();
        CharacterClassSO activeClass = (djinnController != null) ? djinnController.CurrentClass : null;

        if (activeClass != null)
        {
            foreach (var kvp in statsMap)
            {
                StatType statType = kvp.Key;
                float multiplier = activeClass.GetStatMultiplier(statType);

                // In StatModifierType.PercentTotal, 1.10 = +0.10f modifier
                float percentTotalMod = multiplier - 1.0f;

                if (Mathf.Abs(percentTotalMod) > 0.001f)
                {
                    StatModifier mod = new StatModifier(percentTotalMod, StatModifierType.PercentTotal, classStatSourceTag);
                    AddStatModifier(statType, mod);
                }
            }
        }

        ClampCurrentHp();
        ClampCurrentPp();
    }

    /// <summary>
    /// Recalculates all available psynergies from active sources:
    /// 1. Innate Level Progression + Story Granted
    /// 2. Equipped Relics
    /// 3. Current Class Psynergies
    /// </summary>
    public void ReevaluatePsynergies()
    {
        HashSet<PsynergySO> newConsolidatedSet = new HashSet<PsynergySO>();

        // Source A: Innate + Story
        List<PsynergySO> innateAndStory = GetInnateAndStoryPsynergies();
        foreach (var p in innateAndStory)
        {
            if (p != null) newConsolidatedSet.Add(p);
        }

        // Source B: Equipped Relics
        HeroEquipment equipment = GetComponent<HeroEquipment>();
        if (equipment != null)
        {
            List<PsynergySO> relicPsynergies = equipment.GetEquippedRelicPsynergies();
            foreach (var p in relicPsynergies)
            {
                if (p != null) newConsolidatedSet.Add(p);
            }
        }

        // Source C: Active Class Psynergies
        if (djinnController == null) djinnController = GetComponent<HeroDjinnController>();
        if (djinnController != null && djinnController.CurrentClass != null)
        {
            IReadOnlyList<ClassPsynergyEntry> classEntries = djinnController.CurrentClass.GrantedPsynergies;
            for (int i = 0; i < classEntries.Count; i++)
            {
                if (classEntries[i].psynergy != null && level >= classEntries[i].requiredLevel)
                {
                    newConsolidatedSet.Add(classEntries[i].psynergy);
                }
            }
        }

        // Identify lost psynergies
        List<PsynergySO> lostPsynergies = new List<PsynergySO>();
        foreach (var oldPsynergy in psynergies)
        {
            if (oldPsynergy != null && !newConsolidatedSet.Contains(oldPsynergy))
            {
                lostPsynergies.Add(oldPsynergy);
            }
        }

        // Update active list in FighterStats
        psynergies.Clear();
        psynergies.AddRange(newConsolidatedSet);

        // Fire events for cleanup
        foreach (var lost in lostPsynergies)
        {
            Debug.Log($"[HeroStats] {fighterName} lost access to psynergy: '{lost.PsynergyName}'.");
            OnPsynergyLost?.Invoke(lost);
        }
    }

    #endregion
}