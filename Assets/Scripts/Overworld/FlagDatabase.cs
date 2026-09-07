using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central database repository for all FlagSO assets loaded from Resources.
/// Resolves slug-to-object lookup for Save System and provides zero-alloc queries.
/// Fully decoupled via GameEvents.Database.
/// </summary>
public class FlagDatabase : MonoBehaviour
{
    #region Serialized Fields

    [Header("Resource Path Settings")]
    [Tooltip("Folder path inside Resources where FlagSO assets are stored.")]
    [SerializeField] private string flagsResourcePath = "ScriptableObjects/Flags";

    #endregion

    #region Private Fields

    private readonly List<FlagSO> flagList = new List<FlagSO>();
    private readonly Dictionary<string, FlagSO> flagDictionary = new Dictionary<string, FlagSO>();

    #endregion

    #region Public Properties

    public IReadOnlyList<FlagSO> FlagList => flagList;

    #endregion

    #region Unity Lifecycle & Event Subscriptions

    private void Awake()
    {
        BuildDatabase();
    }

    private void OnEnable()
    {
        GameEvents.Database.OnRequestFlagBySlug += HandleRequestFlagBySlug;
        GameEvents.Database.OnRequestAllFlags += HandleRequestAllFlags;
    }

    private void OnDisable()
    {
        GameEvents.Database.OnRequestFlagBySlug -= HandleRequestFlagBySlug;
        GameEvents.Database.OnRequestAllFlags -= HandleRequestAllFlags;
    }

    #endregion

    #region Initialization Logic

    public void BuildDatabase()
    {
        flagList.Clear();
        flagDictionary.Clear();

        FlagSO[] loadedFlags = Resources.LoadAll<FlagSO>(flagsResourcePath);

        if (loadedFlags == null || loadedFlags.Length == 0)
        {
            Debug.LogWarning($"[FlagDatabase] No flags found at path Resources/{flagsResourcePath}.", this);
            return;
        }

        for (int i = 0; i < loadedFlags.Length; i++)
        {
            FlagSO flag = loadedFlags[i];
            if (flag == null) continue;

            if (string.IsNullOrWhiteSpace(flag.FlagSlug))
            {
                Debug.LogWarning($"[FlagDatabase] Flag '{flag.name}' has an empty or null FlagSlug.", this);
                continue;
            }

            if (flagDictionary.ContainsKey(flag.FlagSlug))
            {
                Debug.LogError($"[FlagDatabase] Duplicate FlagSlug detected: '{flag.FlagSlug}'. Flag '{flag.name}' skipped.", this);
                continue;
            }

            flagList.Add(flag);
            flagDictionary.Add(flag.FlagSlug, flag);
        }

        Debug.Log($"[FlagDatabase] Successfully loaded {flagList.Count} flags from Resources/{flagsResourcePath}.");
    }

    #endregion

    #region Event Handlers

    private void HandleRequestFlagBySlug(string slug, Action<FlagSO> onResult)
    {
        FlagSO flag = GetFlag(slug);
        onResult?.Invoke(flag);
    }

    private void HandleRequestAllFlags(Action<IReadOnlyList<FlagSO>> onResult)
    {
        onResult?.Invoke(flagList);
    }

    #endregion

    #region Database Query Methods

    public FlagSO GetFlag(string flagSlug)
    {
        if (string.IsNullOrEmpty(flagSlug)) return null;

        if (flagDictionary.TryGetValue(flagSlug, out FlagSO flag))
        {
            return flag;
        }

        Debug.LogWarning($"[FlagDatabase] Flag with slug '{flagSlug}' not found in database.");
        return null;
    }

    public IReadOnlyList<FlagSO> GetAllFlags()
    {
        return flagList;
    }

    #endregion
}