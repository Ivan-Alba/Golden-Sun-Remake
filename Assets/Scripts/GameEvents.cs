using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameEvents
{
    // ==========================================
    // CAMERA & VIEWPORT EVENTS
    // ==========================================
    public static class Camera
    {
        /// <summary>
        /// Triggered whenever the primary active rendering camera changes in the scene.
        /// (e.g., Exploration 3rd Person, Cinematic Cutscene, Battle Camera).
        /// </summary>
        public static event Action<UnityEngine.Camera> OnActiveCameraChanged;
        public static void TriggerActiveCameraChanged(UnityEngine.Camera newCamera)
            => OnActiveCameraChanged?.Invoke(newCamera);
    }

    // ==========================================
    // INPUT & CONTROLLER EVENTS
    // ==========================================
    public static class Input
    {
        // Continuous Movement & State Inputs

        /// <summary>
        /// Triggered continuous movement vector input changes (x: horizontal, y: vertical).
        /// </summary>
        public static event Action<Vector2> OnMoveInputChanged;
        public static void TriggerMoveInputChanged(Vector2 moveInput)
            => OnMoveInputChanged?.Invoke(moveInput);

        /// <summary>
        /// Triggered when the running modifier button state changes (held / released).
        /// </summary>
        public static event Action<bool> OnRunStateChanged;
        public static void TriggerRunStateChanged(bool isRunning)
            => OnRunStateChanged?.Invoke(isRunning);

        // Exploration & World Interaction Inputs

        /// <summary>
        /// Triggered when the primary world interaction button (Interact) is pressed.
        /// </summary>
        public static event Action OnInteractPressed;
        public static void TriggerInteractPressed()
            => OnInteractPressed?.Invoke();

        /// <summary>
        /// Triggered when the pause/start menu toggle button is pressed.
        /// </summary>
        public static event Action OnToggleMenuPressed;
        public static void TriggerToggleMenuPressed()
            => OnToggleMenuPressed?.Invoke();

        /// <summary>
        /// Triggered when requesting a direct active leader hero swap by hero index.
        /// </summary>
        public static event Action<int> OnChangeHeroPressed;
        public static void TriggerChangeHeroPressed(int heroIndex)
            => OnChangeHeroPressed?.Invoke(heroIndex);

        /// <summary>
        /// Triggered when the first-person view toggle button (Mind's Eye / Look) is pressed.
        /// </summary>
        public static event Action OnToggleFirstPersonViewPressed;
        public static void TriggerToggleFirstPersonViewPressed()
            => OnToggleFirstPersonViewPressed?.Invoke();

        /// <summary>
        /// Triggered when pressing a field Psynergy shortcut button (D-Pad Hotline).
        /// </summary>
        public static event Action<int> OnUsePsynergyPressed;
        public static void TriggerUsePsynergyPressed(int slotIndex)
            => OnUsePsynergyPressed?.Invoke(slotIndex);

        // Dialogue Inputs

        /// <summary>
        /// Triggered when the advance dialogue key is pressed to move text forward or accept a line.
        /// </summary>
        public static event Action OnDialogueAdvancePressed;
        public static void TriggerDialogueAdvancePressed()
            => OnDialogueAdvancePressed?.Invoke();

        // Battle Inputs

        /// <summary>
        /// Triggered when the confirm action button is pressed inside battle UI.
        /// </summary>
        public static event Action OnBattleConfirmPressed;
        public static void TriggerBattleConfirmPressed()
            => OnBattleConfirmPressed?.Invoke();

        /// <summary>
        /// Triggered when the cancel/back button is pressed inside battle UI.
        /// </summary>
        public static event Action OnBattleCancelPressed;
        public static void TriggerBattleCancelPressed()
            => OnBattleCancelPressed?.Invoke();

        /// <summary>
        /// Triggered when navigate up key is pressed inside battle UI.
        /// </summary>
        public static event Action OnBattleUpPressed;
        public static void TriggerBattleUpPressed()
            => OnBattleUpPressed?.Invoke();

        /// <summary>
        /// Triggered when navigate down key is pressed inside battle UI.
        /// </summary>
        public static event Action OnBattleDownPressed;
        public static void TriggerBattleDownPressed()
            => OnBattleDownPressed?.Invoke();

        /// <summary>
        /// Triggered when navigate left key is pressed inside battle UI.
        /// </summary>
        public static event Action OnBattleLeftPressed;
        public static void TriggerBattleLeftPressed()
            => OnBattleLeftPressed?.Invoke();

        /// <summary>
        /// Triggered when navigate right key is pressed inside battle UI.
        /// </summary>
        public static event Action OnBattleRightPressed;
        public static void TriggerBattleRightPressed()
            => OnBattleRightPressed?.Invoke();

        // Menu Navigation Inputs

        /// <summary>
        /// Triggered when the confirm button is pressed in standard full-screen or sub-menus.
        /// </summary>
        public static event Action OnMenuConfirmPressed;
        public static void TriggerMenuConfirmPressed()
            => OnMenuConfirmPressed?.Invoke();

        /// <summary>
        /// Triggered when the cancel/back button is pressed in standard full-screen or sub-menus.
        /// </summary>
        public static event Action OnMenuCancelPressed;
        public static void TriggerMenuCancelPressed()
            => OnMenuCancelPressed?.Invoke();

        /// <summary>
        /// Triggered when requesting navigation to the next tab in full-screen menus.
        /// </summary>
        public static event Action OnMenuNextTabPressed;
        public static void TriggerMenuNextTabPressed()
            => OnMenuNextTabPressed?.Invoke();

        /// <summary>
        /// Triggered when requesting navigation to the previous tab in full-screen menus.
        /// </summary>
        public static event Action OnMenuPreviousTabPressed;
        public static void TriggerMenuPreviousTabPressed()
            => OnMenuPreviousTabPressed?.Invoke();

        /// <summary>
        /// Triggered when cycling to inspect the next hero in full-screen sub-menus.
        /// </summary>
        public static event Action OnMenuNextHeroPressed;
        public static void TriggerMenuNextHeroPressed()
            => OnMenuNextHeroPressed?.Invoke();

        /// <summary>
        /// Triggered when cycling to inspect the previous hero in full-screen sub-menus.
        /// </summary>
        public static event Action OnMenuPreviousHeroPressed;
        public static void TriggerMenuPreviousHeroPressed()
            => OnMenuPreviousHeroPressed?.Invoke();
    }

    // ==========================================
    // GAMEPLAY & GAME STATE EVENTS
    // ==========================================
    public static class GameState
    {
        /// <summary>
        /// Request to change the current game state. Listened exclusively by GameManager.
        /// </summary>
        public static event Action<GameManager.GameState> OnChangeGameStateRequested;
        public static void RequestGameStateChange(GameManager.GameState newState)
            => OnChangeGameStateRequested?.Invoke(newState);

        /// <summary>
        /// Triggered AFTER the GameManager has validated and officially changed the state.
        /// </summary>
        public static event Action<GameManager.GameState, GameManager.GameState> OnGameStateChanged;
        public static void TriggerGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
            => OnGameStateChanged?.Invoke(previousState, newState);

        /// <summary>
        /// Requests the current total playtime in seconds via callback. Listened by GameManager.
        /// </summary>
        public static event Action<Action<float>> OnRequestPlayTime;
        public static void RequestPlayTime(Action<float> callback) => OnRequestPlayTime?.Invoke(callback);

        /// <summary>
        /// Requests an update for the player's current zone and region names. Listened by GameManager.
        /// </summary>
        public static event Action<string, string> OnUpdateZoneRequested;
        public static void RequestUpdateZone(string zoneName, string regionName)
            => OnUpdateZoneRequested?.Invoke(zoneName, regionName);

        /// <summary>
        /// Request to change the global time scale (1.0f = normal, 2.0f = x2 speed, 0.0f = pause).
        /// Listened by GameManager.
        /// </summary>
        public static event Action<float> OnChangeGameSpeedRequested;
        public static void RequestChangeGameSpeed(float newSpeed)
            => OnChangeGameSpeedRequested?.Invoke(newSpeed);

        /// <summary>
        /// Request to toggle game speed between normal (1.0f) and fast-forward (2.0f).
        /// Listened by GameManager.
        /// </summary>
        public static event Action OnToggleGameSpeedRequested;
        public static void RequestToggleGameSpeed()
            => OnToggleGameSpeedRequested?.Invoke();
    }

    // ==========================================
    // PARTY & GROUP MANAGEMENT EVENTS
    // ==========================================
    public static class Party
    {
        /// <summary>
        /// Triggered whenever the active party roster structure changes (hero added, removed, or swapped positions).
        /// Passes the updated read-only list of active party hero GameObjects.
        /// </summary>
        public static event Action<IReadOnlyList<GameObject>> OnPartyChanged;
        public static void TriggerPartyChanged(IReadOnlyList<GameObject> activeParty)
            => OnPartyChanged?.Invoke(activeParty);

        /// <summary>
        /// Triggered whenever the reserve party roster structure changes.
        /// Passes the updated read-only list of reserve hero GameObjects.
        /// </summary>
        public static event Action<IReadOnlyList<GameObject>> OnReservePartyChanged;
        public static void TriggerReservePartyChanged(IReadOnlyList<GameObject> reserveParty)
            => OnReservePartyChanged?.Invoke(reserveParty);

        /// <summary>
        /// Request sent by UI or PlayerController to swap the active overworld leader by party index.
        /// Listened exclusively by GameManager.
        /// </summary>
        public static event Action<int> OnActiveHeroIndexChangeRequested;
        public static void RequestActiveHeroIndexChange(int index)
            => OnActiveHeroIndexChangeRequested?.Invoke(index);

        /// <summary>
        /// Request sent to swap active leader directly by GameObject reference.
        /// Listened exclusively by GameManager.
        /// </summary>
        public static event Action<GameObject> OnActiveHeroChangeRequested;
        public static void RequestActiveHeroChange(GameObject newHero)
            => OnActiveHeroChangeRequested?.Invoke(newHero);

        /// <summary>
        /// Triggered AFTER GameManager has successfully validated and changed the active leader.
        /// Listened by PlayerController, Cameras, UI, etc.
        /// </summary>
        public static event Action<GameObject> OnActiveHeroChanged;
        public static void TriggerActiveHeroChanged(GameObject activeHero)
            => OnActiveHeroChanged?.Invoke(activeHero);

        /// <summary>
        /// Triggered whenever the party's total gold amount changes.
        /// </summary>
        public static event Action<int> OnGoldChanged;
        public static void TriggerGoldChanged(int newTotalGold) => OnGoldChanged?.Invoke(newTotalGold);

        /// <summary>
        /// Request sent to modify party gold (amount can be positive or negative).
        /// Listened by GameManager.
        /// </summary>
        public static event Action<int> OnModifyGoldRequested;
        public static void RequestModifyGold(int amount) => OnModifyGoldRequested?.Invoke(amount);

        /// <summary>
        /// Request to query current total gold via callback. Listened by GameManager.
        /// </summary>
        public static event Action<Action<int>> OnRequestGold;
        public static void RequestGold(Action<int> callback) => OnRequestGold?.Invoke(callback);

        /// <summary>
        /// Request to query current live elemental Djinn counts (array of 4 elements) via callback.
        /// </summary>
        public static event Action<Action<int[]>> OnRequestDjinnCounts;
        public static void RequestDjinnCounts(Action<int[]> callback) => OnRequestDjinnCounts?.Invoke(callback);
    }

    // ==========================================
    // WORLD & EXPLORATION EVENTS
    // ==========================================
    public static class World
    {
        /// <summary>
        /// Triggered when the player interaction detector focuses on or loses an interactable target in proximity.
        /// </summary>
        public static event Action<Interactable> OnInteractableTargetChanged;
        public static void TriggerInteractableTargetChanged(Interactable target)
            => OnInteractableTargetChanged?.Invoke(target);

        /// <summary>
        /// Triggered when the player's Psynergy detector focuses on or loses an IPsynergyTargetable in proximity/viewport.
        /// Sends IPsynergyTargetable target (or null when target is lost).
        /// </summary>
        public static event Action<IPsynergyTargetable> OnPsynergyTargetChanged;
        public static void TriggerPsynergyTargetChanged(IPsynergyTargetable target)
            => OnPsynergyTargetChanged?.Invoke(target);

        /// <summary>
        /// Request to display an emotional balloon/emote floating above a character's head in world space.
        /// Params: targetTransform, emoteType, duration
        /// </summary>
        public static event Action<Transform, EmoteType, float> OnShowEmoteRequested;
        public static void RequestShowEmote(Transform targetHead, EmoteType emoteType, float duration = 2.0f)
            => OnShowEmoteRequested?.Invoke(targetHead, emoteType, duration);

        /// <summary>
        /// Triggered when an overworld enemy updates its threat/alert percentage towards the player.
        /// Params: enemyGameObject, threatPercentage (0.0f to 1.0f), currentAIState
        /// </summary>
        public static event Action<GameObject, float, EnemyAlertState> OnEnemyThreatLevelChanged;
        public static void TriggerEnemyThreatLevelChanged(GameObject enemyObj, float threatValue, EnemyAlertState state)
            => OnEnemyThreatLevelChanged?.Invoke(enemyObj, threatValue, state);

        /// <summary>
        /// Triggered when an entity enters the player's visual proximity range.
        /// Params: entityGameObject
        /// </summary>
        public static event Action<GameObject> OnEntityEnteredProximity;
        public static void TriggerEntityEnteredProximity(GameObject entityObj)
            => OnEntityEnteredProximity?.Invoke(entityObj);

        /// <summary>
        /// Triggered when an entity exits the player's visual proximity range or is destroyed.
        /// Params: entityGameObject
        /// </summary>
        public static event Action<GameObject> OnEntityExitedProximity;
        public static void TriggerEntityExitedProximity(GameObject entityObj)
            => OnEntityExitedProximity?.Invoke(entityObj);

        /// <summary>
        /// Request to update a world flag value using a FlagSO reference.
        /// Listened by WorldStateController.
        /// </summary>
        public static event Action<FlagSO, bool> OnSetFlagRequested;
        public static void RequestSetFlag(FlagSO flag, bool value)
            => OnSetFlagRequested?.Invoke(flag, value);

        /// <summary>
        /// Request to query a world flag value via callback using a FlagSO reference.
        /// Listened by WorldStateController.
        /// </summary>
        public static event Action<FlagSO, Action<bool>> OnCheckFlagRequested;
        public static void RequestCheckFlag(FlagSO flag, Action<bool> callback)
            => OnCheckFlagRequested?.Invoke(flag, callback);

        /// <summary>
        /// Request to retrieve the last active chronicle flag slug via callback.
        /// Listened by WorldStateController.
        /// </summary>
        public static event Action<Action<string>> OnRequestLastChronicleFlagSlug;
        public static void RequestLastChronicleFlagSlug(Action<string> callback)
            => OnRequestLastChronicleFlagSlug?.Invoke(callback);

        /// <summary>
        /// Broadcasted whenever any world flag value is updated at runtime.
        /// </summary>
        public static event Action<FlagSO, bool> OnFlagChanged;
        public static void TriggerFlagChanged(FlagSO flag, bool newValue)
            => OnFlagChanged?.Invoke(flag, newValue);
    }

    // ==========================================
    // DATABASE & REPOSITORY EVENTS
    // ==========================================
    public static class Database
    {
        // --- ITEM DATABASE QUERIES ---

        /// <summary>
        /// Request to retrieve an ItemSO by its unique slug. 
        /// Sends slug and a callback action returning the ItemSO (or null if not found).
        /// </summary>
        public static event Action<string, Action<ItemSO>> OnRequestItemBySlug;
        public static void RequestItemBySlug(string slug, Action<ItemSO> onResult)
            => OnRequestItemBySlug?.Invoke(slug, onResult);

        /// <summary>
        /// Request to retrieve the complete read-only list of all registered ItemSO assets in the database.
        /// </summary>
        public static event Action<Action<IReadOnlyList<ItemSO>>> OnRequestAllItems;
        public static void RequestAllItems(Action<IReadOnlyList<ItemSO>> onResult)
            => OnRequestAllItems?.Invoke(onResult);

        /// <summary>
        /// Request to populate a provided buffer list with ItemSO assets filtered by ItemType.
        /// Zero-allocation query pattern.
        /// </summary>
        public static event Action<ItemType, List<ItemSO>> OnRequestItemsByType;
        public static void RequestItemsByType(ItemType itemType, List<ItemSO> resultsBuffer)
            => OnRequestItemsByType?.Invoke(itemType, resultsBuffer);

        // --- PSYNERGY DATABASE QUERIES ---

        /// <summary>
        /// Request to retrieve a PsynergySO by its unique SlugName.
        /// Callback returns the PsynergySO (or null if not found).
        /// </summary>
        public static event Action<string, Action<PsynergySO>> OnRequestPsynergyBySlug;
        public static void RequestPsynergyBySlug(string slug, Action<PsynergySO> onResult)
            => OnRequestPsynergyBySlug?.Invoke(slug, onResult);

        /// <summary>
        /// Request to retrieve the complete read-only list of all registered Psynergies.
        /// </summary>
        public static event Action<Action<IReadOnlyList<PsynergySO>>> OnRequestAllPsynergies;
        public static void RequestAllPsynergies(Action<IReadOnlyList<PsynergySO>> onResult)
            => OnRequestAllPsynergies?.Invoke(onResult);

        /// <summary>
        /// Request to retrieve the elemental UI Icon Sprite for a given ElementalType.
        /// </summary>
        public static event Action<ElementalType, Action<Sprite>> OnRequestElementIcon;
        public static void RequestElementIcon(ElementalType element, Action<Sprite> onResult)
            => OnRequestElementIcon?.Invoke(element, onResult);

        /// <summary>
        /// Request to retrieve the elemental circle VFX prefab for a given ElementalType.
        /// </summary>
        public static event Action<ElementalType, Action<GameObject>> OnRequestElementVFX;
        public static void RequestElementVFX(ElementalType element, Action<GameObject> onResult)
            => OnRequestElementVFX?.Invoke(element, onResult);

        // --- DJINN DATABASE QUERIES ---

        /// <summary>
        /// Request a DjinnSO asset by its unique djinnSlug.
        /// Params: djinnSlug, callbackResult
        /// </summary>
        public static event Action<string, Action<DjinnSO>> OnRequestDjinnBySlug;
        public static void RequestDjinnBySlug(string djinnSlug, Action<DjinnSO> callback)
            => OnRequestDjinnBySlug?.Invoke(djinnSlug, callback);

        /// <summary>
        /// Request all registered DjinnSO assets from the database.
        /// Params: callbackResult
        /// </summary>
        public static event Action<Action<IReadOnlyList<DjinnSO>>> OnRequestAllDjinns;
        public static void RequestAllDjinns(Action<IReadOnlyList<DjinnSO>> callback)
            => OnRequestAllDjinns?.Invoke(callback);

        /// <summary>
        /// Request all DjinnSO assets matching a specific ElementalType.
        /// Params: element, resultsBuffer
        /// </summary>
        public static event Action<ElementalType, List<DjinnSO>> OnRequestDjinnsByElement;
        public static void RequestDjinnsByElement(ElementalType element, List<DjinnSO> resultsBuffer)
            => OnRequestDjinnsByElement?.Invoke(element, resultsBuffer);

        // --- FLAG DATABASE QUERIES ---

        /// <summary>
        /// Request a FlagSO asset by its unique flagSlug identifier.
        /// Params: flagSlug, callbackResult
        /// </summary>
        public static event Action<string, Action<FlagSO>> OnRequestFlagBySlug;
        public static void RequestFlagBySlug(string flagSlug, Action<FlagSO> callback)
            => OnRequestFlagBySlug?.Invoke(flagSlug, callback);

        /// <summary>
        /// Request all registered FlagSO assets from the database repository.
        /// Params: callbackResult
        /// </summary>
        public static event Action<Action<IReadOnlyList<FlagSO>>> OnRequestAllFlags;
        public static void RequestAllFlags(Action<IReadOnlyList<FlagSO>> callback)
            => OnRequestAllFlags?.Invoke(callback);
    }

    // ==========================================
    // SCENE & TRANSITION EVENTS
    // ==========================================
    public static class Scene
    {

        /// <summary>
        /// Sets the currently active spawn point ID in GameManager for save state tracking.
        /// </summary>
        public static event Action<string> OnSetCurrentSpawnPoint;
        public static void SetCurrentSpawnPoint(string spawnID)
            => OnSetCurrentSpawnPoint?.Invoke(spawnID);

        /// <summary>
        /// Request to reset progress metrics and boot up a new game sequence.
        /// Params: firstSceneName, initialSpawnID
        /// Listened exclusively by GameManager.
        /// </summary>
        public static event Action<string, string> OnStartNewGameRequested;
        public static void RequestStartNewGame(string firstSceneName, string initialSpawnID)
            => OnStartNewGameRequested?.Invoke(firstSceneName, initialSpawnID);

        /// <summary>
        /// Requests loading a scene and moving the player to a target spawn point. Listened by GameManager.
        /// </summary>
        public static event Action<string, string, GameManager.GameState> OnChangeSceneRequested;
        public static void RequestChangeScene(string sceneName, string spawnID, GameManager.GameState targetState = GameManager.GameState.WorldExploration)
            => OnChangeSceneRequested?.Invoke(sceneName, spawnID, targetState);

        /// <summary>
        /// Request to load a scene asynchronously covered by the closing transition animation.
        /// </summary>
        public static event Action<string> OnRequestLoadScene;
        public static void RequestLoadScene(string sceneName)
            => OnRequestLoadScene?.Invoke(sceneName);

        /// <summary>
        /// Request to play a full closing/opening transition and execute an action while screen is covered.
        /// </summary>
        public static event Action<Action> OnRequestFullTransition;
        public static void RequestFullTransition(Action onScreenCovered)
            => OnRequestFullTransition?.Invoke(onScreenCovered);

        /// <summary>
        /// Request to play the opening transition animation to reveal the new scene/exploration.
        /// </summary>
        public static event Action OnRequestFadeInScene;
        public static void RequestFadeInScene()
            => OnRequestFadeInScene?.Invoke();

        /// <summary>
        /// Request a smooth ambient black overlay fade to a target alpha.
        /// Params: targetAlpha, duration
        /// </summary>
        public static event Action<float, float> OnRequestFadeToBlack;
        public static void RequestFadeToBlack(float targetAlpha = 1.0f, float duration = 1.0f)
            => OnRequestFadeToBlack?.Invoke(targetAlpha, duration);

        /// <summary>
        /// Request to reset and instantly hide the ambient black fade overlay.
        /// </summary>
        public static event Action OnRequestResetFade;
        public static void RequestResetFade()
            => OnRequestResetFade?.Invoke();

        // --- Notifications (Broadcasted by SceneTransitionManager) ---

        /// <summary>
        /// Triggered when the transition animation completely covers the screen with the logo overlay.
        /// </summary>
        public static event Action OnTransitionCovered;
        public static void TriggerTransitionCovered()
            => OnTransitionCovered?.Invoke();

        /// <summary>
        /// Triggered when the transition animation finishes revealing the scene.
        /// </summary>
        public static event Action OnTransitionCompleted;
        public static void TriggerTransitionCompleted()
            => OnTransitionCompleted?.Invoke();
    }

    // ==========================================
    // SAVE & LOAD EVENTS
    // ==========================================
    public static class SaveLoad
    {
        /// <summary>
        /// Request to save the game to a specified save slot index.
        /// </summary>
        public static event Action<int> OnRequestSaveGame;
        public static void RequestSaveGame(int slotIndex)
            => OnRequestSaveGame?.Invoke(slotIndex);

        /// <summary>
        /// Request to load the game state from a specified save slot index.
        /// </summary>
        public static event Action<int> OnRequestLoadGame;
        public static void RequestLoadGame(int slotIndex)
            => OnRequestLoadGame?.Invoke(slotIndex);

        /// <summary>
        /// Requests an automated save operation (typically targeting slot 0) with an optional custom location or trigger tag.
        /// </summary>
        public static event Action OnRequestAutoSave;
        public static void TriggerRequestAutoSave()
            => OnRequestAutoSave?.Invoke();

        /// <summary>
        /// Broadcasted during save creation to collect data from subscribed managers/entities.
        /// Params: targetGameData
        /// </summary>
        public static event Action<GameData> OnCollectSaveData;
        public static void TriggerCollectSaveData(GameData saveData)
            => OnCollectSaveData?.Invoke(saveData);

        /// <summary>
        /// Broadcasted after loading GameData from disk to restore state across managers/entities.
        /// Params: loadedGameData
        /// </summary>
        public static event Action<GameData> OnRestoreSaveData;
        public static void TriggerRestoreSaveData(GameData saveData)
            => OnRestoreSaveData?.Invoke(saveData);

        /// <summary>
        /// Triggered when a save operation finishes.
        /// Params: slotIndex, isSuccess
        /// </summary>
        public static event Action<int, bool> OnSaveCompleted;
        public static void TriggerSaveCompleted(int slotIndex, bool success)
            => OnSaveCompleted?.Invoke(slotIndex, success);

        /// <summary>
        /// Triggered when a load operation finishes.
        /// Params: slotIndex, isSuccess
        /// </summary>
        public static event Action<int, bool> OnLoadCompleted;
        public static void TriggerLoadCompleted(int slotIndex, bool success)
            => OnLoadCompleted?.Invoke(slotIndex, success);
    }

    // ==========================================
    // BATTLE EVENTS
    // ==========================================
    public static class Battle
    {
        /// <summary>
        /// Request to initiate a battle from an overworld physical collision.
        /// Params: enemyObj, spawnZoneObj, encounterType
        /// </summary>
        public static event Action<GameObject, GameObject, EncounterType> OnRequestCollisionBattle;
        public static void RequestCollisionBattle(GameObject enemyObj, GameObject spawnZoneObj, EncounterType encounterType)
            => OnRequestCollisionBattle?.Invoke(enemyObj, spawnZoneObj, encounterType);

        /// <summary>
        /// Request to initiate a scripted battle from dialogue, cutscenes, boss triggers, or chest traps.
        /// Params: encounterID / preSetEnemies, isBossBattle
        /// </summary>
        public static event Action<List<GameObject>, bool> OnRequestEventBattle;
        public static void RequestEventBattle(List<GameObject> preSetEnemies, bool isBossBattle = false)
            => OnRequestEventBattle?.Invoke(preSetEnemies, isBossBattle);
    }

    // ==========================================
    // FIGHTER & STATS EVENTS
    // ==========================================
    public static class Fighter
    {
        // --- Resource Changes (UI & Status) ---

        /// <summary>
        /// Triggered whenever a fighter's Health Points (HP) change.
        /// Params: fighterObj, currentHp, maxHp
        /// </summary>
        public static event Action<GameObject, int, int> OnHpChanged;
        public static void TriggerHpChanged(GameObject fighterObj, int currentHp, int maxHp)
            => OnHpChanged?.Invoke(fighterObj, currentHp, maxHp);

        /// <summary>
        /// Triggered whenever a fighter's Psynergy Points (PP) change.
        /// Params: fighterObj, currentPp, maxPp
        /// </summary>
        public static event Action<GameObject, int, int> OnPpChanged;
        public static void TriggerPpChanged(GameObject fighterObj, int currentPp, int maxPp)
            => OnPpChanged?.Invoke(fighterObj, currentPp, maxPp);

        // --- Impact & Damage Events (Floating Text, Animations & SFX) ---

        /// <summary>
        /// Triggered when a fighter receives damage in battle or overworld.
        /// Params: fighterObj, amount, isCritical, elementalType
        /// </summary>
        public static event Action<GameObject, int, bool, ElementalType> OnDamaged;
        public static void TriggerDamaged(GameObject fighterObj, int amount, bool isCritical, ElementalType element)
            => OnDamaged?.Invoke(fighterObj, amount, isCritical, element);

        /// <summary>
        /// Triggered when a fighter is restored HP through healing items or Psynergy.
        /// Params: fighterObj, amount
        /// </summary>
        public static event Action<GameObject, int> OnHealed;
        public static void TriggerHealed(GameObject fighterObj, int amount)
            => OnHealed?.Invoke(fighterObj, amount);

        // --- Life & Death States ---

        /// <summary>
        /// Triggered when a fighter reaches 0 HP and enters the downed/knocked out state.
        /// </summary>
        public static event Action<GameObject> OnFighterDied;
        public static void TriggerFighterDied(GameObject fighterObj)
            => OnFighterDied?.Invoke(fighterObj);

        /// <summary>
        /// Triggered when a downed fighter is revived back to life.
        /// </summary>
        public static event Action<GameObject> OnFighterRevived;
        public static void TriggerFighterRevived(GameObject fighterObj)
            => OnFighterRevived?.Invoke(fighterObj);

        // --- Status Effects & Modifiers ---

        /// <summary>
        /// Triggered when an active status effect (poison, stun, buff, etc.) is applied to a fighter.
        /// </summary>
        public static event Action<GameObject, ActiveStatusEffect> OnStatusEffectAdded;
        public static void TriggerStatusEffectAdded(GameObject fighterObj, ActiveStatusEffect activeEffect)
            => OnStatusEffectAdded?.Invoke(fighterObj, activeEffect);

        /// <summary>
        /// Triggered when an active status effect is removed or expires from a fighter.
        /// </summary>
        public static event Action<GameObject, ActiveStatusEffect> OnStatusEffectRemoved;
        public static void TriggerStatusEffectRemoved(GameObject fighterObj, ActiveStatusEffect activeEffect)
            => OnStatusEffectRemoved?.Invoke(fighterObj, activeEffect);

        /// <summary>
        /// Triggered when all active status effects on a fighter are completely purged.
        /// </summary>
        public static event Action<GameObject> OnStatusEffectsCleared;
        public static void TriggerStatusEffectsCleared(GameObject fighterObj)
            => OnStatusEffectsCleared?.Invoke(fighterObj);
    }

    // ==========================================
    // HERO & EQUIPMENT EVENTS
    // ==========================================
    public static class Hero
    {
        /// <summary>
        /// Triggered whenever a hero changes their equipped items or relics.
        /// Params: heroGameObject
        /// </summary>
        public static event Action<GameObject> OnEquipmentChanged;
        public static void TriggerEquipmentChanged(GameObject hero)
            => OnEquipmentChanged?.Invoke(hero);

        /// <summary>
        /// Triggered when hero stats or overall status update (damage, healing, level up, class change, etc.).
        /// UI elements subscribe to this to refresh party info panels (HP, PP, Level, Icons).
        /// </summary>
        public static event Action OnHeroStatusChanged;
        public static void TriggerHeroStatusChanged()
            => OnHeroStatusChanged?.Invoke();

        /// <summary>
        /// Requests the portrait Sprite for a specific hero by name/identifier.
        /// Listened by GameManager or persistent hero systems.
        /// Params: heroName, callbackReturningSprite
        /// </summary>
        public static event Action<string, Action<Sprite>> OnRequestHeroPortrait;
        public static void RequestHeroPortrait(string heroName, Action<Sprite> callback)
            => OnRequestHeroPortrait?.Invoke(heroName, callback);
    }

    // ==========================================
    // PLAYER AVATAR & PHYSICS EVENTS
    // ==========================================
    public static class Player
    {
        /// <summary>
        /// Triggered when an object requests the player to perform an interaction animation.
        /// </summary>
        public static event Action<PlayerInteractionAnimType> OnPlayInteractionAnimation;
        public static void TriggerPlayInteractionAnimation(PlayerInteractionAnimType animType)
            => OnPlayInteractionAnimation?.Invoke(animType);

        /// <summary>
        /// Request to relocate/teleport the player avatar to a target position and rotation safely.
        /// </summary>
        public static event Action<Vector3, Quaternion> OnRequestTeleport;
        public static void RequestTeleport(Vector3 position, Quaternion rotation)
            => OnRequestTeleport?.Invoke(position, rotation);

        /// <summary>
        /// Request the player avatar to smoothly rotate and face toward a target position in the world.
        /// Params: targetPosition, duration
        /// </summary>
        public static event Action<Vector3, float> OnRequestLookAt;
        public static void RequestLookAt(Vector3 targetPosition, float duration = 0.25f)
            => OnRequestLookAt?.Invoke(targetPosition, duration);

        /// <summary>
        /// Request the player avatar to automatically walk toward a target destination.
        /// </summary>
        public static event Action<Vector3> OnRequestAutoWalk;
        public static void RequestAutoWalk(Vector3 destination)
            => OnRequestAutoWalk?.Invoke(destination);

        /// <summary>
        /// Request to stop any active auto-walk operation on the player avatar.
        /// </summary>
        public static event Action OnRequestStopAutoWalk;
        public static void RequestStopAutoWalk()
            => OnRequestStopAutoWalk?.Invoke();

        /// <summary>
        /// Request to toggle the visibility of the player model mesh.
        /// </summary>
        public static event Action<bool> OnSetPlayerVisibilityRequested;
        public static void RequestSetPlayerVisibility(bool visible)
            => OnSetPlayerVisibilityRequested?.Invoke(visible);

        /// <summary>
        /// Triggered whenever the player's stamina changes during exploration.
        /// Params: currentStamina, maxStamina
        /// </summary>
        public static event Action<float, float> OnStaminaChanged;
        public static void TriggerStaminaChanged(float currentStamina, float maxStamina)
            => OnStaminaChanged?.Invoke(currentStamina, maxStamina);

        /// <summary>
        /// Triggered whenever the player enters or exits the First-Person View state.
        /// Params: isFirstPersonView
        /// </summary>
        public static event Action<bool> OnFirstPersonViewStateChanged;
        public static void TriggerFirstPersonViewChanged(bool isFirstPersonView)
            => OnFirstPersonViewStateChanged?.Invoke(isFirstPersonView);
    }

    // ==========================================
    // AUDIO & SOUND EVENTS
    // ==========================================
    public static class Audio
    {
        // --- Sound Effects (SFX 2D) ---

        /// <summary>
        /// Request to play a sound effect using its database enum identifier (2D UI/Global).
        /// </summary>
        public static event Action<SFXType> OnPlaySFXRequested;
        public static void RequestPlaySFX(SFXType type)
            => OnPlaySFXRequested?.Invoke(type);

        /// <summary>
        /// Request to play an explicit AudioClip directly with optional volume and pitch scaling (2D UI/Global).
        /// Params: clip, volume, pitch
        /// </summary>
        public static event Action<AudioClip, float, float> OnPlaySFXClipRequested;
        public static void RequestPlaySFXClip(AudioClip clip, float volume = 1.0f, float pitch = 1.0f)
            => OnPlaySFXClipRequested?.Invoke(clip, volume, pitch);

        // --- Spatialized Sound Effects (SFX 3D Object Pool) ---

        /// <summary>
        /// Request to play a sound effect in 3D world space using its database enum identifier.
        /// Params: type, position
        /// </summary>
        public static event Action<SFXType, Vector3> OnPlaySFXAtPositionRequested;
        public static void RequestPlaySFXAtPosition(SFXType type, Vector3 position)
            => OnPlaySFXAtPositionRequested?.Invoke(type, position);

        /// <summary>
        /// Request to play an explicit AudioClip in 3D world space at a given position.
        /// Params: clip, position, volume, pitch
        /// </summary>
        public static event Action<AudioClip, Vector3, float, float> OnPlaySFXClipAtPositionRequested;
        public static void RequestPlaySFXClipAtPosition(AudioClip clip, Vector3 position, float volume = 1.0f, float pitch = 1.0f)
            => OnPlaySFXClipAtPositionRequested?.Invoke(clip, position, volume, pitch);

        // --- Background Music (BGM) & Music States ---

        /// <summary>
        /// Request to play a BGM track assigned to a specific scene name in the AudioDatabase.
        /// </summary>
        public static event Action<string> OnPlayBGMForSceneRequested;
        public static void RequestPlayBGMForScene(string sceneName)
            => OnPlayBGMForSceneRequested?.Invoke(sceneName);

        /// <summary>
        /// Request to play a specific BGM AudioClip directly with custom target volume and fade duration.
        /// </summary>
        public static event Action<AudioClip, float, float, float> OnPlayBGMRequested;
        public static void RequestPlayBGM(AudioClip clip, float targetVolume = 1.0f, float fadeDuration = 1.0f, float startTime = 0f)
            => OnPlayBGMRequested?.Invoke(clip, targetVolume, fadeDuration, startTime);

        /// <summary>
        /// Request to stop the currently active BGM with a fade duration in seconds.
        /// </summary>
        public static event Action<float> OnStopBGMRequested;
        public static void RequestStopBGM(float fadeDuration = 1.0f)
            => OnStopBGMRequested?.Invoke(fadeDuration);

        /// <summary>
        /// Request to push current BGM to state stack and play battle theme (normal or boss).
        /// Params: isBossBattle, fadeDuration
        /// </summary>
        public static event Action<bool, float> OnPlayBattleBGMRequested;
        public static void RequestPlayBattleBGM(bool isBossBattle = false, float fadeDuration = 0.5f)
            => OnPlayBattleBGMRequested?.Invoke(isBossBattle, fadeDuration);

        /// <summary>
        /// Request to play victory theme (intro + loop scheduling supported).
        /// </summary>
        public static event Action<float> OnPlayVictoryBGMRequested;
        public static void RequestPlayVictoryBGM(float fadeDuration = 0.5f)
            => OnPlayVictoryBGMRequested?.Invoke(fadeDuration);

        /// <summary>
        /// Request to play Game Over theme.
        /// </summary>
        public static event Action<float> OnPlayGameOverBGMRequested;
        public static void RequestPlayGameOverBGM(float fadeDuration = 1.0f)
            => OnPlayGameOverBGMRequested?.Invoke(fadeDuration);

        /// <summary>
        /// Request to pop previous BGM state from stack and resume exploration/event music from its exact timestamp.
        /// </summary>
        public static event Action<float> OnResumeBGMRequested;
        public static void RequestResumeBGM(float fadeDuration = 1.0f)
            => OnResumeBGMRequested?.Invoke(fadeDuration);

        // --- Dynamic Volume Settings (Options Menu) ---

        /// <summary>
        /// Request to adjust master BGM volume scale (0.0 to 1.0) in real time.
        /// </summary>
        public static event Action<float> OnSetBGMVolumeRequested;
        public static void RequestSetBGMVolume(float volume)
            => OnSetBGMVolumeRequested?.Invoke(volume);

        /// <summary>
        /// Request to adjust master SFX volume scale (0.0 to 1.0) in real time.
        /// </summary>
        public static event Action<float> OnSetSFXVolumeRequested;
        public static void RequestSetSFXVolume(float volume)
            => OnSetSFXVolumeRequested?.Invoke(volume);
    }

    // ==========================================
    // INVENTORY & ITEM EVENTS
    // ==========================================
    public static class Inventory
    {
        /// <summary>
        /// Request to add an item to the inventory using its ScriptableObject reference. 
        /// Sends ItemSO, quantity and a callback action returning the remaining items count (0 = success, >0 = overflow).
        /// </summary>
        public static event Action<ItemSO, int, Action<int>> OnRequestAddItem;
        public static void RequestAddItem(ItemSO item, int quantity, Action<int> onResult)
            => OnRequestAddItem?.Invoke(item, quantity, onResult);

        /// <summary>
        /// Request to remove an item from the inventory using its ScriptableObject reference.
        /// Sends ItemSO, quantity and a callback action returning success (true/false).
        /// </summary>
        public static event Action<ItemSO, int, Action<bool>> OnRequestRemoveItem;
        public static void RequestRemoveItem(ItemSO item, int quantity, Action<bool> onResult)
            => OnRequestRemoveItem?.Invoke(item, quantity, onResult);

        /// <summary>
        /// Request to clear all items across all hero inventories and global bags.
        /// </summary>
        public static event Action OnClearAllInventoriesRequested;
        public static void RequestClearAllInventories() => OnClearAllInventoriesRequested?.Invoke();

        /// <summary>
        /// Check if the inventory contains a specific amount of an item using its ScriptableObject reference.
        /// </summary>
        public static event Action<ItemSO, int, Action<bool>> OnCheckHasItem;
        public static void CheckHasItem(ItemSO item, int amount, Action<bool> onResult)
            => OnCheckHasItem?.Invoke(item, amount, onResult);

        // TESTING

        /// <summary>
        /// Debug testing request to populate inventories with all items.
        /// </summary>
        public static event Action OnAllItemsRequested;
        public static void RequestAllItems() => OnAllItemsRequested?.Invoke();

        // ==========================================
        // GLOBAL BAG & KEY ITEM EVENTS
        // ==========================================

        /// <summary>
        /// Request to add a KeyItem to the global bag.
        /// Sends ItemSO, quantity and a callback action returning the remaining overflow count (0 = success).
        /// </summary>
        public static event Action<ItemSO, int, Action<int>> OnRequestAddKeyItem;
        public static void RequestAddKeyItem(ItemSO item, int quantity, Action<int> onResult)
            => OnRequestAddKeyItem?.Invoke(item, quantity, onResult);

        /// <summary>
        /// Request to remove a KeyItem from the global bag.
        /// Sends ItemSO, quantity and a callback action returning success (true/false).
        /// </summary>
        public static event Action<ItemSO, int, Action<bool>> OnRequestRemoveKeyItem;
        public static void RequestRemoveKeyItem(ItemSO item, int quantity, Action<bool> onResult)
            => OnRequestRemoveKeyItem?.Invoke(item, quantity, onResult);

        /// <summary>
        /// Query if the global bag contains a specific amount of a KeyItem.
        /// Sends ItemSO, required quantity and a callback action returning whether it is possessed (true/false).
        /// </summary>
        public static event Action<ItemSO, int, Action<bool>> OnCheckHasKeyItem;
        public static void CheckHasKeyItem(ItemSO item, int amount, Action<bool> onResult)
            => OnCheckHasKeyItem?.Invoke(item, amount, onResult);

        /// <summary>
        /// Query the total owned quantity of a specific KeyItem in the global bag.
        /// Sends ItemSO and a callback returning the total count.
        /// </summary>
        public static event Action<ItemSO, Action<int>> OnRequestKeyItemQuantity;
        public static void RequestKeyItemQuantity(ItemSO item, Action<int> onResult)
            => OnRequestKeyItemQuantity?.Invoke(item, onResult);

        /// <summary>
        /// Query to retrieve the complete read-only list of key item slots from the global bag.
        /// Sends a callback action returning IReadOnlyList<InventorySlot>.
        /// </summary>
        public static event Action<Action<IReadOnlyList<InventorySlot>>> OnRequestKeyItemSlots;
        public static void RequestKeyItemSlots(Action<IReadOnlyList<InventorySlot>> onResult)
            => OnRequestKeyItemSlots?.Invoke(onResult);

        /// <summary>
        /// Broadcast event triggered whenever the global key item bag contents are modified.
        /// </summary>
        public static event Action OnGlobalBagUpdated;
        public static void TriggerGlobalBagUpdated() => OnGlobalBagUpdated?.Invoke();
    }

    // ==========================================
    // UI & HUD EVENTS
    // ==========================================
    public static class UI
    {
        /// <summary>
        /// Triggered when a new zone or area banner should be displayed on screen.
        /// Params: zoneName, regionName, displayDuration
        /// </summary>
        public static event Action<string, string, float> OnShowZoneBanner;
        public static void TriggerShowZoneBanner(string zoneName, string regionName, float duration = 3.5f)
            => OnShowZoneBanner?.Invoke(zoneName, regionName, duration);

        /// <summary>
        /// Triggered to push rich notifications to the UI stream (Warning, ItemObtained, Info, etc.).
        /// Params: type, message, optional icon
        /// </summary>
        public static event Action<NotificationType, string, Sprite> OnShowNotification;
        public static void TriggerShowNotification(NotificationType type, string message, Sprite icon = null)
            => OnShowNotification?.Invoke(type, message, icon);

        /// <summary>
        /// Request to show or hide the aiming reticle UI overlay.
        /// </summary>
        public static event Action<bool> OnSetFirstPersonViewUIVisibleRequested;
        public static void RequestSetFirstPersonUIVisible(bool visible)
            => OnSetFirstPersonViewUIVisibleRequested?.Invoke(visible);

        /// <summary>
        /// Request to initiate a dialogue conversation using DialogueSO.
        /// Params: dialogueData, defaultSpeakerName, speakerGameObject, defaultPortrait
        /// </summary>
        public static event Action<DialogueSO, string, GameObject, Sprite> OnStartDialogueRequested;
        public static void RequestStartDialogue(DialogueSO dialogue, string speakerName, GameObject speakerObj = null, Sprite portrait = null)
            => OnStartDialogueRequested?.Invoke(dialogue, speakerName, speakerObj, portrait);

        /// <summary>
        /// Request to update active dialogue UI text, speaker name, portrait frame and optional choices.
        /// Params: speakerName, text, portrait, choiceType, choicesList
        /// </summary>
        public static event Action<string, string, Sprite, DialogueChoiceType, List<DialogueChoiceData>> OnUpdateDialogueRequested;
        public static void RequestUpdateDialogue(string speakerName, string text, Sprite portrait = null, DialogueChoiceType choiceType = DialogueChoiceType.StandardText, List<DialogueChoiceData> choices = null)
            => OnUpdateDialogueRequested?.Invoke(speakerName, text, portrait, choiceType, choices);

        /// <summary>
        /// Request sent by UI elements when the player selects a dialogue choice index.
        /// Listened by DialogueManager.
        /// </summary>
        public static event Action<int> OnSelectDialogueChoiceRequested;
        public static void RequestSelectDialogueChoice(int choiceIndex)
            => OnSelectDialogueChoiceRequested?.Invoke(choiceIndex);

        // ==========================================
        // SAVE / LOAD BOOK UI EVENTS
        // ==========================================
        public enum SaveBookMode
        {
            Save,
            Load
        }

        public static event Action<SaveBookMode> OnOpenSaveLoadBookRequested;
        public static void RequestOpenSaveLoadBook(SaveBookMode mode)
            => OnOpenSaveLoadBookRequested?.Invoke(mode);
    }

    // ==========================================
    // ENVIRONMENT & EXPLORATION EVENTS
    // ==========================================
    public static class Environment
    {
        /// <summary>
        /// Triggered when entering a new area or sub-zone volume in the scene.
        /// </summary>
        public static event Action<string> OnZoneEntered;
        public static void TriggerZoneEntered(string zoneID)
            => OnZoneEntered?.Invoke(zoneID);
    }

    // ==========================================
    // PSYNERGY & WORLD INTERACTIONS
    // ==========================================
    public static class Psynergy
    {
        /// <summary>
        /// Triggered when a hero loses access to a Psynergy (e.g. class change, unequipped relic, Djinn state swap).
        /// Params: heroGameObject, lostPsynergySO
        /// </summary>
        public static event Action<GameObject, PsynergySO> OnPsynergyLost;
        public static void TriggerPsynergyLost(GameObject hero, PsynergySO psynergy)
            => OnPsynergyLost?.Invoke(hero, psynergy);

        /// <summary>
        /// Triggered when the player casts a Psynergy ability in the world.
        /// Params: psynergySlug, casterTransform
        /// </summary>
        public static event Action<string, Transform> OnWorldPsynergyCast;
        public static void TriggerWorldPsynergyCast(string psynergySlug, Transform caster)
            => OnWorldPsynergyCast?.Invoke(psynergySlug, caster);

        /// <summary>
        /// Request to assign or clear a Psynergy shortcut slot for a specific hero.
        /// Params: heroTarget, psynergySO, slotIndex (0..3)
        /// </summary>
        public static event Action<GameObject, PsynergySO, int> OnAssignShortcutRequested;
        public static void RequestAssignShortcut(GameObject hero, PsynergySO psynergy, int slotIndex)
            => OnAssignShortcutRequested?.Invoke(hero, psynergy, slotIndex);

        /// <summary>
        /// Request to retrieve assigned Psynergy shortcuts array for a hero via callback.
        /// Params: heroTarget, callbackReturningShortcuts
        /// </summary>
        public static event Action<GameObject, Action<PsynergySO[]>> OnGetShortcutsRequested;
        public static void RequestGetShortcuts(GameObject hero, Action<PsynergySO[]> onResult)
            => OnGetShortcutsRequested?.Invoke(hero, onResult);

        /// <summary>
        /// Request to execute the Psynergy assigned to a D-Pad shortcut slot.
        /// Params: slotIndex (0..3), targetObject
        /// </summary>
        public static event Action<int, IPsynergyTargetable> OnUseShortcutRequested;
        public static void RequestUseShortcut(int slotIndex, IPsynergyTargetable target)
            => OnUseShortcutRequested?.Invoke(slotIndex, target);

        /// <summary>
        /// Triggered when shortcuts or assigned Psynergies update on a hero.
        /// Params: heroTarget, shortcutsArray
        /// </summary>
        public static event Action<GameObject, PsynergySO[]> OnShortcutsChanged;
        public static void TriggerShortcutsChanged(GameObject hero, PsynergySO[] shortcuts)
            => OnShortcutsChanged?.Invoke(hero, shortcuts);
    }

    // ==========================================
    // CUTSCENE & STORY EVENTS
    // ==========================================
    public static class Cutscene
    {
        /// <summary>
        /// Triggered when a scripted cutscene sequence starts.
        /// </summary>
        public static event Action<string> OnCutsceneStart;

        /// <summary>
        /// Triggered when a scripted cutscene sequence ends.
        /// </summary>
        public static event Action<string> OnCutsceneEnd;
    }
}