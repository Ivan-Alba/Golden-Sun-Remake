using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BattleState
{
    None,
    IntroTransition,      // Camera/Animation entering battle
    WaitingTurnExecution, // Blocked inputs while attack/psynergy/enemy AI executes
    CommandSelection,     // Choosing Attack, Psynergy, Djinn, Defense, Item, Escape...
    SubMenuSelection,     // Navigating Psynergy list or Item list
    TargetSelection,      // Selecting target enemy or ally
    VictoryFanfare,       // 3D camera fanfare, victory poses, and audio sequence
    ResultsScreen,        // EXP, Gold and Loot screen UI presentation
    GameOverScreen        // Defeat screen
}

public struct BattleActionRequest
{
    public FighterController Caster;          // Quién ejecuta la acción
    public BattleCommandType CommandType;     // Attack, Psynergy, Item, Djinn, Defend, etc.
    public object ActionPayload;              // PsynergySO, ItemSO, DjinnInstance, etc.
    public GameObject PrimaryTarget;          // Objetivo principal (o null si es global/AOE masivo)
    public List<GameObject> AffectedTargets;  // Lista completa de objetivos impactados
}

public class BattleManager : MonoBehaviour
{
    [Header("Internal Prefab References")]
    [SerializeField] private Transform audioCenter;
    [SerializeField] private BattleCameraController battleCameraController;
    [SerializeField] private TargetSelectionController targetSelectionController;
    [SerializeField] private BattleFieldSpawner fieldSpawner;
    [SerializeField] private BattleActionExecutor actionExecutor;
    [SerializeField] private BattleRewardTracker rewardTracker;
    [SerializeField] private GameObject battleField;

    private IReadOnlyList<GameObject> _cachedParty;

    // Local GameState Cache
    private GameManager.GameState _cachedGameState;

    // Active Selection State
    private BattleCommandType _selectedCommandType = BattleCommandType.Attack;
    private List<GameObject> targetList = new List<GameObject>();

    // Combatant Lists
    private List<GameObject> enemiesList = new List<GameObject>();
    private List<GameObject> heroList = new List<GameObject>();
    private List<GameObject> fighters = new List<GameObject>();

    // Local Controllers
    private TurnController turnController;

    #region Local C# Events for Local HUD & Components

    /// <summary>
    /// Fired when the battle state changes.
    /// </summary>
    public event Action<BattleState> OnBattleStateChanged;

    /// <summary>
    /// Fired when a command type (Psynergy, Item, Djinn, etc.) is chosen from the Command Cross.
    /// </summary>
    public event Action<BattleCommandType> OnCommandTypeSelected;

    /// <summary>
    /// Fired when active target selection changes.
    /// </summary>
    public event Action<List<GameObject>, GameObject> OnTargetSelectionChanged;

    /// <summary>
    /// Fired when victory fanfare concludes and results UI should be displayed.
    /// Passes active hero party, reward summary, and a completion callback to trigger battle teardown.
    /// </summary>
    public event Action<IReadOnlyList<GameObject>, BattleRewardSummary, Action> OnBattleResultsReady;

    #endregion

    // Encapsulated State Property
    private BattleState _currentBattleState = BattleState.None;
    public BattleState CurrentBattleState
    {
        get => _currentBattleState;
        private set
        {
            if (_currentBattleState == value) return;

            _currentBattleState = value;
            OnBattleStateChanged?.Invoke(_currentBattleState);
        }
    }

    private void Awake()
    {
        turnController = GetComponent<TurnController>();

        if (targetSelectionController == null)
            targetSelectionController = GetComponent<TargetSelectionController>() ?? GetComponentInChildren<TargetSelectionController>();

        if (fieldSpawner == null)
            fieldSpawner = GetComponent<BattleFieldSpawner>() ?? GetComponentInChildren<BattleFieldSpawner>();

        if (actionExecutor == null)
            actionExecutor = GetComponent<BattleActionExecutor>() ?? GetComponentInChildren<BattleActionExecutor>();

        if (rewardTracker == null)
            rewardTracker = GetComponent<BattleRewardTracker>() ?? GetComponentInChildren<BattleRewardTracker>();

        ResetBattleVariables();
    }

    private void OnEnable()
    {
        SubscribeToGameEvents();

        if (targetSelectionController != null)
        {
            targetSelectionController.OnTargetConfirmed += HandleTargetSelectionConfirmed;
            targetSelectionController.OnTargetCancelled += HandleTargetSelectionCancelled;
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromGameEvents();

        if (targetSelectionController != null)
        {
            targetSelectionController.OnTargetConfirmed -= HandleTargetSelectionConfirmed;
            targetSelectionController.OnTargetCancelled -= HandleTargetSelectionCancelled;
        }
    }

    #region Event Subscriptions

    private void SubscribeToGameEvents()
    {
        GameEvents.Battle.OnRequestCollisionBattle += InitializeCollisionBattle;

        GameEvents.Party.OnPartyChanged -= HandlePartyChanged;
        GameEvents.Party.OnPartyChanged += HandlePartyChanged;

        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;
    }

    private void UnsubscribeFromGameEvents()
    {
        GameEvents.Battle.OnRequestCollisionBattle -= InitializeCollisionBattle;
        GameEvents.Party.OnPartyChanged -= HandlePartyChanged;
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void HandlePartyChanged(IReadOnlyList<GameObject> activeParty)
    {
        _cachedParty = activeParty;
    }

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        _cachedGameState = newState;
    }

    #endregion

    #region Direct Command Cross Triggers (Exposed to BattleHUDController)

    public void ExecuteCommandFromUI(BattleCommandType commandType)
    {
        if (_cachedGameState != GameManager.GameState.InBattle || CurrentBattleState != BattleState.CommandSelection) return;

        _selectedCommandType = commandType;

        switch (commandType)
        {
            case BattleCommandType.Attack:
                if (targetSelectionController != null)
                {
                    targetSelectionController.StartTargetingForAction(commandType, null);
                }
                CurrentBattleState = BattleState.TargetSelection;
                break;

            case BattleCommandType.Psynergy:
            case BattleCommandType.Item:
            case BattleCommandType.Djinn:
            case BattleCommandType.Summon:
                OnCommandTypeSelected?.Invoke(commandType);
                CurrentBattleState = BattleState.SubMenuSelection;
                break;

            case BattleCommandType.Defend:
                CurrentBattleState = BattleState.WaitingTurnExecution;

                FighterController caster = turnController != null ? turnController.CurrentFighter : null;
                GameObject casterObj = caster != null ? caster.gameObject : null;

                BattleActionRequest defendRequest = new BattleActionRequest
                {
                    Caster = caster,
                    CommandType = BattleCommandType.Defend,
                    ActionPayload = null,
                    PrimaryTarget = casterObj,
                    AffectedTargets = casterObj != null ? new List<GameObject> { casterObj } : new List<GameObject>()
                };

                StartCoroutine(ExecuteConfirmedActionSequence(defendRequest));
                break;
        }
    }

    #endregion

    #region SubMenu Action Selection Handlers

    public void HandleSubMenuActionSelected(BattleCommandType commandType, object actionPayload)
    {
        if (_cachedGameState != GameManager.GameState.InBattle || CurrentBattleState != BattleState.SubMenuSelection)
            return;

        _selectedCommandType = commandType;

        if (targetSelectionController != null)
        {
            targetSelectionController.StartTargetingForAction(commandType, actionPayload);
        }

        CurrentBattleState = BattleState.TargetSelection;
    }

    public void HandleSubMenuCancelled()
    {
        if (_cachedGameState != GameManager.GameState.InBattle || CurrentBattleState != BattleState.SubMenuSelection)
            return;

        CurrentBattleState = BattleState.CommandSelection;
    }

    #endregion

    #region Target Selection Callbacks

    private void HandleTargetSelectionConfirmed(BattleActionRequest request)
    {
        if (CurrentBattleState != BattleState.TargetSelection) return;

        CurrentBattleState = BattleState.WaitingTurnExecution;
        StartCoroutine(ExecuteConfirmedActionSequence(request));
    }

    private IEnumerator ExecuteConfirmedActionSequence(BattleActionRequest request)
    {
        if (battleCameraController != null)
        {
            battleCameraController.HideTurnCamera();
        }

        FighterController casterController = request.Caster ?? (turnController != null ? turnController.CurrentFighter : null);
        GameObject activeFighterObj = casterController != null ? casterController.gameObject : null;

        targetList = request.AffectedTargets ?? new List<GameObject>();

        // Register action CTB cost directly to TurnController prior to sequence execution
        int cost = actionExecutor != null ? actionExecutor.GetActionCost(request) : 50;
        if (turnController != null)
        {
            turnController.SetNextActionCost(cost);
        }

        if (actionExecutor != null && casterController != null)
        {
            yield return actionExecutor.ExecuteActionSequence(request);
        }
        else
        {
            Debug.LogError($"[{nameof(BattleManager)}] CRITICAL: BattleActionExecutor or Caster is missing!");
        }

        CheckDefeated(activeFighterObj);
    }

    private void HandleTargetSelectionCancelled()
    {
        if (CurrentBattleState != BattleState.TargetSelection) return;

        switch (_selectedCommandType)
        {
            case BattleCommandType.Attack:
                CurrentBattleState = BattleState.CommandSelection;
                break;

            case BattleCommandType.Psynergy:
            case BattleCommandType.Item:
            case BattleCommandType.Djinn:
            case BattleCommandType.Summon:
                CurrentBattleState = BattleState.SubMenuSelection;
                break;
        }
    }

    #endregion

    #region Battle Lifecycle & Setup

    public void InitializeCollisionBattle(GameObject enemyInitiator, GameObject spawnZone, CollisionAdvantageType advantageType)
    {
        ResetBattleVariables();

        GameEvents.Audio.RequestPlayBattleBGM();

        EnemySpawnZone zoneController = spawnZone != null ? spawnZone.GetComponent<EnemySpawnZone>() : null;

        BattleInitializationData initData = new BattleInitializationData
        {
            SourceType = BattleSourceType.OverworldCollision,
            AdvantageType = advantageType,
            SpawnZone = zoneController,
            InitiatorWorldObject = enemyInitiator,
            CustomBattleBGM = null
        };

        StartCoroutine(StartBattle(initData));
    }

    private IEnumerator StartBattle(BattleInitializationData initData)
    {
        CurrentBattleState = BattleState.IntroTransition;

        if (battleCameraController != null)
        {
            StartCoroutine(battleCameraController.StartTransition());

            while (!battleCameraController.TransitionDone)
            {
                yield return null;
            }
        }

        if (fieldSpawner != null)
        {
            BattleSpawnResult spawnResult = fieldSpawner.SpawnBattleField(initData, _cachedParty);

            heroList = spawnResult.HeroList;
            enemiesList = spawnResult.EnemiesList;
            fighters = spawnResult.AllActiveFighters;

            if (targetSelectionController != null)
            {
                targetSelectionController.SetCombatants(heroList, enemiesList);
            }
        }
        else
        {
            Debug.LogError($"[{nameof(BattleManager)}] CRITICAL: BattleFieldSpawner component is missing!");
        }

        if (HybridAudioListener.Instance != null)
        {
            Transform battleCamTransform = battleCameraController != null ? battleCameraController.transform : null;
            Transform centerTarget = audioCenter != null ? audioCenter : battleField.transform;

            HybridAudioListener.Instance.SetBattleReferences(battleCamTransform, centerTarget);
        }

        if (battleCameraController != null)
        {
            battleCameraController.IntroBattle();
            yield return new WaitForSeconds(3f);

            StartCoroutine(battleCameraController.StartTransition());

            while (!battleCameraController.TransitionDone)
            {
                yield return null;
            }

            battleCameraController.SetInitialIdle();
            yield return new WaitForSeconds(0.7f);
        }

        InitializeBattleTurns();
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.InBattle);
    }

    private void InitializeBattleTurns()
    {
        if (turnController != null)
        {
            turnController.InitializeTurnSystem(fighters);
        }
        EvaluateTurnStart();
    }

    #endregion

    #region Action Execution & Turn Flow

    private void CheckDefeated(GameObject activeFighter)
    {
        for (int i = 0; i < targetList.Count; i++)
        {
            GameObject target = targetList[i];
            if (target == null) continue;

            if (target.TryGetComponent<FighterStats>(out var targetData) && targetData.CurrHp <= 0)
            {
                if (fieldSpawner != null)
                {
                    fieldSpawner.ProcessCombatantDeath(target, rewardTracker);
                }
            }
        }

        if (fieldSpawner != null && fieldSpawner.AreAllEnemiesDefeated())
        {
            StartCoroutine(BattleWinned());
        }
        else if (fieldSpawner != null && fieldSpawner.AreAllHeroesDefeated())
        {
            GameOver();
        }
        else
        {
            TurnFinished(activeFighter);
        }
    }

    private void TurnFinished(GameObject executedFighter)
    {
        targetList.Clear();
        if (turnController != null)
        {
            turnController.AdvanceTurn(executedFighter);
        }
        EvaluateTurnStart();
    }

    private void EvaluateTurnStart()
    {
        if (turnController == null || turnController.CurrentFighter == null) return;

        turnController.CurrentFighter.StartsTurn();

        if (turnController.CurrentFighter.TryGetComponent<EnemyStats>(out _))
        {
            CurrentBattleState = BattleState.WaitingTurnExecution;

            if (turnController.CurrentFighter.TryGetComponent<EnemyAIController>(out var ai))
            {
                BattleActionRequest enemyRequest = ai.BuildEnemyActionRequest(heroList, enemiesList);
                StartCoroutine(ExecuteConfirmedActionSequence(enemyRequest));
            }
        }
        else
        {
            CurrentBattleState = BattleState.CommandSelection;
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Confirm);

            if (battleCameraController != null)
            {
                battleCameraController.SetTurnCameraOnHero(turnController.CurrentFighter.gameObject);
            }
        }
    }

    private IEnumerator BattleWinned()
    {
        CurrentBattleState = BattleState.VictoryFanfare;

        yield return new WaitForSeconds(0.5f);
        GameEvents.Audio.RequestPlayVictoryBGM();
        yield return new WaitForSeconds(0.5f);

        for (int i = 0; i < heroList.Count; i++)
        {
            if (heroList[i] != null && heroList[i].TryGetComponent<FighterController>(out var controller))
            {
                controller.PlayVictoryPose();
            }
        }

        FighterController victor = turnController != null ? turnController.CurrentFighter : null;
        if (victor != null && battleCameraController != null)
        {
            battleCameraController.Victory(victor.gameObject);
        }

        yield return new WaitForSeconds(3.0f);

        BattleRewardSummary rewardSummary = rewardTracker != null
            ? rewardTracker.ApplyAndGetRewardSummary()
            : default;

        CurrentBattleState = BattleState.ResultsScreen;

        if (OnBattleResultsReady != null)
        {
            OnBattleResultsReady.Invoke(heroList, rewardSummary, () =>
            {
                GameEvents.Scene.RequestFullTransition(SetOffBattle);
            });
        }
        else
        {
            GameEvents.Scene.RequestFullTransition(SetOffBattle);
        }
    }

    private void SetOffBattle()
    {
        for (int i = 0; i < heroList.Count; i++)
        {
            if (heroList[i] != null && heroList[i].TryGetComponent<FighterController>(out var controller))
            {
                controller.OnBattleEnded();
            }
        }

        GameEvents.Hero.TriggerHeroStatusChanged();

        if (battleCameraController != null)
        {
            battleCameraController.SetOffBattleCamera();
        }

        if (HybridAudioListener.Instance != null)
        {
            HybridAudioListener.Instance.ClearBattleReferences();
        }

        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);

        GameEvents.Audio.RequestResumeBGM();
        ResetBattleVariables();
    }

    private void GameOver()
    {
        CurrentBattleState = BattleState.GameOverScreen;
        GameEvents.Audio.RequestPlayGameOverBGM();
    }

    private void ResetBattleVariables()
    {
        fighters.Clear();
        heroList.Clear();
        enemiesList.Clear();
        targetList.Clear();

        if (fieldSpawner != null)
        {
            fieldSpawner.ResetArenaState();
        }

        if (rewardTracker != null)
        {
            rewardTracker.ResetTracker();
        }

        _selectedCommandType = BattleCommandType.Attack;
        CurrentBattleState = BattleState.None;

        OnTargetSelectionChanged?.Invoke(null, null);
    }

    #endregion
}