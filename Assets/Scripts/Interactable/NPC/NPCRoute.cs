using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct Waypoint
{
    [Tooltip("World position for this waypoint.")]
    public Vector3 position;

    [Tooltip("Time in seconds the NPC pauses at this position before resuming patrol.")]
    public float waitTime;

    [Tooltip("Target movement speed towards this waypoint. If 0, uses default NPC patrol speed.")]
    public float customSpeed;

    [Tooltip("Optional trigger/action to execute upon reaching this point.")]
    public string arrivalTrigger;
}

public enum PatrolMode
{
    Loop,       // 0 -> 1 -> 2 -> 0 -> 1 ...
    PingPong,   // 0 -> 1 -> 2 -> 1 -> 0 ...
    Once        // 0 -> 1 -> 2 -> Stops at 2
}

/// <summary>
/// Defines a spatial patrol route composed of waypoints with visual Gizmos debugging.
/// </summary>
public class NPCRoute : MonoBehaviour
{
    #region Serialized Fields

    [Header("Route Settings")]
    [SerializeField] private PatrolMode patrolMode = PatrolMode.Loop;
    [SerializeField] private List<Waypoint> waypoints = new List<Waypoint>();

    [Header("Gizmos Visual Debugging")]
    [SerializeField] private Color gizmoColor = Color.cyan;
    [SerializeField] private float waypointsSphereRadius = 0.35f;

    #endregion

    #region Properties

    public PatrolMode Mode => patrolMode;
    public IReadOnlyList<Waypoint> Waypoints => waypoints;
    public int Count => waypoints.Count;

    #endregion

    #region Public Helper Methods

    public Waypoint GetWaypoint(int index)
    {
        if (waypoints == null || waypoints.Count == 0) return default;
        int clampedIndex = Mathf.Clamp(index, 0, waypoints.Count - 1);
        return waypoints[clampedIndex];
    }

    #endregion

    #region Unity Gizmos

    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        Gizmos.color = gizmoColor;

        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 currentPos = waypoints[i].position;

            // Draw sphere at waypoint
            Gizmos.DrawSphere(currentPos, waypointsSphereRadius);

            // Draw directional connection lines
            if (i < waypoints.Count - 1)
            {
                Gizmos.DrawLine(currentPos, waypoints[i + 1].position);
            }
            else if (patrolMode == PatrolMode.Loop && waypoints.Count > 1)
            {
                Gizmos.DrawLine(currentPos, waypoints[0].position);
            }
        }
    }

    #endregion
}