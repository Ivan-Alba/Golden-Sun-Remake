using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MonoBehaviour attached to heroes. Manages assigned Djinns, resolves current CharacterClassSO,
/// controls the Leader Djinn passive perk, applies Djinn stat modifiers, and notifies HeroStats for stat/psynergy recalculations.
/// </summary>
[RequireComponent(typeof(HeroStats))]
public class HeroDjinnController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Database & Initial Configuration")]
    [Tooltip("Global database used to resolve character classes based on active SET Djinns.")]
    [SerializeField] private ClassDatabaseSO classDatabase;

    [Tooltip("Initial list of Djinns assigned to this hero on start.")]
    [SerializeField] private List<DjinnSO> initialDjinns = new List<DjinnSO>();

    [Tooltip("Index of the initial Leader Djinn in the assigned list (-1 if none).")]
    [SerializeField] private int initialLeaderIndex = 0;

    #endregion

    #region Private Fields & Runtime State

    private HeroStats heroStats;
    private List<DjinnInstance> assignedDjinns = new List<DjinnInstance>();

    private DjinnInstance leaderDjinn = null;
    private CharacterClassSO currentClass = null;

    // Track active leader status effect instance applied to HeroStats
    private StatusEffectSO activeLeaderEffectSO = null;

    #endregion

    #region Events

    /// <summary>
    /// Event triggered when the hero's class changes due to Djinn state transitions.
    /// </summary>
    public event Action<CharacterClassSO> OnClassChanged;

    /// <summary>
    /// Event triggered when a Djinn's state changes or a new Leader Djinn is assigned.
    /// </summary>
    public event Action OnDjinnSetupUpdated;

    #endregion

    #region Properties

    public IReadOnlyList<DjinnInstance> AssignedDjinns => assignedDjinns;
    public DjinnInstance LeaderDjinn => leaderDjinn;
    public CharacterClassSO CurrentClass => currentClass;

    public ClassDatabaseSO ClassDatabase => classDatabase;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        heroStats = GetComponent<HeroStats>();
    }

    private void Start()
    {
        InitializeDjinns();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Populates initial Djinn instances and resolves initial class and leader passive.
    /// </summary>
    public void InitializeDjinns()
    {
        // Unsubscribe existing instances if re-initializing
        foreach (var djinn in assignedDjinns)
        {
            if (djinn != null) djinn.OnStateChanged -= HandleDjinnStateChanged;
        }

        assignedDjinns.Clear();

        for (int i = 0; i < initialDjinns.Count; i++)
        {
            if (initialDjinns[i] != null)
            {
                DjinnInstance instance = new DjinnInstance(initialDjinns[i], DjinnState.Set);
                instance.OnStateChanged += HandleDjinnStateChanged;
                assignedDjinns.Add(instance);
            }
        }

        if (initialLeaderIndex >= 0 && initialLeaderIndex < assignedDjinns.Count)
        {
            leaderDjinn = assignedDjinns[initialLeaderIndex];
        }
        else
        {
            leaderDjinn = (assignedDjinns.Count > 0) ? assignedDjinns[0] : null;
        }

        EvaluateClassAndStats(forceRefresh: true);
    }

    #endregion

    #region Djinn Management API

    /// <summary>
    /// Returns the total number of assigned Djinns of a specific element that are currently in SET state.
    /// </summary>
    public int GetSetCountByElement(ElementalType element)
    {
        int count = 0;
        for (int i = 0; i < assignedDjinns.Count; i++)
        {
            DjinnInstance djinn = assignedDjinns[i];
            if (djinn != null && djinn.CurrentState == DjinnState.Set && djinn.DjinnData != null && djinn.DjinnData.Element == element)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Returns the total number of assigned Djinns of a specific element that are currently in SET state.
    /// </summary>
    public int GetTotalCountByElement(ElementalType element)
    {
        int count = 0;
        for (int i = 0; i < assignedDjinns.Count; i++)
        {
            DjinnInstance djinn = assignedDjinns[i];
            if (djinn != null && djinn.DjinnData != null && djinn.DjinnData.Element == element)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Designates a new Leader Djinn from the hero's assigned list (Out of combat feature).
    /// </summary>
    public bool SetLeaderDjinn(DjinnInstance djinn)
    {
        if (djinn != null && !assignedDjinns.Contains(djinn))
        {
            Debug.LogWarning($"[HeroDjinnController] Cannot set {djinn.DjinnData.DjinnName} as leader. Not assigned to {heroStats.Name}.");
            return false;
        }

        leaderDjinn = djinn;
        UpdateLeaderPassivePerk();
        OnDjinnSetupUpdated?.Invoke();
        return true;
    }

    /// <summary>
    /// Adds a new Djinn to this hero.
    /// </summary>
    public void AssignDjinn(DjinnSO djinnSO, DjinnState initialState = DjinnState.Set)
    {
        if (djinnSO == null) return;

        DjinnInstance newInstance = new DjinnInstance(djinnSO, initialState);
        newInstance.OnStateChanged += HandleDjinnStateChanged;
        assignedDjinns.Add(newInstance);

        if (leaderDjinn == null)
        {
            leaderDjinn = newInstance;
        }

        EvaluateClassAndStats(forceRefresh: false);
    }

    /// <summary>
    /// Removes a Djinn from this hero (e.g., when transferring to another party member).
    /// </summary>
    public bool UnassignDjinn(DjinnInstance djinn)
    {
        if (djinn == null || !assignedDjinns.Contains(djinn)) return false;

        djinn.OnStateChanged -= HandleDjinnStateChanged;

        // Clear stat modifiers before removing
        if (djinn.DjinnData != null && heroStats != null)
        {
            heroStats.RemoveModifiersFromSource(djinn.DjinnData);
        }

        assignedDjinns.Remove(djinn);

        if (leaderDjinn == djinn)
        {
            leaderDjinn = (assignedDjinns.Count > 0) ? assignedDjinns[0] : null;
        }

        EvaluateClassAndStats(forceRefresh: false);
        return true;
    }

    #endregion

    #region Turn Progression (Recovery Loop)

    /// <summary>
    /// Ticks down recovery turns for all Djinns in RECOVERY state at the start/end of individual hero turn.
    /// </summary>
    public void TickDjinnRecovery()
    {
        bool anyRecovered = false;

        for (int i = 0; i < assignedDjinns.Count; i++)
        {
            if (assignedDjinns[i].TickRecovery())
            {
                anyRecovered = true;
            }
        }

        if (anyRecovered)
        {
            EvaluateClassAndStats(forceRefresh: false);
        }
    }

    #endregion

    #region Internal Class & Stat Evaluation

    private void HandleDjinnStateChanged(DjinnInstance djinn, DjinnState newState)
    {
        EvaluateClassAndStats(forceRefresh: false);
    }

    /// <summary>
    /// Evaluates active SET Djinns by element, queries ClassDatabaseSO, updates Djinn StatModifiers, and notifies HeroStats.
    /// </summary>
    public void EvaluateClassAndStats(bool forceRefresh = false)
    {
        // Uso del nuevo helper para mantener el código limpio y conciso
        int venusSet = GetSetCountByElement(ElementalType.Venus);
        int marsSet = GetSetCountByElement(ElementalType.Mars);
        int jupiterSet = GetSetCountByElement(ElementalType.Jupiter);
        int mercurySet = GetSetCountByElement(ElementalType.Mercury);

        ElementalType heroBaseElement = (heroStats != null) ? heroStats.BaseElement : ElementalType.Venus;

        CharacterClassSO newClass = (classDatabase != null)
            ? classDatabase.EvaluateClass(heroBaseElement, venusSet, marsSet, jupiterSet, mercurySet)
            : null;

        bool classHasChanged = (newClass != currentClass) || forceRefresh;
        currentClass = newClass;

        UpdateDjinnStatModifiers();
        UpdateLeaderPassivePerk();

        if (classHasChanged)
        {
            OnClassChanged?.Invoke(currentClass);
        }

        OnDjinnSetupUpdated?.Invoke();

        if (heroStats != null)
        {
            heroStats.RecalculateStatsAndPsynergies();
        }
    }

    /// <summary>
    /// Synchronizes StatModifiers on FighterStats for all Djinns currently in SET state.
    /// Removes modifiers for Djinns in Standby or Recovery.
    /// </summary>
    private void UpdateDjinnStatModifiers()
    {
        if (heroStats == null) return;

        // 1. Clear previous Djinn stat modifiers for all assigned Djinns
        for (int i = 0; i < assignedDjinns.Count; i++)
        {
            if (assignedDjinns[i]?.DjinnData != null)
            {
                heroStats.RemoveModifiersFromSource(assignedDjinns[i].DjinnData);
            }
        }

        // 2. Apply stat modifiers ONLY for Djinns currently in SET state
        for (int i = 0; i < assignedDjinns.Count; i++)
        {
            DjinnInstance djinn = assignedDjinns[i];
            if (djinn != null && djinn.CurrentState == DjinnState.Set && djinn.DjinnData != null)
            {
                var modifiers = djinn.DjinnData.GetStatModifiers();
                for (int j = 0; j < modifiers.Count; j++)
                {
                    heroStats.AddStatModifier(modifiers[j].statType, modifiers[j].modifier);
                }
            }
        }
    }

    /// <summary>
    /// Applies or removes the Leader Djinn passive StatusEffectSO based on whether the Leader Djinn is in SET state.
    /// </summary>
    private void UpdateLeaderPassivePerk()
    {
        if (heroStats == null) return;

        // 1. Remove previous leader passive effect if applied
        if (activeLeaderEffectSO != null)
        {
            heroStats.RemoveStatusEffect(activeLeaderEffectSO);
            activeLeaderEffectSO = null;
        }

        // 2. Apply new leader passive effect ONLY IF Leader Djinn is set AND in SET state
        if (leaderDjinn != null && leaderDjinn.CurrentState == DjinnState.Set)
        {
            StatusEffectSO passiveEffect = leaderDjinn.DjinnData.LeaderPassiveEffect;
            if (passiveEffect != null)
            {
                heroStats.ApplyStatusEffect(passiveEffect);
                activeLeaderEffectSO = passiveEffect;
            }
        }
    }

    #endregion
}