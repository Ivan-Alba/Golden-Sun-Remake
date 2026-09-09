using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Provides focus data, eye-level offsets, framing centers, and optional custom cinematic cameras 
/// for dialogue framing and camera direction.
/// </summary>
public class DialogueFocusProvider : MonoBehaviour
{
    #region Serialized Fields

    [Header("Framing Settings")]
    [Tooltip("Vertical offset for eye-level focus (where the camera looks at).")]
    [SerializeField] private float eyeHeightOffset = 1.4f;

    [Tooltip("Vertical offset for the framing bounding center (where the camera centers the group bounds to include feet/base).")]
    [SerializeField] private float framingCenterOffset = 1.0f;

    [Tooltip("Radius size used by the Cinemachine Target Group to calculate framing and zoom.")]
    [SerializeField] private float targetRadius = 0.8f;

    [Header("Cinematic Customization (Optional)")]
    [Tooltip("Optional custom CinemachineCamera for unique cinematic framing. If null, standard dialogue framing is used.")]
    [SerializeField] private CinemachineCamera customDialogueCamera;

    #endregion

    #region Public Properties

    public float EyeHeightOffset => eyeHeightOffset;
    public float FramingCenterOffset => framingCenterOffset;
    public float TargetRadius => targetRadius;
    public CinemachineCamera CustomDialogueCamera => customDialogueCamera;

    #endregion

    #region Public API

    /// <summary>
    /// Dynamically updates all framing parameters (used by PlayerVisualManager when swapping heroes).
    /// </summary>
    public void UpdateProfile(float newEyeHeight, float newFramingCenter, float newRadius)
    {
        eyeHeightOffset = newEyeHeight;
        framingCenterOffset = newFramingCenter;
        targetRadius = newRadius;
    }

    #endregion

    #region Unity Editor Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Yellow Gizmo: Eye-level look-at point
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * eyeHeightOffset, 0.2f);

        // Cyan Gizmo: Framing center and bounding radius
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * framingCenterOffset, targetRadius);
    }
#endif

    #endregion
}