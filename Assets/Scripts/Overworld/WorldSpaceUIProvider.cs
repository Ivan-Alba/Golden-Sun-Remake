using UnityEngine;

/// <summary>
/// Defines the visual category of the 3D-to-2D World Space UI element.
/// </summary>
public enum WorldSpaceUIType
{
    None,               // No visual template rendered (pure spatial anchor or hidden)
    EnemyIndicator,     // Displays Name, Level, and Threat/Alert State bar
    NPCIndicator,       // Displays Name, Role (e.g. 'Merchant'), and optional quest icon
    SimpleSign,         // Displays simple descriptive text or nameplate
    PointOfInterest     // Displays a floating inspection/interest icon
}

/// <summary>
/// Attached to any overworld entity that requires a 3D-to-2D World Space UI element or a spatial anchor.
/// Defines template types, anchor points, and dynamic data getters for the OverworldWorldSpaceUIController.
/// </summary>
public class WorldSpaceUIProvider : MonoBehaviour
{
    #region Serialized Fields

    [Header("UI Configuration")]
    [Tooltip("The visual UI template category assigned to this entity.")]
    [SerializeField] private WorldSpaceUIType uiType = WorldSpaceUIType.NPCIndicator;

    [Header("Entity Info")]
    [Tooltip("Optional custom display name. If left empty, it will automatically resolve from components (like NPC or EnemyStats) or fallback to GameObject name.")]
    [SerializeField] private string customEntityName = "";

    [Tooltip("Optional role or secondary text (e.g. 'Merchant', 'Guard', 'Sacred Monument'). Leave empty to hide.")]
    [SerializeField] private string entitySubTitle = "";

    [Header("Anchoring")]
    [Tooltip("Specific transform where the UI or interaction prompt should hover. If null, uses entity root + height offset.")]
    [SerializeField] private Transform uiAnchor;
    [SerializeField] private float defaultHeightOffset = 2.0f;

    #endregion

    #region Private Fields

    private EnemyStats cachedEnemyStats;
    private NPC cachedNPC;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the visual UI template type for this entity.
    /// </summary>
    public WorldSpaceUIType UIType => uiType;

    /// <summary>
    /// Gets the secondary description or subtitle text of the entity.
    /// </summary>
    public string EntitySubTitle => entitySubTitle;

    /// <summary>
    /// Calculates and returns the exact 3D world position where world-space UI or prompts should hover.
    /// </summary>
    public Vector3 AnchorPosition => uiAnchor != null
        ? uiAnchor.position
        : transform.position + Vector3.up * defaultHeightOffset;

    /// <summary>
    /// Dynamically resolves the entity name using a priority fallback chain:
    /// 1. Custom Inspector Override Name.
    /// 2. Domain components (EnemyStats, NPC).
    /// 3. GameObject name fallback.
    /// </summary>
    public string EntityName
    {
        get
        {
            // Priority 1: Explicit custom name set in the Inspector
            if (!string.IsNullOrEmpty(customEntityName))
            {
                return customEntityName;
            }

            // Priority 2: Resolve from domain components
            if (cachedEnemyStats != null) return cachedEnemyStats.Name;
            if (cachedNPC != null) return cachedNPC.NpcName;

            // Priority 3: Fallback to GameObject name
            return gameObject.name;
        }
    }

    /// <summary>
    /// Dynamically resolves the entity level from EnemyStats. Returns 0 for NPCs/Signs (hiding the level label).
    /// </summary>
    public int EntityLevel
    {
        get
        {
            // if (cachedEnemyStats != null) return cachedEnemyStats.Level;
            return 0;
        }
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        cachedEnemyStats = GetComponent<EnemyStats>();
        cachedNPC = GetComponent<NPC>();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Updates the secondary subtitle text dynamically at runtime.
    /// </summary>
    /// <param name="newSubTitle">The new subtitle string to assign.</param>
    public void SetSubTitle(string newSubTitle)
    {
        entitySubTitle = newSubTitle;
    }

    /// <summary>
    /// Updates the custom entity name dynamically at runtime.
    /// </summary>
    /// <param name="newName">The new display name to assign.</param>
    public void SetEntityName(string newName)
    {
        customEntityName = newName;
    }

    #endregion
}