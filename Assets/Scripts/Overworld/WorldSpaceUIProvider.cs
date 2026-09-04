using UnityEngine;

public enum WorldSpaceUIType
{
    None,
    EnemyBar,       // Muestra Nombre, Nivel y Barra de Amenaza/Alert State
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
    [SerializeField] private string entityName = "Unknown";
    [SerializeField] private string entitySubTitle = ""; // ej. "Mercader", "Guardia"
    [SerializeField] private int entityLevel = 1;

    [Header("Anchoring")]
    [Tooltip("Specific transform where the UI should hover. If null, uses entity root + height offset.")]
    [SerializeField] private Transform uiAnchor;
    [SerializeField] private float defaultHeightOffset = 2.0f;

    #endregion

    #region Properties

    public WorldSpaceUIType UIType => uiType;
    public string EntityName => entityName;
    public string EntitySubTitle => entitySubTitle;
    public int EntityLevel => entityLevel;

    public Vector3 AnchorPosition => uiAnchor != null
        ? uiAnchor.position
        : transform.position + Vector3.up * defaultHeightOffset;

    #endregion

    #region Public Methods

    public void UpdateLevel(int newLevel) => entityLevel = newLevel;
    public void UpdateName(string newName) => entityName = newName;

    #endregion
}