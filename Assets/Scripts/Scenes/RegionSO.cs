using UnityEngine;

/// <summary>
/// ScriptableObject representing a major region or continent in the game world (e.g., Angara, Gondowan).
/// Serves as a top-level parent container for localized ZoneSO assets.
/// </summary>
[CreateAssetMenu(fileName = "SO_Region_NewRegion", menuName = "GoldenSun/Scene/Region Data", order = 1)]
public class RegionSO : ScriptableObject
{
    [Header("Region Identification")]
    [Tooltip("Unique string key used for save/load serialization (e.g., 'region_angara').")]
    [SerializeField] private string regionID;

    [Tooltip("Human-readable region name displayed on the UI Zone Banner (e.g., 'Región de Angara').")]
    [SerializeField] private string regionDisplayName = "NUEVA REGIÓN";

    #region Properties

    public string RegionID => regionID;
    public string RegionDisplayName => regionDisplayName;

    #endregion

    private void OnValidate()
    {
        // Auto-assign ID from asset name if left blank in editor
        if (string.IsNullOrEmpty(regionID))
        {
            regionID = name.ToLower().Replace(" ", "_");
        }
    }
}