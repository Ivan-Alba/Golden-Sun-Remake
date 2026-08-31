using System.Collections;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    #region Serialized Fields

    [SerializeField] private string spawnPointID;

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

    public string SpawnPointID => spawnPointID;
    public string ZoneName => zoneName;
    public string RegionName => regionName;
    public bool AutoWalkOnArrival => autoWalkOnArrival;

    #endregion

    #region Arrival Sequence

    /// <summary>
    /// Executes the smooth arrival sequence when teleporting from another scene during gameplay.
    /// </summary>
    public IEnumerator Routine_ExecuteArrivalSequence()
    {
        if (PlayerController.Instance != null && arrivalWalkTarget != null)
        {
            PlayerController.Instance.MoveToDestination(arrivalWalkTarget.position);
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