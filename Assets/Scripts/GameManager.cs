using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine.SceneManagement;
using UnityEngine;
using System.Linq;

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
            player = GameObject.FindGameObjectWithTag("Player");
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }

        if (SaveManager.loadedData == null)
        {
            if (!party.Contains(GameObject.Find("Isaac")))
            {
                party.Add(GameObject.Find("Isaac"));
                party.Add(GameObject.Find("Garet"));
            }
            activeHero = party[0];
        }
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
            loadingData = true;
            LoadData();
        }
    }

    public void Start()
    {
        
        //party[0].GetComponent<HeroEquipment>().SetEquipment((int)HeroEquipment.slotsIndex.HANDS, (Equipable)ItemDatabase.Instance.GetItem("Metal_Bracelet"));

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

            changeModelHero();
        }
    }

    private void changeModelHero()
    {
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

    //SEPARAR EN SAVEMANAGER?
    private void LoadData()
    {
        GameData dataToLoad = SaveManager.loadedData[0];

        //------------SCENE------------    
        player.GetComponent<Animator>().enabled = false;
        player.transform.position = new Vector3(dataToLoad.playerPosition[0], dataToLoad.playerPosition[1], dataToLoad.playerPosition[2]);
        player.transform.eulerAngles = new Vector3(0, dataToLoad.playerRotation[1], 0);

        //----------GAMEMANAGER--------
        totalPlayed = dataToLoad.totalTime;
        totalGold = dataToLoad.totalGold;

        //------------PARTY----------
        foreach (string heroName in dataToLoad.party)
        {
            party.Add(GameObject.Find(heroName));
        }
        activeHero = party[0];
        changeModelHero();

        //--Hero settings---
        for (int i = 0; i < party.Count; i++)
        {
            party[i].GetComponent<FighterStats>().CharacterStats = dataToLoad.heroStats[i];
            party[i].GetComponent<FighterStats>().Level = dataToLoad.heroLevelAndXp[i][0];
            party[i].GetComponent<FighterStats>().Xp = dataToLoad.heroLevelAndXp[i][1];
            party[i].GetComponent<FighterStats>().NextLevelXp = dataToLoad.heroLevelAndXp[i][2];
            party[i].GetComponent<FighterStats>().CurrHp = dataToLoad.heroCurrHpPp[i][0];
            party[i].GetComponent<FighterStats>().CurrPp = dataToLoad.heroCurrHpPp[i][1];

            //---Psynergies---
            party[i].GetComponent<FighterStats>().Psynergies = dataToLoad.heroPsynergy[i];

         
            //---Equipment---
            party[i].GetComponent<HeroEquipment>().EquippedItems = dataToLoad.heroEquippedGear[i];

            //Get equipped gear icons
            foreach (Equipable equipPiece in party[i].GetComponent<HeroEquipment>().EquippedItems)
            {
                if (equipPiece != null)
                {
                    equipPiece.Icon = ItemDatabase.Instance.GetItem(equipPiece.ItemSlug).Icon;
                }
            }
        }

        //---Psynergies shortcuts---
        foreach (KeyValuePair<string, Psynergy[]> heroShortcut in dataToLoad.heroShortcuts)
        {
            PsynergyWorldController.Instance.HeroShortcuts.Add(GameObject.Find(heroShortcut.Key), heroShortcut.Value);
        }
        PsynergyWorldController.Instance.ResfreshShortcutsUI(activeHero);




        //---------INVENTORY---------
        List<KeyValuePair<Item, int>> itemsList = dataToLoad.playerItems;
        foreach (KeyValuePair<Item, int> pair in itemsList)
        {
            InventoryController.Instance.AddItem(pair.Key.ItemSlug, pair.Value);
        }

        player.GetComponent<Animator>().enabled = true;

        loadingData = false;




        //----FINISH LOAD-----
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
