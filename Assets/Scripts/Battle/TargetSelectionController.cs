using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Component responsible for managing battle target selection logic, 
/// spatial area calculations, cycling navigation, target confirmation/cancellation,
/// and driving 3D selection pointer visuals across active fighters.
/// Preserves original Golden Sun grid spaces even if enemies are defeated.
/// </summary>
public class TargetSelectionController : MonoBehaviour
{
    [Header("3D Pointer Configuration")]
    [Tooltip("Scale multiplier for the primary targeted fighter's 3D pointer.")]
    [SerializeField] private float primaryPointerScale = 1.0f;

    [Tooltip("Scale multiplier for secondary area-affected fighters' 3D pointers.")]
    [SerializeField] private float secondaryPointerScale = 0.7f;

    private ActionTargetType _currentTargetType;
    private int _currentAreaRange;
    private int _primaryTargetIndex;

    private BattleCommandType _currentCommandType;
    private object _currentActionPayload;

    private IReadOnlyList<GameObject> _heroList;
    private List<GameObject> _enemyList;

    private List<GameObject> _lastAffectedTargets = new List<GameObject>();
    private GameObject _lastPrimaryTargetObject;

    private BattleState _currentBattleState = BattleState.None;

    // Direct local reference to BattleManager on the same GameObject
    private BattleManager _battleManager;

    #region Properties

    /// <summary>
    /// Indicates whether the active targeting selection is currently focused on allies or self.
    /// </summary>
    public bool IsTargetingAllies => IsAllyTarget(_currentTargetType);

    public int PrimaryTargetIndex => _primaryTargetIndex;

    #endregion

    #region Local C# Events

    /// <summary>
    /// Triggered when the user confirms target selection.
    /// Passes the complete BattleActionRequest payload ready for execution.
    /// Notifies the Orchestrator (BattleManager).
    /// </summary>
    public event Action<BattleActionRequest> OnTargetConfirmed;

    /// <summary>
    /// Triggered when the user cancels target selection.
    /// Notifies the Orchestrator (BattleManager).
    /// </summary>
    public event Action OnTargetCancelled;

    /// <summary>
    /// Triggered locally when the active targeted GameObjects or primary target change.
    /// Listened to by local components like BattleHUDController to update overlays.
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

        // Hide pointers automatically when leaving target selection state
        if (_currentBattleState != BattleState.TargetSelection)
        {
            ClearAllPointers();
        }
    }

    /// <summary>
    /// Registers/updates the active combatant lists. Call this at battle initialization or turn start.
    /// </summary>
    public void SetCombatants(IReadOnlyList<GameObject> heroList, List<GameObject> enemyList)
    {
        _heroList = heroList;
        _enemyList = enemyList;
    }

    /// <summary>
    /// Initiates targeting sequence directly from a command type and payload, resolving target rules automatically.
    /// </summary>
    public void StartTargetingForAction(BattleCommandType commandType, object actionPayload, int defaultIndex = -1)
    {
        _currentCommandType = commandType;
        _currentActionPayload = actionPayload;

        ActionTargetType targetType = ActionTargetType.SingleEnemy;
        int areaRange = 0;

        switch (commandType)
        {
            case BattleCommandType.Attack:
                targetType = ActionTargetType.SingleEnemy;
                areaRange = 0;
                break;

            case BattleCommandType.Psynergy:
                if (actionPayload is PsynergySO psynergy)
                {
                    targetType = psynergy.TargetType;
                    areaRange = psynergy.AreaRange;
                }
                break;

            case BattleCommandType.Item:
                if (actionPayload is ItemSO item)
                {
                    targetType = ActionTargetType.SingleAlly;
                    areaRange = 0;
                }
                break;

            case BattleCommandType.Djinn:
                if (actionPayload is DjinnInstance djinn)
                {
                    if (djinn.DjinnData != null && djinn.DjinnData.UnleashEffect != null)
                    {
                        targetType = djinn.DjinnData.UnleashEffect.TargetType;
                        areaRange = djinn.DjinnData.UnleashEffect.AreaRange;
                    }
                    else
                    {
                        targetType = ActionTargetType.SingleEnemy;
                        areaRange = 0;
                    }
                }
                break;

            case BattleCommandType.Summon:
                // TODO: Adapt when SummonSO is fully defined
                targetType = ActionTargetType.AllEnemies;
                areaRange = 0;
                break;
        }

        InitializeTargeting(targetType, areaRange, _heroList, _enemyList, defaultIndex);
    }

    /// <summary>
    /// Initializes targeting state for a new selection sequence.
    /// Pass defaultIndex as -1 to force calculating the center alive enemy.
    /// </summary>
    public void InitializeTargeting(
        ActionTargetType targetType,
        int areaRange,
        IReadOnlyList<GameObject> heroList,
        List<GameObject> enemyList,
        int defaultIndex = -1)
    {
        _currentTargetType = targetType;
        _currentAreaRange = Mathf.Max(0, areaRange);
        _heroList = heroList;
        _enemyList = enemyList;

        _primaryTargetIndex = NormalizeInitialIndex(defaultIndex, IsTargetingAllies);
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

        // Turn off all 3D pointers upon confirmation
        ClearAllPointers();

        // Construct unified BattleActionRequest payload
        FighterController caster = null;
        if (_battleManager != null && _battleManager.GetComponent<TurnController>() != null)
        {
            caster = _battleManager.GetComponent<TurnController>().CurrentFighter;
        }

        BattleActionRequest actionRequest = new BattleActionRequest
        {
            Caster = caster,
            CommandType = _currentCommandType,
            ActionPayload = _currentActionPayload,
            PrimaryTarget = _lastPrimaryTargetObject,
            AffectedTargets = new List<GameObject>(_lastAffectedTargets)
        };

        // Notify Orchestrator
        OnTargetConfirmed?.Invoke(actionRequest);
    }

    private void HandleCancelPressed(InputEventArgs args)
    {
        if (args.Handled || _currentBattleState != BattleState.TargetSelection) return;

        // Consume the input event to prevent bleeding
        args.Use();

        // Play feedback SFX via global Audio request
        GameEvents.Audio.RequestPlaySFX(SFXType.UI_Cancel);

        // Turn off all 3D pointers
        ClearAllPointers();

        // Clear UI overlays via local event
        OnTargetSelectionChanged?.Invoke(null, null);

        // Notify Orchestrator to revert state
        OnTargetCancelled?.Invoke();
    }

    private void HandleLeftPressed()
    {
        if (_currentBattleState != BattleState.TargetSelection) return;

        int previousIndex = _primaryTargetIndex;
        SelectPreviousTarget();

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

        if (previousIndex != _primaryTargetIndex)
        {
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Move);
        }
    }

    #endregion

    #region Navigation Controls

    public void SelectNextTarget()
    {
        if (IsTargetingAllies)
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
        if (IsTargetingAllies)
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

    #region Evaluation, Area Logic & 3D Pointer Presentation

    /// <summary>
    /// Calculates active primary and secondary targets, taking dead slot spacing into account,
    /// and updates 3D selection pointers on active target FighterControllers.
    /// </summary>
    private void EvaluateAndDispatchTargets()
    {
        List<GameObject> affectedTargets = new List<GameObject>();
        GameObject primaryTargetObject = null;

        bool isAlly = IsTargetingAllies;

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
                        primaryTargetObject = null; // Global action: no single primary focus
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
                        if (primaryTargetObject != null)
                        {
                            affectedTargets.Add(primaryTargetObject);
                        }

                        if (_currentAreaRange >= 1)
                        {
                            for (int offset = 1; offset <= _currentAreaRange; offset++)
                            {
                                int rightIndex = _primaryTargetIndex + offset;
                                int leftIndex = _primaryTargetIndex - offset;

                                if (rightIndex < _enemyList.Count)
                                {
                                    GameObject rightTarget = _enemyList[rightIndex];
                                    if (rightTarget != null) affectedTargets.Add(rightTarget);
                                }

                                if (leftIndex >= 0)
                                {
                                    GameObject leftTarget = _enemyList[leftIndex];
                                    if (leftTarget != null) affectedTargets.Add(leftTarget);
                                }
                            }
                        }
                        break;

                    case ActionTargetType.AllEnemies:
                        primaryTargetObject = null; // Global action: no single primary focus
                        foreach (GameObject enemy in _enemyList)
                        {
                            if (enemy != null) affectedTargets.Add(enemy);
                        }
                        break;
                }
            }
        }

        _lastAffectedTargets = affectedTargets;
        _lastPrimaryTargetObject = primaryTargetObject;

        // --- UPDATE 3D SELECTION POINTERS ---
        Update3DPointers(affectedTargets, primaryTargetObject);

        // Dispatch local event for UI overlays
        OnTargetSelectionChanged?.Invoke(affectedTargets, primaryTargetObject);
    }

    /// <summary>
    /// Updates visibility and scaling for 3D selection pointers on all hero and enemy fighters,
    /// ensuring all active pointers reset their animations in sync.
    /// </summary>
    private void Update3DPointers(List<GameObject> affectedTargets, GameObject primaryTarget)
    {
        // 1. First, disable pointers on all known units
        ClearAllPointers();

        // 2. Enable, scale, and reset pointers on active affected targets simultaneously
        foreach (GameObject targetObj in affectedTargets)
        {
            if (targetObj == null) continue;

            if (targetObj.TryGetComponent<FighterController>(out var controller))
            {
                bool isPrimary = (primaryTarget == null || targetObj == primaryTarget);
                controller.SetSelectionPointerVisible(true, isPrimary, primaryPointerScale, secondaryPointerScale);
            }
        }
    }

    /// <summary>
    /// Hides all 3D selection pointers across heroes and enemies.
    /// </summary>
    public void ClearAllPointers()
    {
        if (_heroList != null)
        {
            foreach (GameObject hero in _heroList)
            {
                if (hero != null && hero.TryGetComponent<FighterController>(out var controller))
                {
                    controller.SetSelectionPointerVisible(false);
                }
            }
        }

        if (_enemyList != null)
        {
            foreach (GameObject enemy in _enemyList)
            {
                if (enemy != null && enemy.TryGetComponent<FighterController>(out var controller))
                {
                    controller.SetSelectionPointerVisible(false);
                }
            }
        }
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

        if (requestedIndex >= 0 && requestedIndex < _enemyList.Count && _enemyList[requestedIndex] != null)
        {
            return requestedIndex;
        }

        return GetDefaultCenterEnemyIndex();
    }

    private int GetDefaultCenterEnemyIndex()
    {
        if (_enemyList == null || _enemyList.Count == 0) return 0;

        int centerIndex = _enemyList.Count / 2;

        if (_enemyList[centerIndex] != null)
        {
            return centerIndex;
        }

        for (int offset = 1; offset < _enemyList.Count; offset++)
        {
            int rightIndex = centerIndex + offset;
            int leftIndex = centerIndex - offset;

            if (rightIndex < _enemyList.Count && _enemyList[rightIndex] != null)
            {
                return rightIndex;
            }

            if (leftIndex >= 0 && _enemyList[leftIndex] != null)
            {
                return leftIndex;
            }
        }

        return 0;
    }

    private GameObject GetDefaultCenterEnemyObject()
    {
        int index = GetDefaultCenterEnemyIndex();
        if (_enemyList != null && index >= 0 && index < _enemyList.Count)
        {
            return _enemyList[index];
        }
        return null;
    }

    #endregion
}