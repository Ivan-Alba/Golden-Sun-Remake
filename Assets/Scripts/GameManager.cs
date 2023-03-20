using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine.SceneManagement;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

public class GameManager : MonoBehaviour
{
    private static GameManager instance = null;
    public const int CONFIRM_SOUND = 0;
    public const int MOVE_SOUND = 1;
    public const int NEGATIVE_SOUND = 2;
    public const int CURE_SOUND = 3;
    public const int ITEM_FOUNDED_SOUND = 4;

    private const KeyCode KEY_CONFIRM = KeyCode.E;
    private const KeyCode KEY_CANCEL = KeyCode.R;

    private bool inBattle = false;
    private bool loadingData = false;

    //PARTY INFO
    List<GameObject> party = new List<GameObject>();
    GameObject player;

    //GAME INFO
    int totalGold;
    float totalPlayed;

    //Getters and setters
    public int TotalGold { get => totalGold; set => totalGold = value; }
    public KeyCode KeyConfirm { get => KEY_CONFIRM; }
    public bool InBattle { get => inBattle; set => inBattle = value; }
    public float TotalPlayed { get => totalPlayed; set => totalPlayed = value; }

    private GameObject activeHero;

    public GameObject ActiveHero { get => activeHero; set => activeHero = value; }

    public KeyCode KeyCancel => KEY_CANCEL;

    public bool LoadingData { get => loadingData; set => loadingData = value; }
    public static GameManager Instance { get => instance; set => instance = value; }

    public List<GameObject> GetParty()
    {
        return party;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }

        if (!party.Contains(GameObject.Find("Isaac")))
        {
            party.Add(GameObject.Find("Isaac"));
            party.Add(GameObject.Find("Garet"));
        }

        player = GameObject.FindGameObjectWithTag("Player");
        activeHero = party[0];
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnLevelFinishedLoading;
    }

    void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("Level Loaded");
        Debug.Log(scene.name);
        Debug.Log(mode);


        // LoadSceneState();
        if (SaveManager.loadedData != null)
        {
            LoadData();
        }
    }

    public void Start()
    {
        party[0].GetComponent<HeroEquipment>().SetEquipment((int)HeroEquipment.slotsIndex.HANDS, (Equipable)ItemDatabase.Instance.GetItem("Metal_Bracelet"));

    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnLevelFinishedLoading;
    }

    public void ChangeGameSpeed()
    {
        Debug.Log("Change game speed:");
        if (Time.timeScale == 2f)
        {
            Time.timeScale = 1f;
            Debug.Log($"SpeedChanged to {Time.timeScale}");
        }
        else
        {
            Time.timeScale = 2f;
            Debug.Log($"SpeedChanged to {Time.timeScale}");

        }
    }

    public void ChangeActiveHero(int index)
    {
        if (party.Count > index)
        {
            //Cambiamos hero de posición
            GameObject currentHero = party[0];
            party[0] = party[index];
            party[index] = currentHero;
            activeHero = party[0];
            PsynergyWorldController.Instance.ResfreshShortcutsUI(activeHero);

            //CHANGE MODEL
            for (int i = 0; i < player.transform.Find("Model").childCount; i++)
            {
                player.transform.Find("Model").GetChild(i).gameObject.SetActive(false);
            }
            player.transform.Find("Model").Find($"{activeHero.name}_world").gameObject.SetActive(true);

            //CHANGE ANIMATION CONTROLLER
            player.GetComponent<Animator>().runtimeAnimatorController = Resources.Load<AnimatorController>($"Animations/Hero/{activeHero.name}/World/{activeHero.name}_world");
            player.GetComponent<Animator>().avatar = Resources.Load<Avatar>($"Animations/Hero/{activeHero.name}/World/avatar");
        }
    }

    public void Save()
    {
        SaveManager.Save(player.transform);
    }


    private void LoadData()
    {
        loadingData = true;
        GameData dataToLoad = SaveManager.loadedData;

        //---Scene----    
        player.GetComponent<Animator>().enabled = false;
        player.transform.position = new Vector3(dataToLoad.playerPosition[0], dataToLoad.playerPosition[1], dataToLoad.playerPosition[2]);
        player.transform.eulerAngles = new Vector3(0, dataToLoad.playerRotation[1], 0);

        //---Game---
        totalPlayed = dataToLoad.totalTime;
        totalGold = dataToLoad.totalGold;

        player.GetComponent<Animator>().enabled = true;

        loadingData = false;
        SaveManager.loadedData = null;
    }

    // Update is called once per frame
    void Update()
    {
        if (!loadingData)
        {
            TotalPlayed += Time.deltaTime;
        }
    }


}
