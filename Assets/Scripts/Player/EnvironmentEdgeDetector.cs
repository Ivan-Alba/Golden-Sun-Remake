using UnityEngine;

/// <summary>
/// Handles environment probing and tile-aligned auto-jump target resolution.
/// Restores the original single-raycast logic for clean, smooth slope navigation and edge stopping.
/// </summary>
public class EnvironmentEdgeDetector : MonoBehaviour
{
    #region Serialized Fields

    [Header("Layer Masks")]
    [Tooltip("Layer mask representing valid walkable surfaces (Ground, Platforms, Pillars).")]
    [SerializeField] private LayerMask groundLayer;

    [Tooltip("Layer mask representing crossable gaps or hazards (Water, Lava, Chasms) that can be jumped over.")]
    [SerializeField] private LayerMask jumpableGapLayer;

    [Tooltip("Layer mask representing physical blocking obstacles that prohibit jumps.")]
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Edge Probe Settings")]
    [Tooltip("Forward distance offset from character radius for the single ray probe.")]
    [SerializeField] private float frontMargin = 0.2f;

    [Tooltip("Raycast distance downwards to check for walkable ground.")]
    [SerializeField] private float groundCheckDistance = 1.0f;

    [Header("Grid Jump Settings")]
    [Tooltip("Base distance equivalent to 1 puzzle tile unit in world space (e.g. 1.5m).")]
    [SerializeField] private float tileSize = 1.5f;

    [Tooltip("Maximum number of tiles the player can jump horizontally across a gap.")]
    [SerializeField] private int maxJumpTiles = 1;

    [Tooltip("Maximum height variation allowed between starting edge and landing target.")]
    [SerializeField] private float maxHorizontalJumpHeightDelta = 0.35f;

    [Header("Debug Visuals")]
    [SerializeField] private bool showGizmos = true;

    #endregion

    #region Private Fields

    private CharacterController controller;
    private Vector3 lastDetectedJumpTarget;
    private bool isJumpTargetAvailable;

    #endregion

    #region Properties

    public bool IsJumpTargetAvailable => isJumpTargetAvailable;
    public Vector3 LastDetectedJumpTarget => lastDetectedJumpTarget;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    #endregion

    #region Public Probe Methods

    /// <summary>
    /// Evaluates if an edge or gap is directly ahead using the original single-ray logic.
    /// Returns true if NO ground is detected ahead in the movement direction.
    /// </summary>
    /// <param name="moveDirection">Normalized horizontal movement vector.</param>
    /// <returns>True if movement should be blocked due to missing ground ahead.</returns>
    public bool IsEdgeAhead(Vector3 moveDirection)
    {
        if (controller == null || moveDirection.sqrMagnitude < 0.001f) return false;

        Vector3 forward = moveDirection.normalized;
        float probeDistance = controller.radius + frontMargin;
        float rayStartHeight = controller.stepOffset + 0.1f;

        Vector3 rayOrigin = transform.position + (forward * probeDistance) + (Vector3.up * rayStartHeight);

        // Original logic: Single Raycast down. If hit ground, NO edge. If missed, EDGE.
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayStartHeight + groundCheckDistance, groundLayer))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Scans discrete tile increments ahead across crossable gaps/hazards to locate a safe, 
    /// horizontal same-level ground target for auto-jumping.
    /// </summary>
    public bool TryFindJumpTarget(Vector3 moveDirection, out Vector3 outJumpTarget)
    {
        outJumpTarget = Vector3.zero;
        isJumpTargetAvailable = false;

        if (controller == null || moveDirection.sqrMagnitude < 0.001f) return false;

        Vector3 forward = moveDirection.normalized;
        Vector3 rayOrigin = transform.position + Vector3.up * (controller.stepOffset + 0.2f);

        // 1. Inspect ground immediately in front
        Vector3 immediateFrontOrigin = rayOrigin + (forward * (controller.radius + frontMargin));
        if (Physics.Raycast(immediateFrontOrigin, Vector3.down, out RaycastHit immediateHit, 1.0f))
        {
            int hitLayerMask = 1 << immediateHit.collider.gameObject.layer;
            bool isCrossableGap = (hitLayerMask & jumpableGapLayer) != 0;
            bool isWalkableGround = (hitLayerMask & groundLayer) != 0;

            if (isWalkableGround || !isCrossableGap)
            {
                return false;
            }
        }

        // 2. Scan discrete tile distances
        for (int tileIndex = 1; tileIndex <= maxJumpTiles; tileIndex++)
        {
            float targetDistance = tileSize * tileIndex;

            if (Physics.Raycast(rayOrigin, forward, targetDistance, obstacleLayer))
            {
                return false;
            }

            Vector3 targetProbeOrigin = rayOrigin + (forward * targetDistance) + (Vector3.up * 0.5f);
            float probeRayLength = 1.0f + maxHorizontalJumpHeightDelta;

            if (Physics.Raycast(targetProbeOrigin, Vector3.down, out RaycastHit hit, probeRayLength, groundLayer))
            {
                float heightDelta = Mathf.Abs(hit.point.y - transform.position.y);

                if (heightDelta <= maxHorizontalJumpHeightDelta)
                {
                    outJumpTarget = hit.point;
                    lastDetectedJumpTarget = outJumpTarget;
                    isJumpTargetAvailable = true;
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

        Gizmos.color = isJumpTargetAvailable ? Color.cyan : Color.yellow;
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        Gizmos.DrawWireSphere(origin, controller != null ? controller.radius + frontMargin : 0.4f);

        if (isJumpTargetAvailable)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(lastDetectedJumpTarget, 0.25f);
            Gizmos.DrawLine(transform.position, lastDetectedJumpTarget);
        }
    }
#endif

    #endregion
}