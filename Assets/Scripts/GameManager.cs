using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

/// <summary>
/// Core game manager handling global game states, scene flow, party management, and persistent game metadata.
/// Decoupled via GameEvents (GameState, Scene, SaveLoad, Party, Player, Economy).
/// </summary>
public class GameManager : MonoBehaviour
{
    #region Enumerations

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

    #endregion

    #region Serialized Fields

    [Header("Game State System")]
    [SerializeField] private GameState currentState = GameState.None;

    [Header("Party Hero Database (Persistent Core)")]
    [Tooltip("Serialized references to all available hero GameObjects within [PERSISTENT_CORE].")]
    [SerializeField] private List<GameObject> availableHeroes = new List<GameObject>();

    [Header("Persistent Camera References")]
    [SerializeField] private GameObject playerMainCamera;
    [SerializeField] private CinemachineCamera freeLookCamera;

    [Header("Transition Configuration")]
    [SerializeField] private float transitionDuration = 1.0f;
    [SerializeField] private float arrivalWalkDelay = 0.2f;

    #endregion

    #region Private Fields

    private bool loadingData = false;

    // Scene & Spawn Management
    private string pendingSpawnID = null;
    private GameState pendingTargetState = GameState.WorldExploration;
    private SpawnPoint currentUsedSpawnPoint = null;

    // Zone & Area Tracking
    private string currentZoneName = "DESCONOCIDO";
    private string currentRegionName = "MUNDO";

    // Party Info
    private readonly List<GameObject> activeParty = new List<GameObject>();
    private readonly List<GameObject> reserveParty = new List<GameObject>();
    private GameObject activeLeaderHero;

    // Game Economy & Metrics
    private int totalGold;
    private float totalPlayed;
    private readonly Dictionary<string, bool> gameFlags = new Dictionary<string, bool>();

    #endregion

    #region Unity Lifecycle & Event Subscriptions

    private void Awake()
    {
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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnLevelFinishedLoading;

        // Game State & Zone Event Subscriptions
        GameEvents.GameState.OnChangeGameStateRequested += HandleGameStateRequest;
        GameEvents.GameState.OnRequestPlayTime += HandleRequestPlayTime;
        GameEvents.GameState.OnUpdateZoneRequested += UpdateCurrentZone;

        // Game Flags Event Subscriptions
        GameEvents.GameState.OnSetFlagRequested += HandleSetFlagRequest;
        GameEvents.GameState.OnRequestFlagState += HandleRequestFlagState;

        // Time Management Event Subscriptions
        GameEvents.GameState.OnChangeGameSpeedRequested += HandleChangeGameSpeedRequest;
        GameEvents.GameState.OnToggleGameSpeedRequested += HandleToggleGameSpeedRequest;

        // Party & Economy Event Subscriptions
        GameEvents.Party.OnModifyGoldRequested += HandleModifyGoldRequest;
        GameEvents.Party.OnRequestGold += HandleRequestGold;
        GameEvents.Party.OnActiveHeroIndexChangeRequested += HandleActiveHeroIndexChangeRequest;
        GameEvents.Party.OnActiveHeroChangeRequested += HandleActiveHeroChangeRequest;

        // Scene Transition Event Subscriptions
        GameEvents.Scene.OnStartNewGameRequested += HandleStartNewGameRequest;
        GameEvents.Scene.OnChangeSceneRequested += HandleChangeSceneRequest;

        // Save / Load Event Subscriptions
        GameEvents.SaveLoad.OnCollectSaveData += HandleCollectSaveData;
        GameEvents.SaveLoad.OnRestoreSaveData += HandleRestoreSaveData;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnLevelFinishedLoading;

        // Game State & Zone Event Unsubscriptions
        GameEvents.GameState.OnChangeGameStateRequested -= HandleGameStateRequest;
        GameEvents.GameState.OnRequestPlayTime -= HandleRequestPlayTime;
        GameEvents.GameState.OnUpdateZoneRequested -= UpdateCurrentZone;

        // Game Flags Event Unsubscriptions
        GameEvents.GameState.OnSetFlagRequested -= HandleSetFlagRequest;
        GameEvents.GameState.OnRequestFlagState -= HandleRequestFlagState;

        // Time Management Event Unsubscriptions
        GameEvents.GameState.OnChangeGameSpeedRequested -= HandleChangeGameSpeedRequest;
        GameEvents.GameState.OnToggleGameSpeedRequested -= HandleToggleGameSpeedRequest;

        // Party & Economy Event Unsubscriptions
        GameEvents.Party.OnModifyGoldRequested -= HandleModifyGoldRequest;
        GameEvents.Party.OnRequestGold -= HandleRequestGold;
        GameEvents.Party.OnActiveHeroIndexChangeRequested -= HandleActiveHeroIndexChangeRequest;
        GameEvents.Party.OnActiveHeroChangeRequested -= HandleActiveHeroChangeRequest;

        // Scene Transition Event Unsubscriptions
        GameEvents.Scene.OnStartNewGameRequested -= HandleStartNewGameRequest;
        GameEvents.Scene.OnChangeSceneRequested -= HandleChangeSceneRequest;

        // Save / Load Event Unsubscriptions
        GameEvents.SaveLoad.OnCollectSaveData -= HandleCollectSaveData;
        GameEvents.SaveLoad.OnRestoreSaveData -= HandleRestoreSaveData;
    }

    private void Update()
    {
        if (currentState != GameState.TitleScreen && currentState != GameState.Loading)
        {
            totalPlayed += Time.deltaTime;
        }
    }

    #endregion

    #region Game State & Time Event Handlers

    private void HandleGameStateRequest(GameState requestedState)
    {
        SetGameState(requestedState);
    }

    private void HandleRequestPlayTime(Action<float> callback)
    {
        callback?.Invoke(totalPlayed);
    }

    private void HandleChangeGameSpeedRequest(float targetSpeed)
    {
        float clampedSpeed = Mathf.Clamp(targetSpeed, 0f, 5f);
        Time.timeScale = clampedSpeed;
        Debug.Log($"[GameManager] Game speed set to {Time.timeScale}x");
    }

    private void HandleToggleGameSpeedRequest()
    {
        float newSpeed = (Mathf.Approximately(Time.timeScale, 1f)) ? 2f : 1f;
        HandleChangeGameSpeedRequest(newSpeed);
    }

    private void SetGameState(GameState newState)
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

    #endregion

    #region Game Flags Event Handlers

    private void HandleSetFlagRequest(string flagKey, bool value)
    {
        if (string.IsNullOrEmpty(flagKey)) return;

        gameFlags[flagKey] = value;
        Debug.Log($"[GameManager] GameFlag updated via event: '{flagKey}' => {value}");
    }

    private void HandleRequestFlagState(string flagKey, Action<bool> callback)
    {
        if (string.IsNullOrEmpty(flagKey))
        {
            callback?.Invoke(false);
            return;
        }

        bool state = gameFlags.TryGetValue(flagKey, out bool value) && value;
        callback?.Invoke(state);
    }

    #endregion

    #region Party & Economy Event Handlers

    private void HandleModifyGoldRequest(int amount)
    {
        int newGold = Mathf.Max(0, totalGold + amount);

        if (totalGold != newGold)
        {
            totalGold = newGold;
            GameEvents.Party.TriggerGoldChanged(totalGold);
        }
    }

    private void HandleRequestGold(Action<int> callback)
    {
        callback?.Invoke(totalGold);
    }

    private void HandleActiveHeroIndexChangeRequest(int index)
    {
        if (activeParty != null && index >= 0 && index < activeParty.Count)
        {
            if (activeLeaderHero != activeParty[index])
            {
                activeLeaderHero = activeParty[index];

                GameEvents.Party.TriggerActiveHeroChanged(activeLeaderHero);
                GameEvents.Hero.TriggerHeroStatusChanged();
            }
        }
    }

    private void HandleActiveHeroChangeRequest(GameObject newHero)
    {
        if (newHero != null && activeParty.Contains(newHero) && activeLeaderHero != newHero)
        {
            activeLeaderHero = newHero;

            GameEvents.Party.TriggerActiveHeroChanged(activeLeaderHero);
            GameEvents.Hero.TriggerHeroStatusChanged();
        }
    }

    private void NotifyPartyChanged()
    {
        GameEvents.Party.TriggerPartyChanged(activeParty);
        GameEvents.Party.TriggerReservePartyChanged(reserveParty);
    }

    #endregion

    #region Scene Flow & Zone Event Handlers

    private void HandleStartNewGameRequest(string firstSceneName, string initialSpawnID)
    {
        StartNewGame(firstSceneName, initialSpawnID);
    }

    private void HandleChangeSceneRequest(string sceneName, string spawnID, GameState targetStateAfterLoad = GameState.WorldExploration)
    {
        SetGameState(GameState.Loading);
        pendingSpawnID = spawnID;
        pendingTargetState = targetStateAfterLoad;

        GameEvents.Scene.RequestLoadScene(sceneName);
    }

    private void UpdateCurrentZone(string zoneName, string regionName)
    {
        currentZoneName = zoneName;
        currentRegionName = regionName;
    }

    private void StartNewGame(string firstSceneName, string initialSpawnID)
    {
        totalGold = 0;
        totalPlayed = 0f;
        activeParty.Clear();
        reserveParty.Clear();
        gameFlags.Clear();

        GameEvents.Party.TriggerGoldChanged(totalGold);
        GameEvents.Inventory.RequestClearAllInventories();

        for (int i = 0; i < availableHeroes.Count; i++)
        {
            if (availableHeroes[i] == null) continue;

            string heroName = availableHeroes[i].name;
            if (heroName.Equals("Isaac") || heroName.Equals("Garet"))
            {
                activeParty.Add(availableHeroes[i]);

                if (availableHeroes[i].TryGetComponent<HeroStats>(out var stats))
                {
                    for (int l = 0; l < 7; l++) stats.LevelUp();
                }
            }
        }

        if (activeParty.Count > 0)
        {
            activeLeaderHero = activeParty[0];
        }

        NotifyPartyChanged();
        GameEvents.Party.TriggerActiveHeroChanged(activeLeaderHero);

        HandleChangeSceneRequest(firstSceneName, initialSpawnID, GameState.WorldExploration);
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

        if (!string.IsNullOrEmpty(pendingSpawnID))
        {
            RelocatePlayerToSpawnPoint();
        }

        NotifyPartyChanged();
        if (activeLeaderHero != null)
        {
            GameEvents.Party.TriggerActiveHeroChanged(activeLeaderHero);
        }

        StartCoroutine(Routine_HandlePostLoadSequence());
    }

    private IEnumerator Routine_HandlePostLoadSequence()
    {
        // 1. Reveal Player Mesh and trigger Hero Swap while screen is still black
        GameEvents.Player.RequestSetPlayerVisibility(true);
        if (activeLeaderHero != null)
        {
            GameEvents.Party.TriggerActiveHeroChanged(activeLeaderHero);
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

        // 3. Start revealing the screen via event bus
        GameEvents.Scene.RequestFadeInScene();

        // 4. Handle Arrival vs Immediate Entry
        bool isGameplayTeleport = !loadingData && currentUsedSpawnPoint != null;

        if (pendingTargetState == GameState.WorldExploration && isGameplayTeleport && currentUsedSpawnPoint.AutoWalkOnArrival)
        {
            SetGameState(GameState.Cutscene);

            yield return new WaitForSeconds(arrivalWalkDelay);

            yield return StartCoroutine(currentUsedSpawnPoint.Routine_ExecuteArrivalSequence());
        }
        else if (pendingTargetState == GameState.WorldExploration)
        {
            GameEvents.UI.TriggerShowZoneBanner(currentZoneName, currentRegionName, 3.5f);
            yield return new WaitForSeconds(transitionDuration);
        }

        // 5. Activate target game state (WorldExploration)
        SetGameState(pendingTargetState);
    }

    #endregion

    #region Save & Load Event Handlers

    private void HandleCollectSaveData(GameData saveData)
    {
        if (saveData == null) return;

        // 1. Save Header Meta Info
        saveData.header.totalGold = totalGold;
        saveData.header.totalTime = totalPlayed;
        saveData.header.locationName = currentZoneName;

        saveData.header.partyPreview.Clear();
        for (int i = 0; i < activeParty.Count; i++)
        {
            if (activeParty[i] != null && activeParty[i].TryGetComponent<HeroStats>(out var stats))
            {
                saveData.header.partyPreview.Add(new HeroPreviewData(activeParty[i].name, stats.Level));
            }
        }

        // 2. World & Scene State
        saveData.worldState.currentSceneName = SceneManager.GetActiveScene().name;
        saveData.worldState.currentZoneName = currentZoneName;
        saveData.worldState.currentRegionName = currentRegionName;
        saveData.worldState.spawnPointID = currentUsedSpawnPoint != null ? currentUsedSpawnPoint.SpawnID : string.Empty;

        // 3. Party Progression State
        saveData.partyState.activeLeaderHeroName = activeLeaderHero != null ? activeLeaderHero.name : string.Empty;

        saveData.partyState.partyMemberNames.Clear();
        for (int i = 0; i < activeParty.Count; i++)
        {
            if (activeParty[i] != null) saveData.partyState.partyMemberNames.Add(activeParty[i].name);
        }

        saveData.partyState.reserveHeroNames.Clear();
        for (int i = 0; i < reserveParty.Count; i++)
        {
            if (reserveParty[i] != null) saveData.partyState.reserveHeroNames.Add(reserveParty[i].name);
        }

        // 4. World Story Flags
        saveData.gameFlags.Clear();
        foreach (var kvp in gameFlags)
        {
            saveData.gameFlags.Add(new GameFlagData(kvp.Key, kvp.Value));
        }
    }

    private void HandleRestoreSaveData(GameData saveData)
    {
        if (saveData == null) return;

        loadingData = true;

        // 1. Restore Economy & Meta Metrics
        totalGold = Mathf.Max(0, saveData.header.totalGold);
        totalPlayed = saveData.header.totalTime;
        currentZoneName = saveData.worldState.currentZoneName;
        currentRegionName = saveData.worldState.currentRegionName;

        GameEvents.Party.TriggerGoldChanged(totalGold);

        // 2. Restore Story Flags
        gameFlags.Clear();
        if (saveData.gameFlags != null)
        {
            for (int i = 0; i < saveData.gameFlags.Count; i++)
            {
                if (saveData.gameFlags[i] != null)
                {
                    gameFlags[saveData.gameFlags[i].key] = saveData.gameFlags[i].state;
                }
            }
        }

        // 3. Restore Party Roster Composition without GameObject.Find
        activeParty.Clear();
        reserveParty.Clear();

        if (saveData.partyState != null)
        {
            // Restore Active Party Members
            for (int i = 0; i < saveData.partyState.partyMemberNames.Count; i++)
            {
                string targetName = saveData.partyState.partyMemberNames[i];
                GameObject matchedHero = availableHeroes.Find(h => h != null && h.name.Equals(targetName));
                if (matchedHero != null)
                {
                    activeParty.Add(matchedHero);
                }
            }

            // Restore Inactive/Reserve Party Members
            for (int i = 0; i < saveData.partyState.reserveHeroNames.Count; i++)
            {
                string targetName = saveData.partyState.reserveHeroNames[i];
                GameObject matchedHero = availableHeroes.Find(h => h != null && h.name.Equals(targetName));
                if (matchedHero != null)
                {
                    reserveParty.Add(matchedHero);
                }
            }

            // Restore Active Overworld Leader
            if (!string.IsNullOrEmpty(saveData.partyState.activeLeaderHeroName))
            {
                activeLeaderHero = availableHeroes.Find(h => h != null && h.name.Equals(saveData.partyState.activeLeaderHeroName));
            }
        }

        if (activeLeaderHero == null && activeParty.Count > 0)
        {
            activeLeaderHero = activeParty[0];
        }

        // 4. Relocate Player Position
        Vector3 targetPos = new Vector3(saveData.worldState.playerPosition[0], saveData.worldState.playerPosition[1], saveData.worldState.playerPosition[2]);
        Quaternion targetRot = Quaternion.Euler(0, saveData.worldState.playerRotation[1], 0);

        if (Physics.Raycast(targetPos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 5f))
        {
            targetPos.y = hit.point.y;
        }

        GameEvents.Player.RequestTeleport(targetPos, targetRot);
        ResetCameraBehindPlayer(targetPos, targetRot);

        NotifyPartyChanged();
        GameEvents.Party.TriggerActiveHeroChanged(activeLeaderHero);

        loadingData = false;
    }

    #endregion

    #region Internal Systems & Camera Helpers

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
}