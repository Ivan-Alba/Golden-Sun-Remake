using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

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

    #region Serialized Fields

    [Header("Game State System")]
    [SerializeField] private GameState currentState = GameState.None;

    [Header("Persistent References")]
    [SerializeField] private GameObject playerMainCamera;
    [SerializeField] private CinemachineCamera freeLookCamera;

    #endregion

    #region Private Fields

    private bool loadingData = false;

    // SCENE & SPAWN MANAGEMENT
    private string pendingSpawnID = null;
    private GameState pendingTargetState = GameState.WorldExploration;
    private SpawnPoint currentUsedSpawnPoint = null;

    // ZONE & AREA TRACKING
    private string currentZoneName = "DESCONOCIDO";
    private string currentRegionName = "MUNDO";

    // PARTY INFO
    private List<GameObject> party = new List<GameObject>();

    // GAME INFO & FLAGS
    private int totalGold;
    private float totalPlayed;
    private GameObject activeHero;
    private readonly Dictionary<string, bool> gameFlags = new Dictionary<string, bool>();

    #endregion

    #region Properties

    public GameState CurrentState { get => currentState; private set => currentState = value; }
    public int TotalGold { get => totalGold; set => totalGold = value; }
    public float TotalPlayed { get => totalPlayed; set => totalPlayed = value; }
    public GameObject ActiveHero { get => activeHero; set => activeHero = value; }
    public bool LoadingData { get => loadingData; set => loadingData = value; }
    public string CurrentZoneName => currentZoneName;
    public string CurrentRegionName => currentRegionName;
    public static GameManager Instance { get => instance; set => instance = value; }

    public List<GameObject> GetParty()
    {
        return party;
    }

    #endregion

    #region Events

    public event Action OnPartyChanged;

    #endregion

    #region Unity Lifecycle & Initialization

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
        GameEvents.GameState.OnChangeGameStateRequested += HandleGameStateRequest;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnLevelFinishedLoading;
        GameEvents.GameState.OnChangeGameStateRequested -= HandleGameStateRequest;
    }

    private void Update()
    {
        if (currentState != GameState.TitleScreen && currentState != GameState.Loading)
        {
            TotalPlayed += Time.deltaTime;
        }
    }

    #endregion

    #region Game Flags System

    /// <summary>
    /// Checks if a specific story/game flag is set to active.
    /// </summary>
    public bool HasFlag(string flagKey)
    {
        if (string.IsNullOrEmpty(flagKey)) return false;
        return gameFlags.TryGetValue(flagKey, out bool value) && value;
    }

    /// <summary>
    /// Sets the value of a story/game flag.
    /// </summary>
    public void SetFlag(string flagKey, bool value)
    {
        if (string.IsNullOrEmpty(flagKey)) return;

        gameFlags[flagKey] = value;
        Debug.Log($"[GameManager] GameFlag updated: '{flagKey}' => {value}");
    }

    /// <summary>
    /// Removes a flag completely from memory.
    /// </summary>
    public void ClearFlag(string flagKey)
    {
        if (string.IsNullOrEmpty(flagKey)) return;

        if (gameFlags.ContainsKey(flagKey))
        {
            gameFlags.Remove(flagKey);
            Debug.Log($"[GameManager] GameFlag cleared: '{flagKey}'");
        }
    }

    #endregion

    #region State Machine Management

    private void HandleGameStateRequest(GameState requestedState)
    {
        SetGameState(requestedState);
    }

    public void SetGameState(GameState newState)
    {
        if (currentState == newState) return;

        GameState previousState = currentState;
        Debug.Log($"[GameManager] Game State changed from {previousState} to {newState}");
        currentState = newState;

        switch (currentState)
        {
            case GameState.TitleScreen:
            case GameState.InBattle:
                ToggleGameplaySystems(active: false);
                break;

            case GameState.WorldExploration:
                ToggleGameplaySystems(active: true);
                break;

            case GameState.InMenu:
            case GameState.Interacting:
            case GameState.InDialogue:
            case GameState.Cutscene:
            case GameState.Loading:
                ToggleCameraInput(false);
                break;
        }

        GameEvents.GameState.TriggerGameStateChanged(previousState, currentState);
    }

    private void ToggleGameplaySystems(bool active)
    {
        ToggleCameraInput(active);

        if (playerMainCamera != null)
        {
            bool isTitle = (currentState == GameState.TitleScreen);

            playerMainCamera.SetActive(!isTitle);

            CinemachineBrain brain = playerMainCamera.GetComponent<CinemachineBrain>();
            if (brain != null)
            {
                brain.enabled = !isTitle;
            }
        }
    }

    private void ToggleCameraInput(bool active)
    {
        if (freeLookCamera == null && playerMainCamera != null)
        {
            freeLookCamera = playerMainCamera.GetComponentInChildren<CinemachineCamera>();
        }

        if (freeLookCamera != null)
        {
            if (freeLookCamera.TryGetComponent<CinemachineInputAxisController>(out var inputController))
            {
                inputController.enabled = active;
            }
        }
    }

    #endregion

    #region Zone & Area Management

    public void UpdateCurrentZone(string zoneName, string regionName)
    {
        currentZoneName = zoneName;
        currentRegionName = regionName;
    }

    #endregion

    #region New Game & Scene Flow

    public void StartNewGame(string firstSceneName, string initialSpawnID)
    {
        totalGold = 0;
        totalPlayed = 0f;
        party.Clear();
        gameFlags.Clear();

        GameEvents.Inventory.RequestClearAllInventories();

        GameObject isaac = GameObject.Find("Isaac");
        GameObject garet = GameObject.Find("Garet");

        if (isaac != null) party.Add(isaac);
        if (garet != null) party.Add(garet);

        if (party.Count > 0)
        {
            activeHero = party[0];

            for (int i = 0; i < party.Count; i++)
            {
                HeroStats stats = party[i].GetComponent<HeroStats>();
                if (stats != null)
                {
                    for (int l = 0; l < 7; l++) stats.LevelUp();
                }
            }
        }

        GameEvents.Inventory.RequestSyncWithParty();

        NotifyPartyChanged(activeHero);

        ChangeScene(firstSceneName, initialSpawnID, GameState.WorldExploration);
    }

    public void ChangeScene(string sceneName, string spawnID, GameState targetStateAfterLoad = GameState.WorldExploration)
    {
        SetGameState(GameState.Loading);
        pendingSpawnID = spawnID;
        pendingTargetState = targetStateAfterLoad;

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
        Debug.Log($"[GameManager] Level Loaded in background: {scene.name}");

        if (scene.name.Equals("Title") || scene.name.Equals("MainMenu"))
        {
            SetGameState(GameState.TitleScreen);
            return;
        }

        SetGameState(GameState.Loading);

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

        NotifyPartyChanged(activeHero);

        StartCoroutine(Routine_HandlePostLoadSequence());
    }

    private IEnumerator Routine_HandlePostLoadSequence()
    {
        // 1. Reveal Player Mesh and trigger Hero Swap while screen is still black
        GameEvents.Player.RequestSetPlayerVisibility(true);
        if (activeHero != null)
        {
            GameEvents.Player.RequestActiveHeroChange(activeHero);
        }

        if (playerMainCamera != null)
        {
            playerMainCamera.SetActive(true);
            CinemachineBrain brain = playerMainCamera.GetComponent<CinemachineBrain>();
            if (brain != null) brain.enabled = true;
        }

        // 2. Wait frames so Cinemachine and physics settle position in the dark
        yield return new WaitForEndOfFrame();
        yield return new WaitForFixedUpdate();

        // 3. Start revealing the screen
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.FadeInNewScene();
        }

        // 4. Handle Arrival vs Immediate Entry
        bool isGameplayTeleport = !loadingData && currentUsedSpawnPoint != null;

        if (pendingTargetState == GameState.WorldExploration && isGameplayTeleport && currentUsedSpawnPoint.AutoWalkOnArrival)
        {
            SetGameState(GameState.Cutscene);

            float walkDelay = SceneTransitionManager.Instance != null
                ? SceneTransitionManager.Instance.ArrivalWalkDelay
                : 0.2f;

            yield return new WaitForSeconds(walkDelay);

            yield return StartCoroutine(currentUsedSpawnPoint.Routine_ExecuteArrivalSequence());
        }
        else if (pendingTargetState == GameState.WorldExploration)
        {
            GameEvents.UI.TriggerShowZoneBanner(currentZoneName, currentRegionName, 3.5f);

            if (SceneTransitionManager.Instance != null)
            {
                yield return new WaitForSeconds(SceneTransitionManager.Instance.TransitionDuration);
            }
        }

        // 5. Activate target game state (WorldExploration)
        SetGameState(pendingTargetState);
    }

    #endregion

    #region Party & Hero Management

    public void NotifyPartyChanged(GameObject activeHero = null)
    {
        // 1. Notificamos que la composición de la party ha cambiado
        OnPartyChanged?.Invoke();

        // 2. Si no hay party, no se puede activar ningún héroe
        if (party == null || party.Count == 0) return;

        // 3. Seleccionamos el héroe: el pasado por parámetro (si está en la party) o el primero de la lista
        GameObject heroToActivate = (activeHero != null && party.Contains(activeHero))
            ? activeHero
            : party[0];

        // 4. Notificamos al sistema global cuál es el héroe activo
        GameEvents.Player.RequestActiveHeroChange(heroToActivate);
    }

    public void ChangeGameSpeed()
    {
        Time.timeScale = (Time.timeScale == 2f) ? 1f : 2f;
        Debug.Log($"[GameManager] SpeedChanged to {Time.timeScale}");
    }

    public void ChangeActiveHero(int index)
    {
        if (party.Count > index)
        {
            activeHero = party[index];

            GameEvents.Player.RequestActiveHeroChange(activeHero);
            NotifyPartyChanged(activeHero);
        }
    }

    private void RelocatePlayerToSpawnPoint()
    {
        if (string.IsNullOrEmpty(pendingSpawnID)) return;

        currentUsedSpawnPoint = null;

        if (SpawnPoint.AllSpawnPoints.TryGetValue(pendingSpawnID, out SpawnPoint sp))
        {
            currentUsedSpawnPoint = sp;
            UpdateCurrentZone(sp.ZoneName, sp.RegionName);

            Vector3 spawnPos = sp.transform.position;
            if (Physics.Raycast(spawnPos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 5f))
            {
                spawnPos.y = hit.point.y;
            }

            GameEvents.Player.RequestTeleport(spawnPos, sp.transform.rotation);
            ResetCameraBehindPlayer(spawnPos, sp.transform.rotation);
        }

        pendingSpawnID = null;
    }

    private void ResetCameraBehindPlayer(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (freeLookCamera == null && playerMainCamera != null)
        {
            freeLookCamera = playerMainCamera.GetComponentInChildren<CinemachineCamera>();
        }

        if (freeLookCamera != null)
        {
            if (freeLookCamera.TryGetComponent<CinemachineOrbitalFollow>(out var orbitalFollow))
            {
                orbitalFollow.HorizontalAxis.Value = targetRotation.eulerAngles.y;
                orbitalFollow.VerticalAxis.Value = 0.5f;
            }
        }
    }

    #endregion

    #region Save & Load System

    private void LoadData()
    {
        if (SaveManager.loadedData == null) return;

        GameData dataToLoad = SaveManager.loadedData[0];

        Vector3 targetPos = new Vector3(dataToLoad.playerPosition[0], dataToLoad.playerPosition[1], dataToLoad.playerPosition[2]);
        Quaternion targetRot = Quaternion.Euler(0, dataToLoad.playerRotation[1], 0);

        if (Physics.Raycast(targetPos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 5f))
        {
            targetPos.y = hit.point.y;
        }

        GameEvents.Player.RequestTeleport(targetPos, targetRot);
        ResetCameraBehindPlayer(targetPos, targetRot);

        totalPlayed = dataToLoad.totalTime;
        totalGold = dataToLoad.totalGold;

        party.Clear();

        // Clears both Hero Inventories AND GlobalBag safely via decoupled event
        GameEvents.Inventory.RequestClearAllInventories();

        for (int i = 0; i < dataToLoad.party.Count; i++)
        {
            string heroName = dataToLoad.party[i];
            GameObject heroObj = GameObject.Find(heroName);
            if (heroObj != null)
            {
                party.Add(heroObj);
            }
        }

        if (party.Count > 0) activeHero = party[0];
        GameEvents.Player.RequestActiveHeroChange(activeHero);

        GameEvents.Inventory.RequestSyncWithParty();

        // -----------------------------------------------------------------------------
        // Restores Global Key Item Bag via Decoupled Events (No GlobalBag.Instance!)
        // -----------------------------------------------------------------------------
        if (dataToLoad.globalBagItems != null)
        {
            for (int i = 0; i < dataToLoad.globalBagItems.Count; i++)
            {
                var savedItem = dataToLoad.globalBagItems[i];
                if (savedItem == null || string.IsNullOrEmpty(savedItem.itemSlug)) continue;

                ItemSO item = null;
                GameEvents.Database.RequestItemBySlug(savedItem.itemSlug, (res) => item = res);

                if (item is KeyItemSO keyItem)
                {
                    GameEvents.Inventory.RequestAddKeyItem(keyItem, savedItem.quantity, null);
                }
            }
        }

        // Restore individual hero stats, inventories and equipment
        for (int i = 0; i < party.Count; i++)
        {
            GameObject heroObj = party[i];
            string currentHeroName = heroObj.name;

            if (heroObj.TryGetComponent<HeroStats>(out var heroStats))
            {
                if (dataToLoad.heroStats != null && i < dataToLoad.heroStats.Count)
                {
                    var savedStats = dataToLoad.heroStats[i];
                    for (int s = 0; s < savedStats.Count; s++)
                    {
                        Stat savedStat = savedStats[s];
                        Stat currentStat = heroStats.GetStat(savedStat.StatType);
                        if (currentStat != null)
                        {
                            currentStat.BaseValue = savedStat.BaseValue;
                        }
                    }
                }

                if (dataToLoad.heroLevelAndXp != null && i < dataToLoad.heroLevelAndXp.Count)
                {
                    int savedLevel = dataToLoad.heroLevelAndXp[i][0];
                    int savedXp = dataToLoad.heroLevelAndXp[i][1];
                    heroStats.LoadProgression(savedLevel, savedXp);
                }

                if (dataToLoad.heroPsynergySlugs != null && i < dataToLoad.heroPsynergySlugs.Count)
                {
                    List<string> savedSlugs = dataToLoad.heroPsynergySlugs[i];
                    List<PsynergySO> loadedPsynergies = new List<PsynergySO>();

                    if (savedSlugs != null)
                    {
                        for (int p = 0; p < savedSlugs.Count; p++)
                        {
                            string slug = savedSlugs[p];
                            if (string.IsNullOrEmpty(slug)) continue;
                            PsynergySO psynergy = PsynergyDatabase.Instance.GetPsynergy(slug);
                            if (psynergy != null) loadedPsynergies.Add(psynergy);
                        }
                    }

                    heroStats.Psynergies = loadedPsynergies;
                }
            }

            if (heroObj.TryGetComponent<HeroInventory>(out var heroInventory))
            {
                var savedInvData = dataToLoad.heroesInventories?.Find(x => x.heroName == currentHeroName);
                if (savedInvData != null && savedInvData.slots != null)
                {
                    for (int slotIdx = 0; slotIdx < savedInvData.slots.Count && slotIdx < heroInventory.Slots.Count; slotIdx++)
                    {
                        var savedSlotData = savedInvData.slots[slotIdx];
                        if (!string.IsNullOrEmpty(savedSlotData.itemSlug))
                        {
                            ItemSO item = null;
                            GameEvents.Database.RequestItemBySlug(savedSlotData.itemSlug, (res) => item = res);
                            heroInventory.GetSlot(slotIdx).SetSlot(item, savedSlotData.quantity);
                        }
                        else
                        {
                            heroInventory.GetSlot(slotIdx).ClearSlot();
                        }
                    }
                }
            }

            if (heroObj.TryGetComponent<HeroEquipment>(out var heroEquipment))
            {
                var eqData = dataToLoad.heroesEquipment?.Find(x => x.heroName == currentHeroName);
                if (eqData != null)
                {
                    if (eqData.standardEquipment != null)
                    {
                        for (int eqIdx = 0; eqIdx < eqData.standardEquipment.Count; eqIdx++)
                        {
                            var eqSlotData = eqData.standardEquipment[eqIdx];
                            if (string.IsNullOrEmpty(eqSlotData.itemSlug)) continue;

                            ItemSO item = null;
                            GameEvents.Database.RequestItemBySlug(eqSlotData.itemSlug, (res) => item = res);

                            if (item is EquipableSO equipable)
                            {
                                heroEquipment.LoadEquippedItem(equipable);
                            }
                        }
                    }

                    if (eqData.relicSlugs != null)
                    {
                        for (int relicIdx = 0; relicIdx < eqData.relicSlugs.Count; relicIdx++)
                        {
                            string relicSlug = eqData.relicSlugs[relicIdx];
                            if (string.IsNullOrEmpty(relicSlug)) continue;

                            ItemSO item = null;
                            GameEvents.Database.RequestItemBySlug(relicSlug, (res) => item = res);

                            if (item is PsynergyRelicSO relic)
                            {
                                heroEquipment.LoadEquippedRelic(relic, relicIdx);
                            }
                        }
                    }
                }
            }

            if (heroObj.TryGetComponent<HeroStats>(out var heroStatsFinal))
            {
                if (dataToLoad.heroCurrHpPp != null && i < dataToLoad.heroCurrHpPp.Count)
                {
                    heroStatsFinal.CurrHp = dataToLoad.heroCurrHpPp[i][0];
                    heroStatsFinal.CurrPp = dataToLoad.heroCurrHpPp[i][1];
                }
            }
        }

        if (dataToLoad.heroShortcuts != null)
        {
            foreach (KeyValuePair<string, string[]> heroShortcut in dataToLoad.heroShortcuts)
            {
                GameObject heroObj = GameObject.Find(heroShortcut.Key);
                if (heroObj != null && heroShortcut.Value != null)
                {
                    string[] savedSlugs = heroShortcut.Value;
                    for (int slotIdx = 0; slotIdx < savedSlugs.Length && slotIdx < 4; slotIdx++)
                    {
                        string slug = savedSlugs[slotIdx];
                        PsynergySO psynergy = !string.IsNullOrEmpty(slug) ? PsynergyDatabase.Instance.GetPsynergy(slug) : null;
                        GameEvents.Psynergy.RequestAssignShortcut(heroObj, psynergy, slotIdx);
                    }
                }
            }
        }

        loadingData = false;
        SaveManager.loadedData = null;

        NotifyPartyChanged(activeHero);
    }

    #endregion
}