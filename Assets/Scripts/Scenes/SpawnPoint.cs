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
    [Tooltip("Reference to the ZoneSO ScriptableObject associated with this spawn point location.")]
    [SerializeField] private ZoneSO zone;

    [Header("Arrival Auto-Walk Settings")]
    [SerializeField] private bool autoWalkOnArrival = true;
    [SerializeField] private Transform arrivalWalkTarget;
    [SerializeField] private float arrivalDistanceThreshold = 0.15f;
    [SerializeField] private float bannerDelay = 0.3f;
    [SerializeField] private float maxWalkTimeout = 5.0f; // Security timeout to prevent endless locks

    #endregion

    #region Properties & State Tracking

    public string SpawnID => spawnID;
    public ZoneSO Zone => zone;
    public bool AutoWalkOnArrival => autoWalkOnArrival;

    /// <summary>
    /// True while the arrival sequence (auto-walk) is actively executing.
    /// </summary>
    public bool IsExecutingArrival { get; private set; }

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
    /// Executes the deterministic arrival sequence by ordering auto-walk and waiting
    /// until the player physically reaches the target position.
    /// </summary>
    public IEnumerator Routine_ExecuteArrivalSequence()
    {
        IsExecutingArrival = true;

        string displayZone = zone != null ? zone.FullZoneDisplayName : "DESCONOCIDO";
        string displayRegion = zone != null ? zone.RegionDisplayName : "DESCONOCIDO";

        if (arrivalWalkTarget != null)
        {
            // 1. Request player auto-walk towards arrival destination
            GameEvents.Player.RequestAutoWalk(arrivalWalkTarget.position);

            // 2. Wait a brief delay before triggering the zone banner mid-walk
            if (bannerDelay > 0f)
            {
                yield return new WaitForSeconds(bannerDelay);
            }

            GameEvents.UI.TriggerShowZoneBanner(displayZone, displayRegion, 3.5f);

            // 3. Monitor physical player distance until arrival or safety timeout
            float elapsedTime = 0f;
            Transform playerTransform = null;

            // Wait until player reference is active
            while (playerTransform == null && elapsedTime < maxWalkTimeout)
            {
                GameObject leaderObj = GameObject.FindWithTag("Player");
                if (leaderObj != null)
                {
                    playerTransform = leaderObj.transform;
                }
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Loop until target is physically reached
            if (playerTransform != null)
            {
                Vector3 targetPos = arrivalWalkTarget.position;
                targetPos.y = 0f; // Distance calculated strictly on horizontal plane XZ

                while (elapsedTime < maxWalkTimeout)
                {
                    Vector3 currentPos = playerTransform.position;
                    currentPos.y = 0f;

                    if (Vector3.Distance(currentPos, targetPos) <= arrivalDistanceThreshold)
                    {
                        break; // Destination reached!
                    }

                    elapsedTime += Time.deltaTime;
                    yield return null;
                }
            }
        }
        else
        {
            // If no walk target was assigned, present banner immediately
            GameEvents.UI.TriggerShowZoneBanner(displayZone, displayRegion, 3.5f);
        }

        IsExecutingArrival = false;
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