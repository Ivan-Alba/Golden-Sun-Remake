using System;
using UnityEngine;

public static class GameEvents
{
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
        /// Triggered to push generic notifications to the UI stream (e.g., "Obtained 50 Gold", "Found Healing Herb").
        /// </summary>
        public static event Action<string> OnShowNotification;
        public static void TriggerShowNotification(string message)
            => OnShowNotification?.Invoke(message);
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
    // PSYNERGY & WORLD INTERACTIONS (Future Expansion)
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
    // CUTSCENE & STORY EVENTS (Future Expansion)
    // ==========================================
    public static class Cutscene
    {
        public static event Action<string> OnCutsceneStart;
        public static event Action<string> OnCutsceneEnd;
    }
}