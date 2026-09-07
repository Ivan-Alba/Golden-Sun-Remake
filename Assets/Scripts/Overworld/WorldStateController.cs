using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the runtime state of all world flags, open chests, solved puzzles, and story triggers.
/// Decoupled via GameEvents.World and handles persistence via GameEvents.SaveLoad.
/// Tracks the latest active chronicle flag for header metadata rendering.
/// </summary>
public class WorldStateController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Default Story Settings")]
    [Tooltip("Default prologue flag used when no other story flags have been activated.")]
    [SerializeField] private FlagSO defaultChronicleFlag;

    #endregion

    #region Private Fields

    // Runtime state tracking flags by their unique FlagSlug
    private readonly Dictionary<string, bool> flagMap = new Dictionary<string, bool>();

    // Tracks the most recently activated FlagSO slug that contains chronicle updates
    private string lastChronicleFlagSlug = string.Empty;

    #endregion

    #region Unity Lifecycle & Event Subscriptions

    private void OnEnable()
    {
        // World Domain Subscriptions
        GameEvents.World.OnSetFlagRequested += HandleSetFlagRequested;
        GameEvents.World.OnCheckFlagRequested += HandleCheckFlagRequested;

        // Save & Load Event Subscriptions
        GameEvents.SaveLoad.OnCollectSaveData += HandleCollectSaveData;
        GameEvents.SaveLoad.OnRestoreSaveData += HandleRestoreSaveData;
    }

    private void OnDisable()
    {
        // World Domain Unsubscriptions
        GameEvents.World.OnSetFlagRequested -= HandleSetFlagRequested;
        GameEvents.World.OnCheckFlagRequested -= HandleCheckFlagRequested;

        // Save & Load Event Unsubscriptions
        GameEvents.SaveLoad.OnCollectSaveData -= HandleCollectSaveData;
        GameEvents.SaveLoad.OnRestoreSaveData -= HandleRestoreSaveData;
    }

    #endregion

    #region Event Handlers

    private void HandleSetFlagRequested(FlagSO flag, bool value)
    {
        SetFlag(flag, value);
    }

    private void HandleCheckFlagRequested(FlagSO flag, System.Action<bool> onResult)
    {
        bool value = GetFlag(flag);
        onResult?.Invoke(value);
    }

    #endregion

    #region Flag Mutation & Query API

    public void SetFlag(FlagSO flag, bool value)
    {
        if (flag == null || string.IsNullOrEmpty(flag.FlagSlug)) return;

        bool stateChanged = false;

        if (flagMap.ContainsKey(flag.FlagSlug))
        {
            if (flagMap[flag.FlagSlug] != value)
            {
                flagMap[flag.FlagSlug] = value;
                stateChanged = true;
                Debug.Log($"<color=yellow>[WorldStateController] Flag '{flag.FlagSlug}' updated to: {value}</color>");
            }
        }
        else
        {
            flagMap.Add(flag.FlagSlug, value);
            stateChanged = true;
            Debug.Log($"<color=yellow>[WorldStateController] Flag '{flag.FlagSlug}' initialized to: {value}</color>");
        }

        if (stateChanged)
        {
            // If the flag is set to true and has chronicle metadata, update the last chronicle flag reference
            if (value && flag.UpdatesChronicle)
            {
                lastChronicleFlagSlug = flag.FlagSlug;
                Debug.Log($"<color=cyan>[WorldStateController] Last Chronicle Flag updated to: '{lastChronicleFlagSlug}'</color>");
            }

            GameEvents.World.TriggerFlagChanged(flag, value);
        }
    }

    public bool GetFlag(FlagSO flag)
    {
        if (flag == null || string.IsNullOrEmpty(flag.FlagSlug)) return false;

        if (flagMap.TryGetValue(flag.FlagSlug, out bool value))
        {
            return value;
        }

        return flag.DefaultValue;
    }

    #endregion

    #region Save & Load Event Handlers

    private void HandleCollectSaveData(GameData saveData)
    {
        if (saveData == null) return;

        // 1. Persist runtime flag states
        saveData.gameFlags.Clear();

        foreach (var kvp in flagMap)
        {
            saveData.gameFlags.Add(new GameFlagData(kvp.Key, kvp.Value));
        }

        // 2. Resolve active chronicle flag slug
        string targetChronicleSlug = lastChronicleFlagSlug;

        if (string.IsNullOrEmpty(targetChronicleSlug) && defaultChronicleFlag != null)
        {
            targetChronicleSlug = defaultChronicleFlag.FlagSlug;
        }

        // 3. Persist the last chronicle flag slug into header metadata
        if (saveData.header != null)
        {
            saveData.header.lastChronicleFlagSlug = targetChronicleSlug;
        }

        Debug.Log($"<color=green>[WorldStateController] Collected save data for {saveData.gameFlags.Count} world flag(s). Last Chronicle Flag: '{targetChronicleSlug}'</color>");
    }

    private void HandleRestoreSaveData(GameData saveData)
    {
        if (saveData == null || saveData.gameFlags == null) return;

        flagMap.Clear();

        // 1. Restore runtime flag states
        for (int i = 0; i < saveData.gameFlags.Count; i++)
        {
            GameFlagData flagData = saveData.gameFlags[i];
            if (flagData == null || string.IsNullOrEmpty(flagData.key)) continue;

            flagMap[flagData.key] = flagData.state;
        }

        // 2. Restore last chronicle flag slug reference
        if (saveData.header != null)
        {
            lastChronicleFlagSlug = saveData.header.lastChronicleFlagSlug ?? string.Empty;
        }

        Debug.Log($"<color=cyan>[WorldStateController] Successfully restored {flagMap.Count} world flags from save data. Restored Chronicle Flag: '{lastChronicleFlagSlug}'</color>");
    }

    #endregion
}