using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Persistent MonoBehaviour service bridging SaveManager static operations and auto-save routines 
/// to the decoupled GameEvents event bus.
/// Handles UI-free auto-saves, manual slot saves with screenshots, and load requests.
/// Attach this component to a persistent Manager GameObject in your starting scene.
/// </summary>
public class SaveManagerService : MonoBehaviour
{
    #region Constants

    public const int AUTOSAVE_SLOT_INDEX = 0;

    #endregion

    #region Unity Lifecycle & Event Subscriptions

    private void OnEnable()
    {
        // Event Subscriptions for Manual Operations
        GameEvents.SaveLoad.OnRequestSaveGame += HandleSaveGameRequested;
        GameEvents.SaveLoad.OnRequestLoadGame += HandleLoadGameRequested;

        // Event Subscription for Auto-Save Routine
        GameEvents.SaveLoad.OnRequestAutoSave += HandleRequestAutoSave;
    }

    private void OnDisable()
    {
        // Event Unsubscriptions for Manual Operations
        GameEvents.SaveLoad.OnRequestSaveGame -= HandleSaveGameRequested;
        GameEvents.SaveLoad.OnRequestLoadGame -= HandleLoadGameRequested;

        // Event Unsubscription for Auto-Save Routine
        GameEvents.SaveLoad.OnRequestAutoSave -= HandleRequestAutoSave;
    }

    #endregion

    #region Manual Event Handlers

    private void HandleSaveGameRequested(int slotIndex)
    {
        if (slotIndex == AUTOSAVE_SLOT_INDEX)
        {
            Debug.LogWarning("[SaveManagerService] Manual save on Slot 0 (AutoSave) is blocked. Use OnRequestAutoSave instead.");
            return;
        }

        Debug.Log($"<color=green>[SaveManagerService] Received OnRequestSaveGame for Slot {slotIndex}. Executing Save...</color>");

        SaveManager.Save(slotIndex);
    }

    private void HandleLoadGameRequested(int slotIndex)
    {
        Debug.Log($"<color=cyan>[SaveManagerService] Received OnRequestLoadGame for Slot {slotIndex}. Executing Load...</color>");
        SaveManager.Load(slotIndex);
    }

    #endregion

    #region AutoSave Execution Pipeline

    private void HandleRequestAutoSave()
    {
        StartCoroutine(Routine_ExecuteAutoSave());
    }

    private IEnumerator Routine_ExecuteAutoSave()
    {
        Debug.Log("<color=yellow>[SaveManagerService] Auto-save request received. Initiating auto-save sequence...</color>");

        // TODO (FASE 2): Trigger "Saving..." UI animation indicator (e.g., animated Djinn icon in corner)
        GameEvents.UI.TriggerShowStatus("Cargando...", StatusIconType.Djinn);

        // 1. Wait until physics and Cinemachine camera settle after player relocation
        yield return new WaitForEndOfFrame();
        yield return new WaitForFixedUpdate();
        yield return new WaitForEndOfFrame();

        // 2. Capture clean, UI-free thumbnail from Main Camera
        string thumbnailBase64 = string.Empty;

        // Llamamos a la rutina pasando null como customViewpoint y el callback de captura
        yield return SaveScreenCapture.Routine_CaptureSaveThumbnail(null, capturedBase64 =>
        {
            thumbnailBase64 = capturedBase64;
        });

        // 3. Transient handler to inject thumbnail and auto-save flag during collection
        Action<GameData> autoSaveCollectHandler = null;
        autoSaveCollectHandler = (dataToSave) =>
        {
            if (dataToSave != null && dataToSave.header != null)
            {
                dataToSave.header.saveThumbnailBase64 = thumbnailBase64;
                if (dataToSave.worldState != null)
                {
                    dataToSave.worldState.isAutoSave = true;
                }
            }
            GameEvents.SaveLoad.OnCollectSaveData -= autoSaveCollectHandler;
        };

        GameEvents.SaveLoad.OnCollectSaveData += autoSaveCollectHandler;

        // 4. Save file to disk in Slot 0
        bool success = SaveManager.Save(AUTOSAVE_SLOT_INDEX);

        if (success)
        {
            Debug.Log($"<color=green>[SaveManagerService] Auto-save successfully written to Slot {AUTOSAVE_SLOT_INDEX}.</color>");
        }
        else
        {
            Debug.LogError($"[SaveManagerService] Critical error writing auto-save to Slot {AUTOSAVE_SLOT_INDEX}.");
        }

        // TODO (FASE 2): Hide "Saving..." UI animation indicator
        GameEvents.UI.TriggerHideStatus();
    }

    #endregion
}