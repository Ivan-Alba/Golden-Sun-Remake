using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine.SceneManagement;
using UnityEngine;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        TitleScreen,
        WorldExploration,
        InBattle,
        InMenu,
        Cutscene,
        Loading
    }

    private static GameManager instance = null;
    public const int CONFIRM_SOUND = 0;
    public const int MOVE_SOUND = 1;
    public const int NEGATIVE_SOUND = 2;
    public const int CURE_SOUND = 3;
    public const int ITEM_FOUNDED_SOUND = 4;

    private const KeyCode KEY_CONFIRM = KeyCode.E;
    private const KeyCode KEY_CANCEL = KeyCode.R;

    [Header("Game State System")]
    [SerializeField] private GameState currentState = GameState.TitleScreen;

    [Header("Persistent References")]
    [SerializeField] private GameObject playerMainCamera;

    private bool inBattle = false;
    private bool loadingData = false;

    // SCENE & SPAWN MANAGEMENT
    private string pendingSpawnID = null;

    // PARTY INFO
    List<GameObject> party = new List<GameObject>();
    GameObject player;

    // GAME INFO
    int totalGold;
    float totalPlayed;

    // Getters and setters
    public GameState CurrentState { get => currentState; private set => currentState = value; }
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

            if (playerMainCamera == null)
            {
                Transform coreTransform = transform.parent != null ? transform.parent : transform;
                Transform camTrans = coreTransform.Find("MainCamera");
                if (camTrans != null)
                {
                    playerMainCamera = camTrans.gameObject;
                }
            }

        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnLevelFinishedLoading;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnLevelFinishedLoading;
    }

    #region State Machine Management

    public void SetGameState(GameState newState)
    {
        currentState = newState;

        // Sync legacy boolean for existing battle logic
        inBattle = (currentState == GameState.InBattle);

        switch (currentState)
        {
            case GameState.TitleScreen:
                ToggleGameplaySystems(false);
                break;

            case GameState.WorldExploration:
                ToggleGameplaySystems(true);
                break;

            case GameState.InBattle:
                // Disable map controls while battle system operates
                ToggleGameplaySystems(false);
                break;

            case GameState.InMenu:
                // Pauses world inputs, menu script will process UI
                break;

            case GameState.Cutscene:
            case GameState.Loading:
                ToggleInputSystem(false);
                break;
        }
    }

    private void ToggleGameplaySystems(bool active)
    {
        if (player != null)
        {
            player.SetActive(active);
        }

        ToggleInputSystem(active);

        if (playerMainCamera != null)
        {
            playerMainCamera.SetActive(active);
        }
    }

    private void ToggleInputSystem(bool active)
    {
        InputManager inputMgr = GetComponentInChildren<InputManager>();
        if (inputMgr != null)
        {
            inputMgr.enabled = active;
        }
    }

    #endregion

    #region New Game Flow

    /// <summary>
    /// Call this function from your Title Screen when pressing "New Game".
    /// </summary>
    public void StartNewGame(string firstSceneName, string initialSpawnID)
    {
        // 1. Reset game counters
        totalGold = 0;
        totalPlayed = 0f;
        party.Clear();

        // 2. Clear inventory
        if (InventoryController.Instance != null)
        {
            InventoryController.Instance.clearInventory();
        }

        // 3. Initialize default starting party
        GameObject isaac = GameObject.Find("Isaac");
        GameObject garet = GameObject.Find("Garet");

        if (isaac != null) party.Add(isaac);
        if (garet != null) party.Add(garet);

        if (party.Count > 0)
        {
            activeHero = party[0];

            // Apply starting level ups if needed
            for (int i = 0; i < party.Count; i++)
            {
                FighterStats stats = party[i].GetComponent<FighterStats>();
                if (stats != null)
                {
                    stats.LevelUp();
                    stats.LevelUp();
                    stats.LevelUp();
                }
            }

            changeModelHero();
        }

        // 4. Initialize Psinergy Shortcuts
        if (PsynergyWorldController.Instance != null)
        {
            PsynergyWorldController.Instance.Initialize();
        }

        // 5. Load first playable scene and set spawn target
        ChangeScene(firstSceneName, initialSpawnID);
    }

    #endregion

    public void ChangeScene(string sceneName, string spawnID)
    {
        pendingSpawnID = spawnID;
        SceneManager.LoadScene(sceneName);
    }

    void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"Level Loaded: {scene.name}");

        // Refresh Player reference if lost
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }

        // 1. Check if we are in Title screen
        if (scene.name.Equals("Title") || scene.name.Equals("MainMenu"))
        {
            SetGameState(GameState.TitleScreen);
            return;
        }

        // 2. If loading from save file
        if (SaveManager.loadedData != null)
        {
            SetGameState(GameState.Loading);
            loadingData = true;
            LoadData();
            pendingSpawnID = null;
        }
        // 3. If moving via SceneTeleporter or Starting New Game
        else if (!string.IsNullOrEmpty(pendingSpawnID))
        {
            RelocatePlayerToSpawnPoint();
        }

        // Set state to Exploration after completing relocation/data setup
        SetGameState(GameState.WorldExploration);
    }

    private void RelocatePlayerToSpawnPoint()
    {
        SpawnPoint[] spawnPoints = FindObjectsOfType<SpawnPoint>();

        foreach (SpawnPoint sp in spawnPoints)
        {
            if (sp.SpawnPointID == pendingSpawnID)
            {
                if (player != null)
                {
                    // Temporarily disable CharacterController for direct transform modification
                    CharacterController cc = player.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;

                    player.transform.position = sp.transform.position;
                    player.transform.rotation = sp.transform.rotation;

                    if (cc != null) cc.enabled = true;
                }
                break;
            }
        }

        pendingSpawnID = null; // Clear pending ID
    }

    public void Start()
    {
        // Initialization handled via StartNewGame or LoadData
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
            // Swap hero order
            GameObject currentHero = party[0];
            party[0] = party[index];
            party[index] = currentHero;
            activeHero = party[0];

            if (PsynergyWorldController.Instance != null)
            {
                PsynergyWorldController.Instance.ResfreshShortcutsUI(activeHero);
            }

            changeModelHero();
        }
    }

    private void changeModelHero()
    {
        if (player == null || activeHero == null) return;

        Transform modelTrans = player.transform.Find("Model");
        if (modelTrans != null)
        {
            for (int i = 0; i < modelTrans.childCount; i++)
            {
                modelTrans.GetChild(i).gameObject.SetActive(false);
            }

            Transform activeModel = modelTrans.Find($"{activeHero.name}_world");
            if (activeModel != null)
            {
                activeModel.gameObject.SetActive(true);
            }
        }

        Animator anim = player.GetComponent<Animator>();
        if (anim != null)
        {
            anim.runtimeAnimatorController = Resources.Load<AnimatorController>($"Animations/Hero/{activeHero.name}/World/{activeHero.name}_world");
            anim.avatar = Resources.Load<Avatar>($"Animations/Hero/{activeHero.name}/World/avatar");
        }
    }

    private void LoadData()
    {
        GameData dataToLoad = SaveManager.loadedData[0];

        //------------SCENE------------    
        if (player != null)
        {
            Animator anim = player.GetComponent<Animator>();
            if (anim != null) anim.enabled = false;

            player.transform.position = new Vector3(dataToLoad.playerPosition[0], dataToLoad.playerPosition[1], dataToLoad.playerPosition[2]);
            player.transform.eulerAngles = new Vector3(0, dataToLoad.playerRotation[1], 0);
        }

        //----------GAMEMANAGER--------
        totalPlayed = dataToLoad.totalTime;
        totalGold = dataToLoad.totalGold;

        //------------PARTY----------
        party.Clear();
        foreach (string heroName in dataToLoad.party)
        {
            GameObject heroObj = GameObject.Find(heroName);
            if (heroObj != null) party.Add(heroObj);
        }

        if (party.Count > 0) activeHero = party[0];
        changeModelHero();

        //--Hero settings---
        for (int i = 0; i < party.Count; i++)
        {
            FighterStats stats = party[i].GetComponent<FighterStats>();
            if (stats != null)
            {
                stats.CharacterStats = dataToLoad.heroStats[i];
                stats.Level = dataToLoad.heroLevelAndXp[i][0];
                stats.Xp = dataToLoad.heroLevelAndXp[i][1];
                stats.NextLevelXp = dataToLoad.heroLevelAndXp[i][2];
                stats.CurrHp = dataToLoad.heroCurrHpPp[i][0];
                stats.CurrPp = dataToLoad.heroCurrHpPp[i][1];

                //---Psynergies---
                stats.Psynergies = dataToLoad.heroPsynergy[i];
            }

            HeroEquipment equipment = party[i].GetComponent<HeroEquipment>();
            if (equipment != null)
            {
                //---Equipment---
                equipment.EquippedItems = dataToLoad.heroEquippedGear[i];

                // Get equipped gear icons
                foreach (Equipable equipPiece in equipment.EquippedItems)
                {
                    if (equipPiece != null && ItemDatabase.Instance != null)
                    {
                        equipPiece.Icon = ItemDatabase.Instance.GetItem(equipPiece.ItemSlug).Icon;
                    }
                }
            }
        }

        // Psynergy Icons
        party.ForEach(x => x.GetComponent<FighterStats>()?.Psynergies.ForEach(p => p.SetElementIcon()));

        //---Psynergies shortcuts---
        if (PsynergyWorldController.Instance != null)
        {
            PsynergyWorldController.Instance.Initialize();

            foreach (KeyValuePair<string, Psynergy[]> heroShortcut in dataToLoad.heroShortcuts)
            {
                PsynergyWorldController.Instance.HeroShortcuts.Add(GameObject.Find(heroShortcut.Key), heroShortcut.Value);
            }
            PsynergyWorldController.Instance.ResfreshShortcutsUI(activeHero);
        }

        //---------INVENTORY---------
        List<KeyValuePair<Item, int>> itemsList = dataToLoad.playerItems;
        if (InventoryController.Instance != null && itemsList != null)
        {
            foreach (KeyValuePair<Item, int> pair in itemsList)
            {
                InventoryController.Instance.AddItem(pair.Key.ItemSlug, pair.Value);
            }
        }

        if (player != null)
        {
            Animator anim = player.GetComponent<Animator>();
            if (anim != null) anim.enabled = true;
        }

        loadingData = false;

        //----FINISH LOAD-----
        SaveManager.loadedData = null;
    }

    void Update()
    {
        if (currentState != GameState.TitleScreen)
        {
            TotalPlayed += Time.deltaTime;
        }
    }
}