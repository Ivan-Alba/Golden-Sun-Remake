using System;
using UnityEngine;

/// <summary>
/// Runtime wrapper instance encapsulating a DjinnSO data asset, its active state, and recovery turn timers.
/// Manages state transitions (Set, Standby, Recovery) for combat calculations and summoning mechanics.
/// </summary>
[Serializable]
public class DjinnInstance
{
    #region Private Fields

    [SerializeField] private DjinnSO djinnData;
    [SerializeField] private DjinnState currentState = DjinnState.Set;
    [SerializeField] private int currentRecoveryTurns;

    #endregion

    #region Events

    /// <summary>
    /// Event triggered whenever this Djinn's state changes (e.g., Set -> Standby, Recovery -> Set).
    /// </summary>
    public event Action<DjinnInstance, DjinnState> OnStateChanged;

    #endregion

    #region Properties

    public DjinnSO DjinnData => djinnData;
    public DjinnState CurrentState => currentState;

    /// <summary>
    /// Gets or sets the current remaining recovery turns.
    /// Safely clamped to non-negative values.
    /// </summary>
    public int RecoveryTurnsLeft
    {
        get => currentRecoveryTurns;
        set => currentRecoveryTurns = Mathf.Max(0, value);
    }

    public int CurrentRecoveryTurns => currentRecoveryTurns;

    // Helper state evaluation shortcuts for UI and Combat logic
    public bool IsSet => currentState == DjinnState.Set;
    public bool IsStandby => currentState == DjinnState.Standby;
    public bool IsRecovery => currentState == DjinnState.Recovery;

    #endregion

    #region Constructors

    public DjinnInstance(DjinnSO data, DjinnState initialState = DjinnState.Set)
    {
        djinnData = data != null ? data : throw new ArgumentNullException(nameof(data), "[DjinnInstance] Cannot initialize with null DjinnSO asset.");
        currentState = initialState;
        currentRecoveryTurns = (initialState == DjinnState.Recovery) ? GetBaseRecoveryTurns() : 0;
    }

    #endregion

    #region State Mutation Methods

    /// <summary>
    /// Sets the Djinn to Set state, making it active for hero stat/class calculations and passives.
    /// </summary>
    public void SetReady()
    {
        if (currentState == DjinnState.Set && currentRecoveryTurns == 0) return;

        currentState = DjinnState.Set;
        currentRecoveryTurns = 0;
        OnStateChanged?.Invoke(this, currentState);
    }

    /// <summary>
    /// Puts the Djinn into Standby state (unleashed or available as currency for Summons).
    /// </summary>
    public void PutOnStandby()
    {
        if (currentState == DjinnState.Standby && currentRecoveryTurns == 0) return;

        currentState = DjinnState.Standby;
        currentRecoveryTurns = 0;
        OnStateChanged?.Invoke(this, currentState);
    }

    /// <summary>
    /// Consumes the Djinn into Recovery state after executing a Summon.
    /// </summary>
    public void EnterRecovery()
    {
        currentState = DjinnState.Recovery;
        currentRecoveryTurns = GetBaseRecoveryTurns();
        OnStateChanged?.Invoke(this, currentState);
    }

    /// <summary>
    /// Forces a specific state and recovery duration (useful for saved game loading or story events).
    /// </summary>
    public void ForceState(DjinnState newState, int recoveryTurns = 0)
    {
        currentState = newState;
        currentRecoveryTurns = Mathf.Max(0, recoveryTurns);
        OnStateChanged?.Invoke(this, currentState);
    }

    /// <summary>
    /// Ticks down recovery turns at turn resolution.
    /// Automatically transitions to Set state when reaching zero.
    /// </summary>
    /// <returns>True if the Djinn recovered to Set state during this tick; otherwise false.</returns>
    public bool TickRecovery()
    {
        if (currentState != DjinnState.Recovery) return false;

        currentRecoveryTurns = Mathf.Max(0, currentRecoveryTurns - 1);

        if (currentRecoveryTurns <= 0)
        {
            SetReady();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Reduces active recovery turns by a custom amount (e.g., via status recovery items or abilities).
    /// </summary>
    public void ReduceRecoveryTurns(int amount)
    {
        if (currentState != DjinnState.Recovery || amount <= 0) return;

        currentRecoveryTurns = Mathf.Max(0, currentRecoveryTurns - amount);

        if (currentRecoveryTurns <= 0)
        {
            SetReady();
        }
    }

    #endregion

    #region Private Helpers

    private int GetBaseRecoveryTurns()
    {
        return (djinnData != null) ? djinnData.BaseRecoveryTurns : 2;
    }

    #endregion
}