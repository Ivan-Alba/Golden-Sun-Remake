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
    ResultsScreen,        // EXP, Gold and Loot screen
    GameOverScreen        // Defeat screen
}

public class BattleManager : MonoBehaviour
{
    [Header("Internal Prefab References")]
    [SerializeField] private Transform audioCenter;
    [SerializeField] private BattleCameraController battleCameraController;
    [SerializeField] private ResultsScreenController resultsController;
    [SerializeField] private TargetSelectionController targetSelectionController;
    [SerializeField] private GameObject battleField;
    [SerializeField] private GameObject[] heroPositions = new GameObject[4];

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

    // RESPAWN CONTROLLER
    private EnemySpawnZone spawnController;

    // LISTS WITH FIGHTERS
    private List<GameObject> enemiesList = new List<GameObject>(); // Fixed slots, can hold nulls for defeated/empty
    private List<GameObject> heroList = new List<GameObject>();
    private List<GameObject> fighters = new List<GameObject>();

    // RESULTS SCREEN
    private int totalXpEarned, totalPhEarned, totalGoldEarned;
    private Dictionary<ItemSO, int> totalItemsObtained = new Dictionary<ItemSO, int>();

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
    /// Fired when active target pointers change.
    /// </summary>
    public event Action<List<GameObject>, GameObject> OnTargetSelectionChanged;

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
    private int actualTarget;

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

        // Ensure clean state on scene load
        ResetBattleVariables();
    }

    private void OnEnable()
    {
        SubscribeToInputEvents();
        SubscribeToGameEvents();

        if (targetSelectionController != null)
        {
            targetSelectionController.OnTargetConfirmed += HandleTargetSelectionConfirmed;
            targetSelectionController.OnTargetCancelled += HandleTargetSelectionCancelled;
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromInputEvents();
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

    private void SubscribeToInputEvents()
    {
        UnsubscribeFromInputEvents();

        GameEvents.Input.OnBattleConfirmPressed += HandleConfirmPressed;
    }

    private void UnsubscribeFromInputEvents()
    {
        GameEvents.Input.OnBattleConfirmPressed -= HandleConfirmPressed;
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

    private void HandleTargetSelectionConfirmed(int primaryIndex, List<GameObject> affectedTargets)
    {
        if (CurrentBattleState != BattleState.TargetSelection) return;

        actualTarget = primaryIndex;
        targetList = affectedTargets;

        turnController.HideTurnCamera();
        HideAllPointers();

        CurrentBattleState = BattleState.WaitingTurnExecution;
        StartCoroutine(PerformAction());
    }

    private void HandleTargetSelectionCancelled()
    {
        if (CurrentBattleState != BattleState.TargetSelection) return;

        HideAllPointers();

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

    #region Input Handlers by Battle State

    private void HandleConfirmPressed(InputEventArgs args)
    {
        if (args.Handled || _cachedGameState != GameManager.GameState.InBattle) return;

        switch (CurrentBattleState)
        {
            case BattleState.ResultsScreen:
                if (resultsController != null)
                {
                    args.Use();
                    resultsController.HandleConfirmInput();
                }
                break;

            case BattleState.GameOverScreen:
                break;
        }
    }

    #endregion

    #region Battle Lifecycle & Setup

    public void InitializeCollisionBattle(GameObject enemyInitiator, GameObject spawnZone, EncounterType encounterType)
    {
        ResetBattleVariables();

        // BATTLE MUSIC VIA AUDIO MANAGER
        GameEvents.Audio.RequestPlayBattleBGM();

        // --- EXTERNAL SCENE / CORE REFERENCES ---
        if (mainCamera == null)
            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");


        enemiesOnBattle = Random.Range(1, maxEnemiesInBattle + 1);

        // RESPAWN OBJECTS
        if (spawnZone != null)
            spawnController = spawnZone.GetComponent<EnemySpawnZone>();

        if (player != null) player.GetComponent<Animator>().enabled = false;

        // START BATTLE TRANSITION
        StartCoroutine(StartBattle(enemyInitiator));
    }

    private IEnumerator StartBattle(GameObject firstEnemy)
    {
        CurrentBattleState = BattleState.IntroTransition;

        // TRANSITION MAIN CAMERA TO BATTLE CAMERA
        StartCoroutine(battleCameraController.StartTransition(mainCamera));

        while (!battleCameraController.TransitionDone)
        {
            yield return null;
        }

        // SET HEROES
        int i = 0;
        if (party != null && party.Count <= 2) i = 1;

        if (party != null)
        {
            foreach (GameObject hero in party)
            {
                if (hero == null) continue;

                // IF HERO IS ALIVE
                if (hero.GetComponent<FighterStats>().CurrHp != 0)
                {
                    fighters.Add(hero);
                    heroList.Add(hero);
                }
                // HERO POSITIONS
                hero.transform.position = heroPositions[i++].transform.position;
            }
        }

        // SET ENEMIES
        SetEnemiesOnBattle(firstEnemy);

        // DESTROY WORLD ENEMY
        if (firstEnemy != null) Destroy(firstEnemy);

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

        // Initializing Turn System and starting first turn
        InitializeBattleTurns();

        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.InBattle);
    }

    private void SetEnemiesOnBattle(GameObject firstEnemy)
    {
        enemyPositions = new GameObject[maxEnemiesInBattle];
        enemiesList = new List<GameObject>(new GameObject[maxEnemiesInBattle]); // Initialize fixed-size list with nulls

        int centerIndex = maxEnemiesInBattle / 2;

        // Sequence of slots radiating outward from center: center, left, right, left-2, right-2...
        List<int> spawnSlotsOrder = CalculateSpawnSlotsOrder(centerIndex, maxEnemiesInBattle);

        Debug.Log($"Spawning {enemiesOnBattle} enemies across {maxEnemiesInBattle} total spatial slots. Center index: {centerIndex}");

        for (int i = 0; i < enemiesOnBattle; i++)
        {
            int slotIndex = spawnSlotsOrder[i];
            GameObject enemyPlace = battleField.transform.Find($"Enemy{slotIndex}").gameObject;

            GameObject enemyPrefab = (i == 0)
                ? spawnController.GetMatchingEnemyPrefab(firstEnemy)
                : spawnController.GetRandomEnemyPrefab();

            GameObject spawnedEnemy = Instantiate(enemyPrefab, enemyPlace.transform.position, Quaternion.identity);

            enemyPositions[slotIndex] = enemyPlace;
            enemiesList[slotIndex] = spawnedEnemy;
            fighters.Add(spawnedEnemy);
        }
    }

    /// <summary>
    /// Calculates slot indices order starting from center and expanding outwards symmetrically.
    /// Example for 3 slots: [1, 0, 2]
    /// Example for 7 slots: [3, 2, 4, 1, 5, 0, 6]
    /// </summary>
    private List<int> CalculateSpawnSlotsOrder(int centerIndex, int maxSlots)
    {
        List<int> order = new List<int> { centerIndex };

        for (int offset = 1; order.Count < maxSlots; offset++)
        {
            int left = centerIndex - offset;
            int right = centerIndex + offset;

            if (left >= 0) order.Add(left);
            if (right < maxSlots) order.Add(right);
        }

        return order;
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
        FighterStats targetStats = null;
        GameObject activeFighter = turnController.CurrentFighter.gameObject;

        // FORCE COMMAND TYPE TO ATTACK FOR ENEMIES TO PREVENT PREVIOUS HERO COMMAND OVERRIDE
        if (activeFighter.TryGetComponent<EnemyStats>(out _))
        {
            _selectedCommandType = BattleCommandType.Attack;
        }

        int baseActionCost = 50; // Default action cost (Attack)

        switch (_selectedCommandType)
        {
            case BattleCommandType.Attack:
                baseActionCost = 50;
                if (activeFighter.TryGetComponent<EnemyStats>(out _))
                {
                    targetList.Clear();

                    if (activeFighter.TryGetComponent<EnemyAIController>(out var ai))
                    {
                        GameObject chosenTarget = ai.SelectTarget(heroList);
                        if (chosenTarget != null)
                        {
                            targetStats = chosenTarget.GetComponent<FighterStats>();
                        }
                    }

                    if (targetStats == null && heroList != null && heroList.Count > 0)
                    {
                        targetStats = heroList[UnityEngine.Random.Range(0, heroList.Count)].GetComponent<FighterStats>();
                    }

                    if (targetStats != null)
                    {
                        targetList.Add(targetStats.gameObject);
                    }
                }
                else
                {
                    if (actualTarget >= 0 && actualTarget < enemiesList.Count && enemiesList[actualTarget] != null)
                    {
                        targetStats = enemiesList[actualTarget].GetComponent<FighterStats>();
                    }
                }

                if (targetStats != null)
                {
                    StartCoroutine(turnController.CurrentFighter.BasicAttack(targetStats.gameObject));

                    while (!turnController.CurrentFighter.ActionFinished)
                    {
                        yield return null;
                    }

                    turnController.CurrentFighter.ActionFinished = false;
                }
                break;

            case BattleCommandType.Psynergy:
                baseActionCost = 60;

                if (nextPsynergy != null && IsAllyTarget(nextPsynergy.TargetType))
                {
                    StartCoroutine(turnController.CurrentFighter.UsePsynergy(nextPsynergy, party[actualTarget], targetList));
                }
                else
                {
                    StartCoroutine(turnController.CurrentFighter.UsePsynergy(nextPsynergy, enemyPositions[actualTarget], targetList));
                }

                while (!turnController.CurrentFighter.ActionFinished)
                {
                    yield return null;
                }

                turnController.CurrentFighter.ActionFinished = false;
                nextPsynergy = null;
                break;

            case BattleCommandType.Defend:
                baseActionCost = 30;
                turnController.CurrentFighter.SetUpDefense();
                yield return new WaitForSeconds(1.5f);
                break;
        }

        CheckDefeated(activeFighter, baseActionCost);
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
                    totalXpEarned += enemyStats.XpReward;
                    totalGoldEarned += enemyStats.CoinsReward;
                    totalPhEarned += enemyStats.CoinsReward;

                    GetItemLoot(enemyStats);

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

    private void GetItemLoot(EnemyStats enemyStats)
    {
        if (enemyStats == null || enemyStats.PossibleDrops == null) return;

        List<ItemDropRate> droppedItems = enemyStats.PossibleDrops;

        foreach (ItemDropRate droppedItem in droppedItems)
        {
            if (string.IsNullOrEmpty(droppedItem.itemSlug)) continue;

            ItemSO item = null;
            GameEvents.Database.RequestItemBySlug(droppedItem.itemSlug, (foundItem) => item = foundItem);
            if (item == null) continue;

            if (totalItemsObtained.ContainsKey(item))
            {
                totalItemsObtained[item] += 1;
            }
            else
            {
                totalItemsObtained.Add(item, 1);
            }
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
        CurrentBattleState = BattleState.ResultsScreen;

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

        yield return new WaitForSeconds(3);

        StartCoroutine(resultsController.ShowResults(heroList, totalXpEarned, totalPhEarned, totalGoldEarned, totalItemsObtained));
        while (!resultsController.ResultsFinished)
        {
            yield return null;
        }

        GameEvents.Scene.RequestFullTransition(SetOffBattle);
        yield return null;
    }

    private void SetOffBattle()
    {
        resultsController.Reset();

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

    private void HideAllPointers()
    {
        if (enemyPositions != null)
        {
            for (int i = 0; i < enemyPositions.Length; i++)
            {
                if (enemyPositions[i] != null)
                {
                    Transform pointer = enemyPositions[i].transform.Find("Pointer");
                    if (pointer != null) pointer.gameObject.SetActive(false);
                }
            }
        }

        if (party != null)
        {
            foreach (GameObject hero in party)
            {
                if (hero != null)
                {
                    Transform pointer = hero.transform.Find("Pointer");
                    if (pointer != null) pointer.gameObject.SetActive(false);
                }
            }
        }

        OnTargetSelectionChanged?.Invoke(null, null);
    }

    private void ResetBattleVariables()
    {
        fighters.Clear();
        heroList.Clear();
        enemiesList.Clear();
        targetList.Clear();
        if (turnsOrdered != null) turnsOrdered.Clear();

        if (totalItemsObtained == null)
            totalItemsObtained = new Dictionary<ItemSO, int>();
        else
            totalItemsObtained.Clear();

        totalXpEarned = 0;
        totalGoldEarned = 0;
        totalPhEarned = 0;
        enemiesDefeated = 0;
        enemiesOnBattle = 0;
        deadHeroes = 0;
        actualTarget = 0;

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