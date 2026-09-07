using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Debug utility to automatically apply a list of FlagSO states on startup 
/// via the decoupled GameEvents.World event bus.
/// </summary>
public class DebugFlagsUtility : MonoBehaviour
{
    #region Serialized Fields

    [Header("Flags to Apply on Startup")]
    [Tooltip("Drag and drop the FlagSO assets you want to set as active when this component initializes.")]
    [SerializeField] private List<FlagSO> testFlagsToSet = new List<FlagSO>();

    [Header("Configuration")]
    [Tooltip("If true, flags will be applied automatically in Start().")]
    [SerializeField] private bool applyOnStart = true;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (applyOnStart)
        {
            ApplyTestFlags();
        }
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Apply Test Flags Now")]
    public void ApplyTestFlags()
    {
        if (testFlagsToSet == null || testFlagsToSet.Count == 0)
        {
            Debug.LogWarning("[DebugFlagsUtility] No FlagSO assets assigned in the test list.");
            return;
        }

        int count = 0;
        foreach (var flag in testFlagsToSet)
        {
            if (flag != null)
            {
                // Request flag state update via decoupled event bus
                GameEvents.World.RequestSetFlag(flag, true);
                count++;
            }
        }

        Debug.Log($"<color=green>[DebugFlagsUtility] Automatically applied {count} test flag(s) to TRUE.</color>");
    }

    #endregion
}