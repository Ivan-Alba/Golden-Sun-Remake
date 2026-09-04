using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using TMPro;

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
    [SerializeField] private GameObject battleField;
    [SerializeField] private GameObject[] heroPositions = new GameObject[4];

    private GameObject player; // player reference
    private List<GameObject> party;

    // TODO REVISAR
    private int maxEnemiesInBattle = 3;

    private GameObject mainCamera;
    private float[] pointerOriginalY;

    // NEXT ACTION & TARGETING
    private PsynergySO nextPsynergy;
    private ItemSO nextItem;

    private List<GameObject> targetList = new List<GameObject>();
    private PsynergyTargetType currentTargetType = PsynergyTargetType.SingleEnemy;
    private int currentAreaRange = 0;

    // RESPAWN CONTROLLER
    private EnemySpawnZone respawnController;

    // LISTS WITH FIGHTERS
    private List<GameObject> enemiesList = new List<GameObject>();
    private List<GameObject> heroList = new List<GameObject>();
    private List<GameObject> fighters = new List<GameObject>();

    // RESULTS SCREEN
    private int totalXpEarned, totalPhEarned, totalGoldEarned;
    private Dictionary<ItemSO, int> totalItemsObtained = new Dictionary<ItemSO, int>();

    // BATTLE FIELD POSITIONS
    private GameObject[] enemyPositions;

    // LIST WITH TURNS OF FIGHTERS
    private List<KeyValuePair<GameObject, int>> turnsOrdered;

    // TURN CONTROLLER
    private TurnController turnController;

    // Minimap
    private GameObject playerCanvas;

    private BattleUIController battleUIController;

    // BATTLE STATE MACHINE
    private BattleState currentBattleState = BattleState.None;
    public BattleState CurrentBattleState => currentBattleState;

    // AUXILIAR
    private int enemiesOnBattle, enemiesDefeated, deadHeroes;
    private int actualTarget;

    // Backwards compatibility getter
    public bool WaitingAction
    {
        get => currentBattleState == BattleState.CommandSelection;
        set
        {
            if (!value && currentBattleState == BattleState.CommandSelection)
                currentBattleState = BattleState.WaitingTurnExecution;
        }
    }

    private void Awake()
    {
        // --- LOCAL COMPONENTS ON SAME GAMEOBJECT ---
        turnController = GetComponent<TurnController>();
        battleUIController = GetComponent<BattleUIController>();

        // --- INITIALIZATION ---
        pointerOriginalY = new float[maxEnemiesInBattle];

        // Ensure clean state on scene load
        ResetBattleVariables();
    }

    private void OnEnable()
    {
        SubscribeToInputEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromInputEvents();
    }

    #region Input Event Subscriptions

    private void SubscribeToInputEvents()
    {
        if (InputManager.Instance == null) return;

        InputManager.Instance.OnBattleConfirmPressed += HandleConfirmPressed;
        InputManager.Instance.OnBattleCancelPressed += HandleCancelPressed;
        InputManager.Instance.OnBattleUpPressed += HandleUpPressed;
        InputManager.Instance.OnBattleDownPressed += HandleDownPressed;
        InputManager.Instance.OnBattleLeftPressed += HandleLeftPressed;
        InputManager.Instance.OnBattleRightPressed += HandleRightPressed;
    }

    private void UnsubscribeFromInputEvents()
    {
        if (InputManager.Instance == null) return;

        InputManager.Instance.OnBattleConfirmPressed -= HandleConfirmPressed;
        InputManager.Instance.OnBattleCancelPressed -= HandleCancelPressed;
        InputManager.Instance.OnBattleUpPressed -= HandleUpPressed;
        InputManager.Instance.OnBattleDownPressed -= HandleDownPressed;
        InputManager.Instance.OnBattleLeftPressed -= HandleLeftPressed;
        InputManager.Instance.OnBattleRightPressed -= HandleRightPressed;
    }

    #endregion

    #region Input Handlers by Battle State

    private void HandleConfirmPressed()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.InBattle) return;

        switch (currentBattleState)
        {
            case BattleState.CommandSelection:
                PlaySFX(SFXType.UI_Confirm);

                switch (battleUIController.CommandSelected)
                {
                    case 0: // ATTACK
                        currentTargetType = PsynergyTargetType.SingleEnemy;
                        currentAreaRange = 0;
                        SelectDefaultTarget(currentTargetType, currentAreaRange);
                        currentBattleState = BattleState.TargetSelection;
                        break;

                    case 1: // PSYNERGY
                        currentBattleState = BattleState.SubMenuSelection;
                        battleUIController.SetContentPanel("Psynergy");
                        battleUIController.ShowContentPanel(true);
                        break;

                    case 2: // DJINN
                        break;

                    case 3: // SUMMON
                        break;

                    case 4: // DEFENSE
                        battleUIController.ShowOrdersPanel(false);
                        turnController.HideTurnCamera();
                        currentBattleState = BattleState.WaitingTurnExecution;
                        StartCoroutine(PerformAction());
                        break;

                    case 5: // ITEM
                        if (turnsOrdered != null && turnsOrdered.Count > 0)
                        {
                            turnsOrdered.ElementAt(0).Key.GetComponent<FighterStats>().CurrHp =
                                turnsOrdered.ElementAt(0).Key.GetComponent<FighterStats>().MaxHp;
                        }
                        break;

                    case 6: // ESTADO
                        break;

                    case 7: // HUIR
                        break;
                }
                break;

            case BattleState.SubMenuSelection:
                string selectedContent = battleUIController.GetContentItem();
                if ((nextPsynergy = PsynergyDatabase.Instance.GetPsynergy(selectedContent)) != null)
                {
                    PlaySFX(SFXType.UI_Confirm);
                    currentBattleState = BattleState.TargetSelection;

                    if (battleUIController.CommandSelected == 1) // PSYNERGY
                    {
                        currentTargetType = nextPsynergy.TargetType;
                        currentAreaRange = nextPsynergy.AreaRange;

                        SelectDefaultTarget(currentTargetType, currentAreaRange);

                        Debug.Log($"Psinergía {nextPsynergy.SlugName} target tipo: {nextPsynergy.TargetType}");
                    }
                    else if (battleUIController.CommandSelected == 5) // ITEMS
                    {
                        // TODO: Item targeting logic
                    }
                }
                break;

            case BattleState.TargetSelection:
                PlaySFX(SFXType.UI_Confirm);

                // Hide target selection UI elements
                battleUIController.ShowOrdersPanel(false);
                turnController.HideTurnCamera();
                battleUIController.HideSelectedTarget();

                HideAllPointers();

                currentBattleState = BattleState.WaitingTurnExecution;
                StartCoroutine(PerformAction());
                break;

            case BattleState.ResultsScreen:
                if (resultsController != null)
                {
                    resultsController.HandleConfirmInput();
                }
                break;

            case BattleState.GameOverScreen:
                break;
        }
    }

    private void HandleCancelPressed()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.InBattle) return;

        switch (currentBattleState)
        {
            case BattleState.TargetSelection:
                HideAllPointers();
                battleUIController.HideSelectedTarget();
                PlaySFX(SFXType.UI_Cancel);

                switch (battleUIController.CommandSelected)
                {
                    case 0: // ATTACK
                        currentBattleState = BattleState.CommandSelection;
                        break;

                    case 1: // PSYNERGY
                        currentBattleState = BattleState.SubMenuSelection;
                        nextPsynergy = null;
                        break;

                    case 2: // DJINN
                        break;

                    case 3: // SUMMON
                        break;

                    case 5: // ITEM
                        break;
                }
                break;

            case BattleState.SubMenuSelection:
                PlaySFX(SFXType.UI_Cancel);
                currentBattleState = BattleState.CommandSelection;
                battleUIController.ShowContentPanel(false);
                break;
        }
    }

    private void HandleLeftPressed()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.InBattle) return;

        switch (currentBattleState)
        {
            case BattleState.CommandSelection:
                battleUIController.ordersPanelMove("LEFT");
                break;

            case BattleState.TargetSelection:
                SelectLeftTarget();
                PlaySFX(SFXType.UI_Move);
                break;
        }
    }

    private void HandleRightPressed()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.InBattle) return;

        switch (currentBattleState)
        {
            case BattleState.CommandSelection:
                battleUIController.ordersPanelMove("RIGHT");
                break;

            case BattleState.TargetSelection:
                SelectRightTarget();
                PlaySFX(SFXType.UI_Move);
                break;
        }
    }

    private void HandleUpPressed()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.InBattle) return;

        if (currentBattleState == BattleState.SubMenuSelection)
        {
            PlaySFX(SFXType.UI_Move);
            battleUIController.MoveContentPanel("UP");
        }
    }

    private void HandleDownPressed()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.InBattle) return;

        if (currentBattleState == BattleState.SubMenuSelection)
        {
            PlaySFX(SFXType.UI_Move);
            battleUIController.MoveContentPanel("DOWN");
        }
    }

    #endregion

    #region Battle Lifecycle & Setup

    public void AwakeBattle(GameObject enemyInitiator = null, GameObject respawnZone = null,
        List<GameObject> preSetEnemies = null)
    {
        ResetBattleVariables();

        // BATTLE MUSIC VIA AUDIO MANAGER
        GameEvents.Audio.RequestPlayBattleBGM();

        // --- EXTERNAL SCENE / CORE REFERENCES ---
        if (mainCamera == null)
            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");

        if (playerCanvas == null)
            playerCanvas = GameObject.Find("PlayerCanvas");

        // Refresh dynamic party members
        party = GameManager.Instance.GetParty();

        // Set up UI hero panels according to current party count
        for (int i = 0; i < 4; i++)
        {
            if (i < party.Count)
            {
                battleUIController.SetHeroInfoPanel(i);
            }
        }

        enemiesOnBattle = Random.Range(1, maxEnemiesInBattle + 1);
        enemyPositions = new GameObject[enemiesOnBattle];

        // RESPAWN OBJECTS
        if (respawnZone != null)
            respawnController = respawnZone.GetComponent<EnemySpawnZone>();

        if (player != null) player.GetComponent<Animator>().enabled = false;

        // START BATTLE TRANSITION
        StartCoroutine(StartBattle(enemyInitiator));
    }

    private IEnumerator StartBattle(GameObject firstEnemy)
    {
        currentBattleState = BattleState.IntroTransition;

        // TRANSITION MAIN CAMERA TO BATTLE CAMERA
        StartCoroutine(battleCameraController.StartTransition(mainCamera));

        while (!battleCameraController.TransitionDone)
        {
            yield return null;
        }

        // SET HEROS
        int i = 0;
        if (GameManager.Instance.GetParty().Count <= 2) i = 1;

        foreach (GameObject hero in GameManager.Instance.GetParty())
        {
            // IF HERO IS ALIVE
            if (hero.GetComponent<FighterStats>().CurrHp != 0)
            {
                fighters.Add(hero);
                heroList.Add(hero);
            }
            // HERO POSITIONS
            hero.transform.position = heroPositions[i++].transform.position;
        }

        // SET INFO ON HERO'S PANEL
        battleUIController.RefreshHeroInfoPanel();

        // SET ENEMIES
        SetEnemiesOnBattle(firstEnemy);

        // DESTROY WORLD ENEMY
        if (firstEnemy != null) Destroy(firstEnemy);

        // HidePlayerCanvas
        if (playerCanvas != null) playerCanvas.SetActive(false);

        // --- DYNAMIC AUDIO LISTENER SETUP ---
        if (HybridAudioListener.Instance != null)
        {
            Transform battleCamTransform = battleCameraController != null ? battleCameraController.transform : null;
            Transform centerTarget = audioCenter != null ? audioCenter : battleField.transform;

            HybridAudioListener.Instance.SetBattleReferences(battleCamTransform, centerTarget);
        }

        // ANIMACIÓN CAMARA INTRO BATLLA
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
        GameObject enemyPlace;
        GameObject newEnemy;

        Debug.Log($"{enemiesOnBattle} enemigos");

        for (int i = 0; i < enemiesOnBattle; i++)
        {
            if (i == 0) // FIRST ENEMY LIKE WORLD ENEMY
            {
                enemyPlace = battleField.transform.Find($"Enemy{i}").gameObject;
                newEnemy = respawnController.GetMatchingEnemyPrefab(firstEnemy);
            }
            else // NEXT RANDOM FROM RESPAWN CONTROLLER ENEMY LIST
            {
                enemyPlace = battleField.transform.Find($"Enemy{i}").gameObject;
                newEnemy = respawnController.GetRandomEnemyPrefab();
            }

            enemyPositions[i] = enemyPlace;
            enemiesList.Add(Instantiate(newEnemy, enemyPlace.transform.position, Quaternion.identity));
            // TODO AUDIOSOURCE MISSING CHECK
            //enemiesList[i].GetComponent<AudioSource>().spatialBlend = 0;
            fighters.Add(enemiesList[i]);

            pointerOriginalY[i] = enemyPositions[i].transform.Find("Pointer").Find("TopPointer").position.y;

            // SET TOP POINTERS POSITION
            GameObject topPointer = enemyPositions[i].transform.Find("Pointer").Find("TopPointer").gameObject;
            topPointer.transform.position = new Vector3(topPointer.transform.position.x,
                topPointer.transform.position.y + enemiesList[i].GetComponent<BoxCollider>().bounds.size.y,
                topPointer.transform.position.z);
        }
    }

    private void InitializeBattleTurns()
    {
        battleUIController.ShowBattleCanvas(true);
        turnsOrdered = turnController.InitializeTurnSystem(fighters);

        if (turnController.CurrentFighter != null)
        {
            turnController.CurrentFighter.StartsTurn();

            if (turnController.CurrentFighter.CompareTag("Enemy"))
            {
                currentBattleState = BattleState.WaitingTurnExecution;
                StartCoroutine(PerformAction());
            }
            else
            {
                currentBattleState = BattleState.CommandSelection;
            }
        }
    }

    #endregion

    #region Action Execution & Turn Flow

    private IEnumerator PerformAction()
    {
        FighterStats targetStats = null;
        GameObject activeFighter = turnController.CurrentFighter.gameObject;

        if (activeFighter.CompareTag("Enemy"))
        {
            battleUIController.CommandSelected = Random.Range(0, 1);
        }

        int baseActionCost = 50; // Default action cost (Attack)

        switch (battleUIController.CommandSelected)
        {
            case 0: // ATTACK
                baseActionCost = 50;
                if (activeFighter.CompareTag("Enemy"))
                {
                    // Use EnemyAIController to pick an alive hero
                    if (activeFighter.TryGetComponent<EnemyAIController>(out var ai))
                    {
                        GameObject chosenTarget = ai.SelectTarget(heroList);
                        if (chosenTarget != null)
                        {
                            targetStats = chosenTarget.GetComponent<FighterStats>();
                        }
                    }

                    // Fallback in case EnemyAIController is missing or returns null
                    if (targetStats == null && heroList != null && heroList.Count > 0)
                    {
                        targetStats = heroList[Random.Range(0, heroList.Count)].GetComponent<FighterStats>();
                    }

                    if (targetStats != null)
                    {
                        targetList.Add(targetStats.gameObject);
                    }
                }
                else
                {
                    targetStats = enemiesList[actualTarget].GetComponent<FighterStats>();
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

            case 1: // PSYNERGY
                baseActionCost = 60; // Base Psynergy delay cost
                battleUIController.ShowContentPanel(false);

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

            case 4: // DEFENSE
                baseActionCost = 30; // Guard is a faster action
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
            FighterStats targetData = target.GetComponent<FighterStats>();
            if (targetData != null && targetData.CurrHp <= 0)
            {
                if (targetData.gameObject.CompareTag("Enemy"))
                {
                    // Retrieve EnemyStats to get rewards
                    if (targetData.TryGetComponent<EnemyStats>(out var enemyStats))
                    {
                        totalXpEarned += enemyStats.XpReward;
                        totalGoldEarned += enemyStats.CoinsReward;
                        totalPhEarned += enemyStats.CoinsReward;

                        GetItemLoot(enemyStats);
                    }

                    fighters.Remove(targetData.gameObject);
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

        if (enemiesDefeated == enemiesList.Count)
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

        // Evaluate enemy drops from EnemyStats configuration
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

        // Advance turn using TurnController with CTB action cost
        turnController.AdvanceTurn(executedFighter, baseActionCost);
        turnsOrdered = turnController.BuildTurnProjection();

        if (turnController.CurrentFighter != null)
        {
            turnController.CurrentFighter.StartsTurn();

            if (turnController.CurrentFighter.CompareTag("Enemy"))
            {
                currentBattleState = BattleState.WaitingTurnExecution;
                StartCoroutine(PerformAction());
            }
            else
            {
                currentBattleState = BattleState.CommandSelection;
            }
        }
    }

    private IEnumerator BattleWinned()
    {
        currentBattleState = BattleState.ResultsScreen;
        battleUIController.ShowBattleCanvas(false);

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

        // Transition
        if (SceneTransitionManager.Instance != null)
        {
            yield return SceneTransitionManager.Instance.PlayFullTransitionRoutine(SetOffBattle);
        }
        else
        {
            // Fallback
            SetOffBattle();
        }
    }

    private void SetOffBattle()
    {
        resultsController.Reset();

        foreach (GameObject hero in heroList)
        {
            if (hero.GetComponent<FighterStats>().CurrHp > 0)
                hero.GetComponentInChildren<Animator>().SetBool("victoryPose", false);
        }

        if (playerCanvas != null) playerCanvas.SetActive(true);

        GameEvents.Player.TriggerHeroStatusChanged();

        battleCameraController.SetOffBattleCamera();

        mainCamera.GetComponent<Camera>().enabled = true;

        if (HybridAudioListener.Instance != null)
        {
            HybridAudioListener.Instance.ClearBattleReferences();
        }

        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);

        if (player != null) player.GetComponent<Animator>().enabled = true;

        // RESTORE WORLD MUSIC VIA AUDIO MANAGER
        GameEvents.Audio.RequestResumeBGM();

        for (int i = 0; i < GameManager.Instance.GetParty().Count; i++)
        {
            if (i < heroList.Count)
            {
                heroList[i].GetComponent<FighterController>().NextTurnCD = 0;
                heroList[i].GetComponent<FighterController>().SetOffDefense();
            }
        }

        for (int i = 0; i < enemiesOnBattle; i++)
        {
            if (enemyPositions[i] != null)
            {
                Transform currentPosition = enemyPositions[i].transform.Find("Pointer").Find("TopPointer");
                currentPosition.position = new Vector3(currentPosition.position.x, pointerOriginalY[i], currentPosition.position.z);
            }
        }

        ResetBattleVariables();
    }

    private void GameOver()
    {
        currentBattleState = BattleState.GameOverScreen;

        GameObject crossfade = transform.Find("Crossfade").gameObject;
        crossfade.SetActive(true);
        crossfade.transform.Find("GameOver").gameObject.SetActive(true);
        crossfade.GetComponent<Animator>().SetTrigger("start");

        GameEvents.Audio.RequestPlayGameOverBGM();

        Debug.Log("GAMEOVER");
    }

    #endregion

    #region State & Target Helpers

    private bool IsAllyTarget(PsynergyTargetType targetType)
    {
        return targetType == PsynergyTargetType.SingleAlly ||
               targetType == PsynergyTargetType.AllAllies ||
               targetType == PsynergyTargetType.Self;
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

        if (GameManager.Instance != null && GameManager.Instance.GetParty() != null)
        {
            foreach (GameObject hero in GameManager.Instance.GetParty())
            {
                if (hero != null)
                {
                    Transform pointer = hero.transform.Find("Pointer");
                    if (pointer != null) pointer.gameObject.SetActive(false);
                }
            }
        }
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

        nextPsynergy = null;
        nextItem = null;
        currentTargetType = PsynergyTargetType.SingleEnemy;
        currentAreaRange = 0;
        currentBattleState = BattleState.None;
    }

    public void ChangeBattleSpeed()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeGameSpeed();
        }
    }

    #endregion

    #region Target Selection Logic

    private void SelectDefaultTarget(PsynergyTargetType targetType, int areaRange = 0, int initialIndex = 0)
    {
        bool targetIsAlly = IsAllyTarget(targetType);

        if (targetIsAlly)
        {
            GameObject pointer;
            List<GameObject> currentParty = GameManager.Instance.GetParty();
            targetList = new List<GameObject>();

            foreach (GameObject hero in currentParty)
            {
                hero.transform.Find("Pointer").gameObject.SetActive(false);
            }

            switch (targetType)
            {
                case PsynergyTargetType.SingleAlly:
                case PsynergyTargetType.Self:
                    pointer = currentParty[initialIndex].transform.Find("Pointer").gameObject;
                    pointer.SetActive(true);
                    targetList.Add(currentParty[initialIndex]);
                    actualTarget = initialIndex;
                    battleUIController.ShowSelectedTarget(targetList);
                    break;

                case PsynergyTargetType.AllAllies:
                    foreach (GameObject hero in currentParty)
                    {
                        if (hero != null)
                        {
                            pointer = hero.transform.Find("Pointer").gameObject;
                            pointer.SetActive(true);
                            targetList.Add(hero);
                        }
                    }
                    actualTarget = 0;
                    battleUIController.ShowSelectedTarget(targetList);
                    break;
            }
        }
        else
        {
            GameObject pointer;
            targetList = new List<GameObject>();

            foreach (GameObject position in enemyPositions)
            {
                if (position != null)
                    position.transform.Find("Pointer").gameObject.SetActive(false);
            }

            switch (targetType)
            {
                case PsynergyTargetType.SingleEnemy:
                    while (enemiesList[initialIndex] == null)
                    {
                        initialIndex++;
                    }

                    pointer = enemyPositions[initialIndex].transform.Find("Pointer").gameObject;
                    pointer.SetActive(true);
                    targetList.Add(enemiesList[initialIndex]);
                    actualTarget = initialIndex;
                    battleUIController.ShowSelectedTarget(targetList);
                    break;

                case PsynergyTargetType.MultiEnemyArea:
                    while (enemiesList[initialIndex] == null)
                    {
                        initialIndex++;
                    }

                    actualTarget = initialIndex;

                    // Add primary target
                    targetList.Add(enemiesList[initialIndex]);
                    pointer = enemyPositions[initialIndex].transform.Find("Pointer").gameObject;
                    pointer.SetActive(true);

                    // Add adjacent targets based on areaRange (Golden Sun spread)
                    if (areaRange >= 1)
                    {
                        // Target to the left/right depending on enemy position index
                        for (int offset = 1; offset <= areaRange; offset++)
                        {
                            int rightIdx = initialIndex + offset;
                            int leftIdx = initialIndex - offset;

                            if (rightIdx < enemiesList.Count && enemiesList[rightIdx] != null)
                            {
                                targetList.Add(enemiesList[rightIdx]);
                                pointer = enemyPositions[rightIdx].transform.Find("Pointer").gameObject;
                                pointer.SetActive(true);
                            }

                            if (leftIdx >= 0 && enemiesList[leftIdx] != null)
                            {
                                targetList.Add(enemiesList[leftIdx]);
                                pointer = enemyPositions[leftIdx].transform.Find("Pointer").gameObject;
                                pointer.SetActive(true);
                            }
                        }
                    }

                    battleUIController.ShowSelectedTarget(targetList);
                    break;

                case PsynergyTargetType.AllEnemies:
                    for (int e = 0; e < enemiesList.Count; e++)
                    {
                        if (enemiesList[e] != null)
                        {
                            targetList.Add(enemiesList[e]);
                            pointer = enemyPositions[e].transform.Find("Pointer").gameObject;
                            pointer.SetActive(true);
                        }
                    }
                    actualTarget = 0;
                    battleUIController.ShowSelectedTarget(targetList);
                    break;
            }
        }
    }

    private void SelectRightTarget()
    {
        int nextTarget = actualTarget;
        bool targetIsAlly = IsAllyTarget(currentTargetType);

        if (targetIsAlly)
        {
            List<GameObject> currentParty = GameManager.Instance.GetParty();
            if (currentParty.Count > 1)
            {
                nextTarget = (actualTarget + 1) % currentParty.Count;
            }

            if (actualTarget != nextTarget)
            {
                SelectDefaultTarget(currentTargetType, currentAreaRange, nextTarget);
            }
        }
        else
        {
            if (enemiesList.Count > 1)
            {
                int count = enemiesList.Count;
                for (int i = 1; i <= count; i++)
                {
                    int candidate = (actualTarget + i) % count;
                    if (enemiesList[candidate] != null)
                    {
                        nextTarget = candidate;
                        break;
                    }
                }
            }

            if (actualTarget != nextTarget)
            {
                SelectDefaultTarget(currentTargetType, currentAreaRange, nextTarget);
            }
        }
    }

    private void SelectLeftTarget()
    {
        int nextTarget = actualTarget;
        bool targetIsAlly = IsAllyTarget(currentTargetType);

        if (targetIsAlly)
        {
            List<GameObject> currentParty = GameManager.Instance.GetParty();
            if (currentParty.Count > 1)
            {
                nextTarget = (actualTarget - 1 + currentParty.Count) % currentParty.Count;
            }

            if (actualTarget != nextTarget)
            {
                SelectDefaultTarget(currentTargetType, currentAreaRange, nextTarget);
            }
        }
        else
        {
            if (enemiesList.Count > 1)
            {
                int count = enemiesList.Count;
                for (int i = 1; i <= count; i++)
                {
                    int candidate = (actualTarget - i + count) % count;
                    if (enemiesList[candidate] != null)
                    {
                        nextTarget = candidate;
                        break;
                    }
                }
            }

            if (actualTarget != nextTarget)
            {
                SelectDefaultTarget(currentTargetType, currentAreaRange, nextTarget);
            }
        }
    }

    #endregion
}