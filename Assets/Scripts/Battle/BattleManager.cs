using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Random = UnityEngine.Random;

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

    private GameObject player; // player reference
    private IReadOnlyList<GameObject> _cachedParty;

    // Local GameState Cache
    private GameManager.GameState _cachedGameState;

    private GameObject mainCamera;

    // NEXT ACTION & TARGETING
    private BattleCommandType _selectedCommandType = BattleCommandType.Attack;

    private List<GameObject> targetList = new List<GameObject>();
    private ActionTargetType currentTargetType = ActionTargetType.SingleEnemy;
    private int currentAreaRange = 0;

    // LISTS WITH FIGHTERS
    private List<GameObject> enemiesList = new List<GameObject>(); // Fixed slots, can hold nulls for defeated/empty
    private List<GameObject> heroList = new List<GameObject>();
    private List<GameObject> fighters = new List<GameObject>();

    // LIST WITH TURNS OF FIGHTERS
    private List<KeyValuePair<GameObject, int>> turnsOrdered;

    // LOCAL CONTROLLERS
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

    // BATTLE STATE MACHINE (Encapsulated State Property)
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

    // AUXILIAR
    private int enemiesOnBattle, enemiesDefeated, deadHeroes;
    private GameObject actualTarget;

    // Backwards compatibility getter
    public bool WaitingAction
    {
        get => CurrentBattleState == BattleState.CommandSelection;
        set
        {
            if (!value && CurrentBattleState == BattleState.CommandSelection)
                CurrentBattleState = BattleState.WaitingTurnExecution;
        }
    }

    private void Awake()
    {
        // --- LOCAL COMPONENTS ON SAME GAMEOBJECT ---
        turnController = GetComponent<TurnController>();

        if (targetSelectionController == null)
            targetSelectionController = GetComponent<TargetSelectionController>();

        if (fieldSpawner == null)
            fieldSpawner = GetComponent<BattleFieldSpawner>() ?? GetComponentInChildren<BattleFieldSpawner>();

        if (actionExecutor == null)
            actionExecutor = GetComponent<BattleActionExecutor>() ?? GetComponentInChildren<BattleActionExecutor>();

        if (rewardTracker == null)
            rewardTracker = GetComponent<BattleRewardTracker>() ?? GetComponentInChildren<BattleRewardTracker>();

        // Ensure clean state on scene load
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

    /// <summary>
    /// Executes a command selection originated from UI/Controller hotkey inputs.
    /// </summary>
    public void ExecuteCommandFromUI(BattleCommandType commandType)
    {
        if (_cachedGameState != GameManager.GameState.InBattle || CurrentBattleState != BattleState.CommandSelection) return;

        _selectedCommandType = commandType;

        PlaySFX(SFXType.UI_Confirm);

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
                turnController.HideTurnCamera();
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

    /// <summary>
    /// Invoked directly by BattleHUDController when an action card is picked in SubMenu.
    /// </summary>
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

    /// <summary>
    /// Invoked directly by BattleHUDController when SubMenu cancellation occurs.
    /// </summary>
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

        turnController.HideTurnCamera();
        CurrentBattleState = BattleState.WaitingTurnExecution;

        // Execute action sequence directly with the encapsulated request payload
        StartCoroutine(ExecuteConfirmedActionSequence(request));
    }

    private IEnumerator ExecuteConfirmedActionSequence(BattleActionRequest request)
    {
        FighterController casterController = request.Caster ?? (turnController != null ? turnController.CurrentFighter : null);
        GameObject activeFighterObj = casterController != null ? casterController.gameObject : null;

        // Keep local references for CheckDefeated logic
        targetList = request.AffectedTargets ?? new List<GameObject>();
        actualTarget = request.PrimaryTarget;

        int executedActionCost = 50;

        if (actionExecutor != null && casterController != null)
        {
            yield return actionExecutor.ExecuteActionSequence(
                casterController,
                request.CommandType,
                request.ActionPayload,
                targetList,
                request.PrimaryTarget,
                (cost) => executedActionCost = cost
            );
        }
        else
        {
            Debug.LogError($"[{nameof(BattleManager)}] CRITICAL: BattleActionExecutor or Caster is missing!");
        }

        CheckDefeated(activeFighterObj, executedActionCost);
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

        // BATTLE MUSIC VIA AUDIO MANAGER
        GameEvents.Audio.RequestPlayBattleBGM();

        // --- EXTERNAL SCENE / CORE REFERENCES ---
        if (mainCamera == null)
            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");

        if (player != null) player.GetComponent<Animator>().enabled = false;

        EnemySpawnZone zoneController = spawnZone != null ? spawnZone.GetComponent<EnemySpawnZone>() : null;

        BattleInitializationData initData = new BattleInitializationData
        {
            SourceType = BattleSourceType.OverworldCollision,
            AdvantageType = advantageType,
            SpawnZone = zoneController,
            InitiatorWorldObject = enemyInitiator,
            CustomBattleBGM = null
        };

        // START BATTLE TRANSITION
        StartCoroutine(StartBattle(initData));
    }

    private IEnumerator StartBattle(BattleInitializationData initData)
    {
        CurrentBattleState = BattleState.IntroTransition;

        // TRANSITION MAIN CAMERA TO BATTLE CAMERA
        StartCoroutine(battleCameraController.StartTransition(mainCamera));

        while (!battleCameraController.TransitionDone)
        {
            yield return null;
        }

        // DELEGATE 3D ARENA SETUP & COMBATANT SPAWNING TO BATTLE FIELD SPAWNER
        if (fieldSpawner != null)
        {
            BattleSpawnResult spawnResult = fieldSpawner.SpawnBattleField(initData, _cachedParty);

            heroList = spawnResult.HeroList;
            enemiesList = spawnResult.EnemiesList;
            fighters = spawnResult.AllActiveFighters;

            enemiesOnBattle = enemiesList.Count(e => e != null);

            // Register combatants into TargetSelectionController
            if (targetSelectionController != null)
            {
                targetSelectionController.SetCombatants(heroList, enemiesList);
            }
        }
        else
        {
            Debug.LogError($"[{nameof(BattleManager)}] CRITICAL: BattleFieldSpawner component is missing!");
        }

        // --- DYNAMIC AUDIO LISTENER SETUP ---
        if (HybridAudioListener.Instance != null)
        {
            Transform battleCamTransform = battleCameraController != null ? battleCameraController.transform : null;
            Transform centerTarget = audioCenter != null ? audioCenter : battleField.transform;

            HybridAudioListener.Instance.SetBattleReferences(battleCamTransform, centerTarget);
        }

        // BATTLE CAMERA INTRO ANIMATION
        battleCameraController.IntroBattle();
        yield return new WaitForSeconds(2f);

        // ENEMY ANIMATION INTRO
        yield return new WaitForSeconds(1f);

        // TODO BATTLE CAMERA FADE
        StartCoroutine(battleCameraController.StartTransition());

        while (!battleCameraController.TransitionDone)
        {
            yield return null;
        }

        battleCameraController.SetInitialIdle();

        yield return new WaitForSeconds(1.0f);

        // Initializing Turn System and starting first turn
        InitializeBattleTurns();

        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.InBattle);
    }

    private void InitializeBattleTurns()
    {
        turnsOrdered = turnController.InitializeTurnSystem(fighters);

        if (turnController.CurrentFighter != null)
        {
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
            }
        }
    }

    #endregion

    #region Action Execution & Turn Flow

    /*private IEnumerator PerformAction()
    {
        if (turnController.CurrentFighter == null) yield break;

        FighterController casterController = turnController.CurrentFighter;
        GameObject activeFighterObj = casterController.gameObject;

        // FORCE COMMAND TYPE TO ATTACK FOR ENEMIES TO PREVENT PREVIOUS HERO COMMAND OVERRIDE
        if (activeFighterObj.TryGetComponent<EnemyStats>(out _))
        {
            _selectedCommandType = BattleCommandType.Attack;
            targetList.Clear();

            // Resolve AI Target for Enemy
            if (activeFighterObj.TryGetComponent<EnemyAIController>(out var ai))
            {
                GameObject chosenTarget = ai.SelectTarget(heroList);
                if (chosenTarget != null) targetList.Add(chosenTarget);
            }

            if (targetList.Count == 0 && heroList != null && heroList.Count > 0)
            {
                targetList.Add(heroList[Random.Range(0, heroList.Count)]);
            }

            actualTarget = targetList.Count > 0 ? targetList[0] : null;
        }

        object actionPayload = null;
        GameObject primaryTargetPos = actualTarget;

        int executedActionCost = 50;

        // DELEGATE ENTIRE COMBAT SEQUENCE TO BATTLE ACTION EXECUTOR
        if (actionExecutor != null)
        {
            yield return actionExecutor.ExecuteActionSequence(
                casterController,
                _selectedCommandType,
                actionPayload,
                targetList,
                primaryTargetPos,
                (cost) => executedActionCost = cost
            );
        }
        else
        {
            Debug.LogError($"[{nameof(BattleManager)}] CRITICAL: BattleActionExecutor component is missing!");
        }

        CheckDefeated(activeFighterObj, executedActionCost);
    }*/

    private void CheckDefeated(GameObject activeFighter, int baseActionCost)
    {
        foreach (GameObject target in targetList)
        {
            if (target == null) continue;

            FighterStats targetData = target.GetComponent<FighterStats>();
            if (targetData != null && targetData.CurrHp <= 0)
            {
                if (targetData.gameObject.TryGetComponent<EnemyStats>(out var enemyStats))
                {
                    // Track rewards and loot tables via BattleRewardTracker
                    if (rewardTracker != null)
                    {
                        rewardTracker.TrackEnemyDefeated(enemyStats);
                    }

                    fighters.Remove(targetData.gameObject);

                    // Preserve spatial slot indices by setting cell to null instead of removing from enemiesList
                    int slotIndex = enemiesList.IndexOf(targetData.gameObject);
                    if (slotIndex >= 0)
                    {
                        enemiesList[slotIndex] = null;
                    }

                    Destroy(targetData.gameObject);
                    enemiesDefeated++;
                }
                else
                {
                    fighters.Remove(targetData.gameObject);
                }
            }
        }

        deadHeroes = 0;
        foreach (GameObject hero in heroList)
        {
            if (hero.GetComponent<FighterStats>().CurrHp <= 0)
            {
                deadHeroes++;
            }
        }

        if (enemiesDefeated == enemiesOnBattle)
        {
            StartCoroutine(BattleWinned());
        }
        else if (deadHeroes == heroList.Count)
        {
            GameOver();
        }
        else
        {
            TurnFinished(activeFighter, baseActionCost);
        }
    }

    private void TurnFinished(GameObject executedFighter, int baseActionCost)
    {
        targetList = new List<GameObject>();

        turnController.AdvanceTurn(executedFighter, baseActionCost);
        turnsOrdered = turnController.BuildTurnProjection();

        if (turnController.CurrentFighter != null)
        {
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
            }
        }
    }

    private IEnumerator BattleWinned()
    {
        // 1. Enter VictoryFanfare state (Blocks UI inputs while 3D animations execute)
        CurrentBattleState = BattleState.VictoryFanfare;

        yield return new WaitForSeconds(0.5f);

        GameEvents.Audio.RequestPlayVictoryBGM();

        yield return new WaitForSeconds(0.5f);

        foreach (GameObject hero in heroList)
        {
            if (hero.GetComponent<FighterStats>().CurrHp > 0)
                hero.GetComponentInChildren<Animator>().SetBool("victoryPose", true);
        }

        if (turnsOrdered != null && turnsOrdered.Count > 0 && turnsOrdered.ElementAt(0).Key != null)
        {
            battleCameraController.Victory(turnsOrdered.ElementAt(0).Key);
        }

        yield return new WaitForSeconds(3.0f);

        // 2. Apply non-XP rewards & fetch summary from tracker
        BattleRewardSummary rewardSummary = rewardTracker != null
            ? rewardTracker.ApplyAndGetRewardSummary()
            : default;

        // 3. Transition to ResultsScreen state to signal active results UI
        CurrentBattleState = BattleState.ResultsScreen;

        // 4. Delegate UI flow via local event, passing completion callback to trigger battle teardown
        if (OnBattleResultsReady != null)
        {
            OnBattleResultsReady.Invoke(heroList, rewardSummary, () =>
            {
                GameEvents.Scene.RequestFullTransition(SetOffBattle);
            });
        }
        else
        {
            // Fallback teardown if no HUD listener is attached
            GameEvents.Scene.RequestFullTransition(SetOffBattle);
        }
    }

    private void SetOffBattle()
    {
        foreach (GameObject hero in heroList)
        {
            if (hero.GetComponent<FighterStats>().CurrHp > 0)
                hero.GetComponentInChildren<Animator>().SetBool("victoryPose", false);
        }

        GameEvents.Hero.TriggerHeroStatusChanged();

        battleCameraController.SetOffBattleCamera();

        mainCamera.GetComponent<Camera>().enabled = true;

        if (HybridAudioListener.Instance != null)
        {
            HybridAudioListener.Instance.ClearBattleReferences();
        }

        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);

        if (player != null) player.GetComponent<Animator>().enabled = true;

        GameEvents.Audio.RequestResumeBGM();

        if (_cachedParty != null)
        {
            for (int i = 0; i < _cachedParty.Count; i++)
            {
                if (i < heroList.Count)
                {
                    heroList[i].GetComponent<FighterController>().NextTurnCD = 0;
                    heroList[i].GetComponent<FighterController>().SetOffDefense();
                }
            }
        }

        ResetBattleVariables();
    }

    private void GameOver()
    {
        CurrentBattleState = BattleState.GameOverScreen;

        GameObject crossfade = transform.Find("Crossfade").gameObject;
        crossfade.SetActive(true);
        crossfade.transform.Find("GameOver").gameObject.SetActive(true);
        crossfade.GetComponent<Animator>().SetTrigger("start");

        GameEvents.Audio.RequestPlayGameOverBGM();

        Debug.Log("GAMEOVER");
    }

    #endregion

    #region State & Target Helpers

    private bool IsAllyTarget(ActionTargetType targetType)
    {
        return targetType == ActionTargetType.SingleAlly ||
               targetType == ActionTargetType.AllAllies ||
               targetType == ActionTargetType.Self;
    }

    private void PlaySFX(SFXType sfx)
    {
        GameEvents.Audio.RequestPlaySFX(sfx);
    }

    private void ResetBattleVariables()
    {
        fighters.Clear();
        heroList.Clear();
        enemiesList.Clear();
        targetList.Clear();
        if (turnsOrdered != null) turnsOrdered.Clear();

        if (rewardTracker != null)
        {
            rewardTracker.ResetTracker();
        }

        enemiesDefeated = 0;
        enemiesOnBattle = 0;
        deadHeroes = 0;
        actualTarget = null;

        _selectedCommandType = BattleCommandType.Attack;
        currentTargetType = ActionTargetType.SingleEnemy;
        currentAreaRange = 0;
        CurrentBattleState = BattleState.None;

        OnTargetSelectionChanged?.Invoke(null, null);
    }

    public void ChangeBattleSpeed()
    {
        GameEvents.GameState.RequestToggleGameSpeed();
    }

    #endregion
}