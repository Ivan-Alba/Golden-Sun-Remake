using System;
using UnityEngine;

/// <summary>
/// Runtime wrapper instance encapsulating a DjinnSO data asset, its current state, and recovery turn timers.
/// </summary>
[Serializable]
public class DjinnInstance
{
    #region Private Fields

    [SerializeField] private DjinnSO djinnData;
    [SerializeField] private DjinnState currentState = DjinnState.Set;
    [SerializeField] private int currentRecoveryTurns = 0;

    #endregion

    #region Events

    /// <summary>
    /// Event triggered whenever this Djinn's state changes (e.g. Set -> Standby, Recovery -> Set).
    /// </summary>
    public event Action<DjinnInstance, DjinnState> OnStateChanged;

    #endregion

    #region Properties

    public DjinnSO DjinnData => djinnData;
    public DjinnState CurrentState => currentState;
    public int CurrentRecoveryTurns => currentRecoveryTurns;

    #endregion

    #region Constructor

    public DjinnInstance(DjinnSO data, DjinnState initialState = DjinnState.Set)
    {
        djinnData = data;
        currentState = initialState;
        currentRecoveryTurns = 0;
    }

    #endregion

    #region State Mutation Methods

    /// <summary>
    /// Sets the Djinn to Set state, making it active for stat/class calculation and leader passive.
    /// </summary>
    public void SetReady()
    {
        currentState = DjinnState.Set;
        currentRecoveryTurns = 0;
        OnStateChanged?.Invoke(this, currentState);
    }

    /// <summary>
    /// Puts the Djinn into Standby state (unleashed or ready for summons).
    /// </summary>
    public void PutOnStandby()
    {
        currentState = DjinnState.Standby;
        currentRecoveryTurns = 0;
        OnStateChanged?.Invoke(this, currentState);
    }

    /// <summary>
    /// Consumes the Djinn into Recovery state after performing a Summon.
    /// </summary>
    public void EnterRecovery()
    {
        currentState = DjinnState.Recovery;
        currentRecoveryTurns = (djinnData != null) ? djinnData.BaseRecoveryTurns : 2;
        OnStateChanged?.Invoke(this, currentState);
    }

    /// <summary>
    /// Ticks down recovery turns at the end/start of the individual hero's turn.
    /// Automatically transitions to Set state when reaching zero.
    /// </summary>
    /// <returns>True if the Djinn recovered to Set state during this tick; otherwise false.</returns>
    public bool TickRecovery()
    {
        if (currentState != DjinnState.Recovery) return false;

        currentRecoveryTurns--;

        if (currentRecoveryTurns <= 0)
        {
            SetReady();
            return true;
        }

        return false;
    }

    #endregion
}