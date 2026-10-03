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
    /// Checks if the destination grid tile (1.5m ahead) is clear of obstacles or adjacent pillars.
    /// Evaluates volume clearance up to the exact boundary of the target tile.
    /// </summary>
    public bool CanBePushed(Vector3 pushDirection, out bool willFallIntoPit)
    {
        willFallIntoPit = false;

        Vector3 pushDirNormalized = pushDirection.normalized;

        // 1. Margen lateral para no rozar paredes paralelas (1.2m de ancho sobre 1.5m de tile)
        float boxWidthExtent = (tileSize * 0.5f) - 0.15f;
        Vector3 boxHalfExtents = new Vector3(boxWidthExtent, 0.4f, boxWidthExtent);

        // 2. Origen desde la cara frontal exacta del pilar (0.75m desde el pivote en la base)
        Vector3 frontFaceOrigin = transform.position + Vector3.up * 0.75f + (pushDirNormalized * (tileSize * 0.5f));

        // 3. Distancia del BoxCast: Recorre los 0.75m restantes de la casilla destino con pequeña tolerancia (0.05m)
        float castDistance = (tileSize * 0.5f) - 0.05f;

        if (Physics.BoxCast(frontFaceOrigin, boxHalfExtents, pushDirNormalized, Quaternion.identity, castDistance, blockingLayers))
        {
            return false; // Bloqueado si hay un pilar u obstáculo ocupando la casilla destino B
        }

        // 4. Downward Probe: Valida la presencia y elevación del suelo en la casilla destino B
        Vector3 destinationCenter = transform.position + (pushDirNormalized * tileSize);
        Vector3 probeOrigin = destinationCenter + Vector3.up * 1.0f;

        if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit groundHit, 2.5f, groundLayer))
        {
            float elevationDelta = groundHit.point.y - transform.position.y;

            if (elevationDelta > maxStepTolerance)
            {
                return false; // Bloqueado por escalón superior a 5cm
            }
        }
        else
        {
            willFallIntoPit = true; // No hay suelo -> Caerá al foso/agua
        }

        return true;
    }

    /// <summary>
    /// Executes a smooth grid-aligned displacement routine for the block.
    /// </summary>
    public IEnumerator MoveBlockRoutine(Vector3 pushDirection, bool fallsIntoPit)
    {
        Vector3 startPos = transform.position;
        Vector3 direction = pushDirection.normalized;

        // Calculamos la posición destino exacta basada en el tile entero desde la posición inicial
        Vector3 targetPos = startPos + (direction * tileSize);

        float elapsedTime = 0f;

        while (elapsedTime < pushDuration)
        {
            transform.position = Vector3.Lerp(startPos, targetPos, elapsedTime / pushDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Snap final exacto a la coordenada calculada (sin acumulación de imprecisiones de float)
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