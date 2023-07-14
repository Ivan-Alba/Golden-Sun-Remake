using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

public class BattleManager : MonoBehaviour
{

    GameObject player; //player reference


    List<GameObject> party;

    //TODO REVISAR
    int maxEnemiesInBattle = 3;
    float transitionTime = 2;

    //AUDIO CONTROLLER
    AudioSource musicController;
    AudioClip battleMusic, victoryMusic, gameOverMusic, fieldMusic;

    Animator logoTransition;
    GameObject mainCamera;
    float[] pointerOriginalY;


    //NEXT ACTION
    Psynergy nextPsynergy;
    Item nextItem;

    List<GameObject> targetList = new List<GameObject>();

    string nextAction_targetType;
    int nextAction_targetSize;


    //RESPAWN CONTROLLER
    RespawnController respawnController;

    //LISTS WITH FIGHTERS
    List<GameObject> enemiesList = new List<GameObject>();
    List<GameObject> heroList = new List<GameObject>();
    List<GameObject> fighters = new List<GameObject>();

    //CAMERA CONTROLLER
    BattleCameraController battleCameraController;

    //RESULTS SCREEN
    ResultsScreenController resultsController;
    int totalXpEarned, totalPhEarned, totalGoldEarned;
    Dictionary<Item, int> totalItemsObtained;

    //BATTLE FIELD POSITIONS
    GameObject battleField;
    GameObject[] heroPositions = new GameObject[4];
    GameObject[] enemyPositions;

    //LIST WITH TURNS OF FIGHTERS
    List<KeyValuePair<GameObject, int>> turnsOrdered;

    //TURN CONTROLLER
    TurnController turnController;

    //Minimap
    GameObject playerCanvas;

    BattleUIController battleUIController;

    //AUXILIAR
    int enemiesOnBattle, enemiesDefeated, deadHeroes;
    bool keyPressed;
    int actualTarget;
    bool selectingTarget, waitingAction, choosingList;

    public bool WaitingAction { get => waitingAction; set => waitingAction = value; }

    private void Awake()
    {
        mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        victoryMusic = Resources.Load("Music/FanfareVictory") as AudioClip;
        gameOverMusic = Resources.Load("Music/GameOverMusic") as AudioClip;
        battleMusic = Resources.Load("Music/battleMusic") as AudioClip;
        logoTransition = transform.Find("LogoCross").gameObject.GetComponent<Animator>();
        player = GameObject.Find("Player");
        musicController = GameObject.Find("MusicController").GetComponent<AudioSource>();

        //Party members
        party = GameManager.Instance.GetParty();

        //BattleCamera controller
        battleCameraController = GameObject.Find("BattleCamera").GetComponent<BattleCameraController>();

        //Turn controller
        turnController = transform.GetComponent<TurnController>();

        //Result screen controller
        resultsController = transform.Find("ResultsScreen").GetComponent<ResultsScreenController>();
        fieldMusic = musicController.clip;
        battleUIController = transform.GetComponent<BattleUIController>();
        //Total Items Rresult
        totalItemsObtained = new Dictionary<Item, int>();


        //BATTLE FIELD SETTINGS HERO POSITIONS AND UI HERO INFO PANELS
        battleField = transform.Find("BattleField").gameObject;
        for (int i = 0; i < 4; i++)
        {
            heroPositions[i] = battleField.transform.Find($"Hero{i}").gameObject;
            if (i < GameManager.Instance.GetParty().Count)
                battleUIController.SetHeroInfoPanel(i);
        }

        pointerOriginalY = new float[maxEnemiesInBattle];
        playerCanvas = GameObject.Find("PlayerCanvas");
    }



    // Update is called once per frame
    void Update()
    {
        battleUIController.RefreshHeroInfoPanel();

        if (waitingAction) //WHEN A TURN ENDS
        {
            if (turnController.CurrentFighter.CompareTag("Enemy")) //If enemy's turn
            {
                waitingAction = false; //Starts enemy turn
                StartCoroutine(PerformAction()); //Call IA to decide enemy actions
            }
            else //Hero turn decide orders 
            {
                //Move right in orders buttons
                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    battleUIController.ordersPanelMove("RIGHT");
                }

                //Move left in orders buttons
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    battleUIController.ordersPanelMove("LEFT");
                }


                //Press the confirm Key to select an order
                if (Input.GetKeyDown(GameManager.Instance.KeyConfirm) && !keyPressed)
                {
                    keyPressed = true;
                    GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.CONFIRM_SOUND);

                    //Depending on order
                    switch (battleUIController.CommandSelected)
                    {
                        case 0: //ATTACK
                            SelectDefaultTarget("ONE_TARGET");
                            nextAction_targetType = "ONE_TARGET";
                            waitingAction = false;
                            selectingTarget = true;
                            break;
                        case 1:
                            //PSINERGÍA
                            waitingAction = false;
                            choosingList = true;
                            battleUIController.SetContentPanel("Psynergy");
                            battleUIController.ShowContentPanel(true);
                            break;
                        case 2:
                            //DJINN
                            break;
                        case 3:
                            //INVOCAR
                            break;
                        case 4:
                            //DEFENSA
                            //Hide elements
                            battleUIController.ShowOrdersPanel(false);
                            turnController.HideTurnCamera();
                            StartCoroutine(PerformAction());
                            waitingAction = false;
                            break;
                        case 5:
                            //OBJETO
                            turnsOrdered.ElementAt(0).Key.GetComponent<FighterStats>().CurrHp =
                                turnsOrdered.ElementAt(0).Key.GetComponent<FighterStats>().MaxHp;
                            break;
                        case 6:
                            //ESTADO
                            break;
                        case 7:
                            //HUIR
                            break;

                    }
                }
            }
        }
        else

        if (selectingTarget) //SELECTING TARGET/S FOR ACTION
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) //Right target
            {
                SelectRightTarget();
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow)) //Left target
            {
                SelectLeftTarget();
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
            }

            if (Input.GetKeyDown(KeyCode.R)) //Back Button
            {
                for (int i = 0; i < enemyPositions.Length; i++) //Hide pointers
                {
                    enemyPositions[i].transform.Find("Pointer").gameObject.SetActive(false);
                }

                foreach (GameObject hero in GameManager.Instance.GetParty())
                {
                    hero.transform.Find("Pointer").gameObject.SetActive(false);
                }

                //Disable highlights for selected target on turnbar
                battleUIController.HideSelectedTarget();

                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.NEGATIVE_SOUND);

                switch (battleUIController.CommandSelected)
                {
                    case 0: //ATTACK
                        waitingAction = true;
                        selectingTarget = false;
                        break;
                    case 1: //PSYNERGY
                        selectingTarget = false;
                        choosingList = true;
                        nextPsynergy = null;
                        break;
                    case 2: //DJINN
                        break;
                    case 3: //SUMMON
                        break;
                    case 5: //ITEM
                        break;

                }

            }

            if (Input.GetKeyDown(KeyCode.E) && !keyPressed) //Target selected
            {
                keyPressed = true;
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.CONFIRM_SOUND);

                //Hide elements
                battleUIController.ShowOrdersPanel(false);
                turnController.HideTurnCamera();
                battleUIController.HideSelectedTarget();

                //TODO pointers
                for (int i = 0; i < enemyPositions.Length; i++)
                {
                    enemyPositions[i].transform.Find("Pointer").gameObject.SetActive(false);
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

            if (Input.GetKeyDown(KeyCode.R)) //Returns back to choose a new command
            {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.NEGATIVE_SOUND);
                waitingAction = true;
                choosingList = false;
                battleUIController.ShowContentPanel(false);
            }

            if (Input.GetKeyDown(GameManager.Instance.KeyConfirm) && !keyPressed) //Skill or item selected
            {
                keyPressed = true;

                if ((nextPsynergy = PsynergyDatabase.Instance.GetPsynergy(battleUIController.GetContentItem())) != null)
                {
                    choosingList = false;
                    selectingTarget = true;

                    if (battleUIController.CommandSelected == 1)
                    { // PSYNERGY
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
                    else if (battleUIController.CommandSelected == 5) //ITEMS
                    {

                    }
                }
            }


            if (Input.GetKeyDown(KeyCode.UpArrow) && !keyPressed) //Move content panel
            {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                battleUIController.MoveContentPanel("UP");
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) && !keyPressed) //Move content panel
            {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                battleUIController.MoveContentPanel("DOWN");
            }
        }

        //Auxiliar key pressed controller
        if (Input.GetKeyUp(KeyCode.E) && keyPressed)
        {
            keyPressed = false;
        }
    }


    //TODO ENEMIGOS DE OTRAS FUENTES
    public void AwakeBatlle(GameObject enemyInitiator = null, GameObject respawnZone = null,
        List<GameObject> preSetEnemies = null)
    {

        enemiesOnBattle = Random.Range(1, maxEnemiesInBattle + 1);
        enemyPositions = new GameObject[enemiesOnBattle];

        //RESPAWN OBJECTS
        respawnController = respawnZone.GetComponent<RespawnController>();

        //ACTIVE GLOBAL VARIABLE IN BATTLE
        GameManager.Instance.InBattle = true;


        player.GetComponent<Animator>().enabled = false;

        //BATTLE MUSIC
        musicController.clip = battleMusic;
        musicController.Play();

        //START BATTLE TRANSITION
        StartCoroutine(StartBattle(enemyInitiator));
    }

    private IEnumerator StartBattle(GameObject firstEnemy)
    {
        //TRANSITION MAIN CAMERA TO BATTLE CAMERA
        StartCoroutine(battleCameraController.StartTransition(mainCamera));

        while (!battleCameraController.TransitionDone)
        {
            yield return null;
        }


        //SET HEROS
        int i = 0;
        if (GameManager.Instance.GetParty().Count <= 2) i = 1;

        foreach (GameObject hero in GameManager.Instance.GetParty())
        {
            //IF HERO IS ALIVE
            if (hero.GetComponent<FighterStats>().CurrHp != 0)
            {
                fighters.Add(hero);
                heroList.Add(hero);
            }
            //HERO POSITIONS
            hero.transform.position = heroPositions[i++].transform.position;
        }

        //SET INFO ON HERO'S PANEL
        battleUIController.RefreshHeroInfoPanel();

        //SET ENEMIES
        SetEnemiesOnBattle(firstEnemy);


        //DESTROY WORLD ENEMY
        Destroy(firstEnemy);

        //HidePlayerCanvas
        playerCanvas.SetActive(false);

        //ANIMACIÓN CAMARA INTRO BATLLA
        battleCameraController.IntroBattle();
        yield return new WaitForSeconds(2f);

        //TODO BATTLE CAMERA FADE
        StartCoroutine(battleCameraController.StartTransition());

        while (!battleCameraController.TransitionDone)
        {
            yield return null;
        }

        battleCameraController.SetInitialIdle();
        SetTurns();
    }



    private void SetEnemiesOnBattle(GameObject firstEnemy)
    {
        GameObject enemyPlace;
        GameObject newEnemy;

        //SET ENEMIES
        Debug.Log($"{enemiesOnBattle} enemigos");

        for (int i = 0; i < enemiesOnBattle; i++)
        {
            if (i == 0) //FIRST ENEMY LIKE WORLD ENEMY
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

            //SET TOP POINTERS POSITION
            GameObject topPointer = enemyPositions[i].transform.Find("Pointer").Find("TopPointer").gameObject;
            topPointer.transform.position = new Vector3(topPointer.transform.position.x,
                topPointer.transform.position.y + enemiesList[i].GetComponent<BoxCollider>().bounds.size.y,
                topPointer.transform.position.z);
        }



        //INITIALIZE FIGHTERS FIRST TURN CD BY AGILITY
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
            //SELECCIONAR OPCIÓN CON UN RANDOM
            battleUIController.CommandSelected = Random.Range(0, 1);
        }

        switch (battleUIController.CommandSelected)
        {
            case 0: //ATACK

                if (turnController.CurrentFighter.gameObject.CompareTag("Enemy"))
                {
                    //ESCOGER TARGET ALEATORIAMENTE ENTRE LOS HEROES
                    targetStats = turnController.CurrentFighter.SelectRandomTarget(heroList).GetComponent<FighterStats>();
                    targetList.Add(targetStats.gameObject);
                }
                else
                {
                    targetStats = enemiesList[actualTarget].GetComponent<FighterStats>();
                }

                //ATTACK METHOD
                StartCoroutine(turnController.CurrentFighter.BasicAttack(targetStats.gameObject));

                while (!turnController.CurrentFighter.ActionFinished)
                {
                    yield return null;
                }

                turnController.CurrentFighter.ActionFinished = false;

                break;

            case 1: //PSYNERGY

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

            case 4:
                turnController.CurrentFighter.SetUpDefense();
                yield return new WaitForSeconds(1.5f);
                break;
        }

        CheckDefeated();
    }

    private void CheckDefeated()
    {
        //Check if enemy/hero is dead after action
        foreach (GameObject target in targetList)
        {
            FighterStats targetData = target.GetComponent<FighterStats>();
            if (targetData != null && targetData.CurrHp <= 0)
            {
                if (targetData.gameObject.CompareTag("Enemy"))
                {
                    //ENEMY DEFEATED

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
                    //HERO DEFEATED
                    fighters.Remove(targetData.gameObject);

                }
            }
        }

        //CONTAMOS CUANTOS HEROES HAY MUERTOS
        deadHeroes = 0;
        foreach (GameObject hero in heroList)
        {
            if (hero.GetComponent<FighterStats>().CurrHp <= 0)
            {
                deadHeroes++;
            }
        }


        if (enemiesDefeated == enemiesList.Count) //All enemies defeated
        {
            StartCoroutine(BattleWinned());
            //battleCanvas.SetActive(false);
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
        //Randomize loot
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
        battleUIController.ShowBattleCanvas(false); //Disable battle canvas

        //VICTORY FANFARE
        musicController.clip = victoryMusic;
        musicController.Play();
        yield return new WaitForSeconds(0.5f);

        //ANIMACION VICTORIA
        foreach (GameObject hero in heroList)
        {
            if (hero.GetComponent<FighterStats>().CurrHp > 0)
                hero.GetComponentInChildren<Animator>().SetBool("victoryPose", true);
        }

        //TODO ANIMACION CÁMARA
        battleCameraController.Victory(turnsOrdered.ElementAt(0).Key);
        yield return new WaitForSeconds(3);

        //SHOW RESULTS
        StartCoroutine(resultsController.ShowResults(heroList, totalXpEarned, totalPhEarned, totalGoldEarned, totalItemsObtained));
        while (!resultsController.ResultsFinished)
        {
            yield return null;
        }

        //TRANSICION 
        logoTransition.SetTrigger("start");
        yield return new WaitForSeconds(transitionTime);

        SetOffBattle();
    }


    private void SetOffBattle()
    {
        //DESACTIVAR RESULTSCREEN
        resultsController.Reset();

        //desactivar pose de victoria
        foreach (GameObject hero in heroList)
        {
            if (hero.GetComponent<FighterStats>().CurrHp > 0)
                hero.GetComponentInChildren<Animator>().SetBool("victoryPose", false);
        }

        //Show minimap
        playerCanvas.SetActive(true);

        //CAMBIAMOS A CAMARA MUNDO
        battleCameraController.SetOffBattleCamera();
        object[] Cams = GameObject.FindObjectsOfType(typeof(Camera));
        foreach (Camera C in Cams)
        {
            if (C.gameObject.CompareTag("MainCamera"))
            {
                C.enabled = true;
            }
        }

        //UNACTIVE BATTLE IN GAME MANAGER
        GameManager.Instance.InBattle = false;
        player.GetComponent<Animator>().enabled = true;

        //REPRODUCIMOS MUSICA DE ZONA
        musicController.clip = fieldMusic;
        musicController.Play();

        //RESET HEROS CD
        for (int i = 0; i < GameManager.Instance.GetParty().Count; i++)
        {
            heroList[i].GetComponent<FighterController>().NextTurnCD = 0;
            heroList[i].GetComponent<FighterController>().SetOffDefense();
        }


        //SAVE START TOP POINTERS POSITION
        for (int i = 0; i < enemiesOnBattle; i++)
        {
            Transform currentPosition = enemyPositions[i].transform.Find("Pointer").Find("TopPointer");
            currentPosition.position = new Vector3(currentPosition.position.x, pointerOriginalY[i], currentPosition.position.z);

        }

        Destroy(this);
    }


    private void GameOver()
    {
        GameObject crossfade = transform.Find("Crossfade").gameObject;
        crossfade.SetActive(true);
        crossfade.transform.Find("GameOver").gameObject.SetActive(true);
        crossfade.GetComponent<Animator>().SetTrigger("start");
        musicController.clip = gameOverMusic;
        musicController.Play();
        Debug.Log("GAMEOVER");
    }

    private void SelectDefaultTarget(string targetType, int targetSize = 0, int i = 0)
    {

        //ALLIES PSYNERGY TARGET

        if (nextPsynergy != null && nextPsynergy.Type == Psynergy.Types.HEALING)
        {
            Debug.Log("Healing Psynergy");
            GameObject pointer;
            List<GameObject> party = GameManager.Instance.GetParty();
            targetList = new List<GameObject>();

            foreach (GameObject hero in party)
            {
                hero.transform.Find("Pointer").gameObject.SetActive(false);
            }

            switch (targetType)
            {
                case "ONE_TARGET":
                    //HERO SELECTED 
                    pointer = party[i].transform.Find("Pointer").gameObject;
                    pointer.SetActive(true);
                    targetList.Add(party[i]);
                    actualTarget = i;
                    //SHOW ON TURN BAR ENEMY SELECTED
                    battleUIController.ShowSelectedTarget(targetList);
                    break;

                case "MULTI_TARGET":
                    break;
            }
        }

        //ENEMIES PSYNERGY TARGET

        else
        {


            GameObject pointer;
            targetList = new List<GameObject>();

            foreach (GameObject position in enemyPositions)
            {
                position.transform.Find("Pointer").gameObject.SetActive(false);
            }

            switch (targetType)
            {
                case "ONE_TARGET":

                    while (enemiesList[i] == null)
                    {
                        i++;
                    }

                    //ADD ENEMY TO SELECTED AND ACTIVE POINTER
                    pointer = enemyPositions[i].transform.Find("Pointer").gameObject;
                    pointer.SetActive(true);
                    targetList.Add(enemiesList[i]);
                    actualTarget = i;
                    //SHOW ON TURN BAR ENEMY SELECTED
                    battleUIController.ShowSelectedTarget(targetList);
                    break;

                case "MULTI_TARGET":
                    while (enemiesList[i] == null)
                    {
                        i++;
                    }

                    actualTarget = i;

                    //ADD ENEMY TO SELECTED AND ACTIVE POINTER
                    targetList.Add(enemiesList[i]);
                    pointer = enemyPositions[i].transform.Find("Pointer").gameObject;
                    pointer.SetActive(true);


                    switch (targetSize) //HOW MANY ENEMIES TARGETS ACTION
                    {
                        case 3:
                            switch (i) //ACTIVATE POINTERS OF SIDE'S ENEMIES
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
                        case 0:
                            nextTarget = 1;
                            break;
                        case 1:
                            nextTarget = 0;
                            break;
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
                case 1: //SI HAY 1 ENEMIGO
                    nextTarget = 0;
                    break;

                case 2: //SI HAY 2 ENEMIGOS
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

                case 3: //SI HAY 3 ENEMIGOS
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
                        case 0:
                            nextTarget = 1;
                            break;
                        case 1:
                            nextTarget = 0;
                            break;
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
                case 1: //SI HAY 1 ENEMIGO
                    nextTarget = 0;
                    break;

                case 2: //SI HAY 2 ENEMIGOS
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

                case 3: //SI HAY 3 ENEMIGOS
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
