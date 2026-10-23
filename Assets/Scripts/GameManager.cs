using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

/// <summary>
/// Core game manager handling global game states, scene flow, party management, and persistent game metadata.
/// Decoupled via GameEvents (GameState, Scene, SaveLoad, Party, Player, Economy, Environment).
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
        Loading,
        GameOver
    }

    #endregion

    #region Serialized Fields

    [Header("Game State System")]
    [SerializeField] private GameState currentState = GameState.None;

    [Header("Time & Environment Settings")]
    [Tooltip("Starting hour of the day when beginning a new game (0.0 to 24.0). Default is 8.0 (8:00 AM).")]
    [SerializeField] private float initialGameTimeHours = 8.0f;

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

    // Zone & Area Tracking via ScriptableObject Architecture
    private ZoneSO currentZone;

    // Time Management (Persistent)
    // Range 0.0f to 24.0f (1 in-game hour = 60 real seconds => 24 min full cycle)
    private float currentInGameTimeHours = 8.0f;

    // Party Info
    private readonly List<GameObject> activeParty = new List<GameObject>();
    private readonly List<GameObject> reserveParty = new List<GameObject>();
    private GameObject activeLeaderHero;

    // Game Economy & Metrics
    private int totalGold;
    private float totalPlayed;

    #endregion

    #region Properties

    public float CurrentInGameTimeHours => currentInGameTimeHours;
    public ZoneSO CurrentZone => currentZone;

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
        GameEvents.Environment.OnSetInGameTimeRequested += HandleSetInGameTimeRequested;

        // Time Management Event Subscriptions
        GameEvents.GameState.OnChangeGameSpeedRequested += HandleChangeGameSpeedRequest;
        GameEvents.GameState.OnToggleGameSpeedRequested += HandleToggleGameSpeedRequest;

        // Party & Economy Event Subscriptions
        GameEvents.Party.OnModifyGoldRequested += HandleModifyGoldRequest;
        GameEvents.Party.OnRequestGold += HandleRequestGold;
        GameEvents.Party.OnRequestDjinnCounts += HandleRequestDjinnCounts;
        GameEvents.Fighter.OnFighterDied += HandleFighterDied;
        GameEvents.Party.OnActiveHeroIndexChangeRequested += HandleActiveHeroIndexChangeRequest;
        GameEvents.Party.OnActiveHeroChangeRequested += HandleActiveHeroChangeRequest;

        // Scene Transition Event Subscriptions
        GameEvents.Scene.OnStartNewGameRequested += HandleStartNewGameRequest;
        GameEvents.Scene.OnChangeSceneRequested += HandleChangeSceneRequest;
        GameEvents.Scene.OnSetCurrentSpawnPoint += HandleSetCurrentSpawnPoint;
        GameEvents.Scene.OnTransitionCompleted += HandleTransitionCompleted;

        // Save / Load Event Subscriptions
        GameEvents.SaveLoad.OnCollectSaveData += HandleCollectSaveData;
        GameEvents.SaveLoad.OnRestoreSaveData += HandleRestoreSaveData;

        GameEvents.Hero.OnRequestHeroPortrait += HandleRequestHeroPortrait;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnLevelFinishedLoading;

        // Game State & Zone Event Unsubscriptions
        GameEvents.GameState.OnChangeGameStateRequested -= HandleGameStateRequest;
        GameEvents.GameState.OnRequestPlayTime -= HandleRequestPlayTime;
        GameEvents.GameState.OnUpdateZoneRequested -= UpdateCurrentZone;
        GameEvents.Environment.OnSetInGameTimeRequested -= HandleSetInGameTimeRequested;

        // Time Management Event Unsubscriptions
        GameEvents.GameState.OnChangeGameSpeedRequested -= HandleChangeGameSpeedRequest;
        GameEvents.GameState.OnToggleGameSpeedRequested -= HandleToggleGameSpeedRequest;

        // Party & Economy Event Unsubscriptions
        GameEvents.Party.OnModifyGoldRequested -= HandleModifyGoldRequest;
        GameEvents.Party.OnRequestGold -= HandleRequestGold;
        GameEvents.Party.OnRequestDjinnCounts -= HandleRequestDjinnCounts;
        GameEvents.Fighter.OnFighterDied -= HandleFighterDied;
        GameEvents.Party.OnActiveHeroIndexChangeRequested -= HandleActiveHeroIndexChangeRequest;
        GameEvents.Party.OnActiveHeroChangeRequested -= HandleActiveHeroChangeRequest;

        // Scene Transition Event Unsubscriptions
        GameEvents.Scene.OnStartNewGameRequested -= HandleStartNewGameRequest;
        GameEvents.Scene.OnChangeSceneRequested -= HandleChangeSceneRequest;
        GameEvents.Scene.OnSetCurrentSpawnPoint -= HandleSetCurrentSpawnPoint;
        GameEvents.Scene.OnTransitionCompleted -= HandleTransitionCompleted;

        // Save / Load Event Unsubscriptions
        GameEvents.SaveLoad.OnCollectSaveData -= HandleCollectSaveData;
        GameEvents.SaveLoad.OnRestoreSaveData -= HandleRestoreSaveData;

        GameEvents.Hero.OnRequestHeroPortrait -= HandleRequestHeroPortrait;
    }

    private void Update()
    {
        if (currentState != GameState.TitleScreen && currentState != GameState.Loading)
        {
            totalPlayed += Time.deltaTime;
        }

        // Advance persistent in-game time during Overworld Exploration
        // 60 real seconds = 1 in-game hour (1/60th of an hour per second)
        if (currentState == GameState.WorldExploration)
        {
            AdvanceInGameTime(Time.deltaTime / 60f);
        }
    }

    #endregion

    #region In-Game Time Logic & API

    /// <summary>
    /// Advances persistent in-game time and broadcasts events for UI/HUD update.
    /// </summary>
    /// <param name="hoursToAdd">Fractional hours to advance.</param>
    public void AdvanceInGameTime(float hoursToAdd)
    {
        currentInGameTimeHours = (currentInGameTimeHours + hoursToAdd) % 24.0f;
        if (currentInGameTimeHours < 0f) currentInGameTimeHours += 24.0f;

        int hourInt = Mathf.FloorToInt(currentInGameTimeHours);
        int minuteInt = Mathf.FloorToInt((currentInGameTimeHours - hourInt) * 60f);

        GameEvents.Environment.TriggerTimeUpdated(currentInGameTimeHours, hourInt, minuteInt);
    }

    /// <summary>
    /// Explicitly sets the persistent game time (e.g., when sleeping at an Inn or loading save data).
    /// </summary>
    public void SetInGameTime(float newTimeHours)
    {
        currentInGameTimeHours = Mathf.Repeat(newTimeHours, 24.0f);
        int hourInt = Mathf.FloorToInt(currentInGameTimeHours);
        int minuteInt = Mathf.FloorToInt((currentInGameTimeHours - hourInt) * 60f);

        GameEvents.Environment.TriggerTimeUpdated(currentInGameTimeHours, hourInt, minuteInt);
    }

    #endregion

    #region Game State & Time Event Handlers

    private void HandleGameStateRequest(GameState requestedState)
    {
        SetGameState(requestedState);
    }

    private void HandleSetInGameTimeRequested(float targetHours)
    {
        SetInGameTime(targetHours);
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

    #region Party & Economy Event Handlers

    private void HandleRequestHeroPortrait(string heroName, Action<Sprite> callback)
    {
        if (string.IsNullOrEmpty(heroName) || callback == null) return;

        GameObject matchedHero = availableHeroes.Find(h => h != null && h.name.Equals(heroName, StringComparison.OrdinalIgnoreCase));

        if (matchedHero != null)
        {
            if (matchedHero.TryGetComponent<FighterStats>(out var stats))
            {
                callback.Invoke(stats.TurnBarImage);
                return;
            }
        }

        callback.Invoke(null);
    }

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

    private void HandleRequestDjinnCounts(Action<int[]> callback)
    {
        if (callback == null) return;

        int[] totalDjinnCounts = new int[4]; // [0] Venus, [1] Mars, [2] Jupiter, [3] Mercury

        // Helper function to process a list of heroes (Active or Reserve)
        void AccumulateDjinnCounts(List<GameObject> partyList)
        {
            for (int i = 0; i < partyList.Count; i++)
            {
                GameObject hero = partyList[i];
                if (hero != null && hero.TryGetComponent<HeroDjinnController>(out var djinnController))
                {
                    djinnController.GetSetDjinnCountsSinglePass(out int venus, out int mars, out int jupiter, out int mercury);
                    totalDjinnCounts[0] += venus;
                    totalDjinnCounts[1] += mars;
                    totalDjinnCounts[2] += jupiter;
                    totalDjinnCounts[3] += mercury;
                }
            }
        }

        AccumulateDjinnCounts(activeParty);
        AccumulateDjinnCounts(reserveParty);

        callback.Invoke(totalDjinnCounts);
    }

    private void HandleFighterDied(GameObject deadFighter)
    {
        if (deadFighter == activeLeaderHero)
        {
            EnsureLivingLeader();
        }
    }

    private void HandleActiveHeroIndexChangeRequest(int index)
    {
        if (activeParty == null || index < 0 || index >= activeParty.Count) return;

        GameObject candidateHero = activeParty[index];

        if (candidateHero == null) return;

        if (candidateHero.TryGetComponent<HeroStats>(out var heroStats) && heroStats.IsDead)
        {
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Error);
            return;
        }

        if (activeLeaderHero != candidateHero)
        {
            activeLeaderHero = candidateHero;

            GameEvents.Party.TriggerActiveHeroChanged(activeLeaderHero);
            GameEvents.Hero.TriggerHeroStatusChanged();
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

    private void HandleSetCurrentSpawnPoint(string spawnID)
    {
        if (string.IsNullOrEmpty(spawnID)) return;

        if (SpawnPoint.AllSpawnPoints.TryGetValue(spawnID, out SpawnPoint sp))
        {
            currentUsedSpawnPoint = sp;
            UpdateCurrentZone(sp.Zone);
            Debug.Log($"[GameManager] Current active SpawnPoint set to: {spawnID}");
        }
    }

    private void HandleStartNewGameRequest(string firstSceneName, string initialSpawnID)
    {
        StartNewGame(firstSceneName, initialSpawnID);
    }

    private void HandleChangeSceneRequest(string sceneName, string spawnID, GameState targetStateAfterLoad = GameState.WorldExploration)
    {
        pendingSpawnID = spawnID;
        pendingTargetState = targetStateAfterLoad;

        GameEvents.Scene.RequestLoadScene(sceneName);
    }

    private void UpdateCurrentZone(ZoneSO newZone, float duration = 3.5f)
    {
        if (newZone == null) return;

        // Si ya estamos exactamente en esta misma zona, ignoramos la petición (sin actualizar estado ni mostrar banner)
        if (currentZone == newZone)
        {
            return;
        }

        currentZone = newZone;
        Debug.Log($"[GameManager] Active Zone updated to: {currentZone.FullZoneDisplayName} ({currentZone.RegionDisplayName})");

        // Únicamente si el estado de exploración está activo, disparamos el banner de la UI
        if (currentState == GameState.WorldExploration)
        {
            GameEvents.UI.TriggerShowZoneBanner(currentZone.FullZoneDisplayName, currentZone.RegionDisplayName, duration);
        }
    }

    private void StartNewGame(string firstSceneName, string initialSpawnID)
    {
        totalGold = 200;
        totalPlayed = 0f;
        currentInGameTimeHours = initialGameTimeHours;
        activeParty.Clear();
        reserveParty.Clear();

        GameEvents.Party.TriggerGoldChanged(totalGold);
        GameEvents.Inventory.RequestClearAllInventories();

        for (int i = 0; i < availableHeroes.Count; i++)
        {
            if (availableHeroes[i] == null) continue;

            activeParty.Add(availableHeroes[i]);

            HeroEquipment equipment = availableHeroes[i].GetComponent<HeroEquipment>();
            if (equipment != null)
            {
                equipment.InitializeStartingEquipment();
            }

            HeroStats stats = availableHeroes[i].GetComponent<HeroStats>();
            if (stats != null)
            {
                stats.FullRecover();
            }
        }

        if (activeParty.Count > 0)
        {
            activeLeaderHero = activeParty[0];
        }

        NotifyPartyChanged();
        GameEvents.Party.TriggerActiveHeroChanged(activeLeaderHero);

        // Broadcast initial time
        SetInGameTime(currentInGameTimeHours);

        HandleChangeSceneRequest(firstSceneName, initialSpawnID, GameState.WorldExploration);
    }

    private void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[GameManager] Level Loaded in background: {scene.name}");

        if (scene.name.Equals("Title"))
        {
            SetGameState(GameState.TitleScreen);
            loadingData = false;
            return;
        }

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

    /// <summary>
    /// Pre-opening setup: Re-positions cameras, settles physics in the dark,
    /// triggers arrival movement in the dark until active, and THEN requests opening the curtain.
    /// </summary>
    private IEnumerator Routine_HandlePostLoadSequence()
    {
        // 1. Trigger Hero Swap while screen is still covered
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

        SetInGameTime(currentInGameTimeHours);

        // 2. Wait frames so Cinemachine and physics settle position in the dark
        yield return new WaitForEndOfFrame();
        yield return new WaitForFixedUpdate();

        // 3. Trigger AutoSave if changing scenes during normal gameplay
        if (!loadingData && !SceneManager.GetActiveScene().name.Equals("Title"))
        {
            Debug.Log("<color=cyan>[GameManager] AutoSave triggered upon entering new scene.</color>");
            GameEvents.SaveLoad.TriggerRequestAutoSave();
        }

        // 4. Initiate autowalk in the shadow (Based on IsExecutingArrival)
        bool isGameplayTeleport = !loadingData && currentUsedSpawnPoint != null;
        if (pendingTargetState == GameState.WorldExploration && isGameplayTeleport && currentUsedSpawnPoint.AutoWalkOnArrival)
        {
            StartCoroutine(currentUsedSpawnPoint.Routine_ExecuteArrivalSequence());

            while (!currentUsedSpawnPoint.IsExecutingArrival)
            {
                yield return null;
            }
        }

        // 5. Request opening transition curtain
        GameEvents.Scene.RequestFadeInScene();
    }

    /// <summary>
    /// Post-opening handler triggered when the transition curtain finishes revealing the scene.
    /// Waits for arrival sequences to complete using IsExecutingArrival before enabling player control.
    /// </summary>
    private void HandleTransitionCompleted()
    {
        // Ignore transition completions if we are on the Title Screen
        if (currentState == GameState.TitleScreen || SceneManager.GetActiveScene().name.Equals("Title")) return;

        StartCoroutine(Routine_HandlePostTransitionActivation());
    }

    private IEnumerator Routine_HandlePostTransitionActivation()
    {
        bool isGameplayTeleport = !loadingData && currentUsedSpawnPoint != null;

        if (pendingTargetState == GameState.WorldExploration && isGameplayTeleport && currentUsedSpawnPoint.AutoWalkOnArrival)
        {
            while (currentUsedSpawnPoint.IsExecutingArrival)
            {
                yield return null;
            }

            if (arrivalWalkDelay > 0f)
            {
                yield return new WaitForSeconds(arrivalWalkDelay);
            }
        }
        else if (pendingTargetState == GameState.WorldExploration && currentZone != null)
        {
            GameEvents.UI.TriggerShowZoneBanner(currentZone.FullZoneDisplayName, currentZone.RegionDisplayName, 3.5f);
        }

        SetGameState(pendingTargetState);
        loadingData = false;
    }

    #endregion

    #region Leader Management API

    /// <summary>
    /// Evaluates active party members and reassigns the leader to the first living hero found.
    /// If all heroes are dead, triggers GameOver.
    /// </summary>
    public void EnsureLivingLeader()
    {
        if (activeParty == null || activeParty.Count == 0) return;

        if (activeLeaderHero != null &&
            activeLeaderHero.TryGetComponent<HeroStats>(out var currentLeaderStats) &&
            !currentLeaderStats.IsDead)
        {
            return;
        }

        GameObject newLeader = FindFirstLivingHero();

        if (newLeader != null)
        {
            activeLeaderHero = newLeader;

            GameEvents.Party.TriggerActiveHeroChanged(activeLeaderHero);
            GameEvents.Hero.TriggerHeroStatusChanged();
        }
        else
        {
            Debug.LogWarning("[PartyManager] All heroes in party are dead! Requesting Game Over.");
            GameEvents.GameState.RequestGameStateChange(GameManager.GameState.GameOver);
        }
    }

    /// <summary>
    /// Helper algorithm to find the first non-dead Hero GameObject in activeParty.
    /// </summary>
    private GameObject FindFirstLivingHero()
    {
        for (int i = 0; i < activeParty.Count; i++)
        {
            GameObject heroObj = activeParty[i];
            if (heroObj != null && heroObj.TryGetComponent<HeroStats>(out var stats))
            {
                if (!stats.IsDead)
                {
                    return heroObj;
                }
            }
        }

        return null;
    }

    #endregion

    #region Save & Load Event Handlers

    private void HandleCollectSaveData(GameData saveData)
    {
        if (saveData == null) return;

        // 1. Save Header Meta Info
        saveData.header.totalGold = totalGold;
        saveData.header.totalTime = totalPlayed;
        saveData.header.locationName = currentZone != null ? currentZone.LocationName : "UNKNOWN";

        saveData.header.partyPreview.Clear();
        for (int i = 0; i < activeParty.Count; i++)
        {
            if (activeParty[i] != null && activeParty[i].TryGetComponent<HeroStats>(out var stats))
            {
                saveData.header.partyPreview.Add(new HeroPreviewData(activeParty[i].name, stats.Level));
            }
        }

        // 2. World & Scene State (Persisting Local Game Time & Zone ScriptableObject Keys)
        saveData.worldState.currentSceneName = SceneManager.GetActiveScene().name;
        saveData.worldState.currentZoneName = currentZone != null ? currentZone.ZoneID : string.Empty;
        saveData.worldState.currentRegionName = (currentZone != null && currentZone.ParentRegion != null) ? currentZone.ParentRegion.RegionID : string.Empty;
        saveData.worldState.spawnPointID = currentUsedSpawnPoint != null ? currentUsedSpawnPoint.SpawnID : string.Empty;
        saveData.worldState.currentGameTimeHours = currentInGameTimeHours;

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
    }

    private void HandleRestoreSaveData(GameData saveData)
    {
        if (saveData == null) return;

        loadingData = true;

        // 1. Restore Economy, Meta Metrics & World Time
        totalGold = Mathf.Max(0, saveData.header.totalGold);
        totalPlayed = saveData.header.totalTime;

        // Restore local in-game world time and notify environmental listeners
        SetInGameTime(saveData.worldState.currentGameTimeHours);

        GameEvents.Party.TriggerGoldChanged(totalGold);

        // 2. Restore Party Roster Composition without GameObject.Find
        activeParty.Clear();
        reserveParty.Clear();

        if (saveData.partyState != null)
        {
            // Restore Active Party Members
            for (int i = 0; i < saveData.partyState.partyMemberNames.Count; i++)
            {
                string targetName = saveData.partyState.partyMemberNames[i];
                GameObject matchedHero = availableHeroes.Find(h => h != null && h.name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
                if (matchedHero != null)
                {
                    activeParty.Add(matchedHero);
                }
            }

            // Restore Inactive/Reserve Party Members
            for (int i = 0; i < saveData.partyState.reserveHeroNames.Count; i++)
            {
                string targetName = saveData.partyState.reserveHeroNames[i];
                GameObject matchedHero = availableHeroes.Find(h => h != null && h.name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
                if (matchedHero != null)
                {
                    reserveParty.Add(matchedHero);
                }
            }

            // Restore Active Overworld Leader
            if (!string.IsNullOrEmpty(saveData.partyState.activeLeaderHeroName))
            {
                activeLeaderHero = availableHeroes.Find(h => h != null && h.name.Equals(saveData.partyState.activeLeaderHeroName, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (activeLeaderHero == null && activeParty.Count > 0)
        {
            activeLeaderHero = activeParty[0];
        }

        // 3. Handle Scene Transition or Covered Immediate Teleport
        string targetScene = saveData.worldState.currentSceneName;
        string savedSpawnID = saveData.worldState.spawnPointID;

        pendingSpawnID = savedSpawnID;

        string activeSceneName = SceneManager.GetActiveScene().name;

        // If restoring in the SAME scene (e.g. loading from Pause Menu within exploration)
        if (activeSceneName.Equals(targetScene, StringComparison.OrdinalIgnoreCase))
        {
            // Request full curtain transition: cover screen -> restore position/states -> reveal screen
            GameEvents.Scene.RequestFullTransition(() =>
            {
                Vector3 targetPos;
                Quaternion targetRot;

                // Priority 1: Check if a valid SpawnPoint exists for this save state
                if (!string.IsNullOrEmpty(savedSpawnID) && SpawnPoint.AllSpawnPoints.TryGetValue(savedSpawnID, out SpawnPoint sp))
                {
                    currentUsedSpawnPoint = sp;
                    UpdateCurrentZone(sp.Zone);

                    targetPos = sp.transform.position;
                    targetRot = sp.transform.rotation;
                }
                else
                {
                    // Priority 2: Fallback to exact saved coordinates if no SpawnPoint was set
                    targetPos = new Vector3(saveData.worldState.playerPosition[0], saveData.worldState.playerPosition[1], saveData.worldState.playerPosition[2]);
                    targetRot = Quaternion.Euler(0, saveData.worldState.playerRotation[1], 0);
                }

                // Snap target position to ground level via Raycast
                if (Physics.Raycast(targetPos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 5f))
                {
                    targetPos.y = hit.point.y;
                }

                GameEvents.Player.RequestTeleport(targetPos, targetRot);
                ResetCameraBehindPlayer(targetPos, targetRot);

                NotifyPartyChanged();
                GameEvents.Party.TriggerActiveHeroChanged(activeLeaderHero);

                loadingData = false;
            });
        }
        else
        {
            // If restoring from TitleScreen or across different scenes
            HandleChangeSceneRequest(targetScene, savedSpawnID, GameState.WorldExploration);
        }
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
            UpdateCurrentZone(sp.Zone);

            Vector3 spawnPos = sp.transform.position;
            if (Physics.Raycast(spawnPos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 5f))
            {
                spawnPos.y = hit.point.y + 0.05f;
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

            freeLookCamera.PreviousStateIsValid = false;
        }
    }

    #endregion
}