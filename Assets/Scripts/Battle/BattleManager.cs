using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public FighterController Caster;          // Fighter executing the action
    public BattleCommandType CommandType;     // Attack, Psynergy, Item, Defend, etc.
    public object ActionPayload;              // PsynergySO, ItemSO, DjinnInstance, etc.
    public GameObject PrimaryTarget;          // Main target (or null if massive/global AOE)
    public List<GameObject> AffectedTargets;  // Full list of impacted target GameObjects
}

public class BattleManager : MonoBehaviour
{
    private enum ActionOutcomeState
    {
        Continue,
        Victory,
        GameOver
    }

    #region Serialized Fields & Config

    [Header("Internal Prefab References")]
    [SerializeField] private Transform audioCenter;
    [SerializeField] private BattleCameraController battleCameraController;
    [SerializeField] private TargetSelectionController targetSelectionController;
    [SerializeField] private BattleFieldSpawner fieldSpawner;
    [SerializeField] private BattleActionExecutor actionExecutor;
    [SerializeField] private BattleRewardTracker rewardTracker;
    [SerializeField] private GameObject battleField;

    #endregion

    #region Private State & Buffers

    private BattleSystemMode currentBattleMode = BattleSystemMode.GoldenSunClassic;

    private IReadOnlyList<GameObject> _cachedParty;
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

    // Golden Sun Classic Round Queue State
    private List<CombatCommand> pendingRoundCommands = new List<CombatCommand>();
    private int currentHeroSelectionIndex = 0;

    #endregion

    #region Local C# Events for Local HUD & Components

    public event Action<BattleState> OnBattleStateChanged;
    public event Action<BattleCommandType> OnCommandTypeSelected;
    public event Action<List<GameObject>, GameObject> OnTargetSelectionChanged;
    public event Action<IReadOnlyList<GameObject>, BattleRewardSummary, Action> OnBattleResultsReady;

    #endregion

    #region Properties

    public BattleSystemMode CurrentBattleMode => currentBattleMode;

    private BattleState _currentBattleState = BattleState.None;
    public BattleState CurrentBattleState
    {
        get => _currentBattleState;
        private set
        {
            if (_currentBattleState == value) return;

            Debug.Log($"[BattleManager] State Transition: {_currentBattleState} -> {value}");
            _currentBattleState = value;
            OnBattleStateChanged?.Invoke(_currentBattleState);
        }
    }

    #endregion

    #region Unity Lifecycle

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

    private void Start()
    {
        // Query current game settings upon spawning in scene via Callback
        GameEvents.Settings.RequestCurrentSettings(HandleSettingsLoaded);
    }

    private void OnEnable()
    {
        SubscribeToGameEvents();

        if (battleCameraController != null)
        {
            OnBattleStateChanged += battleCameraController.HandleBattleStateChanged;
        }

        if (targetSelectionController != null)
        {
            targetSelectionController.OnTargetConfirmed += HandleTargetSelectionConfirmed;
            targetSelectionController.OnTargetCancelled += HandleTargetSelectionCancelled;
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromGameEvents();

        if (battleCameraController != null)
        {
            OnBattleStateChanged -= battleCameraController.HandleBattleStateChanged;
        }

        if (targetSelectionController != null)
        {
            targetSelectionController.OnTargetConfirmed -= HandleTargetSelectionConfirmed;
            targetSelectionController.OnTargetCancelled -= HandleTargetSelectionCancelled;
        }
    }

    #endregion

    #region Event Subscriptions & Handlers

    private void SubscribeToGameEvents()
    {
        GameEvents.Battle.OnRequestCollisionBattle += InitializeCollisionBattle;

        GameEvents.Party.OnPartyChanged -= HandlePartyChanged;
        GameEvents.Party.OnPartyChanged += HandlePartyChanged;

        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;

        // Dynamic Settings Subscriptions
        GameEvents.Settings.OnBattleModeChanged -= HandleBattleModeChanged;
        GameEvents.Settings.OnBattleModeChanged += HandleBattleModeChanged;
    }

    private void UnsubscribeFromGameEvents()
    {
        GameEvents.Battle.OnRequestCollisionBattle -= InitializeCollisionBattle;
        GameEvents.Party.OnPartyChanged -= HandlePartyChanged;
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;

        GameEvents.Settings.OnBattleModeChanged -= HandleBattleModeChanged;
    }

    private void HandlePartyChanged(IReadOnlyList<GameObject> activeParty)
    {
        _cachedParty = activeParty;
        Debug.Log($"[BattleManager] Cached Party Updated. Count: {(_cachedParty != null ? _cachedParty.Count : 0)}");
    }

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        _cachedGameState = newState;
        Debug.Log($"[BattleManager] GameState Changed to: {newState}");
    }

    private void HandleSettingsLoaded(GameSettingsData settings)
    {
        if (settings != null)
        {
            currentBattleMode = settings.BattleMode;
            Debug.Log($"[BattleManager] BattleSystemMode queried on awake: {currentBattleMode}");
        }
    }

    private void HandleBattleModeChanged(BattleSystemMode newMode)
    {
        currentBattleMode = newMode;
        Debug.Log($"[BattleManager] BattleSystemMode updated dynamically from settings: {currentBattleMode}");
    }

    #endregion

    #region Direct Command Cross Triggers (Exposed to BattleHUDController)

    public void ExecuteCommandFromUI(BattleCommandType commandType)
    {
        Debug.Log($"[BattleManager] ExecuteCommandFromUI: {commandType} | GameState: {_cachedGameState} | BattleState: {CurrentBattleState}");

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
                FighterController caster = GetActiveSelectionCaster();
                GameObject casterObj = caster != null ? caster.gameObject : null;

                BattleActionRequest defendRequest = new BattleActionRequest
                {
                    Caster = caster,
                    CommandType = BattleCommandType.Defend,
                    ActionPayload = null,
                    PrimaryTarget = casterObj,
                    AffectedTargets = casterObj != null ? new List<GameObject> { casterObj } : new List<GameObject>()
                };

                HandleTargetSelectionConfirmed(defendRequest);
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

    public void HandleCancelCommandSelection()
    {
        if (_cachedGameState != GameManager.GameState.InBattle || CurrentBattleState != BattleState.CommandSelection)
            return;

        if (currentBattleMode != BattleSystemMode.GoldenSunClassic)
            return;

        GameEvents.Audio.RequestPlaySFX(SFXType.UI_Cancel);

        if (currentHeroSelectionIndex <= 0)
        {
            return;
        }

        int previousIndex = currentHeroSelectionIndex - 1;
        while (previousIndex >= 0)
        {
            GameObject heroObj = heroList[previousIndex];
            if (heroObj != null && heroObj.TryGetComponent<FighterStats>(out var stats) && !stats.IsDead)
            {
                break;
            }
            previousIndex--;
        }

        if (previousIndex >= 0)
        {
            currentHeroSelectionIndex = previousIndex;

            GameObject previousHeroObj = heroList[currentHeroSelectionIndex];
            pendingRoundCommands.RemoveAll(cmd => cmd.Caster != null && cmd.Caster.gameObject == previousHeroObj);

            PromptNextHeroOrExecuteRound();
        }
    }

    #endregion

    #region Target Selection Callbacks & Round Accumulation

    private void HandleTargetSelectionConfirmed(BattleActionRequest request)
    {
        Debug.Log($"[BattleManager] HandleTargetSelectionConfirmed: {request.CommandType} by {request.Caster?.name}");

        if (currentBattleMode == BattleSystemMode.FFX_CTB)
        {
            if (CurrentBattleState != BattleState.TargetSelection && request.CommandType != BattleCommandType.Defend) return;

            CurrentBattleState = BattleState.WaitingTurnExecution;
            StartCoroutine(ExecuteConfirmedActionSequence(request));
        }
        else // GoldenSunClassic Mode Behavior
        {
            FighterController caster = request.Caster ?? GetActiveSelectionCaster();
            int agility = caster != null && caster.TryGetComponent<FighterStats>(out var stats) ? stats.Agility : 10;

            CombatCommand newCommand = new CombatCommand(caster, request, agility);
            pendingRoundCommands.Add(newCommand);

            Debug.Log($"[BattleManager] Command Added for {caster?.name}. Total pending: {pendingRoundCommands.Count}");

            currentHeroSelectionIndex++;
            PromptNextHeroOrExecuteRound();
        }
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

    private void PromptNextHeroOrExecuteRound()
    {
        while (currentHeroSelectionIndex < heroList.Count)
        {
            GameObject heroObj = heroList[currentHeroSelectionIndex];
            if (heroObj != null && heroObj.TryGetComponent<FighterStats>(out var stats) && !stats.IsDead)
            {
                break;
            }
            currentHeroSelectionIndex++;
        }

        if (currentHeroSelectionIndex < heroList.Count)
        {
            GameObject nextHeroObj = heroList[currentHeroSelectionIndex];
            FighterController nextHeroController = nextHeroObj != null ? nextHeroObj.GetComponent<FighterController>() : null;

            if (turnController != null && nextHeroController != null)
            {
                turnController.SetManualCurrentFighter(nextHeroController);
            }

            CurrentBattleState = BattleState.CommandSelection;

            if (battleCameraController != null && nextHeroObj != null)
            {
                battleCameraController.SetTurnCameraOnHero(nextHeroObj);
            }
        }
        else
        {
            CurrentBattleState = BattleState.WaitingTurnExecution;

            if (battleCameraController != null)
            {
                battleCameraController.SetExecutionBaseCamera();
            }

            GenerateEnemyAICommandsForRound();
            StartCoroutine(ExecuteGoldenSunRoundRoutine());
        }
    }

    private void GenerateEnemyAICommandsForRound()
    {
        for (int i = 0; i < enemiesList.Count; i++)
        {
            GameObject enemyObj = enemiesList[i];
            if (enemyObj == null) continue;

            if (enemyObj.TryGetComponent<FighterStats>(out var stats) && !stats.IsDead &&
                enemyObj.TryGetComponent<EnemyAIController>(out var ai) &&
                enemyObj.TryGetComponent<FighterController>(out var caster))
            {
                BattleActionRequest enemyRequest = ai.BuildEnemyActionRequest(heroList, enemiesList);
                CombatCommand enemyCommand = new CombatCommand(caster, enemyRequest, stats.Agility);
                pendingRoundCommands.Add(enemyCommand);
                Debug.Log($"[BattleManager] Enemy AI Command Generated for: {enemyObj.name}");
            }
        }
    }

    #endregion

    #region Action Execution Routines (CTB & Round Execution)

    private IEnumerator ExecuteGoldenSunRoundRoutine()
    {
        Debug.Log($"[BattleManager] Starting ExecuteGoldenSunRoundRoutine with {pendingRoundCommands.Count} total commands.");

        yield return new WaitForSeconds(0.6f);

        ActivatePreDeclaredDefenses();

        List<CombatCommand> orderedCommands = pendingRoundCommands
            .OrderByDescending(c => c.RoundPriority)
            .ToList();

        for (int i = 0; i < orderedCommands.Count; i++)
        {
            CombatCommand command = orderedCommands[i];
            if (command == null || command.Caster == null) continue;

            ActivatePendingFallbackDefenses();

            command.IsExecuted = true;

            if (command.Caster.TryGetComponent<FighterStats>(out var casterStats) && casterStats.IsDead)
            {
                Debug.Log($"[BattleManager] Caster {command.Caster.name} died before its turn. Skipping command.");
                continue;
            }

            if (!command.ValidateTargets() && !command.IsDefendingFallback)
            {
                Debug.Log($"[BattleManager] Target invalid at turn start for {command.Caster.name}. Converting to Defense Fallback.");
                command.ConvertToDefenseFallback();
                if (command.Caster != null)
                {
                    command.Caster.SetUpDefense();
                }
            }

            targetList = command.Request.AffectedTargets ?? new List<GameObject>();

            if (battleCameraController != null && command.Caster != null)
            {
                battleCameraController.SetActionCamera(command.Caster.gameObject, targetList, command.Request.CommandType);
            }

            if (actionExecutor != null)
            {
                Debug.Log($"[BattleManager] Executing Command Action for {command.Caster.name} ({command.Request.CommandType})");
                yield return actionExecutor.ExecuteActionSequence(command.Request);
            }

            if (battleCameraController != null)
            {
                battleCameraController.ResetActionTargetGroup();
                battleCameraController.SetExecutionBaseCamera();
            }

            ActionOutcomeState outcome = ProcessActionOutcome(command.Caster != null ? command.Caster.gameObject : null);
            if (outcome == ActionOutcomeState.Victory)
            {
                ClearAllDefenseStances();
                StartCoroutine(BattleWinned());
                yield break;
            }
            if (outcome == ActionOutcomeState.GameOver)
            {
                ClearAllDefenseStances();
                GameOver();
                yield break;
            }

            RegisterTargetLossFallbacksSilently();
        }

        Debug.Log($"[BattleManager] Round Completed successfully. Clearing defenses and resetting round state.");

        ClearAllDefenseStances();

        pendingRoundCommands.Clear();
        currentHeroSelectionIndex = 0;
        EvaluateTurnStart();
    }

    private void RegisterTargetLossFallbacksSilently()
    {
        if (currentBattleMode != BattleSystemMode.GoldenSunClassic) return;

        for (int i = 0; i < pendingRoundCommands.Count; i++)
        {
            CombatCommand cmd = pendingRoundCommands[i];
            if (cmd == null || cmd.Caster == null) continue;

            if (!cmd.IsExecuted && !cmd.IsDefendingFallback && !cmd.ValidateTargets())
            {
                Debug.Log($"[BattleManager] Target loss registered SILENTLY for {cmd.Caster.name}. Defense stance will trigger at next turn start.");
                cmd.ConvertToDefenseFallback();
            }
        }
    }

    private void ActivatePendingFallbackDefenses()
    {
        for (int i = 0; i < pendingRoundCommands.Count; i++)
        {
            CombatCommand cmd = pendingRoundCommands[i];
            if (cmd != null && !cmd.IsExecuted && cmd.IsDefendingFallback && cmd.Caster != null)
            {
                if (!cmd.Caster.IsDefending)
                {
                    cmd.Caster.SetUpDefense();
                }
            }
        }
    }

    private void ActivatePreDeclaredDefenses()
    {
        for (int i = 0; i < pendingRoundCommands.Count; i++)
        {
            CombatCommand cmd = pendingRoundCommands[i];
            if (cmd != null && cmd.Request.CommandType == BattleCommandType.Defend && cmd.Caster != null)
            {
                cmd.Caster.SetUpDefense();
            }
        }
    }

    private void ClearAllDefenseStances()
    {
        for (int i = 0; i < fighters.Count; i++)
        {
            GameObject fighterObj = fighters[i];
            if (fighterObj == null) continue;

            if (fighterObj.TryGetComponent<FighterController>(out var controller) &&
                fighterObj.TryGetComponent<FighterStats>(out var stats))
            {
                if (!stats.IsDead && controller.IsDefending)
                {
                    controller.SetOffDefense();
                }
            }
        }
    }

    private IEnumerator ExecuteConfirmedActionSequence(BattleActionRequest request)
    {
        FighterController casterController = request.Caster ?? (turnController != null ? turnController.CurrentFighter : null);
        GameObject activeFighterObj = casterController != null ? casterController.gameObject : null;

        targetList = request.AffectedTargets ?? new List<GameObject>();

        int cost = actionExecutor != null ? actionExecutor.GetActionCost(request) : 50;
        if (turnController != null)
        {
            turnController.SetNextActionCost(cost);
        }

        if (battleCameraController != null && activeFighterObj != null)
        {
            battleCameraController.SetActionCamera(activeFighterObj, targetList, request.CommandType);
        }

        if (actionExecutor != null && casterController != null)
        {
            yield return actionExecutor.ExecuteActionSequence(request);
        }

        if (battleCameraController != null)
        {
            battleCameraController.ResetActionTargetGroup();
            battleCameraController.SetExecutionBaseCamera();
        }

        ActionOutcomeState outcome = ProcessActionOutcome(activeFighterObj);
        if (outcome == ActionOutcomeState.Victory)
        {
            StartCoroutine(BattleWinned());
        }
        else if (outcome == ActionOutcomeState.GameOver)
        {
            GameOver();
        }
        else
        {
            TurnFinished(activeFighterObj);
        }
    }

    #endregion

    #region Unified Action Outcome & Rewards Evaluation

    private ActionOutcomeState ProcessActionOutcome(GameObject activeFighter)
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
            return ActionOutcomeState.Victory;
        }

        if (fieldSpawner != null && fieldSpawner.AreAllHeroesDefeated())
        {
            return ActionOutcomeState.GameOver;
        }

        return ActionOutcomeState.Continue;
    }

    #endregion

    #region Battle Lifecycle & Setup

    public void InitializeCollisionBattle(GameObject enemyInitiator, GameObject spawnZone, CollisionAdvantageType advantageType)
    {
        Debug.Log($"[BattleManager] InitializeCollisionBattle triggered by overworld collision.");
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
        Debug.Log($"[BattleManager] StartBattle Coroutine Initiated. Mode: {currentBattleMode}");
        CurrentBattleState = BattleState.IntroTransition;

        if (battleCameraController != null)
        {
            yield return battleCameraController.PrepareSnapshotShield();
        }

        if (fieldSpawner != null)
        {
            BattleSpawnResult spawnResult = fieldSpawner.SpawnBattleField(initData, _cachedParty);

            heroList = spawnResult.HeroList;
            enemiesList = spawnResult.EnemiesList;
            fighters = spawnResult.AllActiveFighters;

            Debug.Log($"[BattleManager] Spawner Result: Heroes={heroList.Count}, Enemies={enemiesList.Count}, Total={fighters.Count}");

            if (battleCameraController != null)
            {
                List<GameObject> activeEnemiesOnly = enemiesList.Where(e => e != null).ToList();
                battleCameraController.SetupTargetGroups(heroList, activeEnemiesOnly);
            }

            if (targetSelectionController != null)
            {
                targetSelectionController.SetCombatants(heroList, enemiesList);
            }
        }

        if (HybridAudioListener.Instance != null)
        {
            Transform battleCamTransform = battleCameraController != null ? battleCameraController.transform : null;
            Transform centerTarget = audioCenter != null ? audioCenter : battleField.transform;

            HybridAudioListener.Instance.SetBattleReferences(battleCamTransform, centerTarget);
        }

        if (battleCameraController != null)
        {
            yield return battleCameraController.ExecuteFadeAndStartIntro();
            yield return new WaitForSeconds(1.5f);
        }

        Debug.Log($"[BattleManager] Intro done. Setting GameState to InBattle.");

        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.InBattle);

        InitializeBattleTurns();
    }

    private void InitializeBattleTurns()
    {
        Debug.Log($"[BattleManager] InitializeBattleTurns called.");

        if (currentBattleMode == BattleSystemMode.FFX_CTB && turnController != null)
        {
            turnController.InitializeTurnSystem(fighters);
        }

        currentHeroSelectionIndex = 0;
        pendingRoundCommands.Clear();
        EvaluateTurnStart();
    }

    #endregion

    #region Action Execution & Turn Flow

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
        Debug.Log($"[BattleManager] EvaluateTurnStart in mode {currentBattleMode}. Hero count: {heroList.Count}");

        if (currentBattleMode == BattleSystemMode.FFX_CTB)
        {
            if (turnController == null || turnController.CurrentFighter == null) return;

            FighterController currentFighter = turnController.CurrentFighter;
            currentFighter.StartsTurn();

            if (currentFighter.TryGetComponent<FighterStats>(out var stats) && !stats.IsHero)
            {
                CurrentBattleState = BattleState.WaitingTurnExecution;

                if (currentFighter.TryGetComponent<EnemyAIController>(out var ai))
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
                    battleCameraController.SetTurnCameraOnHero(currentFighter.gameObject);
                }
            }
        }
        else // GoldenSunClassic
        {
            currentHeroSelectionIndex = 0;
            pendingRoundCommands.Clear();
            PromptNextHeroOrExecuteRound();
        }
    }

    private FighterController GetActiveSelectionCaster()
    {
        if (currentBattleMode == BattleSystemMode.FFX_CTB)
        {
            return turnController != null ? turnController.CurrentFighter : null;
        }
        else
        {
            if (currentHeroSelectionIndex >= 0 && currentHeroSelectionIndex < heroList.Count)
            {
                GameObject heroObj = heroList[currentHeroSelectionIndex];
                return heroObj != null ? heroObj.GetComponent<FighterController>() : null;
            }
            return null;
        }
    }

    private IEnumerator BattleWinned()
    {
        yield return new WaitForSeconds(0.7f);
        CurrentBattleState = BattleState.VictoryFanfare;
        GameEvents.Audio.RequestPlayVictoryBGM();
        yield return new WaitForSeconds(0.5f);

        for (int i = 0; i < heroList.Count; i++)
        {
            if (heroList[i] != null && heroList[i].TryGetComponent<FighterController>(out var controller))
            {
                controller.PlayVictoryPose();
            }
        }

        yield return new WaitForSeconds(1.0f);

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

        pendingRoundCommands.Clear();
        currentHeroSelectionIndex = 0;

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