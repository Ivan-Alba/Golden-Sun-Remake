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

    //TODO REVISAR
    int maxEnemiesInBattle = 3;
    float transitionTime = 2;

    //AUDIO CONTROLLER
    AudioSource musicController;
    AudioClip battleMusic, victoryMusic, gameOverMusic, fieldMusic;

    Animator logoTransition;
    GameObject mainCamera;

    //VICTORY ANIMATION

    //RESPAWN CONTROLLER
    RespawnController respawnController;
    GameObject respawnPoint;

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
    GameObject minimap;

    BattleUIController battleUIController;

    //AUXILIAR
    int enemiesOnBattle, enemiesDefeated, deadHeroes;
    bool keyPressed;
    int commandSelected;
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
            if (i < GameManager.instance.GetParty().Count)
                battleUIController.SetHeroInfoPanel(i);
        }

        minimap = GameObject.FindGameObjectWithTag("Minimap");
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
                if (Input.GetKeyDown(GameManager.instance.KeyConfirm) && !keyPressed)
                {
                    keyPressed = true;
                    GameManager.instance.GetComponent<Sounds>().PlaySound(GameManager.CONFIRM_SOUND);

                    //Depending on order
                    switch (battleUIController.CommandSelected)
                    {
                        case 0: //ATTACK
                            SelectDefaultTarget();
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

        if (selectingTarget) //Selectint target/s for command selected
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) //Right target
            {
                SelectRightTarget();
                GameManager.instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow)) //Left target
            {
                SelectLeftTarget();
                GameManager.instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
            }

            if (Input.GetKeyDown(KeyCode.R)) //Returns back to choose a new command
            {
                for (int i = 0; i < enemyPositions.Length; i++) //Hide pointers
                {
                    enemyPositions[i].transform.Find("Pointer").gameObject.SetActive(false);
                }
                //Disable highlights for selected target on turnbar
                battleUIController.HideSelectedTarget();

                GameManager.instance.GetComponent<Sounds>().PlaySound(GameManager.NEGATIVE_SOUND);
                waitingAction = true;
                selectingTarget = false;
            }

            if (Input.GetKeyDown(KeyCode.E) && !keyPressed) //Target selected
            {
                keyPressed = true;
                GameManager.instance.GetComponent<Sounds>().PlaySound(GameManager.CONFIRM_SOUND);

                //Hide elements
                battleUIController.ShowOrdersPanel(false);
                turnController.HideTurnCamera();
                battleUIController.HideSelectedTarget();

                //TODO pointers
                for (int i = 0; i < enemyPositions.Length; i++)
                {
                    enemyPositions[i].transform.Find("Pointer").gameObject.SetActive(false);
                }

                selectingTarget = false;
                StartCoroutine(PerformAction());
            }
        }
        else if (choosingList)
        {

            if (Input.GetKeyDown(KeyCode.R)) //Returns back to choose a new command
            {
                GameManager.instance.GetComponent<Sounds>().PlaySound(GameManager.NEGATIVE_SOUND);
                waitingAction = true;
                choosingList = false;
                battleUIController.ShowContentPanel(false);
            }

            if (Input.GetKeyDown(GameManager.instance.KeyConfirm) && !keyPressed) //Skill or item selected
            {
                if (battleUIController.GetContentItem() != null)
                {
                    Debug.Log($"Lanzas psinergía {battleUIController.GetContentItem()}");
                }
            }


            if (Input.GetKeyDown(KeyCode.UpArrow) && !keyPressed) //Move content panel
            {
                GameManager.instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                battleUIController.MoveContentPanel("UP");
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) && !keyPressed) //Move content panel
            {
                GameManager.instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
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
        respawnPoint = respawnZone;
        respawnController = respawnZone.GetComponent<RespawnController>();

        //ACTIVE GLOBAL VARIABLE IN BATTLE
        GameManager.instance.InBattle = true;


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
        if (GameManager.instance.GetParty().Count <= 2) i = 1;

        foreach (GameObject hero in GameManager.instance.GetParty())
        {
            fighters.Add(hero);
            heroList.Add(hero);
            //HERO POSITIONS
            hero.transform.position = heroPositions[i++].transform.position;
        }

        //SET INFO ON HERO'S PANEL
        battleUIController.RefreshHeroInfoPanel();

        //SET ENEMIES
        SetEnemiesOnBattle(firstEnemy);


        //DESTROY WORLD ENEMY
        Destroy(firstEnemy);

        //HideMinimap
        minimap.SetActive(false);

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

            //SET POINTERS POSITION
            GameObject pointer = enemyPositions[i].transform.Find("Pointer").gameObject;
            pointer.transform.position = new Vector3(pointer.transform.position.x,
                pointer.transform.position.y + enemiesList[i].GetComponent<BoxCollider>().bounds.size.y,
                pointer.transform.position.z);
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
        FighterStats performerStats = turnController.CurrentFighter.gameObject.GetComponent<FighterStats>();
        FighterStats targetStats = null;

        if (turnController.CurrentFighter.gameObject.CompareTag("Enemy"))
        {
            //SELECCIONAR OPCIÓN CON UN RANDOM
            battleUIController.CommandSelected = Random.Range(0, 1);
        }

        //List<FighterStats> multiTargetStats = new List<FighterStats>();

        switch (battleUIController.CommandSelected)
        {
            case 0: //ATACK

                if (turnController.CurrentFighter.gameObject.CompareTag("Enemy"))
                {
                    //ESCOGER TARGET ALEATORIAMENTE ENTRE LOS HEROES
                    targetStats = turnController.CurrentFighter.SelectRandomEnemy(heroList).GetComponent<FighterStats>();
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
            case 4:
                turnController.CurrentFighter.SetUpDefense();
                yield return new WaitForSeconds(1.5f);
                break;
        }

        //Check if enemy/hero is dead after action
        if (targetStats != null && targetStats.CurrHp <= 0)
        {
            if (targetStats.gameObject.CompareTag("Enemy"))
            {
                //ENEMY DEFEATED

                totalXpEarned += targetStats.transform.GetComponent<EnemyDrop>().Xp;
                totalGoldEarned += targetStats.transform.GetComponent<EnemyDrop>().Gold;
                totalPhEarned += targetStats.transform.GetComponent<EnemyDrop>().Ph;

                Item itemToAdd = null;
                //Randomize loot
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

                //----------------------------------------------------


                fighters.Remove(targetStats.gameObject);
                Destroy(targetStats.gameObject);

                enemiesDefeated++;
            }
            else
            {
                deadHeroes = 0;
                //HERO DEFEATED
                foreach (GameObject hero in heroList) //CONTAMOS CUANTOS HEROES HAY MUERTOS
                {
                    if (hero.GetComponent<FighterStats>().CurrHp <= 0)
                    {
                        deadHeroes++;
                    }
                }
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

    private void TurnFinished()
    {
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
        minimap.SetActive(true);

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
        GameManager.instance.InBattle = false;
        player.GetComponent<Animator>().enabled = true;

        //REPRODUCIMOS MUSICA DE ZONA
        musicController.clip = fieldMusic;
        musicController.Play();

        //RESET HEROS CD
        for (int i = 0; i < GameManager.instance.GetParty().Count; i++)
        {
            heroList[i].GetComponent<FighterController>().NextTurnCD = 0;
            heroList[i].GetComponent<FighterController>().SetOffDefense();
        }

        Destroy(this);
    }


    private void GameOver()
    {
        GameObject crossfade = transform.Find("Crossfade").gameObject;
        crossfade.SetActive(true);
        crossfade.GetComponent<Animator>().SetTrigger("start");
        musicController.clip = gameOverMusic;
        musicController.Play();
        Debug.Log("GAMEOVER");
    }

    private void SelectDefaultTarget()
    {
        int i = 0;

        while (enemiesList[i] == null)
        {
            i++;
        }

        //ACTIVE POINTER AND SET POSITION OVER ENEMY
        GameObject pointer = enemyPositions[i].transform.Find("Pointer").gameObject;
        pointer.SetActive(true);


        actualTarget = i;

        //SHOW ON TURN BAR ENEMY SELECTED
        battleUIController.ShowSelectedTarget(enemiesList[actualTarget]);
    }

    private void SelectRightTarget()
    {
        int nextTarget = 0;
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
            //UNACTIVE CURRENT POINTER
            enemyPositions[actualTarget].transform.Find("Pointer").gameObject.SetActive(false);
            //ACTIVE NEXT POINTER
            enemyPositions[nextTarget].transform.Find("Pointer").gameObject.SetActive(true);

            actualTarget = nextTarget;

            //SHOW ON TURN BAR ENEMY SELECTED
            battleUIController.ShowSelectedTarget(enemiesList[actualTarget]);
        }

    }

    private void SelectLeftTarget()
    {
        int nextTarget = 0;
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
            //UNACTIVE CURRENT POINTER
            enemyPositions[actualTarget].transform.Find("Pointer").gameObject.SetActive(false);
            //ACTIVE NEXT POINTER
            enemyPositions[nextTarget].transform.Find("Pointer").gameObject.SetActive(true);

            actualTarget = nextTarget;

            //SHOW ON TURN BAR ENEMY SELECTED
            battleUIController.ShowSelectedTarget(enemiesList[actualTarget]);
        }

    }
}
