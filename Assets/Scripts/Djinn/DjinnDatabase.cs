using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central database repository for all DjinnSO assets loaded from Resources.
/// Resolves slug-to-object deserialization for Save Systems and provides zero-alloc database queries.
/// Fully decoupled via GameEvents.Database.
/// </summary>
public class DjinnDatabase : MonoBehaviour
{
    #region Serialized Fields

    [Header("Resource Path Settings")]
    [Tooltip("Folder path inside Resources where DjinnSO assets are stored.")]
    [SerializeField] private string djinnsResourcePath = "ScriptableObjects/Djinns";

    #endregion

    #region Private Fields

    private readonly List<DjinnSO> djinnList = new List<DjinnSO>();
    private readonly Dictionary<string, DjinnSO> djinnDictionary = new Dictionary<string, DjinnSO>();

    #endregion

    #region Public Properties

    public IReadOnlyList<DjinnSO> DjinnList => djinnList;

    #endregion

    #region Unity Lifecycle & Event Subscriptions

    private void Awake()
    {
        BuildDatabase();
    }

    private void OnEnable()
    {
        // Database bus subscriptions
        GameEvents.Database.OnRequestDjinnBySlug += HandleRequestDjinnBySlug;
        GameEvents.Database.OnRequestAllDjinns += HandleRequestAllDjinns;
        GameEvents.Database.OnRequestDjinnsByElement += HandleRequestDjinnsByElement;
    }

    private void OnDisable()
    {
        // Database bus unsubscriptions
        GameEvents.Database.OnRequestDjinnBySlug -= HandleRequestDjinnBySlug;
        GameEvents.Database.OnRequestAllDjinns -= HandleRequestAllDjinns;
        GameEvents.Database.OnRequestDjinnsByElement -= HandleRequestDjinnsByElement;
    }

    #endregion

    #region Initialization Logic

    /// <summary>
    /// Clears existing caches and populates the database from Resources using non-alloc loops.
    /// </summary>
    public void BuildDatabase()
    {
        djinnList.Clear();
        djinnDictionary.Clear();

        // Load all DjinnSO assets from Resources
        DjinnSO[] loadedDjinns = Resources.LoadAll<DjinnSO>(djinnsResourcePath);

        if (loadedDjinns == null || loadedDjinns.Length == 0)
        {
            Debug.LogWarning($"[DjinnDatabase] No Djinns found at path Resources/{djinnsResourcePath}.", this);
            return;
        }

        for (int i = 0; i < loadedDjinns.Length; i++)
        {
            DjinnSO djinn = loadedDjinns[i];
            if (djinn == null) continue;

            if (string.IsNullOrWhiteSpace(djinn.DjinnSlug))
            {
                Debug.LogWarning($"[DjinnDatabase] Djinn '{djinn.name}' has an empty or null DjinnSlug.", this);
                continue;
            }

            if (djinnDictionary.ContainsKey(djinn.DjinnSlug))
            {
                Debug.LogError($"[DjinnDatabase] Duplicate DjinnSlug detected: '{djinn.DjinnSlug}'. Djinn '{djinn.name}' skipped.", this);
                continue;
            }

            djinnList.Add(djinn);
            djinnDictionary.Add(djinn.DjinnSlug, djinn);
        }

        Debug.Log($"[DjinnDatabase] Successfully loaded {djinnList.Count} Djinns from Resources/{djinnsResourcePath}.");
    }

    #endregion

    #region Event Handlers

    private void HandleRequestDjinnBySlug(string slug, Action<DjinnSO> onResult)
    {
        DjinnSO djinn = GetDjinn(slug);
        onResult?.Invoke(djinn);
    }

    private void HandleRequestAllDjinns(Action<IReadOnlyList<DjinnSO>> onResult)
    {
        onResult?.Invoke(djinnList);
    }

    private void HandleRequestDjinnsByElement(ElementalType element, List<DjinnSO> resultsBuffer)
    {
        GetDjinnsByElement(element, resultsBuffer);
    }

    #endregion

    #region Database Query Methods (Non-Alloc)

    /// <summary>
    /// Retrieves a base DjinnSO by its unique DjinnSlug. Essential for save game deserialization.
    /// </summary>
    public DjinnSO GetDjinn(string djinnSlug)
    {
        if (string.IsNullOrEmpty(djinnSlug)) return null;

        if (djinnDictionary.TryGetValue(djinnSlug, out DjinnSO djinn))
        {
            return djinn;
        }

        Debug.LogWarning($"[DjinnDatabase] Djinn with slug '{djinnSlug}' not found in database.");
        return null;
    }

    /// <summary>
    /// Zero-allocation query: Fills a provided buffer list with Djinns matching the specified ElementalType.
    /// </summary>
    public void GetDjinnsByElement(ElementalType element, List<DjinnSO> resultsBuffer)
    {
        if (resultsBuffer == null) return;
        resultsBuffer.Clear();

        for (int i = 0; i < djinnList.Count; i++)
        {
            DjinnSO djinn = djinnList[i];
            if (djinn != null && djinn.Element == element)
            {
                resultsBuffer.Add(djinn);
            }
        }
    }

    /// <summary>
    /// Returns a read-only list of all registered Djinns in the database for zero-alloc index iteration.
    /// </summary>
    public IReadOnlyList<DjinnSO> GetAllDjinns()
    {
        return djinnList;
    }

    #endregion
}