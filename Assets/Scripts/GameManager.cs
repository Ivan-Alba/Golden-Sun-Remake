using Cinemachine;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        None,
        TitleScreen,
        WorldExploration,
        InBattle,
        InMenu,
        InDialogue,
        Interacting,
        Cutscene,
        Loading
    }

    private static GameManager instance = null;

    [Header("Game State System")]
    [SerializeField] private GameState currentState = GameState.None;

    [Header("Persistent References")]
    [SerializeField] private GameObject playerMainCamera;
    [SerializeField] private CinemachineFreeLook freeLookCamera;

    private bool loadingData = false;

    // SCENE & SPAWN MANAGEMENT
    private string pendingSpawnID = null;

    // PARTY INFO
    private List<GameObject> party = new List<GameObject>();

    // GAME INFO
    private int totalGold;
    private float totalPlayed;

    // Getters and setters
    public GameState CurrentState { get => currentState; private set => currentState = value; }
    public int TotalGold { get => totalGold; set => totalGold = value; }
    public float TotalPlayed { get => totalPlayed; set => totalPlayed = value; }

    private GameObject activeHero;

    public GameObject ActiveHero { get => activeHero; set => activeHero = value; }

    public bool LoadingData { get => loadingData; set => loadingData = value; }
    public static GameManager Instance { get => instance; set => instance = value; }

    public List<GameObject> GetParty()
    {
        return party;
    }

    #region Events

    /// <summary>
    /// Event triggered whenever the global GameState changes.
    /// </summary>
    public event System.Action<GameState> OnGameStateChanged;

    #endregion

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

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
        if (currentState == newState) return;

        Debug.Log("Game State changed from " + currentState + " to " + newState);
        currentState = newState;


        if (currentState != GameState.InMenu && MenuController.Instance != null)
        {
            MenuController.Instance.CloseMenu();
        }

        switch (currentState)
        {
            case GameState.TitleScreen:
                ToggleGameplaySystems(false);
                break;

            case GameState.WorldExploration:
                ToggleGameplaySystems(true);
                break;

            case GameState.InBattle:
                ToggleGameplaySystems(false);
                break;

            case GameState.InMenu:
            case GameState.Interacting:
            case GameState.InDialogue:
            case GameState.Cutscene:
            case GameState.Loading:
                // Camera orbit is paused; InputManager handles switching context maps
                ToggleCameraInput(false);
                break;
        }

        OnGameStateChanged?.Invoke(currentState);

    }

    private void ToggleGameplaySystems(bool active)
    {
        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.gameObject.SetActive(active);
        }

        ToggleCameraInput(active);

        if (playerMainCamera != null)
        {
            playerMainCamera.SetActive(active);
        }
    }

    private float originalXSpeed = 300f;
    private float originalYSpeed = 2f;
    private bool speedsCached = false;

    private void ToggleCameraInput(bool active)
    {
        if (freeLookCamera == null && playerMainCamera != null)
        {
            freeLookCamera = playerMainCamera.GetComponentInChildren<CinemachineFreeLook>();
        }

        if (freeLookCamera != null)
        {
            if (!speedsCached)
            {
                originalXSpeed = freeLookCamera.m_XAxis.m_MaxSpeed;
                originalYSpeed = freeLookCamera.m_YAxis.m_MaxSpeed;
                speedsCached = true;
            }

            if (active)
            {
                freeLookCamera.m_XAxis.m_MaxSpeed = originalXSpeed;
                freeLookCamera.m_YAxis.m_MaxSpeed = originalYSpeed;
            }
            else
            {
                freeLookCamera.m_XAxis.m_MaxSpeed = 0f;
                freeLookCamera.m_YAxis.m_MaxSpeed = 0f;

                freeLookCamera.m_XAxis.m_InputAxisValue = 0f;
                freeLookCamera.m_YAxis.m_InputAxisValue = 0f;
            }
        }
    }

    #endregion

    #region New Game Flow

    public void StartNewGame(string firstSceneName, string initialSpawnID)
    {
        totalGold = 0;
        totalPlayed = 0f;
        party.Clear();

        if (InventoryController.Instance != null)
        {
            InventoryController.Instance.clearInventory();
        }

        GameObject isaac = GameObject.Find("Isaac");
        GameObject garet = GameObject.Find("Garet");

        if (isaac != null) party.Add(isaac);
        if (garet != null) party.Add(garet);

        if (party.Count > 0)
        {
            activeHero = party[0];

            /*for (int i = 0; i < party.Count; i++)
            {
                FighterStats stats = party[i].GetComponent<FighterStats>();
                if (stats != null)
                {
                    stats.LevelUp();
                    stats.LevelUp();
                    stats.LevelUp();
                }
            }*/

            changeModelHero();
        }

        if (PsynergyWorldController.Instance != null)
        {
            PsynergyWorldController.Instance.Initialize();
            PsynergyWorldController.Instance.SetShortcut(party[0], PsynergyDatabase.Instance.GetPsynergy("catch"), 0);
            Debug.Log(PsynergyDatabase.Instance.GetPsynergy("catch").Name + " set to shortcut 0");
        }

        ChangeScene(firstSceneName, initialSpawnID);
    }

    #endregion

    public void ChangeScene(string sceneName, string spawnID)
    {
        SetGameState(GameState.Loading);

        pendingSpawnID = spawnID;

        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.LoadSceneWithTransition(sceneName);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    private void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"Level Loaded: {scene.name}");

        if (scene.name.Equals("Title") || scene.name.Equals("MainMenu"))
        {
            SetGameState(GameState.TitleScreen);
            return;
        }

        if (SaveManager.loadedData != null)
        {
            loadingData = true;
            LoadData();
            pendingSpawnID = null;
        }
        else if (!string.IsNullOrEmpty(pendingSpawnID))
        {
            RelocatePlayerToSpawnPoint();
        }

        SetGameState(GameState.WorldExploration);
    }

    private void RelocatePlayerToSpawnPoint()
    {
        if (PlayerController.Instance == null) return;

        SpawnPoint[] spawnPoints = FindObjectsOfType<SpawnPoint>();

        foreach (SpawnPoint sp in spawnPoints)
        {
            if (sp.SpawnPointID == pendingSpawnID)
            {
                Transform playerTransform = PlayerController.Instance.transform;
                Vector3 previousPosition = playerTransform.position;

                CharacterController cc = playerTransform.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                playerTransform.position = sp.transform.position;
                playerTransform.rotation = sp.transform.rotation;

                ResetCameraBehindPlayer(playerTransform, sp.transform.rotation, previousPosition);

                if (cc != null) cc.enabled = true;

                break;
            }
        }

        pendingSpawnID = null;
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
            activeHero = party[index];

            if (PsynergyWorldController.Instance != null)
            {
                PsynergyWorldController.Instance.ResfreshShortcutsUI(activeHero);
            }

            changeModelHero();
        }
    }

    private void changeModelHero()
    {
        if (PlayerController.Instance == null || activeHero == null) return;

        Transform modelTrans = PlayerController.Instance.transform.Find("Model");
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

        Animator anim = PlayerController.Instance.GetComponent<Animator>();
        if (anim != null)
        {
            anim.runtimeAnimatorController = Resources.Load<AnimatorController>($"Animations/Hero/{activeHero.name}/World/{activeHero.name}_world");
            anim.avatar = Resources.Load<Avatar>($"Animations/Hero/{activeHero.name}/World/avatar");
        }
    }

    private void LoadData()
    {
        if (SaveManager.loadedData == null) return;

        GameData dataToLoad = SaveManager.loadedData[0];

        // ---- PLAYER POSITION & ROTATION ----
        if (PlayerController.Instance != null)
        {
            Transform playerTransform = PlayerController.Instance.transform;
            Vector3 previousPosition = playerTransform.position;

            Animator anim = playerTransform.GetComponent<Animator>();
            if (anim != null) anim.enabled = false;

            CharacterController cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            Vector3 targetPos = new Vector3(dataToLoad.playerPosition[0], dataToLoad.playerPosition[1], dataToLoad.playerPosition[2]);
            Quaternion targetRot = Quaternion.Euler(0, dataToLoad.playerRotation[1], 0);

            playerTransform.position = targetPos;
            playerTransform.rotation = targetRot;

            ResetCameraBehindPlayer(playerTransform, targetRot, previousPosition);

            if (cc != null) cc.enabled = true;
        }

        totalPlayed = dataToLoad.totalTime;
        totalGold = dataToLoad.totalGold;

        // ---- RECONSTRUCT PARTY ----
        party.Clear();
        foreach (string heroName in dataToLoad.party)
        {
            GameObject heroObj = GameObject.Find(heroName);
            if (heroObj != null) party.Add(heroObj);
        }

        if (party.Count > 0) activeHero = party[0];
        changeModelHero();

        // ---- RESTORE HERO STATS & EQUIPMENT ----
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i].TryGetComponent<HeroStats>(out var heroStats))
            {
                // Restore Base Values for each Stat
                if (dataToLoad.heroStats != null && i < dataToLoad.heroStats.Count)
                {
                    foreach (Stat savedStat in dataToLoad.heroStats[i])
                    {
                        Stat currentStat = heroStats.GetStat(savedStat.StatType);
                        if (currentStat != null)
                        {
                            currentStat.BaseValue = savedStat.BaseValue;
                        }
                    }
                }

                // Restore Level and XP
                if (dataToLoad.heroLevelAndXp != null && i < dataToLoad.heroLevelAndXp.Count)
                {
                    int savedLevel = dataToLoad.heroLevelAndXp[i][0];
                    int savedXp = dataToLoad.heroLevelAndXp[i][1];
                    heroStats.LoadProgression(savedLevel, savedXp);
                }

                // Restore Current HP and PP
                if (dataToLoad.heroCurrHpPp != null && i < dataToLoad.heroCurrHpPp.Count)
                {
                    heroStats.CurrHp = dataToLoad.heroCurrHpPp[i][0];
                    heroStats.CurrPp = dataToLoad.heroCurrHpPp[i][1];
                }

                // Restore Psynergies
                if (dataToLoad.heroPsynergy != null && i < dataToLoad.heroPsynergy.Count)
                {
                    heroStats.Psynergies = dataToLoad.heroPsynergy[i];
                }
            }

            // Restore Equipment
            if (party[i].TryGetComponent<HeroEquipment>(out var equipment))
            {
                if (dataToLoad.heroEquippedGear != null && i < dataToLoad.heroEquippedGear.Count)
                {
                    equipment.EquippedItems = dataToLoad.heroEquippedGear[i];

                    foreach (Equipable equipPiece in equipment.EquippedItems)
                    {
                        if (equipPiece != null && ItemDatabase.Instance != null)
                        {
                            equipPiece.Icon = ItemDatabase.Instance.GetItem(equipPiece.ItemSlug)?.Icon;
                        }
                    }
                }
            }
        }

        // Refresh Psynergy Icons
        party.ForEach(x => x.GetComponent<FighterStats>()?.Psynergies?.ForEach(p => p.SetElementIcon()));

        // ---- SHORTCUTS ----
        if (PsynergyWorldController.Instance != null)
        {
            PsynergyWorldController.Instance.Initialize();

            if (dataToLoad.heroShortcuts != null)
            {
                foreach (KeyValuePair<string, Psynergy[]> heroShortcut in dataToLoad.heroShortcuts)
                {
                    GameObject heroObj = GameObject.Find(heroShortcut.Key);

                    if (heroObj != null && PsynergyWorldController.Instance.HeroShortcuts.ContainsKey(heroObj))
                    {
                        PsynergyWorldController.Instance.HeroShortcuts[heroObj] = heroShortcut.Value;
                    }
                }
            }

            PsynergyWorldController.Instance.ResfreshShortcutsUI(activeHero);
        }

        // ---- INVENTORY ----
        List<KeyValuePair<Item, int>> itemsList = dataToLoad.playerItems;
        if (InventoryController.Instance != null)
        {
            InventoryController.Instance.clearInventory();

            if (itemsList != null)
            {
                foreach (KeyValuePair<Item, int> pair in itemsList)
                {
                    InventoryController.Instance.AddItem(pair.Key.ItemSlug, pair.Value);
                }
            }
        }

        // Re-enable Player Animator
        if (PlayerController.Instance != null)
        {
            Animator anim = PlayerController.Instance.GetComponent<Animator>();
            if (anim != null) anim.enabled = true;
        }

        loadingData = false;
        SaveManager.loadedData = null;
    }

    private void ResetCameraBehindPlayer(Transform targetTransform, Quaternion targetRotation, Vector3 previousPosition)
    {
        if (freeLookCamera == null && playerMainCamera != null)
        {
            freeLookCamera = playerMainCamera.GetComponentInChildren<CinemachineFreeLook>();
        }

        if (freeLookCamera != null)
        {
            Vector3 deltaPosition = targetTransform.position - previousPosition;
            freeLookCamera.OnTargetObjectWarped(targetTransform, deltaPosition);

            freeLookCamera.m_XAxis.Value = targetRotation.eulerAngles.y;
            freeLookCamera.m_YAxis.Value = 0.5f;
        }
    }

    private void Update()
    {
        if (currentState != GameState.TitleScreen)
        {
            TotalPlayed += Time.deltaTime;
        }
    }
}