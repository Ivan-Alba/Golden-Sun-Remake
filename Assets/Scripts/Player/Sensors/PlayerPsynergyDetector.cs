using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Passive physics sensor attached to the Player that detects nearby IPsynergyTargetable objects.
/// Handles proximity visual highlights (subtle glow) within trigger radius and evaluates the best centered candidate
/// within effective cast distance relative to camera view to establish the active Psynergy target.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class PlayerPsynergyDetector : MonoBehaviour
{
    #region Serialized Fields

    [Header("Detection Configuration")]
    [Tooltip("Layer mask specifying the PsynergyInteractable layer.")]
    [SerializeField] private LayerMask psynergyLayerMask;

    [Tooltip("Maximum distance from player to target required to gain focus and execute Psynergy.")]
    [SerializeField] private float maxCastDistance = 8f;

    [Tooltip("Maximum angle cone from camera/player forward vector to consider a target valid.")]
    [SerializeField] private float maxFocusAngle = 60f;

    [Header("Re-evaluation Settings")]
    [SerializeField] private int maxReevaluationColliders = 16;

    [Tooltip("Layer mask that blocks Line of Sight between player and target (e.g., Default, Environment, Obstacles).")]
    [SerializeField] private LayerMask losBlockingLayers;

    #endregion

    #region Private Runtime State

    private SphereCollider proximityTrigger;
    private readonly HashSet<IPsynergyTargetable> activeCandidates = new HashSet<IPsynergyTargetable>();
    private readonly List<IPsynergyTargetable> invalidCandidatesBuffer = new List<IPsynergyTargetable>();
    private Collider[] reevaluationBuffer;

    private IPsynergyTargetable currentFocusedTarget;
    private UnityEngine.Camera mainCamera;
    private bool isExplorationActive = true;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the currently focused IPsynergyTargetable candidate inside effective cast distance.
    /// </summary>
    public IPsynergyTargetable CurrentFocusedTarget => currentFocusedTarget;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        proximityTrigger = GetComponent<SphereCollider>();
        if (proximityTrigger != null)
        {
            proximityTrigger.isTrigger = true;
        }

        reevaluationBuffer = new Collider[maxReevaluationColliders];
        mainCamera = UnityEngine.Camera.main;
    }

    private void OnEnable()
    {
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;
        GameEvents.Camera.OnActiveCameraChanged += HandleActiveCameraChanged;
    }

    private void OnDisable()
    {
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        GameEvents.Camera.OnActiveCameraChanged -= HandleActiveCameraChanged;
        ClearAllTrackedTargets();
    }

    private void Update()
    {
        if (!isExplorationActive || activeCandidates.Count == 0) return;

        EvaluateBestCandidate();
    }

    #endregion

    #region PhysX Triggers

    private void OnTriggerEnter(Collider other)
    {
        if (!isExplorationActive) return;

        if (!IsValidTargetable(other, out IPsynergyTargetable targetable)) return;

        if (activeCandidates.Add(targetable))
        {
            targetable.OnProximityEntered();
            EvaluateBestCandidate();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isExplorationActive) return;

        if (!IsValidTargetable(other, out IPsynergyTargetable targetable)) return;

        if (activeCandidates.Remove(targetable))
        {
            targetable.OnProximityExited();

            if (currentFocusedTarget == targetable)
            {
                targetable.OnTargetLost();
                currentFocusedTarget = null;
                GameEvents.World.TriggerPsynergyTargetChanged(null);
                EvaluateBestCandidate();
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
            ReevaluateProximityArea();
        }
        else
        {
            ClearAllTrackedTargets();
        }
    }

    private void HandleActiveCameraChanged(UnityEngine.Camera newCamera)
    {
        if (newCamera != null)
        {
            mainCamera = newCamera;
        }
    }

    public void ReevaluateProximityArea()
    {
        ClearAllTrackedTargets();

        if (proximityTrigger == null) return;

        Vector3 center = transform.TransformPoint(proximityTrigger.center);
        float radius = proximityTrigger.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);

        int hitCount = Physics.OverlapSphereNonAlloc(center, radius, reevaluationBuffer, psynergyLayerMask);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = reevaluationBuffer[i];
            if (col == null) continue;

            if (IsValidTargetable(col, out IPsynergyTargetable targetable))
            {
                if (activeCandidates.Add(targetable))
                {
                    targetable.OnProximityEntered();
                }
            }
        }

        EvaluateBestCandidate();
    }

    private void ClearAllTrackedTargets()
    {
        foreach (var candidate in activeCandidates)
        {
            if (candidate != null)
            {
                candidate.OnProximityExited();
                candidate.OnTargetLost();
            }
        }

        activeCandidates.Clear();

        if (currentFocusedTarget != null)
        {
            currentFocusedTarget = null;
            GameEvents.World.TriggerPsynergyTargetChanged(null);
        }
    }

    #endregion

    #region Candidate Evaluation Logic

    private void EvaluateBestCandidate()
    {
        IPsynergyTargetable bestCandidate = null;
        float minAngle = float.MaxValue;
        invalidCandidatesBuffer.Clear();

        Transform referenceTransform = mainCamera != null ? mainCamera.transform : transform;
        Vector3 referenceForward = referenceTransform.forward;
        Vector3 originPos = transform.position;

        foreach (var candidate in activeCandidates)
        {
            if (candidate == null || !candidate.CanBeTargeted())
            {
                invalidCandidatesBuffer.Add(candidate);
                continue;
            }

            Vector3 targetPos = candidate.Transform.position;
            Vector3 directionToTarget = targetPos - originPos;
            float distanceToTarget = directionToTarget.magnitude;

            // 1. Distance check against effective cast range
            if (distanceToTarget > maxCastDistance) continue;

            // 2. View angle cone check
            directionToTarget.Normalize();
            float angle = Vector3.Angle(referenceForward, directionToTarget);

            if (angle <= maxFocusAngle && angle < minAngle)
            {
                minAngle = angle;
                bestCandidate = candidate;
            }
        }

        // Clean up destroyed or invalidated candidates
        for (int i = 0; i < invalidCandidatesBuffer.Count; i++)
        {
            IPsynergyTargetable invalid = invalidCandidatesBuffer[i];
            invalid.OnProximityExited();
            activeCandidates.Remove(invalid);
        }

        if (currentFocusedTarget != bestCandidate)
        {
            if (currentFocusedTarget != null)
            {
                currentFocusedTarget.OnTargetLost();
            }

            currentFocusedTarget = bestCandidate;

            if (currentFocusedTarget != null)
            {
                currentFocusedTarget.OnTargetGained();
            }

            GameEvents.World.TriggerPsynergyTargetChanged(currentFocusedTarget);
        }
    }

    #endregion

    #region Detection Helpers

    private bool IsValidTargetable(Collider col, out IPsynergyTargetable targetable)
    {
        targetable = null;

        if (((1 << col.gameObject.layer) & psynergyLayerMask) == 0) return false;

        if (col.TryGetComponent<IPsynergyTargetable>(out targetable) ||
            col.GetComponentInParent<IPsynergyTargetable>() is IPsynergyTargetable parentTargetable && (targetable = parentTargetable) != null)
        {
            return targetable.CanBeTargeted();
        }

        return false;
    }

    #endregion

    #region Gizmos & Debugging

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (proximityTrigger == null) proximityTrigger = GetComponent<SphereCollider>();

        Vector3 center = transform.position;

        if (proximityTrigger != null)
        {
            float radius = proximityTrigger.radius;

            // Proximity outer radius (Visual feedback range)
            Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.08f);
            Gizmos.DrawSphere(center, radius);
            Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.4f);
            Gizmos.DrawWireSphere(center, radius);
        }

        // Effective cast range inner radius (Actionable target range)
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.15f);
        Gizmos.DrawSphere(center, maxCastDistance);
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(center, maxCastDistance);
    }
#endif

    #endregion
}