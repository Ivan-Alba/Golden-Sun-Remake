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

    #region Live Calculated Stats Overrides (StatCalculator Brain)

    /// <summary>
    /// Builds a live evaluation context for this hero including base stats, class multipliers,
    /// Djinn flat modifiers, Leader Passive modifiers, and equipment modifiers across all Core and Elemental stats,
    /// then evaluates them via StatCalculator.
    /// </summary>
    public StatBlock GetCalculatedStats()
    {
        StatEvaluationContext context = new StatEvaluationContext();

        // 1. Set Base Stats (Core & Elemental)
        foreach (StatType statType in Enum.GetValues(typeof(StatType)))
        {
            context.SetBaseStat(statType, GetStat(statType)?.BaseValue ?? 0);
        }

        // 2. Set Class Multipliers
        CharacterClassSO activeClass = CurrentClass;
        if (activeClass != null)
        {
            foreach (StatType statType in Enum.GetValues(typeof(StatType)))
            {
                context.SetClassMultiplier(statType, activeClass.GetStatMultiplier(statType));
            }
        }

        // 3. Set Djinn Modifiers & Active Leader Passive Modifiers
        if (djinnController == null) djinnController = GetComponent<HeroDjinnController>();

        if (djinnController != null)
        {
            if (djinnController.AssignedDjinns != null)
            {
                foreach (var djinn in djinnController.AssignedDjinns)
                {
                    if (djinn != null && djinn.CurrentState == DjinnState.Set && djinn.DjinnData != null && djinn.DjinnData.SetStatModifiers != null)
                    {
                        foreach (var mod in djinn.DjinnData.SetStatModifiers)
                        {
                            if (mod == null) continue;
                            context.AddModifier(mod.statType, new StatModifier(mod.value, mod.modifierType, djinn));
                        }
                    }
                }
            }

            // Active Leader Passive Modifiers
            DjinnInstance leader = djinnController.LeaderDjinn;
            if (leader != null && leader.CurrentState == DjinnState.Set && leader.DjinnData != null && leader.DjinnData.LeaderPassiveEffect != null)
            {
                var leaderModifiers = leader.DjinnData.LeaderPassiveEffect.StatModifiers;
                if (leaderModifiers != null)
                {
                    foreach (var bonus in leaderModifiers)
                    {
                        if (bonus == null) continue;
                        context.AddModifier(bonus.statType, new StatModifier(bonus.value, bonus.modifierType, leader));
                    }
                }
            }
        }

        // 4. Set Equipment Modifiers
        HeroEquipment equipment = GetComponent<HeroEquipment>();
        if (equipment != null && equipment.EquippedItems != null)
        {
            foreach (var item in equipment.EquippedItems.Values)
            {
                if (item != null && item.StatModifiers != null)
                {
                    foreach (var mod in item.StatModifiers)
                    {
                        if (mod == null) continue;
                        context.AddModifier(mod.statType, new StatModifier(mod.value, mod.modifierType, item));
                    }
                }
            }
        }

        // 5. Delegate to StatCalculator Core Engine
        return StatCalculator.Evaluate(context);
    }

    // Public Core Stat Properties Redirecting to Live Evaluated StatBlock
    public override int MaxHp => GetCalculatedStats().MaxHP;
    public override int MaxPp => GetCalculatedStats().MaxPP;
    public override int Attack => GetCalculatedStats().Attack;
    public override int Defense => GetCalculatedStats().Defense;
    public override int Agility => GetCalculatedStats().Agility;
    public override int Luck => GetCalculatedStats().Luck;

    // Public Elemental Stat Properties Redirecting to Live Evaluated StatBlock
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
    /// Updates class multipliers directly on Stat.cs instances and consolidates active psynergies.
    /// </summary>
    public void RecalculateStatsAndPsynergies()
    {
        UpdateClassMultipliers();
        ReevaluatePsynergies();
    }

    /// <summary>
    /// Inject canonical Class Percentage Multipliers directly into each Stat.cs ClassMultiplier property.
    /// Formula executed inside Stat.cs: Floor((Base + Flat Equipment + Flat Djinn) * ClassMultiplier * (1 + SumPercentTotal))
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