using System;
using System.Collections.Generic;
using UnityEngine;

public class HeroStats : FighterStats
{
    #region Events

    public event Action<PsynergySO> OnPsynergyLost;

    #endregion

    #region Serialized Fields

    [Header("Hero Base Element")]
    [SerializeField] private ElementalType baseElement = ElementalType.Venus;

    [Header("Hero Progression Assets")]
    [SerializeField] private HeroBaseProgressionSO baseProgression;

    [Header("Hero Progression Data")]
    [SerializeField] private int level = 1;
    [SerializeField] private int currentXp = 0;

    [Header("Story Unlocked Psynergies")]
    [SerializeField] private List<PsynergySO> unlockedStoryPsynergies = new List<PsynergySO>();

    #endregion

    #region Private Fields

    private int nextLevelXp;
    private HeroDjinnController djinnController;

    // Cache Engine State
    private StatBlock cachedBlock;
    private bool isDirty = true;

    #endregion

    #region Properties

    public ElementalType BaseElement => baseElement;
    public int Level { get => level; private set => level = value; }
    public int CurrentXp { get => currentXp; private set => currentXp = value; }
    public int NextLevelXp { get => nextLevelXp; private set => nextLevelXp = value; }
    public HeroBaseProgressionSO BaseProgression => baseProgression;
    public IReadOnlyList<PsynergySO> UnlockedStoryPsynergies => unlockedStoryPsynergies;

    public HeroDjinnController DjinnController => djinnController;
    public CharacterClassSO CurrentClass => djinnController?.CurrentClass;

    #endregion

    #region Live Calculated Stats Engine (Dirty Caching via StatCalculator)

    /// <summary>
    /// Builds an evaluation context directly from FighterStats' single source of truth (statsMap).
    /// </summary>
    public StatEvaluationContext BuildCurrentContext()
    {
        StatEvaluationContext context = new StatEvaluationContext();

        if (statsMap == null) return context;

        foreach (var kvp in statsMap)
        {
            StatType type = kvp.Key;
            Stat statInstance = kvp.Value;

            if (statInstance == null) continue;

            context.SetBaseStat(type, statInstance.BaseValue);
            context.SetClassMultiplier(type, statInstance.ClassMultiplier);
            context.AddModifiers(type, statInstance.StatModifiers);
        }

        return context;
    }

    /// <summary>
    /// Retrieves the live calculated StatBlock using lazy caching. 
    /// Only re-evaluates via StatCalculator if statsMap or class multipliers marked it dirty.
    /// </summary>
    public StatBlock GetCalculatedStats()
    {
        if (isDirty || cachedBlock == null)
        {
            cachedBlock = StatCalculator.Evaluate(BuildCurrentContext());
            isDirty = false;
        }

        return cachedBlock;
    }

    // Public Core Stat Properties Redirecting to Cached StatBlock
    public override int MaxHp => GetCalculatedStats().MaxHP;
    public override int MaxPp => GetCalculatedStats().MaxPP;
    public override int Attack => GetCalculatedStats().Attack;
    public override int Defense => GetCalculatedStats().Defense;
    public override int Agility => GetCalculatedStats().Agility;
    public override int Luck => GetCalculatedStats().Luck;

    // Public Elemental Stat Properties Redirecting to Cached StatBlock
    public override int PowerVenus => GetCalculatedStats().PowerVenus;
    public override int PowerMars => GetCalculatedStats().PowerMars;
    public override int PowerJupiter => GetCalculatedStats().PowerJupiter;
    public override int PowerMercury => GetCalculatedStats().PowerMercury;

    public override int ResistVenus => GetCalculatedStats().ResistVenus;
    public override int ResistMars => GetCalculatedStats().ResistMars;
    public override int ResistJupiter => GetCalculatedStats().ResistJupiter;
    public override int ResistMercury => GetCalculatedStats().ResistMercury;

    #endregion

    #region Unity Lifecycle Methods

    protected override void Awake()
    {
        base.Awake();
        djinnController = GetComponent<HeroDjinnController>();
        CalculateNextLevelXp();

        // Subscribe to FighterStats base mutations for automatic dirtying
        OnStatsChanged += MarkDirty;

        SubscribeToEquipmentEvents();
    }

    private void OnDestroy()
    {
        OnStatsChanged -= MarkDirty;
        UnsubscribeFromEquipmentEvents();
    }

    private void MarkDirty()
    {
        isDirty = true;
    }

    #endregion

    #region Event Subscriptions

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
    /// Updates class multipliers directly on Stat.cs instances, consolidates active psynergies, and emits UI events.
    /// </summary>
    public void RecalculateStatsAndPsynergies()
    {
        UpdateClassMultipliers();
        ReevaluatePsynergies();

        // Force a dirty recalculation and emit centralized event state
        MarkDirty();
        NotifyHpChanged();
        NotifyPpChanged();
    }

    /// <summary>
    /// Inject canonical Class Percentage Multipliers directly into each Stat.cs ClassMultiplier property.
    /// </summary>
    private void UpdateClassMultipliers()
    {
        if (statsMap == null) return;

        if (djinnController == null) djinnController = GetComponent<HeroDjinnController>();
        CharacterClassSO activeClass = (djinnController != null) ? djinnController.CurrentClass : null;

        foreach (var kvp in statsMap)
        {
            StatType statType = kvp.Key;
            Stat statInstance = kvp.Value;

            float multiplier = (activeClass != null) ? activeClass.GetStatMultiplier(statType) : 1f;
            statInstance.ClassMultiplier = multiplier;
        }

        ClampCurrentHp();
        ClampCurrentPp();
    }

    public void ReevaluatePsynergies()
    {
        HashSet<PsynergySO> newConsolidatedSet = new HashSet<PsynergySO>();

        List<PsynergySO> innateAndStory = GetInnateAndStoryPsynergies();
        foreach (var p in innateAndStory)
        {
            if (p != null) newConsolidatedSet.Add(p);
        }

        HeroEquipment equipment = GetComponent<HeroEquipment>();
        if (equipment != null)
        {
            List<PsynergySO> relicPsynergies = equipment.GetEquippedRelicPsynergies();
            foreach (var p in relicPsynergies)
            {
                if (p != null) newConsolidatedSet.Add(p);
            }
        }

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

        List<PsynergySO> lostPsynergies = new List<PsynergySO>();
        foreach (var oldPsynergy in psynergies)
        {
            if (oldPsynergy != null && !newConsolidatedSet.Contains(oldPsynergy))
            {
                lostPsynergies.Add(oldPsynergy);
            }
        }

        psynergies.Clear();
        psynergies.AddRange(newConsolidatedSet);

        foreach (var lost in lostPsynergies)
        {
            Debug.Log($"[HeroStats] {fighterName} lost access to psynergy: '{lost.PsynergyName}'.");
            OnPsynergyLost?.Invoke(lost);
        }
    }

    #endregion
}