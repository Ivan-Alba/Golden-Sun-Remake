using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Core persistent storage service handling JSON serialization and deserialization for save files.
/// Fully decoupled from game logic, coordinating save/load sequences exclusively via GameEvents.SaveLoad.
/// </summary>
public static class SaveManager
{
    #region Constants & Paths

    private const string SAVE_FILE_PREFIX = "savedData_slot_";
    private const string SAVE_FILE_EXTENSION = ".json";

    private static string GetSaveFilePath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, $"{SAVE_FILE_PREFIX}{slotIndex}{SAVE_FILE_EXTENSION}");
    }

    #endregion

    #region Save Operations

    /// <summary>
    /// Collects save data from all listening game systems via GameEvents and writes it to disk.
    /// </summary>
    /// <param name="slotIndex">Target save slot index.</param>
    /// <returns>True if file was successfully written; false otherwise.</returns>
    public static bool Save(int slotIndex)
    {
        try
        {
            GameData dataToSave = new GameData();
            dataToSave.header.saveSlotIndex = slotIndex;
            dataToSave.header.saveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // 1. Broadcast collection request to all active systems
            GameEvents.SaveLoad.TriggerCollectSaveData(dataToSave);

            // 2. Post-process header metadata (Calculate compact Djinn counts derived from collected Djinns)
            CalculateHeaderDjinnMetrics(dataToSave);

            // 3. Serialize GameData container to JSON
            string json = JsonConvert.SerializeObject(dataToSave, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            });

            // 4. Write JSON file to disk
            string filePath = GetSaveFilePath(slotIndex);
            File.WriteAllText(filePath, json);

            //Debug.Log($"<color=green>[SaveManager] Successfully saved game to Slot {slotIndex} at path: {filePath}</color>");
            GameEvents.SaveLoad.TriggerSaveCompleted(slotIndex, true);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Failed to save game to Slot {slotIndex}: {ex.Message}");
            GameEvents.SaveLoad.TriggerSaveCompleted(slotIndex, false);
            return false;
        }
    }

    /// <summary>
    /// Calculates the sum of collected Djinns grouped by elemental index for header UI previews.
    /// </summary>
    private static void CalculateHeaderDjinnMetrics(GameData saveData)
    {
        if (saveData == null || saveData.header == null) return;

        saveData.header.djinnCountByElement = new int[4];

        if (saveData.collectedDjinn == null || saveData.collectedDjinn.Count == 0) return;

        for (int i = 0; i < saveData.collectedDjinn.Count; i++)
        {
            DjinnSaveData djinnData = saveData.collectedDjinn[i];
            if (djinnData == null || string.IsNullOrEmpty(djinnData.djinnSlug)) continue;

            // Query DjinnSO asset asynchronously/cached via GameEvents.Database
            DjinnSO djinnAsset = null;
            GameEvents.Database.RequestDjinnBySlug(djinnData.djinnSlug, asset => djinnAsset = asset);

            if (djinnAsset != null)
            {
                int elementIndex = (int)djinnAsset.Element; // 0: Venus, 1: Mars, 2: Jupiter, 3: Mercury
                if (elementIndex >= 0 && elementIndex < 4)
                {
                    saveData.header.djinnCountByElement[elementIndex]++;
                }
            }
        }
    }

    #endregion

    #region Load Operations

    /// <summary>
    /// Reads save file from disk and triggers restoration event across all subscribed systems.
    /// </summary>
    /// <param name="slotIndex">Target save slot index.</param>
    /// <returns>Loaded GameData instance, or null if file does not exist or failed to parse.</returns>
    public static GameData Load(int slotIndex)
    {
        string filePath = GetSaveFilePath(slotIndex);

        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"[SaveManager] Save slot file not found at path: {filePath}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            GameData loadedData = JsonConvert.DeserializeObject<GameData>(json, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });

            if (loadedData == null)
            {
                Debug.LogError($"[SaveManager] Failed to deserialize Save Data for Slot {slotIndex}. Object is null.");
                GameEvents.SaveLoad.TriggerLoadCompleted(slotIndex, false);
                return null;
            }

            // Broadcast restoration request to all active systems
            GameEvents.SaveLoad.TriggerRestoreSaveData(loadedData);

            Debug.Log($"<color=cyan>[SaveManager] Successfully loaded game state from Slot {slotIndex}.</color>");
            GameEvents.SaveLoad.TriggerLoadCompleted(slotIndex, true);
            return loadedData;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Critical error loading save file for Slot {slotIndex}: {ex.Message}");
            GameEvents.SaveLoad.TriggerLoadCompleted(slotIndex, false);
            return null;
        }
    }

    /// <summary>
    /// Reads and returns only the lightweight header metadata of a save slot for UI display without restoring game state.
    /// </summary>
    public static SaveHeaderData LoadHeader(int slotIndex)
    {
        string filePath = GetSaveFilePath(slotIndex);

        if (!File.Exists(filePath)) return null;

        try
        {
            string json = File.ReadAllText(filePath);
            GameData fullData = JsonConvert.DeserializeObject<GameData>(json);
            return fullData?.header;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Failed to read header for Slot {slotIndex}: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region Query & Utility Operations

    /// <summary>
    /// Checks if a save file exists for a given slot index.
    /// </summary>
    public static bool HasSaveFile(int slotIndex)
    {
        return File.Exists(GetSaveFilePath(slotIndex));
    }

    /// <summary>
    /// Deletes a save file for a given slot index from disk.
    /// </summary>
    public static bool DeleteSaveFile(int slotIndex)
    {
        string filePath = GetSaveFilePath(slotIndex);

        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
                Debug.Log($"[SaveManager] Deleted save file for Slot {slotIndex}.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Failed to delete save file for Slot {slotIndex}: {ex.Message}");
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Scans persistent storage and retrieves all valid save headers for save slot selection UI.
    /// </summary>
    public static List<SaveHeaderData> GetAllSaveHeaders(int maxSlotsToScan = 10)
    {
        List<SaveHeaderData> headers = new List<SaveHeaderData>();

        for (int i = 0; i < maxSlotsToScan; i++)
        {
            SaveHeaderData header = LoadHeader(i);
            if (header != null)
            {
                headers.Add(header);
            }
        }

        return headers;
    }

    #endregion
}