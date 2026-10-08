using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Domain controller responsible for Conditional Turn-Based (CTB) queue resolution,
/// agility-damped action delay calculations, and turn timeline projection.
/// Pure logic component with zero camera or visual dependencies.
/// </summary>
public class TurnController : MonoBehaviour
{
    private const int DEFAULT_ACTION_COST = 50;

    [Header("Turn Controller Settings")]
    [SerializeField] private int maxTurnBoxes = 6;

    [Header("CTB Delay Tuning")]
    [Tooltip("Scale factor multiplier K used in CTB delay calculation.")]
    [SerializeField] private float agilityScaleFactor = 100f;

    [Tooltip("Base offset added to Agility denominator to dampen turn frequency gaps between fast and slow fighters.")]
    [SerializeField] private float baseAgilityBias = 50f;

    private FighterController currentFighter;
    private List<GameObject> activeFighters = new List<GameObject>();

    // Cached Action Cost for the active turn execution
    private int _pendingActionCost = DEFAULT_ACTION_COST;

    #region Properties

    /// <summary>
    /// Gets or sets the maximum number of projected turns rendered in the UI timeline.
    /// </summary>
    public int MaxTurnBoxes { get => maxTurnBoxes; set => maxTurnBoxes = value; }

    /// <summary>
    /// Gets the FighterController currently active for this turn.
    /// </summary>
    public FighterController CurrentFighter { get => currentFighter; private set => currentFighter = value; }

    #endregion

    #region Public Turn API

    /// <summary>
    /// Explicitly sets the current active fighter for round-based selection modes (e.g., Golden Sun Classic).
    /// </summary>
    public void SetManualCurrentFighter(FighterController fighter)
    {
        CurrentFighter = fighter;

        // Optionally build a simple turn projection or notify queue
        if (fighter != null)
        {
            List<GameObject> singleQueue = new List<GameObject> { fighter.gameObject };
            GameEvents.Battle.TriggerTurnQueueUpdated(singleQueue);
        }
    }

    /// <summary>
    /// Registers the action cost to be applied to the current active fighter upon turn completion.
    /// </summary>
    public void SetNextActionCost(int cost)
    {
        _pendingActionCost = Mathf.Max(1, cost);
    }

    /// <summary>
    /// Initializes initial turn delays based on combatant agility at battle start.
    /// </summary>
    public List<KeyValuePair<GameObject, int>> InitializeTurnSystem(List<GameObject> fighters)
    {
        activeFighters = new List<GameObject>(fighters ?? new List<GameObject>());
        _pendingActionCost = DEFAULT_ACTION_COST;

        for (int i = 0; i < activeFighters.Count; i++)
        {
            GameObject fighterObj = activeFighters[i];
            if (fighterObj == null) continue;

            if (fighterObj.TryGetComponent<FighterController>(out var controller) &&
                fighterObj.TryGetComponent<FighterStats>(out var stats))
            {
                int agility = Mathf.Max(1, stats.Agility);
                float dampedAgility = agility + baseAgilityBias;
                controller.NextTurnCD = Mathf.Max(1, Mathf.RoundToInt(agilityScaleFactor / dampedAgility * 10f));
            }
        }

        return RefreshTurnState();
    }

    /// <summary>
    /// Advances the CTB timeline using the pre-registered action cost (_pendingActionCost).
    /// </summary>
    public List<KeyValuePair<GameObject, int>> AdvanceTurn(GameObject executedFighter)
    {
        List<KeyValuePair<GameObject, int>> result = AdvanceTurn(executedFighter, _pendingActionCost);
        _pendingActionCost = DEFAULT_ACTION_COST;
        return result;
    }

    /// <summary>
    /// Advances the CTB timeline by applying calculated action delay to the active fighter.
    /// </summary>
    public List<KeyValuePair<GameObject, int>> AdvanceTurn(GameObject executedFighter, int baseActionCost)
    {
        if (executedFighter != null)
        {
            if (executedFighter.TryGetComponent<FighterController>(out var controller))
            {
                int delay = CalculateDelay(executedFighter, baseActionCost);
                controller.NextTurnCD += delay;
            }
        }

        for (int i = activeFighters.Count - 1; i >= 0; i--)
        {
            GameObject f = activeFighters[i];
            if (f == null || (f.TryGetComponent<FighterStats>(out var s) && s.CurrHp <= 0))
            {
                activeFighters.RemoveAt(i);
            }
        }

        return RefreshTurnState();
    }

    /// <summary>
    /// Calculates action delay using Damped CTB Formula.
    /// </summary>
    public int CalculateDelay(GameObject fighterObj, int baseActionCost)
    {
        if (fighterObj == null) return baseActionCost;

        int agility = 1;
        if (fighterObj.TryGetComponent<FighterStats>(out var stats))
        {
            agility = Mathf.Max(1, stats.Agility);
        }

        float dampedAgility = agility + baseAgilityBias;
        int finalDelay = Mathf.RoundToInt((baseActionCost * agilityScaleFactor) / dampedAgility);

        return Mathf.Max(1, finalDelay);
    }

    /// <summary>
    /// Simulates future turn order up to maxTurnBoxes for timeline UI presentation.
    /// </summary>
    public List<KeyValuePair<GameObject, int>> BuildTurnProjection()
    {
        List<KeyValuePair<GameObject, int>> projection = new List<KeyValuePair<GameObject, int>>();
        if (activeFighters.Count == 0) return projection;

        Dictionary<GameObject, int> simulatedCDs = new Dictionary<GameObject, int>();
        for (int i = 0; i < activeFighters.Count; i++)
        {
            GameObject f = activeFighters[i];
            if (f != null && f.TryGetComponent<FighterController>(out var controller))
            {
                simulatedCDs[f] = controller.NextTurnCD;
            }
        }

        while (projection.Count < maxTurnBoxes && simulatedCDs.Count > 0)
        {
            KeyValuePair<GameObject, int> nextUnit = simulatedCDs.OrderBy(kvp => kvp.Value).First();
            projection.Add(new KeyValuePair<GameObject, int>(nextUnit.Key, nextUnit.Value));

            int estimatedDelay = CalculateDelay(nextUnit.Key, DEFAULT_ACTION_COST);
            simulatedCDs[nextUnit.Key] += estimatedDelay;
        }

        return projection;
    }

    #endregion

    #region Internal State Refresh Pipeline

    private List<KeyValuePair<GameObject, int>> RefreshTurnState()
    {
        NormalizeCooldowns();
        UpdateCurrentFighter();

        List<KeyValuePair<GameObject, int>> turnProjection = BuildTurnProjection();
        NotifyTurnQueueUpdated(turnProjection);

        return turnProjection;
    }

    private void NormalizeCooldowns()
    {
        if (activeFighters.Count == 0) return;

        int minCD = int.MaxValue;

        for (int i = 0; i < activeFighters.Count; i++)
        {
            GameObject f = activeFighters[i];
            if (f != null && f.TryGetComponent<FighterController>(out var controller))
            {
                if (controller.NextTurnCD < minCD)
                {
                    minCD = controller.NextTurnCD;
                }
            }
        }

        if (minCD == int.MaxValue || minCD <= 0) return;

        for (int i = 0; i < activeFighters.Count; i++)
        {
            GameObject f = activeFighters[i];
            if (f != null && f.TryGetComponent<FighterController>(out var controller))
            {
                controller.NextTurnCD -= minCD;
            }
        }
    }

    private void UpdateCurrentFighter()
    {
        if (activeFighters.Count == 0)
        {
            CurrentFighter = null;
            return;
        }

        GameObject leadFighter = null;
        int lowestCD = int.MaxValue;

        for (int i = 0; i < activeFighters.Count; i++)
        {
            GameObject f = activeFighters[i];
            if (f != null && f.TryGetComponent<FighterController>(out var controller))
            {
                if (controller.NextTurnCD < lowestCD)
                {
                    lowestCD = controller.NextTurnCD;
                    leadFighter = f;
                }
            }
        }

        CurrentFighter = leadFighter != null ? leadFighter.GetComponent<FighterController>() : null;
    }

    private void NotifyTurnQueueUpdated(List<KeyValuePair<GameObject, int>> projection)
    {
        List<GameObject> turnQueue = new List<GameObject>(projection.Count);
        for (int i = 0; i < projection.Count; i++)
        {
            turnQueue.Add(projection[i].Key);
        }

        GameEvents.Battle.TriggerTurnQueueUpdated(turnQueue);
    }

    #endregion
}