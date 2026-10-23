using UnityEngine;

/// <summary>
/// ScriptableObject representing a specific localized area (e.g., Tale Village Entrance, Crystal Plaza, Mt. Aleph Peak).
/// Holds a reference to its parent RegionSO for hierarchical UI banner display.
/// </summary>
[CreateAssetMenu(fileName = "SO_Zone_NewZone", menuName = "GoldenSun/Scene/Zone Data", order = 2)]
public class ZoneSO : ScriptableObject
{
    [Header("Parent Hierarchy")]
    [Tooltip("Reference to the parent RegionSO this zone belongs to.")]
    [SerializeField] private RegionSO parentRegion;

    [Header("Zone Identification")]
    [Tooltip("Unique string key used for save/load serialization (e.g., 'zone_tale_plaza').")]
    [SerializeField] private string zoneID;

    [Tooltip("Main location context (e.g., 'Pueblo de Tale', 'Minas de Altin'). Optional.")]
    [SerializeField] private string locationName = "";

    [Tooltip("Human-readable sub-zone name (e.g., 'Plaza del Cristal', 'Nivel B1').")]
    [SerializeField] private string zoneDisplayName = "NUEVA ZONA";

    [Header("Display Options")]
    [Tooltip("If true, the UI banner will only show the zone name and hide the parent region name.")]
    [SerializeField] private bool hideRegionInBanner = false;

    #region Properties

    public RegionSO ParentRegion => parentRegion;
    public string ZoneID => zoneID;
    public bool HideRegionInBanner => hideRegionInBanner;

    /// <summary>
    /// Returns the formatted full name for the UI banner (e.g., 'Pueblo de Tale - Plaza del Cristal' or just 'Plaza del Cristal').
    /// </summary>
    public string FullZoneDisplayName => string.IsNullOrEmpty(LocationName)
        ? zoneDisplayName
        : $"{LocationName} - {zoneDisplayName}";

    /// <summary>
    /// Helper property to safely fetch the parent region display name even if parentRegion is null.
    /// </summary>
    public string RegionDisplayName => parentRegion != null ? parentRegion.RegionDisplayName : "UNKNOWN";

    public string LocationName => string.IsNullOrEmpty(locationName) ? "UNKNOWN" : locationName;

    #endregion

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(zoneID))
        {
            zoneID = name.ToLower().Replace(" ", "_");
        }
    }
}