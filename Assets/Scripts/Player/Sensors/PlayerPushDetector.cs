using UnityEngine;

/// <summary>
/// Active sensor component located under Player -> Sensors.
/// Probes for PushableBlock components on PsynergyInteractable layers while moving against them,
/// evaluating orthogonal alignment, relative ground heights via downward probes, and push intention timers.
/// </summary>
public class PlayerPushDetector : MonoBehaviour
{
    #region Serialized Fields

    [Header("Player Prefab References")]
    [Tooltip("Direct reference to the root PlayerController component.")]
    [SerializeField] private PlayerController playerController;

    [Tooltip("Direct reference to the root CharacterController component.")]
    [SerializeField] private CharacterController characterController;

    [Header("Detection Settings")]
    [Tooltip("LayerMask containing PsynergyInteractable (and any other interactable layers).")]
    [SerializeField] private LayerMask pushableLayer;

    [Header("Height Validation")]
    [Tooltip("Maximum allowed Y-height difference between player feet and pillar base.")]
    [SerializeField] private float maxHeightDifference = 0.35f;

    [Tooltip("Forward raycast probe distance to detect pushable blocks.")]
    [SerializeField] private float probeDistance = 0.6f;

    [Tooltip("Time in seconds player must continuously push against block before triggering push action.")]
    [SerializeField] private float pushChargeTime = 0.3f;

    [Tooltip("Strict alignment angle threshold between movement input and block normal (~0.85 = ~30 deg).")]
    [Range(0.6f, 0.99f)]
    [SerializeField] private float strictAngleThreshold = 0.75f;

    [Header("Debug Visuals")]
    [SerializeField] private bool showGizmos = true;

    #endregion

    #region Private Fields

    private float currentPushTimer = 0f;

    #endregion

    #region Public Probe Methods

    /// <summary>
    /// Probes forward for PushableBlock components. Called directly from PlayerController during active exploration.
    /// </summary>
    public void CheckForPushableBlock(Vector3 moveDirection, float currentSpeed)
    {
        if (playerController == null || characterController == null) return;

        // Reset timer if player stops moving input
        if (moveDirection.sqrMagnitude < 0.01f)
        {
            currentPushTimer = 0f;
            return;
        }

        Vector3 rayOrigin = playerController.transform.position + Vector3.up * 0.8f;

        if (Physics.Raycast(rayOrigin, moveDirection.normalized, out RaycastHit hit, probeDistance, pushableLayer))
        {
            PushableBlock block = hit.collider.GetComponentInParent<PushableBlock>();

            if (block != null)
            {
                // Measure real ground levels for player feet and block base (using transform position Y and probe fallback)
                float playerGroundY = playerController.transform.position.y;
                float blockGroundY = GetPillarGroundY(block);

                float heightDifference = Mathf.Abs(playerGroundY - blockGroundY);

                if (heightDifference > maxHeightDifference)
                {
                    Debug.LogWarning($"[PUSH DETECTOR] Faltó altura. Dif Y: {heightDifference:F2}m > Max: {maxHeightDifference:F2}m (Jugador Y: {playerGroundY:F2}, Pilar Ground Y: {blockGroundY:F2})");
                    currentPushTimer = 0f;
                    return;
                }

                // Determine cardinal direction closest to impact normal (-hit.normal)
                Vector3 cardinalDir = GetCardinalDirection(-hit.normal);

                // Verify alignment of player move direction against cardinal push direction
                float alignment = Vector3.Dot(moveDirection.normalized, cardinalDir);

                if (alignment < strictAngleThreshold)
                {
                    Debug.LogWarning($"[PUSH DETECTOR] Ángulo/Alineación insuficiente ({alignment:F2} < {strictAngleThreshold:F2}). Apunta más de frente al pilar.");
                    currentPushTimer = 0f;
                    return;
                }

                currentPushTimer += Time.deltaTime;
                Debug.Log($"[PUSH DETECTOR] Carga activa: {currentPushTimer:F2}s / {pushChargeTime:F2}s");

                if (currentPushTimer >= pushChargeTime)
                {
                    currentPushTimer = 0f;
                    Debug.Log("<color=green>[PUSH DETECTOR] ¡Carga completada! Ejecutando empuje...</color>");
                    playerController.ExecuteBlockPush(block, cardinalDir);
                }
                return;
            }
            else
            {
                Debug.LogWarning($"[PUSH DETECTOR] Impactó contra {hit.collider.name} en capa '{LayerMask.LayerToName(hit.collider.gameObject.layer)}', pero NO encontró PushableBlock en sus padres.");
            }
        }

        currentPushTimer = 0f;
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Probes downward from block volume to find the exact ground surface it rests upon.
    /// Prefers direct transform.position.y if pivot is grounded, falling back to raycast for unaligned models.
    /// </summary>
    private float GetPillarGroundY(PushableBlock block)
    {
        // Primary check: Block transform Y position (assumes pivot aligned at base Y = 0)
        float directY = block.transform.position.y;

        // Secondary downward probe from center height to verify true ground collision surface
        Vector3 origin = block.transform.position + Vector3.up * 0.75f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2.0f))
        {
            return hit.point.y;
        }

        return directY;
    }

    /// <summary>
    /// Snaps a 3D direction vector to the nearest orthogonal cardinal direction (+X, -X, +Z, -Z).
    /// </summary>
    private Vector3 GetCardinalDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
        {
            return new Vector3(Mathf.Sign(direction.x), 0f, 0f);
        }
        else
        {
            return new Vector3(0f, 0f, Mathf.Sign(direction.z));
        }
    }

    #endregion

    #region Debug Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Transform rootTransform = playerController != null ? playerController.transform : transform;

        Gizmos.color = Color.cyan;
        Vector3 origin = rootTransform.position + Vector3.up * 0.8f;
        Gizmos.DrawRay(origin, rootTransform.forward * probeDistance);
        Gizmos.DrawWireSphere(origin + rootTransform.forward * probeDistance, 0.05f);

        Gizmos.color = Color.yellow;
        Vector3 feetPos = rootTransform.position;
        Gizmos.DrawWireCube(feetPos, new Vector3(0.4f, maxHeightDifference * 2f, 0.4f));
    }
#endif

    #endregion
}