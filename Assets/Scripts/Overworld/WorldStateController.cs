using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the runtime state of all world flags, open chests, solved puzzles, and story triggers.
/// Decoupled via GameEvents.World and handles persistence via GameEvents.SaveLoad.
/// </summary>
public class WorldStateController : MonoBehaviour
{
    #region Private Fields

    // Runtime state tracking flags by their unique FlagSlug
    private readonly Dictionary<string, bool> flagMap = new Dictionary<string, bool>();

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

        if (flagMap.ContainsKey(flag.FlagSlug))
        {
            if (flagMap[flag.FlagSlug] != value)
            {
                flagMap[flag.FlagSlug] = value;
                GameEvents.World.TriggerFlagChanged(flag, value);
                Debug.Log($"<color=yellow>[WorldStateController] Flag '{flag.FlagSlug}' updated to: {value}</color>");
            }
        }
        else
        {
            flagMap.Add(flag.FlagSlug, value);
            GameEvents.World.TriggerFlagChanged(flag, value);
            Debug.Log($"<color=yellow>[WorldStateController] Flag '{flag.FlagSlug}' initialized to: {value}</color>");
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

        saveData.gameFlags.Clear();

        foreach (var kvp in flagMap)
        {
            saveData.gameFlags.Add(new GameFlagData(kvp.Key, kvp.Value));
        }

        Debug.Log($"<color=green>[WorldStateController] Collected save data for {saveData.gameFlags.Count} world flag(s).</color>");
    }

    private void HandleRestoreSaveData(GameData saveData)
    {
        if (saveData == null || saveData.gameFlags == null) return;

        flagMap.Clear();

        for (int i = 0; i < saveData.gameFlags.Count; i++)
        {
            GameFlagData flagData = saveData.gameFlags[i];
            if (flagData == null || string.IsNullOrEmpty(flagData.key)) continue;

            flagMap[flagData.key] = flagData.state;
        }

        Debug.Log($"<color=cyan>[WorldStateController] Successfully restored {flagMap.Count} world flags from save data.</color>");
    }

    #endregion
}