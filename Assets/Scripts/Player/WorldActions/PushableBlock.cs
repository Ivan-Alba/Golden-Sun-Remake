using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Attached to the root of puzzle pillars or moveable blocks with pivot aligned at base floor (Y = 0).
/// Inherits from PsynergyInteractable to support both manual physical pushing and Psynergy Move abilities.
/// Validates destination grid tiles (1.5m) using volume BoxCasts to prevent corner clipping,
/// strictly enforces flat step tolerance to trap blocks in valleys/grooves,
/// and manages deterministic physics transitions when falling into water or chasms.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PushableBlock : PsynergyInteractable
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

    #region Private Runtime State

    private bool isMoving;

    #endregion

    #region Properties

    public float TileSize => tileSize;
    public float PushDuration => pushDuration;
    public bool IsMoving => isMoving;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        // Enforce Move settings for PsynergyInteractable base
        requiredWorldAction = WorldActionType.Move;
        singleUse = false; // Block can be pushed multiple times until puzzle logic explicitly locks it

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    #endregion

    #region IPsynergyTargetable Overrides

    public override bool CanBeTargeted()
    {
        // Block cannot be targeted while actively executing a displacement routine
        return base.CanBeTargeted() && !isMoving;
    }

    protected override void OnPsynergyTriggered(PsynergySO psynergy, GameObject player)
    {
        if (player == null)
        {
            CompleteInteraction();
            return;
        }

        Vector3 pullDirection = CalculatePullDirection(player.transform.position);

        EvaluateDirectionChoice(
            pullDirection,
            onSuccess: () =>
            {
                // Transición completada con éxito -> llamamos al cierre de interacción en la base
                CompleteInteraction();
            },
            onBlocked: () =>
            {
                // Movimiento bloqueado -> devolvemos el control inmediatamente mediante la base
                CompleteInteraction();
            }
        );
    }

    #endregion

    #region Public Resolution Methods (Manual & Psynergy Pushing)

    /// <summary>
    /// Checks whether the block can be moved in any of the 4 cardinal directions.
    /// Useful for UI/Controller to determine if the block is completely trapped before presenting prompts.
    /// </summary>
    public bool HasAnyValidPushDirection()
    {
        if (isMoving) return false;

        Vector3[] cardinalDirections = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        for (int i = 0; i < cardinalDirections.Length; i++)
        {
            if (CanBePushed(cardinalDirections[i], out _))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Evaluates a directional input from Psynergy UI prompt or automated pull calculation.
    /// Triggers movement if valid, or fires error feedback and invokes onBlocked if the path is obstructed.
    /// Guaranteed to call either onSuccess or onBlocked in all execution branches.
    /// </summary>
    /// <param name="pushDirection">Normalized target direction vector.</param>
    /// <param name="onSuccess">Callback invoked when the valid push routine finishes moving.</param>
    /// <param name="onBlocked">Callback invoked when the push in this direction is physically blocked.</param>
    public void EvaluateDirectionChoice(Vector3 pushDirection, Action onSuccess, Action onBlocked)
    {
        if (isMoving)
        {
            onBlocked?.Invoke();
            return;
        }

        if (CanBePushed(pushDirection, out bool willFallIntoPit))
        {
            StartCoroutine(Routine_ExecutePush(pushDirection, willFallIntoPit, onSuccess));
        }
        else
        {
            // Play negative error SFX via decoupled event bus
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Error);
            Debug.Log($"[PushableBlock] Direction {pushDirection} is blocked on {gameObject.name}.");
            onBlocked?.Invoke();
        }
    }

    /// <summary>
    /// Attempts to push the block in a specific cardinal direction (Direct/Manual entry point).
    /// Used by physical collision pushes.
    /// </summary>
    public bool TryPushDirection(Vector3 pushDirection, Action onComplete = null)
    {
        if (isMoving) return false;

        if (CanBePushed(pushDirection, out bool willFallIntoPit))
        {
            StartCoroutine(Routine_ExecutePush(pushDirection, willFallIntoPit, onComplete));
            return true;
        }

        return false;
    }

    /// <summary>
/// Checks if the destination grid tile (1.5m ahead) and the path towards it are clear of obstacles, walls, or fences.
/// Uses a combination of BoxCast for the transitional volume and OverlapBox for target tile occupancy.
/// </summary>
public bool CanBePushed(Vector3 pushDirection, out bool willFallIntoPit)
{
    willFallIntoPit = false;

    if (isMoving) return false;

    Vector3 pushDirNormalized = pushDirection.normalized;
    Collider selfCollider = GetComponent<Collider>();

    // --- 1. COMPROBACIÓN DE BARRIDO/CAMINO (Evita atravesar vallas, rejas o paredes delgadas en la frontera) ---
    // El origen parte desde el centro del bloque actual + un pequeño offset frontal
    Vector3 castOrigin = transform.position + (Vector3.up * 0.75f) + (pushDirNormalized * 0.1f);
    
    // Extensión del volumen de barrido (un poco menor que 1.5m para no rozar paredes laterales paralelas)
    float boxWidthExtent = (tileSize * 0.5f) - 0.12f;
    Vector3 sweepHalfExtents = new Vector3(boxWidthExtent, 0.5f, boxWidthExtent);
    
    // Distancia exacta del recorrido (1.5m)
    float sweepDistance = tileSize - 0.15f;

    RaycastHit[] sweepHits = Physics.BoxCastAll(
        castOrigin, 
        sweepHalfExtents, 
        pushDirNormalized, 
        Quaternion.identity, 
        sweepDistance, 
        blockingLayers, 
        QueryTriggerInteraction.Ignore
    );

    for (int i = 0; i < sweepHits.Length; i++)
    {
        Collider hitCol = sweepHits[i].collider;

        // Ignorar el propio collider o hijos del bloque
        if (hitCol == selfCollider || hitCol.transform.IsChildOf(transform))
        {
            continue;
        }

        // ¡Atención! Se ha encontrado una valla, pared u objeto en el camino directo de desplazamiento
        Debug.Log($"[PushableBlock] Push blocked by obstacle in path: {hitCol.name}");
        return false;
    }

    // --- 2. COMPROBACIÓN DE OCUPACIÓN DEL DESTINO (Evita solaparse con cubos u objetos centrados en Tile B) ---
    Vector3 destinationCenter = transform.position + (pushDirNormalized * tileSize) + (Vector3.up * 0.75f);
    Vector3 overlapHalfExtents = new Vector3(boxWidthExtent, 0.5f, boxWidthExtent);

    Collider[] hitColliders = Physics.OverlapBox(
        destinationCenter, 
        overlapHalfExtents, 
        Quaternion.identity, 
        blockingLayers, 
        QueryTriggerInteraction.Ignore
    );

    for (int i = 0; i < hitColliders.Length; i++)
    {
        if (hitColliders[i] == selfCollider || hitColliders[i].transform.IsChildOf(transform))
        {
            continue;
        }

        Debug.Log($"[PushableBlock] Push blocked on {gameObject.name}. Destination occupied by: {hitColliders[i].name}");
        return false;
    }

    // --- 3. PROSPECCIÓN DE SUELO / FOSO (Raycast hacia abajo en Tile B) ---
    Vector3 destinationFloorCenter = transform.position + (pushDirNormalized * tileSize);
    Vector3 probeOrigin = destinationFloorCenter + Vector3.up * 1.0f;

    if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit groundHit, 2.5f, groundLayer))
    {
        float elevationDelta = groundHit.point.y - transform.position.y;

        if (elevationDelta > maxStepTolerance)
        {
            return false; // Bloqueado por escalón superior a la tolerancia (5cm)
        }
    }
    else
    {
        willFallIntoPit = true; // No hay suelo debajo -> El bloque caerá al agua o foso
    }

    return true;
}

    /// <summary>
    /// Legacy/Direct entry point for smooth grid-aligned displacement routine.
    /// Wrapper maintained for full compatibility with manual push systems.
    /// </summary>
    public IEnumerator MoveBlockRoutine(Vector3 pushDirection, bool fallsIntoPit)
    {
        yield return StartCoroutine(Routine_ExecutePush(pushDirection, fallsIntoPit, null));
    }

    #endregion

    #region Private Internal Routines

    /// <summary>
    /// Calculates the primary cardinal direction (+X, -X, +Z, -Z) to pull the block toward the caster position.
    /// </summary>
    private Vector3 CalculatePullDirection(Vector3 casterPosition)
    {
        Vector3 toCaster = casterPosition - transform.position;
        toCaster.y = 0f; // Constrain strictly to horizontal plane

        if (Mathf.Abs(toCaster.x) > Mathf.Abs(toCaster.z))
        {
            return new Vector3(Mathf.Sign(toCaster.x), 0f, 0f);
        }
        else
        {
            return new Vector3(0f, 0f, Mathf.Sign(toCaster.z));
        }
    }

    private IEnumerator Routine_ExecutePush(Vector3 pushDirection, bool fallsIntoPit, Action onComplete)
    {
        isMoving = true;

        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.Interacting);

        try
        {
            Vector3 startPos = transform.position;
            Vector3 direction = pushDirection.normalized;

            // Calculate exact target position based on full tile displacement
            Vector3 targetPos = startPos + (direction * tileSize);

            float elapsedTime = 0f;

            while (elapsedTime < pushDuration)
            {
                transform.position = Vector3.Lerp(startPos, targetPos, elapsedTime / pushDuration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Exact snap to target coordinate to prevent float precision drift
            transform.position = targetPos;

            if (fallsIntoPit)
            {
                yield return StartCoroutine(FallIntoPitRoutine());
            }
        }
        finally
        {
            isMoving = false;

            // Callback to release player input lock in Controller / Manager once movement fully settles
            onComplete?.Invoke();

            GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);

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