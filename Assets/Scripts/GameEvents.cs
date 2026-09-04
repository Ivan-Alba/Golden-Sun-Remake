using System;
using System.Collections.Generic;
using UnityEditor.Localization.Plugins.XLIFF.Common;
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
        /// Triggered when the aiming reticle focuses on or loses a targetable object in the world.
        /// Sends target GameObject and displayable target name (or null when target is lost).
        /// </summary>
        public static event Action<GameObject, string> OnAimTargetChanged;
        public static void TriggerAimTargetChanged(GameObject targetObj, string targetName)
            => OnAimTargetChanged?.Invoke(targetObj, targetName);

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
    }

    // ==========================================
    // SCENE & TRANSITION EVENTS
    // ==========================================
    public static class Scene
    {
        // --- Requests (Driven by Game Logic, Doors, Combat Results) ---

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
        public static event Action<GameObject, int, int> OnHpChanged;
        public static void TriggerHpChanged(GameObject fighterObj, int currentHp, int maxHp)
            => OnHpChanged?.Invoke(fighterObj, currentHp, maxHp);

        public static event Action<GameObject, int, int> OnPpChanged;
        public static void TriggerPpChanged(GameObject fighterObj, int currentPp, int maxPp)
            => OnPpChanged?.Invoke(fighterObj, currentPp, maxPp);

        // --- Impact & Damage Events (Floating Text, Animations & SFX) ---
        public static event Action<GameObject, int, bool, ElementalType> OnDamaged;
        public static void TriggerDamaged(GameObject fighterObj, int amount, bool isCritical, ElementalType element)
            => OnDamaged?.Invoke(fighterObj, amount, isCritical, element);

        public static event Action<GameObject, int> OnHealed;
        public static void TriggerHealed(GameObject fighterObj, int amount)
            => OnHealed?.Invoke(fighterObj, amount);

        // --- Life & Death States ---
        public static event Action<GameObject> OnFighterDied;
        public static void TriggerFighterDied(GameObject fighterObj)
            => OnFighterDied?.Invoke(fighterObj);

        public static event Action<GameObject> OnFighterRevived;
        public static void TriggerFighterRevived(GameObject fighterObj)
            => OnFighterRevived?.Invoke(fighterObj);

        // --- Status Effects & Modifiers ---
        public static event Action<GameObject, ActiveStatusEffect> OnStatusEffectAdded;
        public static void TriggerStatusEffectAdded(GameObject fighterObj, ActiveStatusEffect activeEffect)
            => OnStatusEffectAdded?.Invoke(fighterObj, activeEffect);

        public static event Action<GameObject, ActiveStatusEffect> OnStatusEffectRemoved;
        public static void TriggerStatusEffectRemoved(GameObject fighterObj, ActiveStatusEffect activeEffect)
            => OnStatusEffectRemoved?.Invoke(fighterObj, activeEffect);

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
    }

    // ==========================================
    // PLAYER EVENTS
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
        /// Request to relocate/teleport the player to a target position and rotation safely.
        /// </summary>
        public static event Action<Vector3, Quaternion> OnRequestTeleport;
        public static void RequestTeleport(Vector3 position, Quaternion rotation)
            => OnRequestTeleport?.Invoke(position, rotation);

        /// <summary>
        /// Request the player to smoothly rotate and face toward a target position in the world.
        /// Params: targetPosition, duration
        /// </summary>
        public static event Action<Vector3, float> OnRequestLookAt;
        public static void RequestLookAt(Vector3 targetPosition, float duration = 0.25f)
            => OnRequestLookAt?.Invoke(targetPosition, duration);

        /// <summary>
        /// Request the player to automatically walk toward a target destination.
        /// </summary>
        public static event Action<Vector3> OnRequestAutoWalk;
        public static void RequestAutoWalk(Vector3 destination)
            => OnRequestAutoWalk?.Invoke(destination);

        /// <summary>
        /// Request to stop any active auto-walk operation on the player.
        /// </summary>
        public static event Action OnRequestStopAutoWalk;
        public static void RequestStopAutoWalk()
            => OnRequestStopAutoWalk?.Invoke();

        /// <summary>
        /// Request to change the active hero model and animator controller on the player GameObject.
        /// </summary>
        public static event Action<GameObject> OnActiveHeroChanged;
        public static void RequestActiveHeroChange(GameObject newHero)
            => OnActiveHeroChanged?.Invoke(newHero);

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
        /// Triggered when party stats or status update (active hero swap, damage, healing, level up, etc.).
        /// UI elements subscribe to this to refresh party info panels (HP, PP, Level, Icons).
        /// </summary>
        public static event Action OnHeroStatusChanged;
        public static void TriggerHeroStatusChanged()
            => OnHeroStatusChanged?.Invoke();
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

        public static event Action OnSyncWithPartyRequested;
        public static void RequestSyncWithParty() => OnSyncWithPartyRequested?.Invoke();

        public static event Action OnClearAllInventoriesRequested;
        public static void RequestClearAllInventories() => OnClearAllInventoriesRequested?.Invoke();

        /// <summary>
        /// Check if the inventory contains a specific amount of an item using its ScriptableObject reference.
        /// </summary>
        public static event Action<ItemSO, int, Action<bool>> OnCheckHasItem;
        public static void CheckHasItem(ItemSO item, int amount, Action<bool> onResult)
            => OnCheckHasItem?.Invoke(item, amount, onResult);

        // TESTING
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
        public static event Action<bool> OnSetAimUIVisibleRequested;
        public static void RequestSetAimUIVisible(bool visible)
            => OnSetAimUIVisibleRequested?.Invoke(visible);

        /// <summary>
        /// Request to change the aiming reticle lock-on highlight state.
        /// </summary>
        public static event Action<bool> OnSetAimTargetLockedRequested;
        public static void RequestSetAimTargetLocked(bool locked)
            => OnSetAimTargetLockedRequested?.Invoke(locked);

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
        public static event Action<string> OnCutsceneStart;
        public static event Action<string> OnCutsceneEnd;
    }
}