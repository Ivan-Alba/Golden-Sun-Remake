using UnityEngine;

public class PlayerAiming : MonoBehaviour
{
    #region Serialized Fields

    [Header("Detection Settings")]
    [SerializeField] private float maxTargetDistance = 15f;
    [SerializeField] private float sphereCastRadius = 0.6f;
    [SerializeField] private LayerMask targetLayerMask;

    [Header("References")]
    [SerializeField] private Transform mainCamera;

    #endregion

    #region Private Fields

    private IPsynergyTargetable currentTarget;

    #endregion

    #region Properties

    public IPsynergyTargetable CurrentTarget => currentTarget;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (mainCamera == null && Camera.main != null)
        {
            mainCamera = Camera.main.transform;
        }
    }

    private void Update()
    {
        PerformTargetDetection();
    }

    private void OnDisable()
    {
        ClearCurrentTarget();
    }

    #endregion

    #region Detection Logic

    private void PerformTargetDetection()
    {
        if (mainCamera == null) return;

        Ray ray = new Ray(mainCamera.position, mainCamera.forward);
        IPsynergyTargetable detectedTarget = null;

        // Perform thick SphereCast from camera along the view vector
        if (Physics.SphereCast(ray, sphereCastRadius, out RaycastHit hit, maxTargetDistance, targetLayerMask))
        {
            if (hit.collider.TryGetComponent<IPsynergyTargetable>(out var targetable))
            {
                if (targetable.CanBeTargeted())
                {
                    detectedTarget = targetable;
                }
            }
        }

        // Target changed detection
        if (detectedTarget != currentTarget)
        {
            // Clear previous target
            if (currentTarget != null)
            {
                currentTarget.OnTargetLost();
            }

            // Assign new target
            currentTarget = detectedTarget;

            if (currentTarget != null)
            {
                currentTarget.OnTargetGained();
            }

            // Update UI Reticle feedback (e.g. highlight reticle when locked on)
            if (PlayerUIManager.Instance != null)
            {
                PlayerUIManager.Instance.SetAimTargetLocked(currentTarget != null);
            }
        }
    }

    /// <summary>
    /// Resets aiming component state when exiting aim mode.
    /// </summary>
    public void Reset()
    {
        ClearCurrentTarget();
    }

    private void ClearCurrentTarget()
    {
        if (currentTarget != null)
        {
            currentTarget.OnTargetLost();
            currentTarget = null;
        }

        if (PlayerUIManager.Instance != null)
        {
            PlayerUIManager.Instance.SetAimTargetLocked(false);
        }
    }

    #endregion

    #region Debug Gizmos

    private void OnDrawGizmosSelected()
    {
        if (mainCamera == null) return;

        Gizmos.color = currentTarget != null ? Color.green : Color.red;
        Gizmos.DrawRay(mainCamera.position, mainCamera.forward * maxTargetDistance);
        Gizmos.DrawWireSphere(mainCamera.position + mainCamera.forward * maxTargetDistance, sphereCastRadius);
    }

    #endregion
}