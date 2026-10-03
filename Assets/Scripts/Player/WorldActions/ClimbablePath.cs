using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Structure representing a single directional segment of a climbing route.
/// </summary>
[Serializable]
public struct ClimbSegment
{
    public Vector3 startPoint;
    public Vector3 endPoint;
    public Vector3 wallNormal;
    public bool isTopExit;
    public bool isBottomExit;
}

/// <summary>
/// Component attached to climbable ladders, vines, or rock surfaces.
/// Uses transform.forward automatically as the wall normal pointing towards the player.
/// </summary>
public class ClimbablePath : MonoBehaviour
{
    #region Serialized Fields

    [Header("Path Configuration")]
    [Tooltip("Nodes relative to GameObject Transform position (0,0,0). Node 0 is bottom floor, last node is top ledge.")]
    [SerializeField]
    private List<Vector3> localNodes = new List<Vector3>
    {
        new Vector3(0f, 0f, 0f),    // Base / Ground level
        new Vector3(0f, 3f, 0f)     // Top ledge level
    };

    [Header("Exit Override Options")]
    [Tooltip("If true, overrides the default dismount step distance configured on the PlayerController.")]
    [SerializeField] private bool overrideDismountOffset = false;

    [Tooltip("Custom forward offset distance to step onto the top ledge upon dismount.")]
    [SerializeField] private float customTopDismountOffset = 0.8f;

    [Header("Debug Visuals")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color pathColor = Color.green;

    #endregion

    #region Public Properties

    public bool OverrideDismountOffset => overrideDismountOffset;
    public float CustomTopDismountOffset => customTopDismountOffset;

    #endregion

    #region Public Helper Methods

    /// <summary>
    /// Returns the dismount offset to use, falling back to player default if override is disabled.
    /// </summary>
    public float GetEffectiveTopDismountOffset(float defaultPlayerOffset)
    {
        return overrideDismountOffset ? customTopDismountOffset : defaultPlayerOffset;
    }

    public List<ClimbSegment> GetWorldSegments()
    {
        List<ClimbSegment> segments = new List<ClimbSegment>();

        if (localNodes == null || localNodes.Count < 2) return segments;

        Vector3 worldNormal = transform.forward;

        for (int i = 0; i < localNodes.Count - 1; i++)
        {
            ClimbSegment segment = new ClimbSegment
            {
                startPoint = transform.TransformPoint(localNodes[i]),
                endPoint = transform.TransformPoint(localNodes[i + 1]),
                wallNormal = worldNormal,
                isBottomExit = (i == 0),
                isTopExit = (i == localNodes.Count - 2)
            };

            segments.Add(segment);
        }

        return segments;
    }

    public bool GetClosestPointOnPath(Vector3 position, out Vector3 closestPoint, out int segmentIndex)
    {
        closestPoint = position;
        segmentIndex = -1;

        List<ClimbSegment> segments = GetWorldSegments();
        if (segments.Count == 0) return false;

        float minDistanceSqr = float.MaxValue;

        for (int i = 0; i < segments.Count; i++)
        {
            Vector3 projected = ProjectPointOnSegment(position, segments[i].startPoint, segments[i].endPoint);
            float distSqr = (position - projected).sqrMagnitude;

            if (distSqr < minDistanceSqr)
            {
                minDistanceSqr = distSqr;
                closestPoint = projected;
                segmentIndex = i;
            }
        }

        return segmentIndex != -1;
    }

    public Vector3 GetAttachPointAtPlayerHeight(Vector3 playerWorldPosition, int segmentIndex)
    {
        List<ClimbSegment> segments = GetWorldSegments();
        if (segments.Count == 0 || segmentIndex < 0 || segmentIndex >= segments.Count)
            return playerWorldPosition;

        ClimbSegment segment = segments[segmentIndex];

        float minY = Mathf.Min(segment.startPoint.y, segment.endPoint.y);
        float maxY = Mathf.Max(segment.startPoint.y, segment.endPoint.y);
        float targetY = Mathf.Clamp(playerWorldPosition.y, minY, maxY);

        Vector3 segVector = segment.endPoint - segment.startPoint;
        segVector.y = 0f;

        Vector3 playerToStart = playerWorldPosition - segment.startPoint;
        playerToStart.y = 0f;

        float sqrMag = segVector.sqrMagnitude;
        float t = (sqrMag > 0.0001f) ? Mathf.Clamp01(Vector3.Dot(playerToStart, segVector) / sqrMag) : 0f;

        Vector3 horizontalPoint = segment.startPoint + t * segVector;

        return new Vector3(horizontalPoint.x, targetY, horizontalPoint.z);
    }

    #endregion

    #region Private Helpers

    private Vector3 ProjectPointOnSegment(Vector3 point, Vector3 segStart, Vector3 segEnd)
    {
        Vector3 segVector = segEnd - segStart;
        float segLengthSqr = segVector.sqrMagnitude;

        if (segLengthSqr < 0.0001f) return segStart;

        float t = Mathf.Clamp01(Vector3.Dot(point - segStart, segVector) / segLengthSqr);
        return segStart + t * segVector;
    }

    /// <summary>
    /// Calculates the exact 3D rotation (including pitch/tilt around X axis) 
    /// so the player's spine aligns with the slope between nodes while facing the wall.
    /// </summary>
    public Quaternion GetSegmentRotation(int segmentIndex)
    {
        List<ClimbSegment> segments = GetWorldSegments();
        if (segments.Count == 0 || segmentIndex < 0 || segmentIndex >= segments.Count)
            return transform.rotation;

        ClimbSegment segment = segments[segmentIndex];

        // Vector dirección desde el nodo inicio hasta el nodo fin en espacio del mundo
        Vector3 segmentVector = segment.endPoint - segment.startPoint;

        if (segmentVector.sqrMagnitude < 0.0001f)
            return Quaternion.LookRotation(-segment.wallNormal);

        // 1. La columna del personaje (eje UP local, transform.up) sigue la trayectoria del segmento
        Vector3 playerUp = segmentVector.normalized;

        // 2. La mirada/pecho (eje FORWARD local) debe apuntar HACIA la pared (-segment.wallNormal)
        Vector3 faceWallDirection = -segment.wallNormal;

        // Calculamos el eje derecho (Right) usando la dirección a la pared y la columna
        Vector3 playerRight = Vector3.Cross(faceWallDirection, playerUp).normalized;

        // Calculamos el Forward definitivo ortogonal que mantendrá al héroe mirando de frente a la escalera
        Vector3 playerForward = Vector3.Cross(playerUp, playerRight).normalized;

        return Quaternion.LookRotation(playerForward, playerUp);
    }

    #endregion

    #region Debug Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        List<ClimbSegment> segments = GetWorldSegments();
        if (segments.Count == 0) return;

        for (int i = 0; i < segments.Count; i++)
        {
            Gizmos.color = pathColor;
            Gizmos.DrawLine(segments[i].startPoint, segments[i].endPoint);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(segments[i].startPoint, 0.1f);

            Vector3 midPoint = (segments[i].startPoint + segments[i].endPoint) * 0.5f;
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(midPoint, segments[i].wallNormal * 0.6f);
            Gizmos.DrawWireSphere(midPoint + segments[i].wallNormal * 0.6f, 0.04f);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(segments[segments.Count - 1].endPoint, 0.12f);
    }
#endif

    #endregion
}