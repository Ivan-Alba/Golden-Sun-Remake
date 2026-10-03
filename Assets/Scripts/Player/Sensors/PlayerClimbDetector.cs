using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Active sensor component located under Player -> Sensors.
/// Probes forward and downward for ClimbablePath components on Climbable layers,
/// evaluating proximity, wall normal alignment, and mount entry points (ground or top ledge).
/// </summary>
public class PlayerClimbDetector : MonoBehaviour
{
    #region Serialized Fields

    [Header("Player Prefab References")]
    [Tooltip("Direct reference to the root PlayerController component.")]
    [SerializeField] private PlayerController playerController;

    [Tooltip("Direct reference to the root CharacterController component.")]
    [SerializeField] private CharacterController characterController;

    [Header("Detection Settings")]
    [Tooltip("LayerMask containing Climbable surfaces (e.g., Ladders, Vines, Rock Walls).")]
    [SerializeField] private LayerMask climbableLayer;

    [Tooltip("Forward raycast probe distance to detect climbable paths while moving on ground.")]
    [SerializeField] private float probeDistance = 0.5f;

    [Tooltip("Forward offset distance in front of player feet when probing for top ledge descent.")]
    [SerializeField] private float topLedgeForwardOffset = 0.4f;

    [Tooltip("Downward probe distance to detect ladder top exits when approaching a ledge to descend.")]
    [SerializeField] private float topLedgeProbeDistance = 1.0f;

    [Tooltip("Maximum distance allowed between player position and the climbing path line.")]
    [SerializeField] private float maxAttachDistance = 0.8f;

    [Header("Debug Visuals")]
    [SerializeField] private bool showGizmos = true;

    #endregion

    #region Public Probe Methods

    /// <summary>
    /// Probes for a nearby ClimbablePath during free exploration movement.
    /// Returns true if a path is detected, setting initial segment index and whether entry is from top ledge.
    /// </summary>
    public bool TryDetectClimbablePath(Vector3 moveDirection, out ClimbablePath detectedPath, out Vector3 attachPoint, out int initialSegmentIndex, out bool isMountingFromTop)
    {
        detectedPath = null;
        attachPoint = Vector3.zero;
        initialSegmentIndex = -1;
        isMountingFromTop = false;

        if (playerController == null || moveDirection.sqrMagnitude < 0.01f) return false;

        Vector3 playerPos = playerController.transform.position;
        Vector3 forwardOrigin = playerPos + Vector3.up * 0.8f;

        // 1. Proyección Frontal: Detectar pared/escalera justo enfrente (subir desde el suelo)
        if (Physics.Raycast(forwardOrigin, moveDirection.normalized, out RaycastHit hit, probeDistance, climbableLayer))
        {
            ClimbablePath path = hit.collider.GetComponentInParent<ClimbablePath>();

            if (path != null && path.GetClosestPointOnPath(playerPos, out Vector3 closestPoint, out int segmentIndex))
            {
                float distSqr = (playerPos - closestPoint).sqrMagnitude;

                if (distSqr <= maxAttachDistance * maxAttachDistance)
                {
                    detectedPath = path;
                    attachPoint = closestPoint;
                    initialSegmentIndex = segmentIndex;
                    isMountingFromTop = false;
                    return true;
                }
            }
        }

        // 2. Proyección en Cornisa (Top Ledge Probe): Detectar borde de escalera al caminar hacia el saliente para bajar
        Vector3 ledgeOrigin = playerPos + (moveDirection.normalized * topLedgeForwardOffset) + Vector3.up * 0.2f;

        if (Physics.Raycast(ledgeOrigin, Vector3.down, out RaycastHit ledgeHit, topLedgeProbeDistance, climbableLayer))
        {
            ClimbablePath path = ledgeHit.collider.GetComponentInParent<ClimbablePath>();

            if (path != null && path.GetClosestPointOnPath(playerPos, out Vector3 closestPoint, out int segmentIndex))
            {
                List<ClimbSegment> segments = path.GetWorldSegments();
                if (segments.Count > 0 && segmentIndex == segments.Count - 1)
                {
                    detectedPath = path;
                    attachPoint = closestPoint;
                    initialSegmentIndex = segmentIndex;
                    isMountingFromTop = true; // Flag indicando que se entra desde el saliente superior
                    return true;
                }
            }
        }

        return false;
    }

    #endregion

    #region Debug Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Transform rootTransform = playerController != null ? playerController.transform : transform;

        // Visualización del probe frontal
        Gizmos.color = Color.green;
        Vector3 forwardOrigin = rootTransform.position + Vector3.up * 0.8f;
        Gizmos.DrawRay(forwardOrigin, rootTransform.forward * probeDistance);
        Gizmos.DrawWireSphere(forwardOrigin + rootTransform.forward * probeDistance, 0.05f);

        // Visualización del probe de cornisa para bajar
        Gizmos.color = Color.yellow;
        Vector3 ledgeOrigin = rootTransform.position + (rootTransform.forward * topLedgeForwardOffset) + Vector3.up * 0.2f;
        Gizmos.DrawRay(ledgeOrigin, Vector3.down * topLedgeProbeDistance);
    }
#endif

    #endregion
}