using System;
using UnityEngine;

public static class GameEvents
{
    // ==========================================
    // GAMEPLAY & GAME STATE EVENTS
    // ==========================================
    public static class GameState
    {
        // 1. COMMANDS / REQUESTS (Entidades piden al GameManager)
        /// <summary>
        /// Request to change the current game state. Listened exclusively by GameManager.
        /// </summary>
        public static event Action<GameManager.GameState> OnChangeGameStateRequested;
        public static void RequestGameStateChange(GameManager.GameState newState)
            => OnChangeGameStateRequested?.Invoke(newState);

        // 2. NOTIFICATIONS / EVENTS (GameManager informa al mundo)
        /// <summary>
        /// Triggered AFTER the GameManager has validated and officially changed the state.
        /// </summary>
        public static event Action<GameManager.GameState, GameManager.GameState> OnGameStateChanged;
        public static void TriggerGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
            => OnGameStateChanged?.Invoke(previousState, newState);
    }

    // ==========================================
    // PLAYER EVENTS
    // ==========================================
    public static class Player
    {
        /// <summary>
        /// Triggered when an object requests the player to perform an interaction animation.
        /// Params: animType (e.g., OpenChest, PickFloorItem, TouchObject)
        /// </summary>
        public static event Action<PlayerInteractionAnimType> OnPlayInteractionAnimation;
        public static void TriggerPlayInteractionAnimation(PlayerInteractionAnimType animType)
            => OnPlayInteractionAnimation?.Invoke(animType);

        /// <summary>
        /// Request to relocate/teleport the player to a target position and rotation safely.
        /// Params: targetPosition, targetRotation
        /// </summary>
        public static event Action<Vector3, Quaternion> OnRequestTeleport;
        public static void RequestTeleport(Vector3 position, Quaternion rotation)
            => OnRequestTeleport?.Invoke(position, rotation);

        /// <summary>
        /// Request the player to automatically walk toward a target destination.
        /// Params: destinationPosition
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
    }

    // ==========================================
    // AUDIO & SOUND EVENTS
    // ==========================================
    public static class Audio
    {
        /// <summary>
        /// Request to play a sound effect using its database enum identifier.
        /// </summary>
        public static event Action<SFXType> OnPlaySFXRequested;
        public static void RequestPlaySFX(SFXType type)
            => OnPlaySFXRequested?.Invoke(type);

        /// <summary>
        /// Request to play an explicit AudioClip directly with optional volume scaling.
        /// </summary>
        public static event Action<AudioClip, float> OnPlaySFXClipRequested;
        public static void RequestPlaySFXClip(AudioClip clip, float volume = 1.0f)
            => OnPlaySFXClipRequested?.Invoke(clip, volume);
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
        /// Triggered when the player casts a Psynergy ability in the world.
        /// Params: psynergySlug, casterTransform
        /// </summary>
        public static event Action<string, Transform> OnWorldPsynergyCast;
        public static void TriggerWorldPsynergyCast(string psynergySlug, Transform caster)
            => OnWorldPsynergyCast?.Invoke(psynergySlug, caster);
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