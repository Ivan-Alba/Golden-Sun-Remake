using UnityEngine;

/// <summary>
/// Helper component attached to puzzle pillars or jump platforms.
/// Visualizes the 4 cardinal jump target cells at exact tileSize distance in the Editor.
/// </summary>
public class PuzzleTileAnchor : MonoBehaviour
{
    #region Serialized Fields

    [Header("Puzzle Grid Settings")]
    [Tooltip("Base tile unit distance. Must match tileSize in EnvironmentEdgeDetector.")]
    [SerializeField] private float tileSize = 1.5f;

    [Header("Visual Settings")]
    [SerializeField] private Color cellColor = new Color(0f, 1f, 0f, 0.35f);
    [SerializeField] private Color originColor = Color.cyan;

    #endregion

    #region Debug Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position;

        // Draw center origin marker
        Gizmos.color = originColor;
        Gizmos.DrawWireCube(origin, new Vector3(0.4f, 0.1f, 0.4f));

        // Draw the 4 cardinal tiles (North, South, East, West)
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

        Gizmos.color = cellColor;
        foreach (Vector3 dir in directions)
        {
            Vector3 targetTileCenter = origin + (dir * tileSize);
            Gizmos.DrawWireCube(targetTileCenter, new Vector3(tileSize * 0.8f, 0.05f, tileSize * 0.8f));
            Gizmos.DrawLine(origin, targetTileCenter);
        }
    }
#endif

    #endregion
}