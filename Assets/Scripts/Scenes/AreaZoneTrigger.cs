using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AreaZoneTrigger : MonoBehaviour
{
    #region Serialized Fields

    [Header("Zone Identification Settings")]
    [SerializeField] private string zoneName = "BOSQUE DE KOLIMA";
    [SerializeField] private string regionName = "Región de Bilibin";
    [SerializeField] private float displayDuration = 3.5f;

    [Header("Trigger Behavior")]
    [SerializeField] private bool triggerOnlyOnce = false;

    [Header("Editor Visualization")]
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 0.4f, 0.35f);

    #endregion

    #region Private Fields

    private bool _hasBeenTriggered = false;

    #endregion

    #region Properties

    public string ZoneName => zoneName;
    public string RegionName => regionName;

    #endregion

    #region Unity Lifecycle & Trigger Events

    private void Awake()
    {
        // Ensure collider is marked as trigger
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasBeenTriggered && triggerOnlyOnce) return;

        if (other.CompareTag("Player"))
        {
            _hasBeenTriggered = true;

            // 1. Update global zone tracking in GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.UpdateCurrentZone(zoneName, regionName);
            }

            // 2. Fire decoupled UI banner event
            GameEvents.UI.TriggerShowZoneBanner(zoneName, regionName, displayDuration);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Allow re-triggering when returning to this zone if not set to once
        if (other.CompareTag("Player") && !triggerOnlyOnce)
        {
            _hasBeenTriggered = false;
        }
    }

    #endregion

    #region Editor Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = gizmoColor;

        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawSphere(sphere.center, sphere.radius);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
    }
#endif

    #endregion
}