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
    [Header("Turn Controller Settings")]
    [SerializeField] private int maxTurnBoxes = 6;

    [Header("CTB Delay Tuning")]
    [Tooltip("Scale factor multiplier K used in CTB delay calculation.")]
    [SerializeField] private float agilityScaleFactor = 100f;

    [Tooltip("Base offset added to Agility denominator to dampen turn frequency gaps between fast and slow fighters.")]
    [SerializeField] private float baseAgilityBias = 50f;

    private FighterController currentFighter;
    private List<GameObject> activeFighters = new List<GameObject>();

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
    /// Initializes initial turn delays based on combatant agility at battle start.
    /// </summary>
    /// <param name="fighters">List of active combatant GameObjects in battle.</param>
    /// <returns>A projected list of turn order KeyValuePairs for UI binding.</returns>
    public List<KeyValuePair<GameObject, int>> InitializeTurnSystem(List<GameObject> fighters)
    {
        activeFighters = new List<GameObject>(fighters ?? new List<GameObject>());

        // Calculate initial turn delays for all fighters based on damped agility
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
    /// Advances the CTB timeline by applying calculated action delay to the active fighter.
    /// </summary>
    /// <param name="executedFighter">The fighter GameObject that completed its action.</param>
    /// <param name="baseActionCost">Base delay cost of the performed action.</param>
    /// <returns>Updated projected list of turn order KeyValuePairs.</returns>
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

        // Remove defeated or destroyed combatants efficiently
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
    /// Calculates action delay using Damped CTB Formula:
    /// Delay = Max(1, Round((BaseCost * K) / (Agility + BaseAgilityBias)))
    /// </summary>
    /// <param name="fighterObj">Target fighter GameObject.</param>
    /// <param name="baseActionCost">Base action delay cost (e.g. 50 for Attack, 30 for Defend).</param>
    /// <returns>Final calculated delay integer value.</returns>
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
    /// Simulates future turn order up to maxTurnBoxes for timeline UI presentation without modifying real combatant state.
    /// </summary>
    /// <returns>List of projected combatant KeyValuePairs containing virtual delays.</returns>
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

        int defaultCost = 50; // Standard baseline for projection calculation

        while (projection.Count < maxTurnBoxes && simulatedCDs.Count > 0)
        {
            KeyValuePair<GameObject, int> nextUnit = simulatedCDs.OrderBy(kvp => kvp.Value).First();
            projection.Add(new KeyValuePair<GameObject, int>(nextUnit.Key, nextUnit.Value));

            int estimatedDelay = CalculateDelay(nextUnit.Key, defaultCost);
            simulatedCDs[nextUnit.Key] += estimatedDelay;
        }

        return projection;
    }

    #endregion

    #region Internal State Refresh Pipeline

    /// <summary>
    /// Executes normalization, current fighter recalculation, and broadcasts turn queue events.
    /// </summary>
    private List<KeyValuePair<GameObject, int>> RefreshTurnState()
    {
        NormalizeCooldowns();
        UpdateCurrentFighter();

        List<KeyValuePair<GameObject, int>> turnProjection = BuildTurnProjection();
        NotifyTurnQueueUpdated(turnProjection);

        return turnProjection;
    }

    /// <summary>
    /// Subtracts the lowest NextTurnCD from all active fighters to keep turn values normalized near zero.
    /// </summary>
    private void NormalizeCooldowns()
    {
        if (activeFighters.Count == 0) return;

        int minCD = int.MaxValue;

        // Efficient loop without LINQ allocation
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

    /// <summary>
    /// Determines and assigns the active fighter with the lowest NextTurnCD.
    /// </summary>
    private void UpdateCurrentFighter()
    {
        if (activeFighters.Count == 0)
        {
            CurrentFighter = null;
            return;
        }

        GameObject leadFighter = null;
        int lowestCD = int.MaxValue;

        // Direct allocation-free iteration
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

    /// <summary>
    /// Extracts game objects from projection list and dispatches global turn queue updated event.
    /// </summary>
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