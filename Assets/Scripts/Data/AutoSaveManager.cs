using System.Collections;
using UnityEngine;

/// <summary>
/// Autonomous manager listening for auto-save triggers across the game.
/// Handles UI-free thumbnail capture, sets the auto-save flag on GameData, 
/// and executes asynchronous persistent save operations targeting Slot 0.
/// </summary>
public class AutoSaveManager : MonoBehaviour
{
    #region Constants

    public const int AUTOSAVE_SLOT_INDEX = 0;

    #endregion

    #region Unity Lifecycle & Event Subscriptions

    private void OnEnable()
    {
        GameEvents.SaveLoad.OnRequestAutoSave += HandleRequestAutoSave;
    }

    private void OnDisable()
    {
        GameEvents.SaveLoad.OnRequestAutoSave -= HandleRequestAutoSave;
    }

    #endregion

    #region Event Handlers

    private void HandleRequestAutoSave()
    {
        StartCoroutine(Routine_ExecuteAutoSave());
    }

    #endregion

    #region AutoSave Execution Pipeline

    private IEnumerator Routine_ExecuteAutoSave()
    {
        Debug.Log("<color=yellow>[AutoSaveManager] Auto-save request received. Initiating auto-save sequence...</color>");

        // TODO: Trigger "Saving..." UI animation indicator (e.g., animated Djinn icon in corner)

        // 1. Capture clean, UI-free thumbnail from Main Camera
        string thumbnailBase64 = string.Empty;
        Camera mainCam = Camera.main;

        if (mainCam != null)
        {
            yield return SaveScreenCapture.Routine_CaptureSaveThumbnail(mainCam, capturedBase64 =>
            {
                thumbnailBase64 = capturedBase64;
            });
        }
        else
        {
            Debug.LogWarning("[AutoSaveManager] Camera.main is null during auto-save capture. Proceeding without thumbnail.");
        }

        // 2. Prepare GameData container
        GameData autoSaveData = new GameData();
        autoSaveData.header.saveSlotIndex = AUTOSAVE_SLOT_INDEX;
        autoSaveData.header.saveTimestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        autoSaveData.header.saveThumbnailBase64 = thumbnailBase64;
        autoSaveData.worldState.isAutoSave = true;

        // 3. Collect state synchronously across all subscribed systems
        GameEvents.SaveLoad.TriggerCollectSaveData(autoSaveData);

        // 4. Save file to disk in Slot 0
        bool success = SaveManager.Save(AUTOSAVE_SLOT_INDEX);

        if (success)
        {
            Debug.Log($"<color=green>[AutoSaveManager] Auto-save successfully written to Slot {AUTOSAVE_SLOT_INDEX}.</color>");
        }
        else
        {
            Debug.LogError($"[AutoSaveManager] Critical error writing auto-save to Slot {AUTOSAVE_SLOT_INDEX}.");
        }

        // TODO: Hide "Saving..." UI animation indicator
    }

    #endregion
}