using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Passive physics trigger sensor attached to the persistent Player.
/// Detects nearby points of interest entering the visual proximity range
/// and emits global events to manage World-Space UI projections without polling in Update.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class OverworldProximityDetector : MonoBehaviour
{
    #region Serialized Fields

    [Header("Detection Configuration")]
    [Tooltip("Layer mask identifying entity colliders that trigger visual world-space UI.")]
    [SerializeField] private LayerMask detectionLayerMask;

    [Header("Re-evaluation Settings")]
    [SerializeField] private int maxReevaluationColliders = 16;

    #endregion

    #region Private Runtime State

    private SphereCollider proximityTrigger;
    private readonly HashSet<GameObject> activeNearbyEntities = new HashSet<GameObject>();
    private readonly List<GameObject> invalidEntitiesBuffer = new List<GameObject>();
    private Collider[] reevaluationBuffer;

    private bool isExplorationActive = true;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        proximityTrigger = GetComponent<SphereCollider>();
        if (proximityTrigger != null)
        {
            proximityTrigger.isTrigger = true;
        }

        reevaluationBuffer = new Collider[maxReevaluationColliders];
    }

    private void OnEnable()
    {
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDisable()
    {
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        ClearAllTrackedEntities();
    }

    #endregion

    #region Unity Lifecycle Triggers

    private void OnTriggerEnter(Collider other)
    {
        if (!isExplorationActive) return;

        if (!IsValidTrackable(other, out GameObject entityRoot)) return;

        if (activeNearbyEntities.Add(entityRoot))
        {
            GameEvents.World.TriggerEntityEnteredProximity(entityRoot);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isExplorationActive) return;

        if (!IsValidTrackable(other, out GameObject entityRoot)) return;

        // Direct distance validation to avoid PhysX false OnTriggerExit spikes when clipping colliders
        if (proximityTrigger != null)
        {
            Vector3 triggerCenter = transform.TransformPoint(proximityTrigger.center);
            float scaledRadius = proximityTrigger.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);

            Vector3 closestPoint = other.ClosestPoint(triggerCenter);
            float distanceToCenter = Vector3.Distance(triggerCenter, closestPoint);

            if (distanceToCenter < scaledRadius)
            {
                return;
            }
        }

        if (activeNearbyEntities.Remove(entityRoot))
        {
            GameEvents.World.TriggerEntityExitedProximity(entityRoot);
        }
    }

    #endregion

    #region State Event Handler & Area Re-evaluation

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        isExplorationActive = (newState == GameManager.GameState.WorldExploration);

        if (isExplorationActive)
        {
            ReevaluateProximityArea();
        }
        else
        {
            activeNearbyEntities.Clear();
        }
    }

    public void ReevaluateProximityArea()
    {
        activeNearbyEntities.Clear();

        if (proximityTrigger == null) return;

        Vector3 center = transform.TransformPoint(proximityTrigger.center);
        float radius = proximityTrigger.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);

        int hitCount = Physics.OverlapSphereNonAlloc(center, radius, reevaluationBuffer, detectionLayerMask);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = reevaluationBuffer[i];
            if (col == null) continue;

            if (IsValidTrackable(col, out GameObject entityRoot))
            {
                if (activeNearbyEntities.Add(entityRoot))
                {
                    GameEvents.World.TriggerEntityEnteredProximity(entityRoot);
                }
            }
        }
    }

    #endregion

    #region Detection Helpers & Cleanup

    private bool IsValidTrackable(Collider col, out GameObject entityRoot)
    {
        entityRoot = null;

        // 1. Layer Mask check
        if (((1 << col.gameObject.layer) & detectionLayerMask) == 0)
        {
            return false;
        }

        // 2. Validate C# provider component
        if (col.TryGetComponent<WorldSpaceUIProvider>(out _))
        {
            entityRoot = col.gameObject;
            return true;
        }

        return false;
    }

    public void CleanupInvalidEntities()
    {
        invalidEntitiesBuffer.Clear();

        foreach (var entity in activeNearbyEntities)
        {
            if (entity == null || !entity.activeInHierarchy)
            {
                invalidEntitiesBuffer.Add(entity);
            }
        }

        for (int i = 0; i < invalidEntitiesBuffer.Count; i++)
        {
            GameObject invalid = invalidEntitiesBuffer[i];
            activeNearbyEntities.Remove(invalid);
            if (invalid != null)
            {
                GameEvents.World.TriggerEntityExitedProximity(invalid);
            }
        }
    }

    private void ClearAllTrackedEntities()
    {
        foreach (var entity in activeNearbyEntities)
        {
            if (entity != null)
            {
                GameEvents.World.TriggerEntityExitedProximity(entity);
            }
        }
        activeNearbyEntities.Clear();
    }

    #endregion

    #region Editor Debugging Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (proximityTrigger == null) proximityTrigger = GetComponent<SphereCollider>();

        if (proximityTrigger != null)
        {
            Gizmos.color = new Color(0.1f, 0.9f, 0.3f, 0.15f);
            Gizmos.DrawSphere(transform.position, proximityTrigger.radius);
            Gizmos.color = new Color(0.1f, 0.9f, 0.3f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, proximityTrigger.radius);
        }
    }
#endif

    #endregion
}