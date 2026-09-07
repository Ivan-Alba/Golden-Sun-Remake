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
        GameEvents.Scene.OnSetCurrentSpawnPoint += HandleSetCurrentSpawnPoint;

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
        GameEvents.Scene.OnSetCurrentSpawnPoint -= HandleSetCurrentSpawnPoint;

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

    #region Party & Economy Event Handlers

    private void HandleRequestHeroPortrait(string heroName, Action<Sprite> callback)
    {
        if (string.IsNullOrEmpty(heroName) || callback == null) return;

        // Buscar el héroe en la lista disponible centralizada del GameManager
        GameObject matchedHero = availableHeroes.Find(h => h != null && h.name.Equals(heroName, StringComparison.OrdinalIgnoreCase));

        if (matchedHero != null)
        {
            // Intentar obtener el retrato desde HeroStats o FighterStats
            if (matchedHero.TryGetComponent<FighterStats>(out var stats))
            {
                // Usamos TurnBarImage o un campo Portrait según tengas en tu componente
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

    private void HandleSetCurrentSpawnPoint(string spawnID)
    {
        if (string.IsNullOrEmpty(spawnID)) return;

        if (SpawnPoint.AllSpawnPoints.TryGetValue(spawnID, out SpawnPoint sp))
        {
            currentUsedSpawnPoint = sp;
            UpdateCurrentZone(sp.ZoneName, sp.RegionName);
            Debug.Log($"[GameManager] Current active SpawnPoint set to: {spawnID}");
        }
    }

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
            loadingData = false;
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
        loadingData = false;
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
                    UpdateCurrentZone(sp.ZoneName, sp.RegionName);

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
            // If restoring from TitleScreen/MainMenu or across different scenes
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