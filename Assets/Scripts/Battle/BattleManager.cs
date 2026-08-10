using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using TMPro;

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
    private float transitionTime = 2;

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

    // AUXILIAR
    private int enemiesOnBattle, enemiesDefeated, deadHeroes;
    private bool keyPressed;
    private int actualTarget;
    private bool selectingTarget, waitingAction, choosingList;

    public bool WaitingAction { get => waitingAction; set => waitingAction = value; }

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

    private void Update()
    {
        // Guard Clause: Exit immediately if GameManager is not ready or game is not in battle
        if (GameManager.Instance == null || !GameManager.Instance.InBattle) return;

        battleUIController.RefreshHeroInfoPanel();

        if (waitingAction) // WHEN A TURN ENDS
        {
            if (turnController.CurrentFighter != null && turnController.CurrentFighter.CompareTag("Enemy")) // If enemy's turn
            {
                waitingAction = false; // Starts enemy turn
                StartCoroutine(PerformAction()); // Call IA to decide enemy actions
            }
            else // Hero turn decide orders 
            {
                // Move right in orders buttons
                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    battleUIController.ordersPanelMove("RIGHT");
                }

                // Move left in orders buttons
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    battleUIController.ordersPanelMove("LEFT");
                }

                // Press the confirm Key to select an order
                if (Input.GetKeyDown(GameManager.Instance.KeyConfirm) && !keyPressed)
                {
                    keyPressed = true;
                    GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.CONFIRM_SOUND);

                    // Depending on order
                    switch (battleUIController.CommandSelected)
                    {
                        case 0: // ATTACK
                            SelectDefaultTarget("ONE_TARGET");
                            nextAction_targetType = "ONE_TARGET";
                            waitingAction = false;
                            selectingTarget = true;
                            break;
                        case 1: // PSINERGÍA
                            waitingAction = false;
                            choosingList = true;
                            battleUIController.SetContentPanel("Psynergy");
                            battleUIController.ShowContentPanel(true);
                            break;
                        case 2: // DJINN
                            break;
                        case 3: // INVOCAR
                            break;
                        case 4: // DEFENSA
                            battleUIController.ShowOrdersPanel(false);
                            turnController.HideTurnCamera();
                            StartCoroutine(PerformAction());
                            waitingAction = false;
                            break;
                        case 5: // OBJETO
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
                }
            }
        }
        else if (selectingTarget) // SELECTING TARGET/S FOR ACTION
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) // Right target
            {
                SelectRightTarget();
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow)) // Left target
            {
                SelectLeftTarget();
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
            }

            if (Input.GetKeyDown(KeyCode.R)) // Back Button
            {
                if (enemyPositions != null)
                {
                    for (int i = 0; i < enemyPositions.Length; i++) // Hide pointers
                    {
                        if (enemyPositions[i] != null)
                            enemyPositions[i].transform.Find("Pointer").gameObject.SetActive(false);
                    }
                }

                foreach (GameObject hero in GameManager.Instance.GetParty())
                {
                    hero.transform.Find("Pointer").gameObject.SetActive(false);
                }

                // Disable highlights for selected target on turnbar
                battleUIController.HideSelectedTarget();

                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.NEGATIVE_SOUND);

                switch (battleUIController.CommandSelected)
                {
                    case 0: // ATTACK
                        waitingAction = true;
                        selectingTarget = false;
                        break;
                    case 1: // PSYNERGY
                        selectingTarget = false;
                        choosingList = true;
                        nextPsynergy = null;
                        break;
                    case 2: // DJINN
                        break;
                    case 3: // SUMMON
                        break;
                    case 5: // ITEM
                        break;
                }
            }

            if (Input.GetKeyDown(KeyCode.E) && !keyPressed) // Target selected
            {
                keyPressed = true;
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.CONFIRM_SOUND);

                // Hide elements
                battleUIController.ShowOrdersPanel(false);
                turnController.HideTurnCamera();
                battleUIController.HideSelectedTarget();

                if (enemyPositions != null)
                {
                    for (int i = 0; i < enemyPositions.Length; i++)
                    {
                        if (enemyPositions[i] != null)
                            enemyPositions[i].transform.Find("Pointer").gameObject.SetActive(false);
                    }
                }

                foreach (GameObject hero in GameManager.Instance.GetParty())
                {
                    hero.transform.Find("Pointer").gameObject.SetActive(false);
                }

                selectingTarget = false;
                StartCoroutine(PerformAction());
            }
        }
        else if (choosingList)
        {
            if (Input.GetKeyDown(KeyCode.R)) // Returns back to choose a new command
            {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.NEGATIVE_SOUND);
                waitingAction = true;
                choosingList = false;
                battleUIController.ShowContentPanel(false);
            }

            if (Input.GetKeyDown(GameManager.Instance.KeyConfirm) && !keyPressed) // Skill or item selected
            {
                keyPressed = true;

                if ((nextPsynergy = PsynergyDatabase.Instance.GetPsynergy(battleUIController.GetContentItem())) != null)
                {
                    choosingList = false;
                    selectingTarget = true;

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

                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) && !keyPressed) // Move content panel
            {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                battleUIController.MoveContentPanel("UP");
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) && !keyPressed) // Move content panel
            {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                battleUIController.MoveContentPanel("DOWN");
            }
        }

        // Auxiliar key pressed controller
        if (Input.GetKeyUp(KeyCode.E) && keyPressed)
        {
            keyPressed = false;
        }
    }

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

        GameManager.Instance.InBattle = true;
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

        waitingAction = true;
    }

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

        GameManager.Instance.InBattle = false;
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

    #region State Reset Logic

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
        waitingAction = false;
        selectingTarget = false;
        choosingList = false;
        keyPressed = false;
    }

    #endregion


    public void ChangeBattleSpeed()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeGameSpeed();
        }
    }

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
}