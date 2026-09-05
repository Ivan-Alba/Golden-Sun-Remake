using UnityEngine;

/// <summary>
/// Handles environmental scanning to detect nearby interactable objects based on proximity and viewing angle.
/// Emits global GameEvents when the active interaction target changes, decoupling gameplay physics from UI rendering.
/// </summary>
public class PlayerInteractionDetector : MonoBehaviour
{
    #region Serialized Fields

    [Header("Detection Parameters")]
    [SerializeField] private float detectionRadius = 1.5f;
    [SerializeField] private float maxInteractionAngle = 50f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private Vector3 detectionCenterOffset = new Vector3(0f, 1f, 0f);

    [Header("Optimization Settings")]
    [SerializeField] private int maxDetectedColliders = 8;

    #endregion

    #region Private Fields

    private Collider[] hitBuffer;
    private Interactable currentTarget;
    private bool isExplorationActive = true;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the currently focused interactable object in front of the player.
    /// </summary>
    public Interactable CurrentTarget => currentTarget;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        hitBuffer = new Collider[maxDetectedColliders];
    }

    private void OnEnable()
    {
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDisable()
    {
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        ClearInteractionTarget();
    }

    private void Update()
    {
        if (!isExplorationActive) return;

        ScanForInteractables();
    }

    #endregion

    #region Event Handlers & State Management

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        isExplorationActive = (newState == GameManager.GameState.WorldExploration);

        if (!isExplorationActive)
        {
            // Silent local reset when leaving exploration state
            currentTarget = null;
        }
    }

    private void ClearInteractionTarget()
    {
        if (currentTarget != null)
        {
            currentTarget = null;
            GameEvents.World.TriggerInteractableTargetChanged(null);
        }
    }

    #endregion

    #region Detection Logic

    /// <summary>
    /// Scans surrounding space for interactables and selects the best candidate aligned with player facing direction.
    /// </summary>
    private void ScanForInteractables()
    {
        Vector3 center = transform.position + detectionCenterOffset;
        int hitCount = Physics.OverlapSphereNonAlloc(center, detectionRadius, hitBuffer, interactableLayer);

        Interactable bestCandidate = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitBuffer[i];
            if (col == null) continue;

            if (col.TryGetComponent<Interactable>(out var interactable))
            {
                Vector3 directionToTarget = (interactable.transform.position - transform.position).normalized;
                directionToTarget.y = 0f; // Ignore vertical height difference for angle calculation

                Vector3 playerForward = transform.forward;
                playerForward.y = 0f;

                float angle = Vector3.Angle(playerForward, directionToTarget);

                // Check if target is within acceptable forward viewing cone
                if (angle <= maxInteractionAngle)
                {
                    float distance = Vector3.Distance(transform.position, interactable.transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        bestCandidate = interactable;
                    }
                }
            }
        }

        // Notify subscribers via global event architecture if target reference changes
        if (currentTarget != bestCandidate)
        {
            currentTarget = bestCandidate;
            GameEvents.World.TriggerInteractableTargetChanged(currentTarget);
        }
    }

    #endregion

    #region Gizmos & Debugging

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position + detectionCenterOffset;

        // Draw detection sphere radius
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
        Gizmos.DrawWireSphere(center, detectionRadius);

        // Draw forward viewing angle cone lines
        Vector3 leftBound = Quaternion.Euler(0, -maxInteractionAngle, 0) * transform.forward;
        Vector3 rightBound = Quaternion.Euler(0, maxInteractionAngle, 0) * transform.forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(center, leftBound * detectionRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(center, rightBound * detectionRadius);
    }

    #endregion
}