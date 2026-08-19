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

    #endregion

    #region Properties

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

    /// <summary>
    /// Subscribes to MaxHP and MaxPP value change events to ensure current HP/PP never exceeds maximums.
    /// </summary>
    private void SubscribeToStatEvents()
    {
        Stat maxHpStat = GetStat(StatType.MaxHP);
        if (maxHpStat != null)
        {
            maxHpStat.OnValueChanged += ClampCurrentHp;
        }

        Stat maxPpStat = GetStat(StatType.MaxPP);
        if (maxPpStat != null)
        {
            maxPpStat.OnValueChanged += ClampCurrentPp;
        }
    }

    /// <summary>
    /// Unsubscribes from stat events upon destruction to prevent memory leaks.
    /// </summary>
    private void UnsubscribeFromStatEvents()
    {
        Stat maxHpStat = GetStat(StatType.MaxHP);
        if (maxHpStat != null)
        {
            maxHpStat.OnValueChanged -= ClampCurrentHp;
        }

        Stat maxPpStat = GetStat(StatType.MaxPP);
        if (maxPpStat != null)
        {
            maxPpStat.OnValueChanged -= ClampCurrentPp;
        }
    }

    /// <summary>
    /// Subscribes to HeroEquipment changes to trigger central psynergy re-evaluation.
    /// </summary>
    private void SubscribeToEquipmentEvents()
    {
        HeroEquipment equipment = GetComponent<HeroEquipment>();
        if (equipment != null)
        {
            equipment.OnEquipmentChanged += ReevaluatePsynergies;
        }
    }

    /// <summary>
    /// Unsubscribes from HeroEquipment events upon destruction.
    /// </summary>
    private void UnsubscribeFromEquipmentEvents()
    {
        HeroEquipment equipment = GetComponent<HeroEquipment>();
        if (equipment != null)
        {
            equipment.OnEquipmentChanged -= ReevaluatePsynergies;
        }
    }

    private void ClampCurrentHp()
    {
        if (CurrHp > MaxHp)
        {
            CurrHp = MaxHp;
        }
    }

    private void ClampCurrentPp()
    {
        if (CurrPp > MaxPp)
        {
            CurrPp = MaxPp;
        }
    }

    #endregion

    #region Progression & Experience Logic

    private void CalculateNextLevelXp()
    {
        nextLevelXp = level * 125 + (125 * level / 2);
    }

    /// <summary>
    /// Adds experience to the hero, handles potential level-ups and returns a list of newly unlocked PsynergySO objects.
    /// </summary>
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

    /// <summary>
    /// Advances the hero to the next level, recalculates XP thresholds, and updates innate psynergies.
    /// </summary>
    public List<PsynergySO> LevelUp()
    {
        level++;
        currentXp -= nextLevelXp;
        CalculateNextLevelXp();

        List<PsynergySO> newPsynergies = GetNewInnatePsynergiesForCurrentLevel();
        ReevaluatePsynergies();

        return newPsynergies;
    }

    /// <summary>
    /// Restores level and experience progress from save data and triggers a psynergy refresh.
    /// </summary>
    public void LoadProgression(int savedLevel, int savedXp, List<PsynergySO> savedStoryPsynergies = null)
    {
        level = savedLevel;
        currentXp = savedXp;

        if (savedStoryPsynergies != null)
        {
            unlockedStoryPsynergies = new List<PsynergySO>(savedStoryPsynergies);
        }

        CalculateNextLevelXp();
        ReevaluatePsynergies();
    }

    #endregion

    #region Story Psynergy Management

    /// <summary>
    /// Grants a psynergy permanently via a story event (e.g., Reveal, Force).
    /// </summary>
    public bool GrantStoryPsynergy(PsynergySO psynergy)
    {
        if (psynergy == null) return false;

        if (!unlockedStoryPsynergies.Contains(psynergy))
        {
            unlockedStoryPsynergies.Add(psynergy);
            Debug.Log($"[HeroStats] {fighterName} learned story psynergy: '{psynergy.PsynergyName}'.");

            ReevaluatePsynergies();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Consolidates all innate psynergies (by current level) and story-granted psynergies into a single list.
    /// </summary>
    public List<PsynergySO> GetInnateAndStoryPsynergies()
    {
        List<PsynergySO> consolidatedInnate = new List<PsynergySO>();

        // 1. Add innate level-based psynergies from HeroBaseProgressionSO
        if (baseProgression != null)
        {
            List<PsynergySO> levelPsynergies = baseProgression.GetInnatePsynergiesForLevel(level);
            foreach (var p in levelPsynergies)
            {
                if (!consolidatedInnate.Contains(p))
                {
                    consolidatedInnate.Add(p);
                }
            }
        }

        // 2. Add story-granted psynergies
        if (unlockedStoryPsynergies != null)
        {
            foreach (var p in unlockedStoryPsynergies)
            {
                if (p != null && !consolidatedInnate.Contains(p))
                {
                    consolidatedInnate.Add(p);
                }
            }
        }

        return consolidatedInnate;
    }

    /// <summary>
    /// Evaluates if any new innate psynergies were unlocked specifically at the current level.
    /// </summary>
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

    #region Psynergy Source Consolidation

    /// <summary>
    /// Central method that recalculates all available psynergies by combining all active sources:
    /// 1. Innate Level Progression + Story Granted
    /// 2. Currently Equipped Relics / Equipment
    /// 3. Current Class Psynergies (Will be integrated in Phase 4)
    /// </summary>
    public void ReevaluatePsynergies()
    {
        // 1. Collect all valid psynergies from current active sources without duplicates
        HashSet<PsynergySO> newConsolidatedSet = new HashSet<PsynergySO>();

        // Source A: Innate + Story
        List<PsynergySO> innateAndStory = GetInnateAndStoryPsynergies();
        foreach (var p in innateAndStory)
        {
            if (p != null) newConsolidatedSet.Add(p);
        }

        // Source B: Equipped Relics & Equipment
        HeroEquipment equipment = GetComponent<HeroEquipment>();
        if (equipment != null)
        {
            for (int i = 0; i < equipment.MaxRelicSlots; i++)
            {
                var relic = equipment.GetEquippedRelic(i);
                if (relic != null && relic.GrantedPsynergy != null)
                {
                    newConsolidatedSet.Add(relic.GrantedPsynergy);
                }
            }
        }

        // Source C: Class Psynergies (Will be integrated in Phase 4)
        // List<PsynergySO> classPsynergies = GetCurrentClassPsynergies();
        // foreach (var p in classPsynergies) { if (p != null) newConsolidatedSet.Add(p); }

        // 2. Identify which psynergies were available previously but are no longer in any active source
        List<PsynergySO> lostPsynergies = new List<PsynergySO>();
        foreach (var oldPsynergy in psynergies)
        {
            if (oldPsynergy != null && !newConsolidatedSet.Contains(oldPsynergy))
            {
                lostPsynergies.Add(oldPsynergy);
            }
        }

        // 3. Update active list in FighterStats
        psynergies.Clear();
        psynergies.AddRange(newConsolidatedSet);

        // 4. Fire events for any lost psynergies so systems like Shortcuts can clean up
        foreach (var lost in lostPsynergies)
        {
            Debug.Log($"[HeroStats] {fighterName} lost access to psynergy: '{lost.PsynergyName}'.");
            OnPsynergyLost?.Invoke(lost);
        }
    }

    #endregion
}