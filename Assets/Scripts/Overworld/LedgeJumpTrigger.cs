using UnityEngine;

/// <summary>
/// Passive environment component for cliff edges and puzzle platforms.
/// Validates jump conditions and resolves geometric target centers based on the exact player contact position.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class LedgeJumpTrigger : MonoBehaviour
{
    #region Serialized Fields

    [Header("Layer Masks")]
    [Tooltip("Layer representing valid landing surfaces (Platforms, Pillars, Ground).")]
    [SerializeField] private LayerMask groundLayer;

    [Tooltip("Layer representing blocking obstacles in mid-air.")]
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Grid & Jump Parameters")]
    [Tooltip("Grid tile unit size (e.g. 1.5m).")]
    [SerializeField] private float tileSize = 1.5f;

    [Tooltip("Maximum allowed height difference between start edge and destination platform.")]
    [SerializeField] private float maxJumpHeightDelta = 0.5f;

    [Tooltip("Max size dimension to consider an object an individual puzzle pillar (e.g. <= 2.0m).")]
    [SerializeField] private float maxPillarBoundsSize = 2.0f;

    [Tooltip("Extra forward penetration distance when landing on continuous terrain to clear border colliders.")]
    [SerializeField] private float continuousTerrainLandingOffset = 0.4f;

    #endregion

    #region Public Resolution Methods

    /// <summary>
    /// Evaluates if there is a valid landing surface across the gap along the perpendicular edge normal.
    /// Uses the exact player position to project the scan ray forward.
    /// </summary>
    public bool TryResolveJumpTarget(Vector3 playerPosition, out Vector3 landingTarget, out Collider edgeColliderOut)
    {
        landingTarget = Vector3.zero;
        edgeColliderOut = GetComponent<Collider>();

        // Direccion perpendicular pura del borde de despegue
        Vector3 edgePerpendicularForward = transform.forward;

        // Distancia base: 1 hueco (1.5m) + centro de la casilla destino (0.75m) = 2.25m
        float targetScanDistance = tileSize + (tileSize * 0.5f);

        // 1. El rayo debe partir EXACTAMENTE desde la posición horizontal del jugador
        Vector3 rayOrigin = playerPosition + Vector3.up * 0.5f;

        // Debug visual en la vista Scene (línea verde para ver la sonda en tiempo real)
        Debug.DrawRay(rayOrigin, edgePerpendicularForward * targetScanDistance, Color.green, 0.2f);

        // Comprobar obstáculos en el aire desde la posición del jugador
        if (Physics.Raycast(rayOrigin, edgePerpendicularForward, targetScanDistance, obstacleLayer))
        {
            return false;
        }

        // 2. Prospección hacia abajo en el punto objetivo exacto proyectado desde el jugador
        Vector3 probeOrigin = rayOrigin + (edgePerpendicularForward * targetScanDistance) + (Vector3.up * 1.0f);
        if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit targetHit, 2.5f, groundLayer))
        {
            float heightDelta = Mathf.Abs(targetHit.point.y - playerPosition.y);
            if (heightDelta <= maxJumpHeightDelta)
            {
                Collider hitCollider = targetHit.collider;

                bool isIndividualPillar = false;
                if (hitCollider != null)
                {
                    Vector3 extents = hitCollider.bounds.size;
                    isIndividualPillar = (extents.x <= maxPillarBoundsSize && extents.z <= maxPillarBoundsSize);
                }

                if (isIndividualPillar)
                {
                    // Pilar de puzle ($1.5m \times 1.5m$): Centro geométrico exacto de la plataforma destino
                    landingTarget = hitCollider.bounds.center;
                    landingTarget.y = targetHit.point.y;
                }
                else
                {
                    // Terreno continuo/orilla: Proyectamos directamente desde la posición actual del jugador + offset de entrada
                    float finalLandingDistance = targetScanDistance + continuousTerrainLandingOffset;
                    Vector3 landingPointWorld = playerPosition + (edgePerpendicularForward * finalLandingDistance);

                    landingTarget = new Vector3(landingPointWorld.x, targetHit.point.y, landingPointWorld.z);
                }

                return true;
            }
        }

        return false;
    }

    #endregion
}