using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a deterministic player spawn location within a scene.
/// Manages static lookup registry, zone context metadata, and automated arrival sequences.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    #region Static Registry ($O(1)$ Lookup)

    /// <summary>
    /// Global registry for all active SpawnPoints in the currently loaded scenes.
    /// </summary>
    public static readonly Dictionary<string, SpawnPoint> AllSpawnPoints = new Dictionary<string, SpawnPoint>();

    #endregion

    #region Serialized Fields

    [Header("Identification Settings")]
    [Tooltip("Unique string identifier used for teleportation and persistence matching.")]
    [SerializeField] private string spawnID;

    [Header("Zone Identification Settings")]
    [SerializeField] private string zoneName = "ENTRADA AL BOSQUE";
    [SerializeField] private string regionName = "Región de Bilibin";

    [Header("Arrival Auto-Walk Settings")]
    [SerializeField] private bool autoWalkOnArrival = true;
    [SerializeField] private Transform arrivalWalkTarget;
    [SerializeField] private float walkDuration = 2f;
    [SerializeField] private float bannerDelay = 0.5f;

    #endregion

    #region Properties

    public string SpawnID => spawnID;
    public string ZoneName => zoneName;
    public string RegionName => regionName;
    public bool AutoWalkOnArrival => autoWalkOnArrival;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        RegisterSpawnPoint();
    }

    private void OnDestroy()
    {
        UnregisterSpawnPoint();
    }

    #endregion

    #region Registration Logic

    private void RegisterSpawnPoint()
    {
        if (!string.IsNullOrEmpty(spawnID))
        {
            AllSpawnPoints[spawnID] = this;
        }
        else
        {
            Debug.LogWarning($"[SpawnPoint] SpawnPoint on GameObject '{gameObject.name}' has an empty spawnID!", this);
        }
    }

    private void UnregisterSpawnPoint()
    {
        if (!string.IsNullOrEmpty(spawnID) && AllSpawnPoints.ContainsKey(spawnID))
        {
            if (AllSpawnPoints[spawnID] == this)
            {
                AllSpawnPoints.Remove(spawnID);
            }
        }
    }

    #endregion

    #region Arrival Sequence

    /// <summary>
    /// Executes the smooth arrival sequence when teleporting from another scene during gameplay.
    /// </summary>
    public IEnumerator Routine_ExecuteArrivalSequence()
    {
        if (arrivalWalkTarget != null)
        {
            GameEvents.Player.RequestAutoWalk(arrivalWalkTarget.position);
        }

        // Wait a small fraction before presenting the zone banner mid-walk
        yield return new WaitForSeconds(bannerDelay);

        GameEvents.UI.TriggerShowZoneBanner(zoneName, regionName, 3.5f);

        // Wait for the remaining walk duration before restoring HUD & player control
        float remainingWalkTime = Mathf.Max(0f, walkDuration - bannerDelay);
        yield return new WaitForSeconds(remainingWalkTime);
    }

    #endregion

    #region Editor Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward * 1.5f);

        if (autoWalkOnArrival && arrivalWalkTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(arrivalWalkTarget.position, 0.3f);
            Gizmos.DrawLine(transform.position, arrivalWalkTarget.position);
        }
    }
#endif

    #endregion
}