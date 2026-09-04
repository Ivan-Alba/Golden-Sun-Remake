using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// MonoBehaviour attached to heroes. Manages assigned Djinns, resolves current CharacterClassSO,
/// controls the Leader Djinn passive perks, applies zero-allocation Djinn stat modifiers, 
/// and notifies HeroStats for stat and Psynergy recalculations.
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
    private readonly List<DjinnInstance> assignedDjinns = new List<DjinnInstance>();

    private DjinnInstance leaderDjinn = null;
    private CharacterClassSO currentClass = null;

    // Reusable list tracking active leader status effect instances applied to HeroStats
    private readonly List<StatusEffectSO> activeLeaderEffects = new List<StatusEffectSO>();

    // Zero-allocation reusable container for stat modifiers extraction
    private readonly List<(StatType statType, StatModifier modifier)> cachedModifiersList = new List<(StatType, StatModifier)>();

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
    /// Populates initial Djinn instances, resolves initial class, and guarantees Leader passive application on boot.
    /// </summary>
    public void InitializeDjinns()
    {
        // Unsubscribe existing instances if re-initializing at runtime
        for (int i = 0; i < assignedDjinns.Count; i++)
        {
            if (assignedDjinns[i] != null)
            {
                assignedDjinns[i].OnStateChanged -= HandleDjinnStateChanged;
            }
        }

        assignedDjinns.Clear();

        // 1. Instantiate runtime Djinn wrappers
        for (int i = 0; i < initialDjinns.Count; i++)
        {
            if (initialDjinns[i] != null)
            {
                DjinnInstance instance = new DjinnInstance(initialDjinns[i], DjinnState.Set);
                instance.OnStateChanged += HandleDjinnStateChanged;
                assignedDjinns.Add(instance);
            }
        }

        // 2. Assign initial Leader Djinn
        if (initialLeaderIndex >= 0 && initialLeaderIndex < assignedDjinns.Count)
        {
            leaderDjinn = assignedDjinns[initialLeaderIndex];
        }
        else
        {
            leaderDjinn = (assignedDjinns.Count > 0) ? assignedDjinns[0] : null;
        }

        // 3. Force full evaluation including initial leader passive injection
        EvaluateClassAndStats(forceRefresh: true);
    }

    #endregion

    #region Djinn Management API

    /// <summary>
    /// Single-pass evaluation counting active SET Djinns across all elements simultaneously.
    /// </summary>
    public void GetSetDjinnCountsSinglePass(out int venus, out int mars, out int jupiter, out int mercury)
    {
        venus = 0;
        mars = 0;
        jupiter = 0;
        mercury = 0;

        for (int i = 0; i < assignedDjinns.Count; i++)
        {
            DjinnInstance djinn = assignedDjinns[i];
            if (djinn != null && djinn.CurrentState == DjinnState.Set && djinn.DjinnData != null)
            {
                switch (djinn.DjinnData.Element)
                {
                    case ElementalType.Venus: venus++; break;
                    case ElementalType.Mars: mars++; break;
                    case ElementalType.Jupiter: jupiter++; break;
                    case ElementalType.Mercury: mercury++; break;
                }
            }
        }
    }

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
    /// Returns the total count of assigned Djinns matching an element regardless of state.
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
    /// Designates a new Leader Djinn and updates passive perks atomically.
    /// </summary>
    public bool SetLeaderDjinn(DjinnInstance djinn)
    {
        if (djinn != null && !assignedDjinns.Contains(djinn))
        {
            Debug.LogWarning($"[HeroDjinnController] Cannot set {djinn.DjinnData?.DjinnName} as leader. Not assigned to {heroStats?.Name}.");
            return false;
        }

        leaderDjinn = djinn;
        UpdateLeaderPassivePerk();
        OnDjinnSetupUpdated?.Invoke();

        if (heroStats != null)
        {
            heroStats.RecalculateStatsAndPsynergies();
        }

        return true;
    }

    /// <summary>
    /// Adds a new Djinn to this hero and triggers setup recalculations.
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
    /// Ticks down recovery turns for all Djinns in RECOVERY state at turn resolution.
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
    /// Evaluates active SET Djinns by element via single pass, queries ClassDatabaseSO, 
    /// updates zero-alloc Djinn StatModifiers, and notifies HeroStats.
    /// </summary>
    public void EvaluateClassAndStats(bool forceRefresh = false)
    {
        // 1. Single-pass elemental count for maximum efficiency
        GetSetDjinnCountsSinglePass(out int venusSet, out int marsSet, out int jupiterSet, out int mercurySet);

        ElementalType heroBaseElement = (heroStats != null) ? heroStats.BaseElement : ElementalType.Venus;

        CharacterClassSO newClass = (classDatabase != null)
            ? classDatabase.EvaluateClass(heroBaseElement, venusSet, marsSet, jupiterSet, mercurySet)
            : null;

        bool classHasChanged = (newClass != currentClass) || forceRefresh;
        currentClass = newClass;

        // 2. Synchronize modifiers and leader passive
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
    /// Synchronizes StatModifiers on HeroStats using zero-allocation containers.
    /// </summary>
    private void UpdateDjinnStatModifiers()
    {
        if (heroStats == null) return;

        // 1. Clear previous Djinn stat modifiers
        for (int i = 0; i < assignedDjinns.Count; i++)
        {
            if (assignedDjinns[i]?.DjinnData != null)
            {
                heroStats.RemoveModifiersFromSource(assignedDjinns[i].DjinnData);
            }
        }

        // 2. Apply stat modifiers ONLY for Djinns currently in SET state (Zero-Alloc)
        for (int i = 0; i < assignedDjinns.Count; i++)
        {
            DjinnInstance djinn = assignedDjinns[i];
            if (djinn != null && djinn.CurrentState == DjinnState.Set && djinn.DjinnData != null)
            {
                djinn.DjinnData.GetStatModifiersNonAlloc(cachedModifiersList);
                for (int j = 0; j < cachedModifiersList.Count; j++)
                {
                    heroStats.AddStatModifier(cachedModifiersList[j].statType, cachedModifiersList[j].modifier);
                }
            }
        }
    }

    /// <summary>
    /// Applies or removes all active Leader Djinn passive StatusEffectSO instances based on whether the Leader Djinn is in SET state.
    /// </summary>
    private void UpdateLeaderPassivePerk()
    {
        if (heroStats == null) return;

        // 1. Remove all previously applied leader passive effects
        for (int i = 0; i < activeLeaderEffects.Count; i++)
        {
            if (activeLeaderEffects[i] != null)
            {
                heroStats.RemoveStatusEffect(activeLeaderEffects[i]);
            }
        }
        activeLeaderEffects.Clear();

        // 2. Apply new leader passive effects ONLY IF Leader Djinn is set AND in SET state
        if (leaderDjinn != null && leaderDjinn.CurrentState == DjinnState.Set && leaderDjinn.DjinnData != null)
        {
            IReadOnlyList<StatusEffectApplication> passiveApplications = leaderDjinn.DjinnData.LeaderPassiveEffects;

            if (passiveApplications != null && passiveApplications.Count > 0)
            {
                for (int i = 0; i < passiveApplications.Count; i++)
                {
                    StatusEffectApplication app = passiveApplications[i];
                    if (app.StatusEffect == null) continue;

                    // Roll probability (usually 1.0f for Leader Passives)
                    if (Random.value <= app.ApplicationChance)
                    {
                        if (heroStats.ApplyStatusEffect(app.StatusEffect))
                        {
                            activeLeaderEffects.Add(app.StatusEffect);
                        }
                    }
                }
            }
        }
    }

    #endregion
}