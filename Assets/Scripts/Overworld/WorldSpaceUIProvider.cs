using UnityEngine;

public enum WorldSpaceUIType
{
    None,
    EnemyIndicator,       // Muestra Nombre, Nivel y Barra de Amenaza/Alert State
    NPCIndicator,   // Muestra Nombre, Rol ("Mercader") y posible icono de misión
    SimpleSign,     // Muestra nombre/texto descriptivo simple
    PointOfInterest // Muestra icono flotante de inspección
}

/// <summary>
/// Attached to any overworld entity that requires a 3D-to-2D World Space UI element.
/// Defines template type, anchor points, and dynamic data getters for the UI controller.
/// </summary>
public class WorldSpaceUIProvider : MonoBehaviour
{
    #region Serialized Fields

    [Header("UI Configuration")]
    [SerializeField] private WorldSpaceUIType uiType = WorldSpaceUIType.NPCIndicator;

    [Tooltip("Optional role or secondary text (e.g. 'Mercader', 'Guardia'). Leave empty to hide.")]
    [SerializeField] private string entitySubTitle = "";

    [Header("Anchoring")]
    [Tooltip("Specific transform where the UI should hover. If null, uses entity root + height offset.")]
    [SerializeField] private Transform uiAnchor;
    [SerializeField] private float defaultHeightOffset = 2.0f;

    #endregion

    #region Private Fields

    private EnemyStats cachedEnemyStats;
    private NPC cachedNPC;

    #endregion

    #region Properties

    public WorldSpaceUIType UIType => uiType;
    public string EntitySubTitle => entitySubTitle;

    public Vector3 AnchorPosition => uiAnchor != null
        ? uiAnchor.position
        : transform.position + Vector3.up * defaultHeightOffset;

    /// <summary>
    /// Dynamically resolves entity name from domain components (EnemyStats, NPC) or fallback to GameObject name.
    /// </summary>
    public string EntityName
    {
        get
        {
            if (cachedEnemyStats != null) return cachedEnemyStats.Name;
            if (cachedNPC != null) return cachedNPC.NpcName;
            return gameObject.name;
        }
    }

    /// <summary>
    /// Dynamically resolves entity level from EnemyStats. Returns 0 for NPCs/Signs (hiding the level label).
    /// </summary>
    public int EntityLevel
    {
        get
        {
            //if (cachedEnemyStats != null) return cachedEnemyStats.Level;
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

    public void SetSubTitle(string newSubTitle) => entitySubTitle = newSubTitle;

    #endregion
}