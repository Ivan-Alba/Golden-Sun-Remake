using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Central database repository for all PsynergySO assets in the project and elemental UI/VFX assets.
/// Fully decoupled via GameEvents.Database, zero-alloc loop iterations, and Save System ready.
/// </summary>
public class PsynergyDatabase : MonoBehaviour
{
    #region Serialized Fields

    [Header("Data References")]
    [Tooltip("List of all PsynergySO assets automatically populated by Editor script or manually assigned.")]
    [SerializeField] private List<PsynergySO> psynergies = new List<PsynergySO>();

    [Header("Elemental Icon Sprites")]
    [SerializeField] private Sprite venusIcon;
    [SerializeField] private Sprite marsIcon;
    [SerializeField] private Sprite jupiterIcon;
    [SerializeField] private Sprite mercuryIcon;
    [SerializeField] private Sprite noElementalIcon;

    [Header("Elemental Circle VFX Prefabs")]
    [SerializeField] private GameObject venusElementCircle;
    [SerializeField] private GameObject marsElementCircle;
    [SerializeField] private GameObject jupiterElementCircle;
    [SerializeField] private GameObject mercuryElementCircle;
    [SerializeField] private GameObject noElementalCircle;

    #endregion

    #region Private Fields

    private readonly List<PsynergySO> allPsynergies = new List<PsynergySO>();
    private readonly Dictionary<string, PsynergySO> psynergyDictionary = new Dictionary<string, PsynergySO>();

    #endregion

    #region Public Properties

    public IReadOnlyList<PsynergySO> AllPsynergies => allPsynergies;

    #endregion

    #region Unity Lifecycle & Event Subscriptions

    private void Awake()
    {
        InitializeDatabase();
    }

    private void OnEnable()
    {
        GameEvents.Database.OnRequestPsynergyBySlug += HandleRequestPsynergyBySlug;
        GameEvents.Database.OnRequestAllPsynergies += HandleRequestAllPsynergies;
        GameEvents.Database.OnRequestElementIcon += HandleRequestElementIcon;
        GameEvents.Database.OnRequestElementVFX += HandleRequestElementVFX;
    }

    private void OnDisable()
    {
        GameEvents.Database.OnRequestPsynergyBySlug -= HandleRequestPsynergyBySlug;
        GameEvents.Database.OnRequestAllPsynergies -= HandleRequestAllPsynergies;
        GameEvents.Database.OnRequestElementIcon -= HandleRequestElementIcon;
        GameEvents.Database.OnRequestElementVFX -= HandleRequestElementVFX;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Guard clause: Prevent AssetDatabase access while the editor is compiling, updating, or building
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        FindAllPsynergies();
    }
#endif

    #endregion

    #region Initialization Logic

    /// <summary>
    /// Clears existing caches and populates the database using non-alloc loops.
    /// </summary>
    public void InitializeDatabase()
    {
        psynergyDictionary.Clear();
        allPsynergies.Clear();

        if (psynergies == null || psynergies.Count == 0)
        {
            Debug.LogWarning("[PsynergyDatabase] No psynergies assigned in the database serialized list.", this);
            return;
        }

        // Non-alloc index loop iteration for cache building
        for (int i = 0; i < psynergies.Count; i++)
        {
            PsynergySO psynergy = psynergies[i];
            if (psynergy == null) continue;

            if (string.IsNullOrWhiteSpace(psynergy.PsynergySlug))
            {
                Debug.LogWarning($"[PsynergyDatabase] Psynergy '{psynergy.PsynergyName}' has an empty or null SlugName.", this);
                continue;
            }

            if (!psynergyDictionary.ContainsKey(psynergy.PsynergySlug))
            {
                allPsynergies.Add(psynergy);
                psynergyDictionary.Add(psynergy.PsynergySlug, psynergy);
            }
            else
            {
                Debug.LogWarning($"[PsynergyDatabase] Duplicate SlugName detected: '{psynergy.PsynergySlug}'. Skipping duplicate asset '{psynergy.name}'.", this);
            }
        }

        Debug.Log($"[PsynergyDatabase] Successfully loaded and registered {psynergyDictionary.Count} Psynergies.");
    }

    #endregion

    #region Editor Auto-Population

#if UNITY_EDITOR
    [ContextMenu("Find All Psynergies In Project")]
    public void FindAllPsynergies()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        psynergies.Clear();

        string[] guids = AssetDatabase.FindAssets("t:PsynergySO");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            PsynergySO psynergy = AssetDatabase.LoadAssetAtPath<PsynergySO>(path);

            if (psynergy != null)
            {
                psynergies.Add(psynergy);
            }
        }

        EditorUtility.SetDirty(this);
    }
#endif

    #endregion

    #region Event Handlers

    private void HandleRequestPsynergyBySlug(string slug, Action<PsynergySO> onResult)
    {
        PsynergySO psynergy = GetPsynergy(slug);
        onResult?.Invoke(psynergy);
    }

    private void HandleRequestAllPsynergies(Action<IReadOnlyList<PsynergySO>> onResult)
    {
        onResult?.Invoke(allPsynergies);
    }

    private void HandleRequestElementIcon(ElementalType element, Action<Sprite> onResult)
    {
        Sprite icon = GetElementIcon(element);
        onResult?.Invoke(icon);
    }

    private void HandleRequestElementVFX(ElementalType element, Action<GameObject> onResult)
    {
        GameObject vfx = GetElementAnimation(element);
        onResult?.Invoke(vfx);
    }

    #endregion

    #region Public Query API

    /// <summary>
    /// Retrieves a PsynergySO by its unique SlugName in O(1) time.
    /// </summary>
    public PsynergySO GetPsynergy(string psynergySlug)
    {
        if (string.IsNullOrEmpty(psynergySlug)) return null;

        if (psynergyDictionary.TryGetValue(psynergySlug, out PsynergySO psynergy))
        {
            return psynergy;
        }

        Debug.LogWarning($"[PsynergyDatabase] Could not find Psynergy with slug: '{psynergySlug}'.");
        return null;
    }

    /// <summary>
    /// Returns the elemental circle VFX associated with a given ElementalType.
    /// </summary>
    public GameObject GetElementAnimation(ElementalType element)
    {
        switch (element)
        {
            case ElementalType.Venus:
                return venusElementCircle;
            case ElementalType.Mars:
                return marsElementCircle;
            case ElementalType.Jupiter:
                return jupiterElementCircle;
            case ElementalType.Mercury:
                return mercuryElementCircle;
            default:
                return noElementalCircle;
        }
    }

    /// <summary>
    /// Helper overload to retrieve elemental animation directly from a PsynergySO.
    /// </summary>
    public GameObject GetElementAnimation(PsynergySO psynergy)
    {
        if (psynergy == null) return noElementalCircle;
        return GetElementAnimation(psynergy.Element);
    }

    /// <summary>
    /// Returns the elemental UI sprite icon for a given ElementalType.
    /// </summary>
    public Sprite GetElementIcon(ElementalType element)
    {
        switch (element)
        {
            case ElementalType.Venus:
                return venusIcon;
            case ElementalType.Mars:
                return marsIcon;
            case ElementalType.Jupiter:
                return jupiterIcon;
            case ElementalType.Mercury:
                return mercuryIcon;
            default:
                return noElementalIcon;
        }
    }

    /// <summary>
    /// Helper overload to retrieve elemental icon directly from a PsynergySO.
    /// </summary>
    public Sprite GetElementIcon(PsynergySO psynergy)
    {
        if (psynergy == null) return noElementalIcon;
        return GetElementIcon(psynergy.Element);
    }

    #endregion
}