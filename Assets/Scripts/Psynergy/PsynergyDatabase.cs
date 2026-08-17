using System.Collections.Generic;
using UnityEngine;

public class PsynergyDatabase : MonoBehaviour
{
    public static PsynergyDatabase Instance { get; private set; }

    [Header("Resources Settings")]
    [Tooltip("Path inside Resources folder where PsynergySO assets are located.")]
    [SerializeField] private string psynergiesResourcePath = "Psynergies";

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

    // Loaded Assets & Fast O(1) Lookup
    private readonly List<PsynergySO> allPsynergies = new List<PsynergySO>();
    private readonly Dictionary<string, PsynergySO> psynergyDictionary = new Dictionary<string, PsynergySO>();

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeDatabase();
    }

    #endregion

    #region Initialization

    private void InitializeDatabase()
    {
        psynergyDictionary.Clear();
        allPsynergies.Clear();

        // 1. Automatically load all PsynergySO assets from Resources/Psynergies
        PsynergySO[] loadedPsynergies = Resources.LoadAll<PsynergySO>(psynergiesResourcePath);

        if (loadedPsynergies == null || loadedPsynergies.Length == 0)
        {
            Debug.LogWarning($"[PsynergyDatabase] No PsynergySO assets found at Resources/{psynergiesResourcePath}.");
            return;
        }

        // 2. Register assets into list and fast lookup dictionary
        foreach (PsynergySO psynergy in loadedPsynergies)
        {
            if (psynergy == null) continue;

            if (string.IsNullOrEmpty(psynergy.SlugName))
            {
                Debug.LogWarning($"[PsynergyDatabase] Psynergy '{psynergy.PsynergyName}' has an empty or null SlugName.");
                continue;
            }

            if (!psynergyDictionary.ContainsKey(psynergy.SlugName))
            {
                allPsynergies.Add(psynergy);
                psynergyDictionary.Add(psynergy.SlugName, psynergy);
            }
            else
            {
                Debug.LogWarning($"[PsynergyDatabase] Duplicate SlugName detected: '{psynergy.SlugName}'. Skipping duplicate asset '{psynergy.name}'.");
            }
        }

        Debug.Log($"[PsynergyDatabase] Successfully loaded and registered {psynergyDictionary.Count} Psynergies from 'Resources/{psynergiesResourcePath}'.");
    }

    #endregion

    #region Public API

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
    /// Helper method to retrieve elemental animation directly from a PsynergySO.
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