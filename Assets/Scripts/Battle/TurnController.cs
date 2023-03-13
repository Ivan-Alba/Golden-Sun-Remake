using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class TurnController : MonoBehaviour
{
    //Variable with the gameObject of current fighter
    FighterController currentFighter;

    bool firstTurn = true;
    int maxTurnBoxes = 6;

    //LIST WITH TURNS OF FIGHTERS
    List<KeyValuePair<GameObject, int>> turnsOrdered;

    //CAMERA HERO
    GameObject turnCameraPrefab;
    Camera turnCamera;
    GameObject turnCameraMask;

    BattleCameraController battleCameraController;
    BattleUIController battleUIController;

    public int MaxTurnBoxes { get => maxTurnBoxes; set => maxTurnBoxes = value; }
    public FighterController CurrentFighter { get => currentFighter; set => currentFighter = value; }

    private void Awake()
    {
        turnCameraPrefab = (GameObject)Resources.Load("Prefabs/TurnCamera");
        battleCameraController = GameObject.Find("BattleCamera").GetComponent<BattleCameraController>();
        battleUIController = gameObject.GetComponent<BattleUIController>();
    }

    public List<KeyValuePair<GameObject, int>> SetTurns(List<GameObject> fighters)
    {
        //CREAMOS DICCIONARIO DONDE GUARDAR LOS GAMEOBJECTS DE LOS FIGHTERS CON EL VALOR DE CD DE TURNO.
        List<KeyValuePair<GameObject, int>> nextTurns = new List<KeyValuePair<GameObject, int>>();

        //CALCULAR NUMERO TURNOS EXTRA
        int turnMultiplier = 2;
        for (int h = turnMultiplier; h * fighters.Count < maxTurnBoxes; h++)
        {
            turnMultiplier = h + 1;
        }

        //AÑADIMOS AL DICCIONARIO LOS OBJETOS
        int dicCount = 0;
        while (nextTurns.Count < fighters.Count * turnMultiplier) //AÑADIMOS TURNOS X VECES HASTA LLENAR LA BARRA
        {
            if (dicCount < fighters.Count) //PRIMERO AÑADIMOS LOS FIGHTERS CON SUS VALORES BÁSICOS
            {
                if (firstTurn)
                {
                    fighters[dicCount].GetComponent<FighterController>().NextTurnCD -= fighters[dicCount].GetComponent<FighterStats>().Agility;
                    nextTurns.Add(new KeyValuePair<GameObject, int>(fighters[dicCount],
                    fighters[dicCount].GetComponent<FighterController>().NextTurnCD));
                    firstTurn = false;
                }
                else
                {
                    nextTurns.Add(new KeyValuePair<GameObject, int>(fighters[dicCount],
                    fighters[dicCount].GetComponent<FighterController>().NextTurnCD));
                }

            }
            else //EMPEZAMOS A REPETIR FIGHTERS
            {
                float increment = dicCount / fighters.Count;
                if (increment < 2)  //LA PRIMERA VEZ QUE REPETIMOS
                {
                    nextTurns.Add(new KeyValuePair<GameObject, int>(fighters[dicCount - fighters.Count],
                        fighters[dicCount - fighters.Count].GetComponent<FighterController>().NextTurnCD +
                         50 - fighters[dicCount - fighters.Count].GetComponent<FighterStats>().Agility / 2));

                }
                else if (increment < 3) //LA SEGUNDA VEZ QUE REPETIMOS
                {
                    nextTurns.Add(new KeyValuePair<GameObject, int>(fighters[dicCount - fighters.Count * 2],
                      fighters[dicCount - fighters.Count * 2].GetComponent<FighterController>().NextTurnCD + 100
                      - fighters[dicCount - fighters.Count * 2].GetComponent<FighterStats>().Agility * 2 / 2));
                }
                else if (increment < 4)
                {
                    nextTurns.Add(new KeyValuePair<GameObject, int>(fighters[dicCount - fighters.Count * 3],
                     fighters[dicCount - fighters.Count * 3].GetComponent<FighterController>().NextTurnCD + 150
                     - fighters[dicCount - fighters.Count * 3].GetComponent<FighterStats>().Agility * 3 / 2));
                }
                else
                {
                    nextTurns.Add(new KeyValuePair<GameObject, int>(fighters[dicCount - fighters.Count * 4],
                     fighters[dicCount - fighters.Count * 4].GetComponent<FighterController>().NextTurnCD + 200
                     - fighters[dicCount - fighters.Count * 4].GetComponent<FighterStats>().Agility * 4 / 2));
                }
            }
            dicCount++;
        }

        turnsOrdered = nextTurns.OrderBy(d => d.Value).ToList();


        //Set UI Turn Panel
        battleUIController.SetTurnsPanel(turnsOrdered);

        if (turnsOrdered.ElementAt(0).Key.CompareTag("Hero"))
        {
            SetHeroTurn();
        }


        //Set actual turn
        CurrentFighter = turnsOrdered.ElementAt(0).Key.GetComponent<FighterController>();
        //Returns a dictionary with fighters ordered by CD
        return turnsOrdered;
    }

    private void SetHeroTurn()
    {
        //Turn Camera
        turnCamera = Instantiate(turnCameraPrefab, turnsOrdered.ElementAt(0).Key.transform).GetComponent<Camera>();
        turnCamera.gameObject.SetActive(true);
        turnCameraMask = turnCamera.transform.GetComponentInChildren<Image>().gameObject;
        turnCameraMask.GetComponent<Animator>().SetTrigger("enter");
        turnCameraMask.transform.Find("TurnImage").GetComponent<Animator>().SetTrigger("enter");

        //UI Shows panels
        battleUIController.ShowOrdersPanel(true);
        battleUIController.ShowTurnsPanel(true);

        GameManager.instance.GetComponent<Sounds>().PlaySound(GameManager.CONFIRM_SOUND);
    }

    public void HideTurnCamera()
    {
        turnCameraMask.GetComponent<Animator>().SetTrigger("exit");
        turnCameraMask.transform.Find("TurnImage").GetComponent<Animator>().SetTrigger("exit");
        Destroy(turnCamera.gameObject, 1f);
    }
}