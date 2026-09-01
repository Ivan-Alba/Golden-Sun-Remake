using Cinemachine;
using System;
using System.Collections;
using System.Collections.Generic;
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

    #region Serialized Fields

    [Header("Game State System")]
    [SerializeField] private GameState currentState = GameState.None;

    [Header("Persistent References")]
    [SerializeField] private GameObject playerMainCamera;
    [SerializeField] private CinemachineFreeLook freeLookCamera;

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

    // GAME INFO
    private int totalGold;
    private float totalPlayed;
    private GameObject activeHero;

    private float originalXSpeed = 300f;
    private float originalYSpeed = 2f;
    private bool speedsCached = false;

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

        if (currentState != GameState.InMenu && MenuController.Instance != null)
        {
            MenuController.Instance.CloseMenu();
        }

        switch (currentState)
        {
            case GameState.TitleScreen:
                ToggleGameplaySystems(active: false);
                break;

            case GameState.WorldExploration:
                ToggleGameplaySystems(active: true);
                break;

            case GameState.InBattle:
                ToggleGameplaySystems(active: false);
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

    /// <summary>
    /// Enables or disables gameplay features cleanly, isolating camera rendering for Title scene.
    /// </summary>
    private void ToggleGameplaySystems(bool active)
    {
        // 1. Manage Player components and mesh visibility
        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.enabled = active;

            CharacterController cc = PlayerController.Instance.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = active;

            Transform modelTrans = PlayerController.Instance.transform.Find("Model");
            if (modelTrans != null)
            {
                modelTrans.gameObject.SetActive(active);
            }
        }

        // 2. Control Camera Inputs and rendering layer
        ToggleCameraInput(active);

        if (playerMainCamera != null)
        {
            bool isTitle = (currentState == GameState.TitleScreen);

            // Hide/Disable persistent camera on Title to allow Title's camera complete control
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

            changeModelHero();
        }

        GameEvents.Inventory.RequestSyncWithParty();

        if (PsynergyWorldController.Instance != null)
        {
            PsynergyWorldController.Instance.Initialize();
        }

        NotifyPartyChanged();

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

        NotifyPartyChanged();

        StartCoroutine(Routine_HandlePostLoadSequence());
    }

    private IEnumerator Routine_HandlePostLoadSequence()
    {
        // 1. Enable visual components while fully covered by curtain
        if (PlayerController.Instance != null)
        {
            Transform modelTrans = PlayerController.Instance.transform.Find("Model");
            if (modelTrans != null)
            {
                modelTrans.gameObject.SetActive(true);
            }
        }

        if (playerMainCamera != null)
        {
            playerMainCamera.SetActive(true);
            CinemachineBrain brain = playerMainCamera.GetComponent<CinemachineBrain>();
            if (brain != null) brain.enabled = true;
        }

        // 2. Wait 1-2 frames so Cinemachine and physics settle position in the dark
        yield return new WaitForEndOfFrame();
        yield return new WaitForFixedUpdate();

        // 3. Start revealing the screen (Curtain begins fade out)
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

        // 5. Activate target game state (revealing Exploration HUD and returning control immediately)
        SetGameState(pendingTargetState);
    }

    #endregion

    #region Party & Hero Management

    public void NotifyPartyChanged()
    {
        OnPartyChanged?.Invoke();
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

            if (PsynergyWorldController.Instance != null)
            {
                PsynergyWorldController.Instance.NotifyShortcutsChanged(activeHero);
            }

            changeModelHero();
            NotifyPartyChanged();
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

    /// <summary>
    /// Relocates player to spawn point, updates zone info and snaps position to ground using Raycast.
    /// </summary>
    private void RelocatePlayerToSpawnPoint()
    {
        if (PlayerController.Instance == null) return;

        SpawnPoint[] spawnPoints = FindObjectsOfType<SpawnPoint>();
        currentUsedSpawnPoint = null;

        foreach (SpawnPoint sp in spawnPoints)
        {
            if (sp.SpawnPointID == pendingSpawnID)
            {
                currentUsedSpawnPoint = sp;
                UpdateCurrentZone(sp.ZoneName, sp.RegionName);

                Transform playerTransform = PlayerController.Instance.transform;
                Vector3 previousPosition = playerTransform.position;

                CharacterController cc = playerTransform.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                // Safety ground snap raycast
                Vector3 spawnPos = sp.transform.position;
                if (Physics.Raycast(spawnPos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 5f))
                {
                    spawnPos.y = hit.point.y;
                }

                playerTransform.position = spawnPos;
                playerTransform.rotation = sp.transform.rotation;

                if (cc != null) cc.enabled = true;

                ResetCameraBehindPlayer(playerTransform, sp.transform.rotation, previousPosition);

                break;
            }
        }

        pendingSpawnID = null;
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

    #endregion

    #region Save & Load System

    private void LoadData()
    {
        if (SaveManager.loadedData == null) return;

        GameData dataToLoad = SaveManager.loadedData[0];

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

            // Ground snap raycast for saved position
            if (Physics.Raycast(targetPos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 5f))
            {
                targetPos.y = hit.point.y;
            }

            playerTransform.position = targetPos;
            playerTransform.rotation = targetRot;

            ResetCameraBehindPlayer(playerTransform, targetRot, previousPosition);
        }

        totalPlayed = dataToLoad.totalTime;
        totalGold = dataToLoad.totalGold;

        party.Clear();

        GameEvents.Inventory.RequestClearAllInventories();

        foreach (string heroName in dataToLoad.party)
        {
            GameObject heroObj = GameObject.Find(heroName);
            if (heroObj != null)
            {
                party.Add(heroObj);
            }
        }

        if (party.Count > 0) activeHero = party[0];
        changeModelHero();

        GameEvents.Inventory.RequestSyncWithParty();

        if (GlobalBag.Instance != null && dataToLoad.globalBagItems != null)
        {
            for (int i = GlobalBag.Instance.KeyItemSlots.Count - 1; i >= 0; i--)
            {
                var slot = GlobalBag.Instance.KeyItemSlots[i];
                if (!slot.IsEmpty) GlobalBag.Instance.RemoveKeyItem(slot.Item.ItemSlug, slot.Quantity);
            }

            foreach (var savedItem in dataToLoad.globalBagItems)
            {
                if (string.IsNullOrEmpty(savedItem.itemSlug)) continue;
                ItemSO item = ItemDatabase.Instance.GetItem(savedItem.itemSlug);
                if (item is KeyItemSO keyItem)
                {
                    GlobalBag.Instance.AddKeyItem(keyItem, savedItem.quantity);
                }
            }
        }

        for (int i = 0; i < party.Count; i++)
        {
            GameObject heroObj = party[i];
            string currentHeroName = heroObj.name;

            if (heroObj.TryGetComponent<HeroStats>(out var heroStats))
            {
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
                        foreach (string slug in savedSlugs)
                        {
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
                if (savedInvData != null)
                {
                    for (int slotIdx = 0; slotIdx < savedInvData.slots.Count && slotIdx < heroInventory.Slots.Count; slotIdx++)
                    {
                        var savedSlotData = savedInvData.slots[slotIdx];
                        if (!string.IsNullOrEmpty(savedSlotData.itemSlug))
                        {
                            ItemSO item = ItemDatabase.Instance.GetItem(savedSlotData.itemSlug);
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
                    foreach (var eqSlotData in eqData.standardEquipment)
                    {
                        if (string.IsNullOrEmpty(eqSlotData.itemSlug)) continue;
                        ItemSO item = ItemDatabase.Instance.GetItem(eqSlotData.itemSlug);
                        if (item is EquipableSO equipable)
                        {
                            heroEquipment.LoadEquippedItem(equipable);
                        }
                    }

                    for (int relicIdx = 0; relicIdx < eqData.relicSlugs.Count; relicIdx++)
                    {
                        string relicSlug = eqData.relicSlugs[relicIdx];
                        if (string.IsNullOrEmpty(relicSlug)) continue;
                        ItemSO item = ItemDatabase.Instance.GetItem(relicSlug);
                        if (item is PsynergyRelicSO relic)
                        {
                            heroEquipment.LoadEquippedRelic(relic, relicIdx);
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

        if (PsynergyWorldController.Instance != null)
        {
            PsynergyWorldController.Instance.Initialize();

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
                            PsynergyWorldController.Instance.SetShortcut(heroObj, psynergy, slotIdx);
                        }
                    }
                }
            }

            if (activeHero != null)
            {
                PsynergyWorldController.Instance.NotifyShortcutsChanged(activeHero);
            }
        }

        if (PlayerController.Instance != null)
        {
            Animator anim = PlayerController.Instance.GetComponent<Animator>();
            if (anim != null) anim.enabled = true;
        }

        loadingData = false;
        SaveManager.loadedData = null;

        NotifyPartyChanged();
    }

    #endregion
}