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

    [Header("Audio Resources")]
    [SerializeField] private AudioClip battleMusic;
    [SerializeField] private AudioClip victoryMusic;
    [SerializeField] private AudioClip gameOverMusic;

    private GameObject player; // player reference

    private List<GameObject> party;

    // TODO REVISAR
    private int maxEnemiesInBattle = 3;

    // AUDIO CONTROLLER
    private AudioSource musicController;
    private AudioClip fieldMusic;

    private GameObject mainCamera;
    private float[] pointerOriginalY;

    // NEXT ACTION
    private Psynergy nextPsynergy;
    private Item nextItem;

    private List<GameObject> targetList = new List<GameObject>();

    private string nextAction_targetType;
    private int nextAction_targetSize;

    // RESPAWN CONTROLLER
    private RespawnController respawnController;

    // LISTS WITH FIGHTERS
    private List<GameObject> enemiesList = new List<GameObject>();
    private List<GameObject> heroList = new List<GameObject>();
    private List<GameObject> fighters = new List<GameObject>();

    // RESULTS SCREEN
    private int totalXpEarned, totalPhEarned, totalGoldEarned;
    private Dictionary<Item, int> totalItemsObtained;

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

    private void Update()
    {
        // Guard Clause: Exit immediately if GameManager is not ready or game is not in battle
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.InBattle) return;

        battleUIController.RefreshHeroInfoPanel();
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
                        SelectDefaultTarget("ONE_TARGET");
                        nextAction_targetType = "ONE_TARGET";
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
                        if (nextPsynergy.TargetType.ToString() == "MULTI_TARGET")
                        {
                            SelectDefaultTarget(nextPsynergy.TargetType.ToString(), nextPsynergy.TargetSize);
                            nextAction_targetType = nextPsynergy.TargetType.ToString();
                            nextAction_targetSize = nextPsynergy.TargetSize;
                        }
                        else
                        {
                            SelectDefaultTarget(nextPsynergy.TargetType.ToString());
                            nextAction_targetType = nextPsynergy.TargetType.ToString();
                        }

                        Debug.Log($"Psinergía {nextPsynergy.Name} target tipo: {nextPsynergy.TargetType}");
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

    public void AwakeBatlle(GameObject enemyInitiator = null, GameObject respawnZone = null,
        List<GameObject> preSetEnemies = null)
    {
        ResetBattleVariables();

        // --- EXTERNAL SCENE / CORE REFERENCES ---
        if (mainCamera == null)
            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");

        if (playerCanvas == null)
            playerCanvas = GameObject.Find("PlayerCanvas");

        if (musicController == null)
        {
            GameObject musicObj = GameObject.Find("MusicController");
            if (musicObj != null)
            {
                musicController = musicObj.GetComponent<AudioSource>();
            }
        }

        // Refresh dynamic party members and world audio
        party = GameManager.Instance.GetParty();
        if (musicController != null) fieldMusic = musicController.clip;

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
            respawnController = respawnZone.GetComponent<RespawnController>();

        if (player != null) player.GetComponent<Animator>().enabled = false;

        // BATTLE MUSIC
        if (musicController != null)
        {
            musicController.clip = battleMusic;
            musicController.Play();
        }

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
        SetTurns();

        GameManager.Instance.SetGameState(GameManager.GameState.InBattle);
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
                newEnemy = respawnController.GetEnemyLike(firstEnemy);
            }
            else // NEXT RANDOM FROM RESPAWN CONTROLLER ENEMY LIST
            {
                enemyPlace = battleField.transform.Find($"Enemy{i}").gameObject;
                newEnemy = respawnController.GetRandomEnemy();
            }

            enemyPositions[i] = enemyPlace;
            enemiesList.Add(Instantiate(newEnemy, enemyPlace.transform.position, Quaternion.identity));
            enemiesList[i].GetComponent<AudioSource>().spatialBlend = 0;
            fighters.Add(enemiesList[i]);

            pointerOriginalY[i] = enemyPositions[i].transform.Find("Pointer").Find("TopPointer").position.y;

            // SET TOP POINTERS POSITION
            GameObject topPointer = enemyPositions[i].transform.Find("Pointer").Find("TopPointer").gameObject;
            topPointer.transform.position = new Vector3(topPointer.transform.position.x,
                topPointer.transform.position.y + enemiesList[i].GetComponent<BoxCollider>().bounds.size.y,
                topPointer.transform.position.z);
        }

        // INITIALIZE FIGHTERS FIRST TURN CD BY AGILITY
        foreach (GameObject fighter in fighters)
        {
            fighter.GetComponent<FighterController>().NextTurnCD -= fighter.GetComponent<FighterStats>().Agility;
        }
    }

    private void SetTurns()
    {
        battleUIController.ShowBattleCanvas(true);
        turnsOrdered = turnController.SetTurns(fighters);
        turnController.CurrentFighter.GetComponent<FighterController>().StartsTurn();

        if (turnController.CurrentFighter != null && turnController.CurrentFighter.CompareTag("Enemy"))
        {
            currentBattleState = BattleState.WaitingTurnExecution;
            StartCoroutine(PerformAction());
        }
        else
        {
            currentBattleState = BattleState.CommandSelection;
        }
    }

    #endregion

    #region Action Execution & Turn Flow

    private IEnumerator PerformAction()
    {
        FighterStats targetStats = null;

        if (turnController.CurrentFighter.gameObject.CompareTag("Enemy"))
        {
            battleUIController.CommandSelected = Random.Range(0, 1);
        }

        switch (battleUIController.CommandSelected)
        {
            case 0: // ATTACK
                if (turnController.CurrentFighter.gameObject.CompareTag("Enemy"))
                {
                    targetStats = turnController.CurrentFighter.SelectRandomTarget(heroList).GetComponent<FighterStats>();
                    targetList.Add(targetStats.gameObject);
                }
                else
                {
                    targetStats = enemiesList[actualTarget].GetComponent<FighterStats>();
                }

                StartCoroutine(turnController.CurrentFighter.BasicAttack(targetStats.gameObject));

                while (!turnController.CurrentFighter.ActionFinished)
                {
                    yield return null;
                }

                turnController.CurrentFighter.ActionFinished = false;
                break;

            case 1: // PSYNERGY
                battleUIController.ShowContentPanel(false);

                if (nextPsynergy.Type == Psynergy.Types.HEALING)
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
                turnController.CurrentFighter.SetUpDefense();
                yield return new WaitForSeconds(1.5f);
                break;
        }

        CheckDefeated();
    }

    private void CheckDefeated()
    {
        foreach (GameObject target in targetList)
        {
            FighterStats targetData = target.GetComponent<FighterStats>();
            if (targetData != null && targetData.CurrHp <= 0)
            {
                if (targetData.gameObject.CompareTag("Enemy"))
                {
                    totalXpEarned += targetData.transform.GetComponent<EnemyDrop>().Xp;
                    totalGoldEarned += targetData.transform.GetComponent<EnemyDrop>().Gold;
                    totalPhEarned += targetData.transform.GetComponent<EnemyDrop>().Ph;

                    GetItemLoot(targetData);

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
            TurnFinished();
        }
    }

    private void GetItemLoot(FighterStats targetStats)
    {
        Item itemToAdd = null;
        float randomDrop = Random.Range(0f, 100f);
        if (targetStats.transform.GetComponent<EnemyDrop>().ItemVeryRareDrop != null && randomDrop < targetStats.transform.GetComponent<EnemyDrop>().ItemVeryRareRatio)
        {
            itemToAdd = ItemDatabase.Instance.GetItem(targetStats.transform.GetComponent<EnemyDrop>().ItemVeryRareDrop);
        }
        else
        {
            randomDrop = Random.Range(0f, 100f);
            if (targetStats.transform.GetComponent<EnemyDrop>().ItemRareDrop != null && randomDrop < targetStats.transform.GetComponent<EnemyDrop>().ItemRareRatio)
            {
                itemToAdd = ItemDatabase.Instance.GetItem(targetStats.transform.GetComponent<EnemyDrop>().ItemRareDrop);
            }
            else
            {
                randomDrop = Random.Range(0f, 100f);
                if (targetStats.transform.GetComponent<EnemyDrop>().ItemEasyDrop != null && randomDrop < targetStats.transform.GetComponent<EnemyDrop>().ItemEasyRatio)
                {
                    itemToAdd = ItemDatabase.Instance.GetItem(targetStats.transform.GetComponent<EnemyDrop>().ItemEasyDrop);
                }
            }
        }

        if (itemToAdd != null)
        {
            if (totalItemsObtained.ContainsKey(itemToAdd))
            {
                totalItemsObtained[itemToAdd] += 1;
            }
            else
            {
                totalItemsObtained.Add(itemToAdd, 1);
            }
        }
    }

    private void TurnFinished()
    {
        targetList = new List<GameObject>();
        SetTurns();
    }

    private IEnumerator BattleWinned()
    {
        currentBattleState = BattleState.ResultsScreen;
        battleUIController.ShowBattleCanvas(false);

        if (musicController != null)
        {
            musicController.clip = victoryMusic;
            musicController.Play();
        }
        yield return new WaitForSeconds(0.5f);

        foreach (GameObject hero in heroList)
        {
            if (hero.GetComponent<FighterStats>().CurrHp > 0)
                hero.GetComponentInChildren<Animator>().SetBool("victoryPose", true);
        }

        battleCameraController.Victory(turnsOrdered.ElementAt(0).Key);
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
        PlayerUIManager.Instance.RefreshPartyInfo();

        battleCameraController.SetOffBattleCamera();

        mainCamera.GetComponent<Camera>().enabled = true;

        if (HybridAudioListener.Instance != null)
        {
            HybridAudioListener.Instance.ClearBattleReferences();
        }

        GameManager.Instance.SetGameState(GameManager.GameState.WorldExploration);

        if (player != null) player.GetComponent<Animator>().enabled = true;

        if (musicController != null)
        {
            musicController.clip = fieldMusic;
            musicController.Play();
        }

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
        if (musicController != null)
        {
            musicController.clip = gameOverMusic;
            musicController.Play();
        }
        Debug.Log("GAMEOVER");
    }

    #endregion

    #region State & Target Helpers

    private void PlaySFX(SFXType sfx)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(sfx);
        }
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
            totalItemsObtained = new Dictionary<Item, int>();
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

    private void SelectDefaultTarget(string targetType, int targetSize = 0, int i = 0)
    {
        if (nextPsynergy != null && nextPsynergy.Type == Psynergy.Types.HEALING)
        {
            Debug.Log("Healing Psynergy");
            GameObject pointer;
            List<GameObject> currentParty = GameManager.Instance.GetParty();
            targetList = new List<GameObject>();

            foreach (GameObject hero in currentParty)
            {
                hero.transform.Find("Pointer").gameObject.SetActive(false);
            }

            switch (targetType)
            {
                case "ONE_TARGET":
                    pointer = currentParty[i].transform.Find("Pointer").gameObject;
                    pointer.SetActive(true);
                    targetList.Add(currentParty[i]);
                    actualTarget = i;
                    battleUIController.ShowSelectedTarget(targetList);
                    break;

                case "MULTI_TARGET":
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
                case "ONE_TARGET":
                    while (enemiesList[i] == null)
                    {
                        i++;
                    }

                    pointer = enemyPositions[i].transform.Find("Pointer").gameObject;
                    pointer.SetActive(true);
                    targetList.Add(enemiesList[i]);
                    actualTarget = i;
                    battleUIController.ShowSelectedTarget(targetList);
                    break;

                case "MULTI_TARGET":
                    while (enemiesList[i] == null)
                    {
                        i++;
                    }

                    actualTarget = i;

                    targetList.Add(enemiesList[i]);
                    pointer = enemyPositions[i].transform.Find("Pointer").gameObject;
                    pointer.SetActive(true);

                    switch (targetSize)
                    {
                        case 3:
                            switch (i)
                            {
                                case 0:
                                    if (enemiesList.Count > 1 && enemiesList[1] != null)
                                    {
                                        targetList.Add(enemiesList[1]);
                                        pointer = enemyPositions[1].transform.Find("Pointer").gameObject;
                                        pointer.SetActive(true);
                                    }

                                    if (enemiesList.Count > 2 && enemiesList[2] != null)
                                    {
                                        targetList.Add(enemiesList[2]);
                                        pointer = enemyPositions[2].transform.Find("Pointer").gameObject;
                                        pointer.SetActive(true);
                                    }
                                    break;

                                case 1:
                                    if (enemiesList.Count > 3 && enemiesList[3] != null)
                                    {
                                        targetList.Add(enemiesList[3]);
                                        pointer = enemyPositions[3].transform.Find("Pointer").gameObject;
                                        pointer.SetActive(true);
                                    }

                                    if (enemiesList[0] != null)
                                    {
                                        targetList.Add(enemiesList[0]);
                                        pointer = enemyPositions[0].transform.Find("Pointer").gameObject;
                                        pointer.SetActive(true);
                                    }
                                    break;

                                case 2:
                                    if (enemiesList.Count > 4 && enemiesList[4] != null)
                                    {
                                        targetList.Add(enemiesList[4]);
                                        pointer = enemyPositions[4].transform.Find("Pointer").gameObject;
                                        pointer.SetActive(true);
                                    }

                                    if (enemiesList[0] != null)
                                    {
                                        targetList.Add(enemiesList[0]);
                                        pointer = enemyPositions[0].transform.Find("Pointer").gameObject;
                                        pointer.SetActive(true);
                                    }
                                    break;
                            }
                            break;

                        case 5:
                            break;
                    }

                    battleUIController.ShowSelectedTarget(targetList);
                    break;
            }
        }
    }

    private void SelectRightTarget()
    {
        int nextTarget = 0;

        if (nextPsynergy != null && nextPsynergy.Type == Psynergy.Types.HEALING)
        {
            switch (GameManager.Instance.GetParty().Count)
            {
                case 1:
                    nextTarget = actualTarget;
                    break;
                case 2:
                    switch (actualTarget)
                    {
                        case 0: nextTarget = 1; break;
                        case 1: nextTarget = 0; break;
                    }
                    break;
                case 3:
                    break;
                case 4:
                    break;
            }

            if (actualTarget != nextTarget)
            {
                switch (nextAction_targetType)
                {
                    case "ONE_TARGET":
                        SelectDefaultTarget(nextAction_targetType, 0, nextTarget);
                        break;
                    case "MULTI_TARGET":
                        SelectDefaultTarget(nextAction_targetType, nextAction_targetSize, nextTarget);
                        break;
                }
            }
        }
        else
        {
            switch (enemiesList.Count)
            {
                case 1:
                    nextTarget = 0;
                    break;

                case 2:
                    switch (actualTarget)
                    {
                        case 0:
                            nextTarget = 0;
                            if (enemiesList[1] != null) nextTarget = 1;
                            break;
                        case 1:
                            nextTarget = 1;
                            if (enemiesList[0] != null) nextTarget = 0;
                            break;
                    }
                    break;

                case 3:
                    switch (actualTarget)
                    {
                        case 0:
                            nextTarget = 0;
                            if (enemiesList[2] != null) nextTarget = 2;
                            if (enemiesList[1] != null) nextTarget = 1;
                            break;
                        case 1:
                            nextTarget = 1;
                            if (enemiesList[0] != null) nextTarget = 0;
                            if (enemiesList[2] != null) nextTarget = 2;
                            break;
                        case 2:
                            nextTarget = 2;
                            if (enemiesList[1] != null) nextTarget = 1;
                            if (enemiesList[0] != null) nextTarget = 0;
                            break;
                    }
                    break;
            }

            if (actualTarget != nextTarget)
            {
                switch (nextAction_targetType)
                {
                    case "ONE_TARGET":
                        SelectDefaultTarget(nextAction_targetType, 0, nextTarget);
                        break;
                    case "MULTI_TARGET":
                        SelectDefaultTarget(nextAction_targetType, nextAction_targetSize, nextTarget);
                        break;
                }
            }
        }
    }

    private void SelectLeftTarget()
    {
        int nextTarget = 0;

        if (nextPsynergy != null && nextPsynergy.Type == Psynergy.Types.HEALING)
        {
            switch (GameManager.Instance.GetParty().Count)
            {
                case 1:
                    nextTarget = actualTarget;
                    break;
                case 2:
                    switch (actualTarget)
                    {
                        case 0: nextTarget = 1; break;
                        case 1: nextTarget = 0; break;
                    }
                    break;
                case 3:
                    break;
                case 4:
                    break;
            }

            if (actualTarget != nextTarget)
            {
                switch (nextAction_targetType)
                {
                    case "ONE_TARGET":
                        SelectDefaultTarget(nextAction_targetType, 0, nextTarget);
                        break;
                    case "MULTI_TARGET":
                        SelectDefaultTarget(nextAction_targetType, nextAction_targetSize, nextTarget);
                        break;
                }
            }
        }
        else
        {
            switch (enemiesList.Count)
            {
                case 1:
                    nextTarget = 0;
                    break;

                case 2:
                    switch (actualTarget)
                    {
                        case 0:
                            nextTarget = 0;
                            if (enemiesList[1] != null) nextTarget = 1;
                            break;
                        case 1:
                            nextTarget = 1;
                            if (enemiesList[0] != null) nextTarget = 0;
                            break;
                    }
                    break;

                case 3:
                    switch (actualTarget)
                    {
                        case 0:
                            nextTarget = 0;
                            if (enemiesList[1] != null) nextTarget = 1;
                            if (enemiesList[2] != null) nextTarget = 2;
                            break;
                        case 1:
                            nextTarget = 1;
                            if (enemiesList[2] != null) nextTarget = 2;
                            if (enemiesList[0] != null) nextTarget = 0;
                            break;
                        case 2:
                            nextTarget = 2;
                            if (enemiesList[0] != null) nextTarget = 0;
                            if (enemiesList[1] != null) nextTarget = 1;
                            break;
                    }
                    break;
            }

            if (actualTarget != nextTarget)
            {
                switch (nextAction_targetType)
                {
                    case "ONE_TARGET":
                        SelectDefaultTarget(nextAction_targetType, 0, nextTarget);
                        break;
                    case "MULTI_TARGET":
                        SelectDefaultTarget(nextAction_targetType, nextAction_targetSize, nextTarget);
                        break;
                }
            }
        }
    }

    #endregion
}