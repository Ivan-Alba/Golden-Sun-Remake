using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Central database repository for all FlagSO assets in the project.
/// Resolves slug-to-object lookup for Save System and provides zero-alloc queries.
/// Fully decoupled via GameEvents.Database.
/// </summary>
public class FlagDatabase : MonoBehaviour
{
    #region Serialized Fields

    [Header("Data References")]
    [Tooltip("List of all FlagSO assets automatically populated by Editor script or manually assigned.")]
    [SerializeField] private List<FlagSO> flags = new List<FlagSO>();

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

#if UNITY_EDITOR
    private void OnValidate()
    {
        FindAllFlags();
    }
#endif

    #endregion

    #region Initialization Logic

    public void BuildDatabase()
    {
        flagList.Clear();
        flagDictionary.Clear();

        if (flags == null || flags.Count == 0)
        {
            Debug.LogWarning("[FlagDatabase] No flags assigned in the database serialized list.", this);
            return;
        }

        for (int i = 0; i < flags.Count; i++)
        {
            FlagSO flag = flags[i];
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

        Debug.Log($"[FlagDatabase] Successfully built database with {flagList.Count} flags.");
    }

    #endregion

    #region Editor Auto-Population

#if UNITY_EDITOR
    [ContextMenu("Find All Flags In Project")]
    public void FindAllFlags()
    {
        flags.Clear();

        string[] guids = AssetDatabase.FindAssets("t:FlagSO");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            FlagSO flag = AssetDatabase.LoadAssetAtPath<FlagSO>(path);

            if (flag != null)
            {
                flags.Add(flag);
            }
        }

        EditorUtility.SetDirty(this);
    }
#endif

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