using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages progression, experience, level-ups, and live calculated stats engine for hero units.
/// Listens to global events (such as GameEvents.Hero.OnEquipmentChanged and GameEvents.SaveLoad)
/// to trigger stat recalculations and handle self-contained save data persistence.
/// </summary>
public class HeroStats : FighterStats
{
    #region Serialized Fields

    [Header("Hero Base Element")]
    [SerializeField] private ElementalType baseElement = ElementalType.Venus;

    [Header("Hero Progression Assets")]
    [SerializeField] private HeroProfileSO heroProfile;

    [Header("Hero Progression Data")]
    [SerializeField] private int level = 1;
    [SerializeField] private int currentXp = 0;

    [Header("Story Unlocked Psynergies")]
    [SerializeField] private List<PsynergySO> unlockedStoryPsynergies = new List<PsynergySO>();

    #endregion

    #region Private Fields & Cached Components

    private int nextLevelXp;
    private HeroDjinnController djinnController;
    private HeroEquipment heroEquipment;
    private HeroInventory heroInventory;

    // Cache Engine State
    private StatBlock cachedBlock;
    private bool isDirty = true;

    // Zero-Allocation Reusable Collections for Recalculation Engine
    private readonly StatEvaluationContext reusableContext = new StatEvaluationContext();
    private readonly HashSet<PsynergySO> consolidatedPsynergySet = new HashSet<PsynergySO>();
    private readonly List<PsynergySO> lostPsynergiesCache = new List<PsynergySO>();

    #endregion

    #region Properties

    public ElementalType BaseElement => baseElement;
    public int Level { get => level; private set => level = value; }
    public int CurrentXp { get => currentXp; private set => currentXp = value; }
    public int NextLevelXp { get => nextLevelXp; private set => nextLevelXp = value; }
    public HeroProfileSO HeroProfile => heroProfile;
    public IReadOnlyList<PsynergySO> UnlockedStoryPsynergies => unlockedStoryPsynergies;
    public HeroEquipment HeroEquipment => heroEquipment;
    public HeroInventory HeroInventory => heroInventory;
    public HeroDjinnController DjinnController => djinnController;
    public CharacterClassSO CurrentClass => djinnController?.CurrentClass;
    public override bool IsHero => true;

    #endregion

    #region Live Calculated Stats Engine (Dirty Caching via StatCalculator)

    /// <summary>
    /// Builds an evaluation context directly from FighterStats' single source of truth (statsMap).
    /// Uses a zero-allocation reusable context object.
    /// </summary>
    public StatEvaluationContext BuildCurrentContext()
    {
        reusableContext.BaseStats.Clear();
        reusableContext.ClassMultipliers.Clear();
        reusableContext.Modifiers.Clear();

        if (statsMap == null) return reusableContext;

        foreach (var kvp in statsMap)
        {
            StatType type = kvp.Key;
            Stat statInstance = kvp.Value;

            if (statInstance == null) continue;

            reusableContext.SetBaseStat(type, statInstance.BaseValue);
            reusableContext.SetClassMultiplier(type, statInstance.ClassMultiplier);
            reusableContext.AddModifiers(type, statInstance.StatModifiers);
        }

        return reusableContext;
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
        heroEquipment = GetComponent<HeroEquipment>();
        heroInventory = GetComponent<HeroInventory>();

        CalculateNextLevelXp();

        // Subscribe to FighterStats base mutations for automatic dirtying
        OnStatsChanged += MarkDirty;
    }

    private void OnEnable()
    {
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void OnDestroy()
    {
        OnStatsChanged -= MarkDirty;
    }

    private void MarkDirty()
    {
        isDirty = true;
    }

    #endregion

    #region Event Subscriptions

    private void SubscribeToEvents()
    {
        GameEvents.Hero.OnEquipmentChanged += HandleHeroEquipmentChanged;

        // Save & Load Event Subscriptions
        GameEvents.SaveLoad.OnCollectSaveData += HandleCollectSaveData;
        GameEvents.SaveLoad.OnRestoreSaveData += HandleRestoreSaveData;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.Hero.OnEquipmentChanged -= HandleHeroEquipmentChanged;

        // Save & Load Event Unsubscriptions
        GameEvents.SaveLoad.OnCollectSaveData -= HandleCollectSaveData;
        GameEvents.SaveLoad.OnRestoreSaveData -= HandleRestoreSaveData;
    }

    private void HandleHeroEquipmentChanged(GameObject heroObj)
    {
        // Only trigger recalculations if the event belongs to this hero instance
        if (heroObj == gameObject)
        {
            RecalculateStatsAndPsynergies();
        }
    }

    #endregion

    #region Save & Load Event Handlers

    private void HandleCollectSaveData(GameData saveData)
    {
        if (saveData == null) return;

        string currentHeroName = gameObject.name;

        // 1. Progression Data
        saveData.heroesProgression.Add(new HeroProgressionData
        {
            heroName = currentHeroName,
            level = level,
            currentXp = currentXp,
            nextLevelXp = nextLevelXp
        });

        // 2. Vital Status Data
        saveData.heroesVitals.Add(new HeroVitalStatusData
        {
            heroName = currentHeroName,
            currentHp = CurrHp,
            currentPp = CurrPp
        });

        // 3. Story Psynergies Data
        HeroStoryPsynergyData storyData = new HeroStoryPsynergyData
        {
            heroName = currentHeroName
        };

        if (unlockedStoryPsynergies != null)
        {
            for (int i = 0; i < unlockedStoryPsynergies.Count; i++)
            {
                if (unlockedStoryPsynergies[i] != null)
                {
                    storyData.unlockedStoryPsynergySlugs.Add(unlockedStoryPsynergies[i].PsynergySlug);
                }
            }
        }
        saveData.heroesStoryPsynergies.Add(storyData);

        // 4. Base Stats Data
        HeroStatsContainerData statsData = new HeroStatsContainerData
        {
            heroName = currentHeroName
        };

        if (statsMap != null)
        {
            foreach (var kvp in statsMap)
            {
                if (kvp.Value != null)
                {
                    statsData.baseStats.Add(new StatBaseData(kvp.Key, kvp.Value.BaseValue));
                }
            }
        }
        saveData.heroesStats.Add(statsData);

        //Debug.Log($"<color=green>[HeroStats] Collected progression, vitals, and base stats save data for '{currentHeroName}'.</color>");
    }

    private void HandleRestoreSaveData(GameData saveData)
    {
        if (saveData == null) return;

        string currentHeroName = gameObject.name;

        // Temporarily unsubscribe from stat mutation events to prevent multiple calculation sweeps during load loop
        OnStatsChanged -= MarkDirty;

        // 1. Restore Base Stats via parent class FighterStats API
        HeroStatsContainerData statsData = saveData.heroesStats.Find(s => s != null && s.heroName.Equals(currentHeroName));
        if (statsData != null && statsData.baseStats != null)
        {
            for (int i = 0; i < statsData.baseStats.Count; i++)
            {
                StatBaseData baseData = statsData.baseStats[i];
                if (baseData != null)
                {
                    SetStatBaseValue(baseData.statType, Mathf.RoundToInt(baseData.baseValue));
                }
            }
        }

        // 2. Restore Progression
        HeroProgressionData progData = saveData.heroesProgression.Find(p => p != null && p.heroName.Equals(currentHeroName));
        if (progData != null)
        {
            level = progData.level;
            currentXp = progData.currentXp;
            CalculateNextLevelXp();
        }

        // 3. Restore Story Psynergies
        HeroStoryPsynergyData storyData = saveData.heroesStoryPsynergies.Find(s => s != null && s.heroName.Equals(currentHeroName));
        unlockedStoryPsynergies.Clear();

        if (storyData != null && storyData.unlockedStoryPsynergySlugs != null)
        {
            for (int i = 0; i < storyData.unlockedStoryPsynergySlugs.Count; i++)
            {
                string slug = storyData.unlockedStoryPsynergySlugs[i];
                if (string.IsNullOrEmpty(slug)) continue;

                PsynergySO psynergyAsset = null;
                GameEvents.Database.RequestPsynergyBySlug(slug, p => psynergyAsset = p);

                if (psynergyAsset != null)
                {
                    unlockedStoryPsynergies.Add(psynergyAsset);
                }
                else
                {
                    Debug.LogWarning($"[HeroStats] Story psynergy slug '{slug}' not found in database for '{currentHeroName}'.");
                }
            }
        }

        // 4. Recalculate stats block, class multipliers, and active psynergies
        RecalculateStatsAndPsynergies();

        // 5. Restore Vital Status (Clamped against newly recalculated Max HP/PP)
        HeroVitalStatusData vitalsData = saveData.heroesVitals.Find(v => v != null && v.heroName.Equals(currentHeroName));
        if (vitalsData != null)
        {
            CurrHp = Mathf.Clamp(vitalsData.currentHp, 0, MaxHp);
            CurrPp = Mathf.Clamp(vitalsData.currentPp, 0, MaxPp);
        }

        // Re-engage stat mutation event listener
        OnStatsChanged += MarkDirty;
        MarkDirty();

        NotifyHpChanged();
        NotifyPpChanged();

        Debug.Log($"<color=cyan>[HeroStats] Stats, progression, and vitals successfully restored for '{currentHeroName}'.</color>");
    }

    #endregion

    #region Resource Event Overrides (UI Synchronization)

    /// <summary>
    /// Overrides FighterStats HP change pipeline to also broadcast hero status updates to HUD components.
    /// </summary>
    public override void NotifyHpChanged()
    {
        base.NotifyHpChanged(); // Clamps HP, fires Fighter.TriggerHpChanged, handles death triggers
        GameEvents.Hero.TriggerHeroStatusChanged(); // Refreshes Hero Status Cards in Battle/Overworld HUD
    }

    /// <summary>
    /// Overrides FighterStats PP change pipeline to also broadcast hero status updates to HUD components.
    /// </summary>
    public override void NotifyPpChanged()
    {
        base.NotifyPpChanged(); // Clamps PP & fires Fighter.TriggerPpChanged
        GameEvents.Hero.TriggerHeroStatusChanged(); // Refreshes Hero Status Cards in Battle/Overworld HUD
    }

    #endregion

    #region Progression & Experience Logic

    /// <summary>
    /// Calculates the experience required to advance from the current level to the next.
    /// Uses a moderate quadratic growth curve suitable for a classic Golden Sun feel.
    /// </summary>
    private void CalculateNextLevelXp()
    {
        if (level >= heroProfile.MaxLevel)
        {
            nextLevelXp = int.MaxValue;
            return;
        }

        // Curva cuadrática moderada (15 * L^2 + 100 * L + 20)
        nextLevelXp = (15 * level * level) + (100 * level) + 20;
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

        if (heroProfile != null)
        {
            List<PsynergySO> levelPsynergies = heroProfile.GetInnatePsynergiesForLevel(level);
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
        if (heroProfile == null || heroProfile.LearnablePsynergies == null) return newEntries;

        foreach (var entry in heroProfile.LearnablePsynergies)
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
    }

    public void ReevaluatePsynergies()
    {
        consolidatedPsynergySet.Clear();

        List<PsynergySO> innateAndStory = GetInnateAndStoryPsynergies();
        foreach (var p in innateAndStory)
        {
            if (p != null) consolidatedPsynergySet.Add(p);
        }

        if (heroEquipment == null) heroEquipment = GetComponent<HeroEquipment>();
        if (heroEquipment != null)
        {
            List<PsynergySO> relicPsynergies = heroEquipment.GetEquippedRelicPsynergies();
            foreach (var p in relicPsynergies)
            {
                if (p != null) consolidatedPsynergySet.Add(p);
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
                    consolidatedPsynergySet.Add(classEntries[i].psynergy);
                }
            }
        }

        lostPsynergiesCache.Clear();
        foreach (var oldPsynergy in psynergies)
        {
            if (oldPsynergy != null && !consolidatedPsynergySet.Contains(oldPsynergy))
            {
                lostPsynergiesCache.Add(oldPsynergy);
            }
        }

        psynergies.Clear();
        psynergies.AddRange(consolidatedPsynergySet);

        foreach (var lost in lostPsynergiesCache)
        {
            Debug.Log($"[HeroStats] {fighterName} lost access to psynergy: '{lost.PsynergyName}'.");
            GameEvents.Psynergy.TriggerPsynergyLost(gameObject, lost);
        }
    }

    #endregion
}