using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Passive physics trigger sensor attached to the persistent Player.
/// Detects nearby points of interest (Enemies, NPCs, Interactables) entering the visual proximity range
/// and emits global events to manage World-Space UI projections without polling in Update.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class OverworldProximityDetector : MonoBehaviour
{
    #region Serialized Fields

    [Header("Detection Configuration")]
    [Tooltip("Layer mask identifying entity colliders that trigger visual world-space UI.")]
    [SerializeField] private LayerMask detectionLayerMask;

    [Tooltip("Tags allowed to register as visible entities.")]
    [SerializeField] private string[] trackableTags = new string[] { "Enemy", "NPC", "Interactable" };

    #endregion

    #region Private Runtime State

    private SphereCollider proximityTrigger;
    private readonly HashSet<GameObject> activeNearbyEntities = new HashSet<GameObject>();
    private readonly List<GameObject> invalidEntitiesBuffer = new List<GameObject>();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        proximityTrigger = GetComponent<SphereCollider>();
        if (proximityTrigger != null)
        {
            proximityTrigger.isTrigger = true;
        }
    }

    private void OnDisable()
    {
        ClearAllTrackedEntities();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidTrackable(other, out GameObject entityRoot)) return;

        if (activeNearbyEntities.Add(entityRoot))
        {
            GameEvents.World.TriggerEntityEnteredProximity(entityRoot);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsValidTrackable(other, out GameObject entityRoot)) return;

        if (activeNearbyEntities.Remove(entityRoot))
        {
            GameEvents.World.TriggerEntityExitedProximity(entityRoot);
        }
    }

    #endregion

    #region Detection Helpers & Cleanup

    private bool IsValidTrackable(Collider col, out GameObject entityRoot)
    {
        entityRoot = null;

        // Check layer mask match
        if (((1 << col.gameObject.layer) & detectionLayerMask) == 0)
        {
            return false;
        }

        // Check allowed tags
        bool hasValidTag = false;
        for (int i = 0; i < trackableTags.Length; i++)
        {
            if (col.CompareTag(trackableTags[i]))
            {
                hasValidTag = true;
                break;
            }
        }

        if (!hasValidTag) return false;

        entityRoot = col.gameObject;
        return true;
    }

    /// <summary>
    /// Call to purge destroyed or invalid entity references from active tracking.
    /// </summary>
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