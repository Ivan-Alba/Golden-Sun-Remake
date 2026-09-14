using UnityEngine;

/// <summary>
/// Utility component to draw directional gizmos in the Scene view for battle anchors.
/// Helps visually orient front, left, and right directions during battlefield setup.
/// </summary>
public class AnchorGizmoDrawer : MonoBehaviour
{
    [Header("Gizmo Visual Settings")]
    [SerializeField] private float arrowLength = 2.0f;
    [SerializeField] private float sphereRadius = 0.15f;
    [SerializeField] private bool showSideVectors = true;

    private void OnDrawGizmos()
    {
        Vector3 origin = transform.position;

        // --- 1. Draw Origin Point ---
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(origin, sphereRadius);

        // --- 2. Draw Forward Direction (Blue) ---
        Vector3 forwardDir = transform.forward * arrowLength;
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(origin, forwardDir);
        DrawArrowHead(origin + forwardDir, transform.forward, Color.blue);

        if (!showSideVectors) return;

        // --- 3. Draw Right Direction (Red) ---
        Vector3 rightDir = transform.right * (arrowLength * 0.75f);
        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin, rightDir);
        DrawArrowHead(origin + rightDir, transform.right, Color.red);

        // --- 4. Draw Left Direction (Green) ---
        Vector3 leftDir = -transform.right * (arrowLength * 0.75f);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(origin, leftDir);
        DrawArrowHead(origin + leftDir, -transform.right, Color.green);
    }

    /// <summary>
    /// Helper method to draw a small arrowhead at the tip of the directional ray.
    /// </summary>
    private void DrawArrowHead(Vector3 tipPosition, Vector3 direction, Color color)
    {
        Gizmos.color = color;
        float arrowHeadSize = 0.3f;

        Vector3 rightAngle = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 150, 0) * Vector3.forward;
        Vector3 leftAngle = Quaternion.LookRotation(direction) * Quaternion.Euler(0, -150, 0) * Vector3.forward;

        Gizmos.DrawRay(tipPosition, rightAngle * arrowHeadSize);
        Gizmos.DrawRay(tipPosition, leftAngle * arrowHeadSize);
    }
}