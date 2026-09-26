using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Central database repository for all DjinnSO assets in the project.
/// Resolves slug-to-object deserialization for Save Systems and provides zero-alloc database queries.
/// Fully decoupled via GameEvents.Database.
/// </summary>
public class DjinnDatabase : MonoBehaviour
{
    #region Serialized Fields

    [Header("Data References")]
    [Tooltip("List of all DjinnSO assets automatically populated by Editor script or manually assigned.")]
    [SerializeField] private List<DjinnSO> djinns = new List<DjinnSO>();

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

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Guard clause: Prevent AssetDatabase access while the editor is compiling, updating, or building
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        FindAllDjinns();
    }
#endif

    #endregion

    #region Initialization Logic

    /// <summary>
    /// Clears existing caches and populates the database using non-alloc loops.
    /// </summary>
    public void BuildDatabase()
    {
        djinnList.Clear();
        djinnDictionary.Clear();

        if (djinns == null || djinns.Count == 0)
        {
            Debug.LogWarning("[DjinnDatabase] No Djinns assigned in the database serialized list.", this);
            return;
        }

        for (int i = 0; i < djinns.Count; i++)
        {
            DjinnSO djinn = djinns[i];
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

        Debug.Log($"[DjinnDatabase] Successfully built database with {djinnList.Count} Djinns.");
    }

    #endregion

    #region Editor Auto-Population

#if UNITY_EDITOR
    [ContextMenu("Find All Djinns In Project")]
    public void FindAllDjinns()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        djinns.Clear();

        string[] guids = AssetDatabase.FindAssets("t:DjinnSO");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            DjinnSO djinn = AssetDatabase.LoadAssetAtPath<DjinnSO>(path);

            if (djinn != null)
            {
                djinns.Add(djinn);
            }
        }

        EditorUtility.SetDirty(this);
    }
#endif

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