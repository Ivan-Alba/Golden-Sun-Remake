using System.Collections;
using UnityEngine;

/// <summary>
/// Attached to the root of puzzle pillars or moveable blocks with pivot aligned at base floor (Y = 0).
/// Validates destination grid tiles (1.5m) using volume BoxCasts to prevent corner clipping,
/// strictly enforces flat step tolerance to trap blocks in valleys/grooves,
/// and manages deterministic physics transitions when falling into water or chasms.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PushableBlock : MonoBehaviour
{
    #region Serialized Fields

    [Header("Grid & Movement Parameters")]
    [Tooltip("Grid tile size matching system standards (1.5m).")]
    [SerializeField] private float tileSize = 1.5f;

    [Tooltip("Duration in seconds of the smooth pushing displacement.")]
    [SerializeField] private float pushDuration = 0.5f;

    [Tooltip("Strict maximum allowed elevation rise (e.g. 0.05m = 5cm) at destination tile. Higher steps block the push.")]
    [SerializeField] private float maxStepTolerance = 0.05f;

    [Tooltip("Layer mask representing solid obstacles blocking block movement (Must include Ground, Obstacle, Default, PsynergyInteractable).")]
    [SerializeField] private LayerMask blockingLayers;

    [Tooltip("Layer mask representing valid ground beneath the block destination.")]
    [SerializeField] private LayerMask groundLayer;

    [Header("Pit & Water Drop Parameters")]
    [Tooltip("Distance the block falls vertically when pushed into water/chasm.")]
    [SerializeField] private float fallDistance = 1.2f;

    [Tooltip("Duration of the falling animation into water.")]
    [SerializeField] private float fallDuration = 0.35f;

    #endregion

    #region Properties

    public float TileSize => tileSize;
    public float PushDuration => pushDuration;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    #endregion

    #region Public Resolution Methods

    /// <summary>
    /// Checks if the volume of the block can advance 1.5m without clipping corners or walls.
    /// </summary>
    public bool CanBePushed(Vector3 pushDirection, out bool willFallIntoPit)
    {
        willFallIntoPit = false;

        Vector3 pushDirNormalized = pushDirection.normalized;
        float halfExtent = (tileSize * 0.5f) - 0.08f; // Margin to avoid touching parallel walls (~0.67m)

        // Volume BoxCast centered at pillar midpoint (Y = 0.75m)
        Vector3 boxHalfExtents = new Vector3(halfExtent, 0.4f, halfExtent);

        // Origin projected slightly in front of the forward face
        Vector3 frontFaceOrigin = transform.position + Vector3.up * 0.75f + (pushDirNormalized * (tileSize * 0.5f + 0.1f));

        // 1. BoxCast de Volumen Frontal: Detecta muros, obstáculos o elevaciones laterales del terreno
        if (Physics.BoxCast(frontFaceOrigin, boxHalfExtents, pushDirNormalized, Quaternion.identity, tileSize - 0.2f, blockingLayers))
        {
            return false;
        }

        // 2. Downward Probe: Valida el suelo de la casilla destino
        Vector3 destinationCenter = transform.position + (pushDirNormalized * tileSize);
        Vector3 probeOrigin = destinationCenter + Vector3.up * 1.0f;

        if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit groundHit, 2.5f, groundLayer))
        {
            // Con pivote en la base, transform.position.y ES la cota real del suelo
            float elevationDelta = groundHit.point.y - transform.position.y;

            // Bloquea si el terreno sube más allá de la tolerancia (5cm)
            if (elevationDelta > maxStepTolerance)
            {
                return false;
            }
        }
        else
        {
            willFallIntoPit = true; // No hay suelo -> Caerá al agua/foso
        }

        return true;
    }

    /// <summary>
    /// Executes a smooth grid-aligned displacement routine for the block.
    /// </summary>
    public IEnumerator MoveBlockRoutine(Vector3 pushDirection, bool fallsIntoPit)
    {
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + (pushDirection.normalized * tileSize);
        float elapsedTime = 0f;

        while (elapsedTime < pushDuration)
        {
            transform.position = Vector3.Lerp(startPos, targetPos, elapsedTime / pushDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;

        if (fallsIntoPit)
        {
            yield return StartCoroutine(FallIntoPitRoutine());
        }
    }

    private IEnumerator FallIntoPitRoutine()
    {
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + Vector3.down * fallDistance;
        float elapsedTime = 0f;

        while (elapsedTime < fallDuration)
        {
            float t = elapsedTime / fallDuration;
            transform.position = Vector3.Lerp(startPos, targetPos, t * t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
    }

    #endregion
}