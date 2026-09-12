using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameEvents;

/// <summary>
/// Component responsible for managing battle target selection logic, 
/// spatial area calculations, cycling navigation, and target confirmation/cancellation.
/// Preserves original Golden Sun grid spaces even if enemies are defeated.
/// </summary>
public class TargetSelectionController : MonoBehaviour
{
    private ActionTargetType _currentTargetType;
    private int _currentAreaRange;
    private int _primaryTargetIndex;

    private IReadOnlyList<GameObject> _heroList;
    private List<GameObject> _enemyList;

    private List<GameObject> _lastAffectedTargets = new List<GameObject>();

    private BattleState _currentBattleState = BattleState.None;

    // Direct local reference to BattleManager on the same GameObject
    private BattleManager _battleManager;

    #region Local C# Events

    /// <summary>
    /// Triggered when the user confirms target selection.
    /// Notifies the Orchestrator (BattleManager).
    /// </summary>
    public event Action<int, List<GameObject>> OnTargetConfirmed;

    /// <summary>
    /// Triggered when the user cancels target selection.
    /// Notifies the Orchestrator (BattleManager).
    /// </summary>
    public event Action OnTargetCancelled;

    /// <summary>
    /// Triggered locally when the active targeted GameObjects or primary target change.
    /// Listened to by local components like BattleHUDController to update pointers and overlays.
    /// </summary>
    public event Action<List<GameObject>, GameObject> OnTargetSelectionChanged;

    #endregion

    /// <summary>
    /// Returns the current list of affected target GameObjects calculated for the selection.
    /// </summary>
    public List<GameObject> GetAffectedTargets()
    {
        return new List<GameObject>(_lastAffectedTargets);
    }

    public int PrimaryTargetIndex => _primaryTargetIndex;

    private void Awake()
    {
        _battleManager = GetComponent<BattleManager>();
    }

    private void OnEnable()
    {
        // Global static input events (User hardware inputs)
        GameEvents.Input.OnBattleLeftPressed += HandleLeftPressed;
        GameEvents.Input.OnBattleRightPressed += HandleRightPressed;
        GameEvents.Input.OnBattleConfirmPressed += HandleConfirmPressed;
        GameEvents.Input.OnBattleCancelPressed += HandleCancelPressed;

        // Local BattleManager C# event
        if (_battleManager != null)
        {
            _battleManager.OnBattleStateChanged -= HandleBattleStateChanged;
            _battleManager.OnBattleStateChanged += HandleBattleStateChanged;
        }
    }

    private void OnDisable()
    {
        GameEvents.Input.OnBattleLeftPressed -= HandleLeftPressed;
        GameEvents.Input.OnBattleRightPressed -= HandleRightPressed;
        GameEvents.Input.OnBattleConfirmPressed -= HandleConfirmPressed;
        GameEvents.Input.OnBattleCancelPressed -= HandleCancelPressed;

        if (_battleManager != null)
        {
            _battleManager.OnBattleStateChanged -= HandleBattleStateChanged;
        }
    }

    private void HandleBattleStateChanged(BattleState newState)
    {
        _currentBattleState = newState;
    }

    /// <summary>
    /// Initializes targeting state for a new selection sequence.
    /// </summary>
    public void InitializeTargeting(
        ActionTargetType targetType,
        int areaRange,
        IReadOnlyList<GameObject> heroList,
        List<GameObject> enemyList,
        int defaultIndex = 0)
    {
        _currentTargetType = targetType;
        _currentAreaRange = Mathf.Max(0, areaRange);
        _heroList = heroList;
        _enemyList = enemyList;

        _primaryTargetIndex = NormalizeInitialIndex(defaultIndex, IsAllyTarget(_currentTargetType));
        EvaluateAndDispatchTargets();
    }

    #region Input Handlers

    private void HandleConfirmPressed(InputEventArgs args)
    {
        if (args.Handled || _currentBattleState != BattleState.TargetSelection) return;

        // Consume the input event to prevent bleeding
        args.Use();

        // Play feedback SFX via global Audio request
        GameEvents.Audio.RequestPlaySFX(SFXType.UI_Confirm);

        // Notify Orchestrator with payload
        OnTargetConfirmed?.Invoke(_primaryTargetIndex, new List<GameObject>(_lastAffectedTargets));
    }

    private void HandleCancelPressed(InputEventArgs args)
    {
        if (args.Handled || _currentBattleState != BattleState.TargetSelection) return;

        // Consume the input event to prevent bleeding
        args.Use();

        // Play feedback SFX via global Audio request
        GameEvents.Audio.RequestPlaySFX(SFXType.UI_Cancel);

        // Clear UI overlays/pointers via local event
        OnTargetSelectionChanged?.Invoke(null, null);

        // Notify Orchestrator to revert state
        OnTargetCancelled?.Invoke();
    }

    private void HandleLeftPressed()
    {
        if (_currentBattleState != BattleState.TargetSelection) return;

        int previousIndex = _primaryTargetIndex;
        SelectPreviousTarget();

        // Reproducir SFX si la diana realmente ha cambiado
        if (previousIndex != _primaryTargetIndex)
        {
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Move);
        }
    }

    private void HandleRightPressed()
    {
        if (_currentBattleState != BattleState.TargetSelection) return;

        int previousIndex = _primaryTargetIndex;
        SelectNextTarget();

        // Reproducir SFX si la diana realmente ha cambiado
        if (previousIndex != _primaryTargetIndex)
        {
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Move);
        }
    }

    #endregion

    #region Navigation Controls

    public void SelectNextTarget()
    {
        if (IsAllyTarget(_currentTargetType))
        {
            if (_heroList == null || _heroList.Count <= 1) return;
            _primaryTargetIndex = (_primaryTargetIndex + 1) % _heroList.Count;
        }
        else
        {
            if (_enemyList == null || _enemyList.Count <= 1) return;

            int count = _enemyList.Count;
            for (int i = 1; i <= count; i++)
            {
                int candidateIdx = (_primaryTargetIndex + i) % count;
                if (_enemyList[candidateIdx] != null) // Moves cursor only to alive enemies
                {
                    _primaryTargetIndex = candidateIdx;
                    break;
                }
            }
        }

        EvaluateAndDispatchTargets();
    }

    public void SelectPreviousTarget()
    {
        if (IsAllyTarget(_currentTargetType))
        {
            if (_heroList == null || _heroList.Count <= 1) return;
            _primaryTargetIndex = (_primaryTargetIndex - 1 + _heroList.Count) % _heroList.Count;
        }
        else
        {
            if (_enemyList == null || _enemyList.Count <= 1) return;

            int count = _enemyList.Count;
            for (int i = 1; i <= count; i++)
            {
                int candidateIdx = (_primaryTargetIndex - i + count) % count;
                if (_enemyList[candidateIdx] != null)
                {
                    _primaryTargetIndex = candidateIdx;
                    break;
                }
            }
        }

        EvaluateAndDispatchTargets();
    }

    #endregion

    #region Evaluation & Area Logic

    /// <summary>
    /// Calculates active primary and secondary targets, taking dead slot spacing into account.
    /// </summary>
    private void EvaluateAndDispatchTargets()
    {
        List<GameObject> affectedTargets = new List<GameObject>();
        GameObject primaryTargetObject = null;

        bool isAlly = IsAllyTarget(_currentTargetType);

        if (isAlly)
        {
            if (_heroList != null && _heroList.Count > 0)
            {
                _primaryTargetIndex = Mathf.Clamp(_primaryTargetIndex, 0, _heroList.Count - 1);

                switch (_currentTargetType)
                {
                    case ActionTargetType.SingleAlly:
                    case ActionTargetType.Self:
                        primaryTargetObject = _heroList[_primaryTargetIndex];
                        if (primaryTargetObject != null) affectedTargets.Add(primaryTargetObject);
                        break;

                    case ActionTargetType.AllAllies:
                        primaryTargetObject = _heroList[0];
                        foreach (GameObject hero in _heroList)
                        {
                            if (hero != null) affectedTargets.Add(hero);
                        }
                        break;
                }
            }
        }
        else
        {
            if (_enemyList != null && _enemyList.Count > 0)
            {
                _primaryTargetIndex = Mathf.Clamp(_primaryTargetIndex, 0, _enemyList.Count - 1);
                primaryTargetObject = _enemyList[_primaryTargetIndex];

                switch (_currentTargetType)
                {
                    case ActionTargetType.SingleEnemy:
                        if (primaryTargetObject != null) affectedTargets.Add(primaryTargetObject);
                        break;

                    case ActionTargetType.MultiEnemyArea:
                        // Add primary target first (guaranteed center of area)
                        if (primaryTargetObject != null)
                        {
                            affectedTargets.Add(primaryTargetObject);
                        }

                        // Calculate Golden Sun spatial area (offsets consume range even on empty/dead slots)
                        if (_currentAreaRange >= 1)
                        {
                            for (int offset = 1; offset <= _currentAreaRange; offset++)
                            {
                                int rightIndex = _primaryTargetIndex + offset;
                                int leftIndex = _primaryTargetIndex - offset;

                                // Check right spatial slot
                                if (rightIndex < _enemyList.Count)
                                {
                                    GameObject rightTarget = _enemyList[rightIndex];
                                    if (rightTarget != null) affectedTargets.Add(rightTarget); // Adds only if alive
                                }

                                // Check left spatial slot
                                if (leftIndex >= 0)
                                {
                                    GameObject leftTarget = _enemyList[leftIndex];
                                    if (leftTarget != null) affectedTargets.Add(leftTarget); // Adds only if alive
                                }
                            }
                        }
                        break;

                    case ActionTargetType.AllEnemies:
                        primaryTargetObject = GetFirstAliveEnemy();
                        foreach (GameObject enemy in _enemyList)
                        {
                            if (enemy != null) affectedTargets.Add(enemy);
                        }
                        break;
                }
            }
        }

        // Dispatch local event for UI overlays and Pointers
        _lastAffectedTargets = affectedTargets;
        OnTargetSelectionChanged?.Invoke(affectedTargets, primaryTargetObject);
    }

    #endregion

    #region Utility Helpers

    private bool IsAllyTarget(ActionTargetType targetType)
    {
        return targetType == ActionTargetType.SingleAlly ||
               targetType == ActionTargetType.AllAllies ||
               targetType == ActionTargetType.Self;
    }

    private int NormalizeInitialIndex(int requestedIndex, bool isAlly)
    {
        if (isAlly)
        {
            if (_heroList == null || _heroList.Count == 0) return 0;
            return Mathf.Clamp(requestedIndex, 0, _heroList.Count - 1);
        }

        if (_enemyList == null || _enemyList.Count == 0) return 0;

        // If requested index points to a dead slot (null), find closest alive enemy
        if (requestedIndex < _enemyList.Count && _enemyList[requestedIndex] != null)
        {
            return requestedIndex;
        }

        for (int i = 0; i < _enemyList.Count; i++)
        {
            if (_enemyList[i] != null) return i;
        }

        return 0;
    }

    private GameObject GetFirstAliveEnemy()
    {
        if (_enemyList == null) return null;
        foreach (GameObject enemy in _enemyList)
        {
            if (enemy != null) return enemy;
        }
        return null;
    }

    #endregion
}