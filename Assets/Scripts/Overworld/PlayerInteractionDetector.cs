using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Passive physics trigger sensor attached to the Player's interaction sensor transform.
/// Tracks nearby interactables entering the physical trigger and evaluates viewing angle cones
/// to identify the best candidate, emitting global events upon focus shifts.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class PlayerInteractionDetector : MonoBehaviour
{
    #region Serialized Fields

    [Header("Detection Parameters")]
    [SerializeField] private float maxInteractionAngle = 50f;
    [SerializeField] private LayerMask interactableLayer;

    [Header("Re-evaluation Settings")]
    [SerializeField] private int maxReevaluationColliders = 8;

    #endregion

    #region Private Runtime State

    private SphereCollider interactionTrigger;
    private readonly HashSet<Interactable> nearbyCandidates = new HashSet<Interactable>();
    private readonly List<Interactable> invalidCandidatesBuffer = new List<Interactable>();
    private Collider[] reevaluationBuffer;

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
        interactionTrigger = GetComponent<SphereCollider>();
        if (interactionTrigger != null)
        {
            interactionTrigger.isTrigger = true;
        }

        reevaluationBuffer = new Collider[maxReevaluationColliders];
    }

    private void OnEnable()
    {
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDisable()
    {
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        ClearAllCandidates();
    }

    private void Update()
    {
        if (!isExplorationActive) return;

        EvaluateBestCandidate();
    }

    #endregion

    #region PhysX Triggers

    private void OnTriggerEnter(Collider other)
    {
        if (!isExplorationActive) return;

        // 1. Layer Mask check
        if (((1 << other.gameObject.layer) & interactableLayer) == 0) return;

        // 2. Fetch component
        if (other.TryGetComponent<Interactable>(out var interactable) ||
            other.GetComponentInParent<Interactable>() is Interactable parentInteractable && (interactable = parentInteractable) != null)
        {
            if (nearbyCandidates.Add(interactable))
            {
                EvaluateBestCandidate();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isExplorationActive) return;

        if (other.TryGetComponent<Interactable>(out var interactable) ||
            other.GetComponentInParent<Interactable>() is Interactable parentInteractable && (interactable = parentInteractable) != null)
        {
            if (nearbyCandidates.Remove(interactable))
            {
                if (currentTarget == interactable)
                {
                    EvaluateBestCandidate();
                }
            }
        }
    }

    #endregion

    #region Event Handlers & State Management

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        isExplorationActive = (newState == GameManager.GameState.WorldExploration);

        if (isExplorationActive)
        {
            ReevaluateInteractionArea();
        }
        else
        {
            ClearAllCandidates();
        }
    }

    /// <summary>
    /// Performs an explicit overlap query to repopulate candidate targets when returning to exploration state.
    /// </summary>
    public void ReevaluateInteractionArea()
    {
        nearbyCandidates.Clear();

        if (interactionTrigger == null) return;

        Vector3 center = transform.TransformPoint(interactionTrigger.center);
        float radius = interactionTrigger.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);

        int hitCount = Physics.OverlapSphereNonAlloc(center, radius, reevaluationBuffer, interactableLayer);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = reevaluationBuffer[i];
            if (col == null) continue;

            if (col.TryGetComponent<Interactable>(out var interactable) ||
                col.GetComponentInParent<Interactable>() is Interactable parentInteractable && (interactable = parentInteractable) != null)
            {
                nearbyCandidates.Add(interactable);
            }
        }

        EvaluateBestCandidate();
    }

    private void ClearAllCandidates()
    {
        nearbyCandidates.Clear();
        if (currentTarget != null)
        {
            currentTarget = null;
            GameEvents.World.TriggerInteractableTargetChanged(null);
        }
    }

    #endregion

    #region Candidate Evaluation Logic

    /// <summary>
    /// Evaluates nearby candidates inside the trigger and selects the closest target within the viewing angle cone.
    /// </summary>
    private void EvaluateBestCandidate()
    {
        Interactable bestCandidate = null;
        float minDistance = float.MaxValue;
        invalidCandidatesBuffer.Clear();

        Vector3 playerForward = transform.forward;
        playerForward.y = 0f;
        playerForward.Normalize();

        Vector3 originPos = transform.position;

        foreach (var interactable in nearbyCandidates)
        {
            if (interactable == null || !interactable.gameObject.activeInHierarchy)
            {
                invalidCandidatesBuffer.Add(interactable);
                continue;
            }

            Vector3 targetPos = interactable.transform.position;
            Vector3 directionToTarget = new Vector3(targetPos.x - originPos.x, 0f, targetPos.z - originPos.z);

            if (directionToTarget.sqrMagnitude < 0.0001f)
            {
                directionToTarget = playerForward;
            }
            else
            {
                directionToTarget.Normalize();
            }

            float angle = Vector3.Angle(playerForward, directionToTarget);

            if (angle <= maxInteractionAngle)
            {
                float distance = Vector3.Distance(new Vector3(originPos.x, 0, originPos.z), new Vector3(targetPos.x, 0, targetPos.z));
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestCandidate = interactable;
                }
            }
        }

        for (int i = 0; i < invalidCandidatesBuffer.Count; i++)
        {
            nearbyCandidates.Remove(invalidCandidatesBuffer[i]);
        }

        if (currentTarget != bestCandidate)
        {
            currentTarget = bestCandidate;
            GameEvents.World.TriggerInteractableTargetChanged(currentTarget);
        }
    }

    #endregion

    #region Gizmos & Debugging

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (interactionTrigger == null) interactionTrigger = GetComponent<SphereCollider>();

        Vector3 center = transform.position;
        float radius = interactionTrigger != null ? interactionTrigger.radius : 1.5f;

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
        Gizmos.DrawWireSphere(center, radius);

        Vector3 forwardDir = transform.forward;
        forwardDir.y = 0f;
        forwardDir.Normalize();

        Vector3 leftBound = Quaternion.Euler(0, -maxInteractionAngle, 0) * forwardDir;
        Vector3 rightBound = Quaternion.Euler(0, maxInteractionAngle, 0) * forwardDir;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(center, leftBound * radius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(center, rightBound * radius);
    }
#endif

    #endregion
}