using UnityEngine;

/// <summary>
/// Active sensor component located inside the Player prefab structure.
/// Probes for adjacent LedgeJumpTrigger colliders while moving and triggers auto-jumps deterministically.
/// </summary>
public class PlayerLedgeDetector : MonoBehaviour
{
    #region Serialized Fields

    [Header("Player Prefab References")]
    [Tooltip("Direct reference to the root PlayerController component.")]
    [SerializeField] private PlayerController playerController;

    [Tooltip("Direct reference to the root CharacterController component.")]
    [SerializeField] private CharacterController characterController;

    [Header("Detection Settings")]
    [Tooltip("Layer mask assigned to LedgeJumpTrigger colliders.")]
    [SerializeField] private LayerMask ledgeLayer;

    [Tooltip("Forward spherecast distance to sense edges.")]
    [SerializeField] private float probeDistance = 0.45f;

    [Tooltip("Spherecast radius matching character body bounds.")]
    [SerializeField] private float probeRadius = 0.25f;

    [Tooltip("Minimum movement input speed required to evaluate jumps.")]
    [SerializeField] private float minSpeedThreshold = 1.0f;

    [Tooltip("Strict alignment threshold between movement direction and edge normal (0.85 = ~30 deg angle).")]
    [Range(0.5f, 0.99f)]
    [SerializeField] private float strictAngleThreshold = 0.85f;

    [Header("Debug Visuals")]
    [SerializeField] private bool showGizmos = true;

    #endregion

    #region Public Probe Methods

    /// <summary>
    /// Evaluates forward surroundings for jump triggers. Called directly from PlayerController during active movement.
    /// </summary>
    public void CheckForLedgeJump(Vector3 moveDirection, float currentSpeed)
    {
        if (playerController == null || characterController == null) return;
        if (currentSpeed < minSpeedThreshold || moveDirection.sqrMagnitude < 0.01f) return;

        Vector3 playerRootPosition = playerController.transform.position;
        Vector3 rayOrigin = playerRootPosition + Vector3.up * (characterController.stepOffset + 0.1f);

        if (Physics.SphereCast(rayOrigin, probeRadius, moveDirection.normalized, out RaycastHit hit, probeDistance, ledgeLayer))
        {
            if (hit.collider.TryGetComponent<LedgeJumpTrigger>(out var ledgeTrigger))
            {
                // Verify strict alignment with edge forward vector
                float alignment = Vector3.Dot(moveDirection.normalized, ledgeTrigger.transform.forward);

                if (alignment >= strictAngleThreshold)
                {
                    if (ledgeTrigger.TryResolveJumpTarget(playerRootPosition, out Vector3 targetPosition, out _))
                    {
                        // Trigger auto-jump (Layer collision is globally handled inside PlayerController)
                        playerController.ExecuteAutoJumpFromLedge(targetPosition, ledgeTrigger.transform.forward);
                    }
                }
            }
        }
    }

    #endregion

    #region Debug Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Transform rootTransform = playerController != null ? playerController.transform : transform;

        Gizmos.color = Color.yellow;
        Vector3 origin = rootTransform.position + Vector3.up * 0.4f;
        Gizmos.DrawWireSphere(origin + rootTransform.forward * probeDistance, probeRadius);
        Gizmos.DrawLine(origin, origin + rootTransform.forward * probeDistance);
    }
#endif

    #endregion
}