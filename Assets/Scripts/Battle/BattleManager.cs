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
    private IReadOnlyList<GameObject> party;

    // Local GameState Cache
    private GameManager.GameState _cachedGameState;

    [Header("Enemies Configuration")]
    [SerializeField] private int maxEnemiesInBattle = 3;

    private GameObject mainCamera;

    // NEXT ACTION & TARGETING
    private BattleCommandType _selectedCommandType = BattleCommandType.Attack;
    private PsynergySO nextPsynergy;
    private ItemSO nextItem;
    private DjinnInstance nextDjinn;

    private List<GameObject> targetList = new List<GameObject>();
    private ActionTargetType currentTargetType = ActionTargetType.SingleEnemy;
    private int currentAreaRange = 0;

    // LISTS WITH FIGHTERS
    private List<GameObject> enemiesList = new List<GameObject>(); // Fixed slots, can hold nulls for defeated/empty
    private List<GameObject> heroList = new List<GameObject>();
    private List<GameObject> fighters = new List<GameObject>();

    // BATTLE FIELD POSITIONS
    private GameObject[] enemyPositions;

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
        party = activeParty;
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

        switch (commandType)
        {
            case BattleCommandType.Attack:
                PlaySFX(SFXType.UI_Confirm);
                currentTargetType = ActionTargetType.SingleEnemy;
                currentAreaRange = 0;
                targetSelectionController.InitializeTargeting(currentTargetType, currentAreaRange, heroList, enemiesList);
                CurrentBattleState = BattleState.TargetSelection;
                break;

            case BattleCommandType.Psynergy:
            case BattleCommandType.Item:
            case BattleCommandType.Djinn:
            case BattleCommandType.Summon:
                PlaySFX(SFXType.UI_Confirm);
                OnCommandTypeSelected?.Invoke(commandType);
                CurrentBattleState = BattleState.SubMenuSelection;
                break;

            case BattleCommandType.Defend:
                PlaySFX(SFXType.UI_Confirm);
                turnController.HideTurnCamera();
                CurrentBattleState = BattleState.WaitingTurnExecution;
                StartCoroutine(PerformAction());
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
        nextPsynergy = null;
        nextItem = null;
        nextDjinn = null;

        switch (commandType)
        {
            case BattleCommandType.Psynergy:
                if (actionPayload is PsynergySO psynergy)
                {
                    nextPsynergy = psynergy;
                    currentTargetType = psynergy.TargetType;
                    currentAreaRange = psynergy.AreaRange;
                }
                break;

            case BattleCommandType.Item:
                if (actionPayload is ItemSO item)
                {
                    nextItem = item;
                    currentTargetType = ActionTargetType.SingleAlly;
                    currentAreaRange = 0;
                }
                break;

            case BattleCommandType.Djinn:
                if (actionPayload is DjinnInstance djinn)
                {
                    nextDjinn = djinn;
                    if (djinn.DjinnData != null && djinn.DjinnData.UnleashEffect != null)
                    {
                        currentTargetType = djinn.DjinnData.UnleashEffect.TargetType;
                        currentAreaRange = djinn.DjinnData.UnleashEffect.AreaRange;
                    }
                    else
                    {
                        currentTargetType = ActionTargetType.SingleEnemy;
                        currentAreaRange = 0;
                    }
                }
                break;

            case BattleCommandType.Summon:
                // TODO: Adapt when SummonSO is fully defined
                currentTargetType = ActionTargetType.AllEnemies;
                currentAreaRange = 0;
                break;
        }

        targetSelectionController.InitializeTargeting(currentTargetType, currentAreaRange, heroList, enemiesList);
        CurrentBattleState = BattleState.TargetSelection;
    }

    /// <summary>
    /// Invoked directly by BattleHUDController when SubMenu cancellation occurs.
    /// </summary>
    public void HandleSubMenuCancelled()
    {
        if (_cachedGameState != GameManager.GameState.InBattle || CurrentBattleState != BattleState.SubMenuSelection)
            return;

        nextPsynergy = null;
        nextItem = null;
        nextDjinn = null;

        CurrentBattleState = BattleState.CommandSelection;
    }

    #endregion

    #region Target Selection Callbacks

    private void HandleTargetSelectionConfirmed(GameObject primaryTarget, List<GameObject> affectedTargets)
    {
        if (CurrentBattleState != BattleState.TargetSelection) return;

        actualTarget = primaryTarget;
        targetList = affectedTargets;

        turnController.HideTurnCamera();

        CurrentBattleState = BattleState.WaitingTurnExecution;
        StartCoroutine(PerformAction());
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
                nextPsynergy = null;
                nextItem = null;
                nextDjinn = null;
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
            BattleSpawnResult spawnResult = fieldSpawner.SpawnBattleField(initData, party);

            heroList = spawnResult.HeroList;
            enemiesList = spawnResult.EnemiesList;
            enemyPositions = spawnResult.EnemyPositions;
            fighters = spawnResult.AllActiveFighters;

            enemiesOnBattle = enemiesList.Count(e => e != null);
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

        yield return new WaitForSeconds(0.6f);

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
                StartCoroutine(PerformAction());
            }
            else
            {
                CurrentBattleState = BattleState.CommandSelection;
            }
        }
    }

    #endregion

    #region Action Execution & Turn Flow

    private IEnumerator PerformAction()
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

        object actionPayload = GetCurrentActionPayload();
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

        // Clean action payloads
        nextPsynergy = null;
        nextItem = null;
        nextDjinn = null;

        CheckDefeated(activeFighterObj, executedActionCost);
    }

    private object GetCurrentActionPayload()
    {
        switch (_selectedCommandType)
        {
            case BattleCommandType.Psynergy: return nextPsynergy;
            case BattleCommandType.Item: return nextItem;
            case BattleCommandType.Djinn: return nextDjinn;
            default: return null;
        }
    }

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
                StartCoroutine(PerformAction());
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

        if (party != null)
        {
            for (int i = 0; i < party.Count; i++)
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
        nextPsynergy = null;
        nextItem = null;
        nextDjinn = null;
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