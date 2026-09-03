using UnityEngine;

/// <summary>
/// Attached to character or enemy root/model hierarchies. Performs ground raycasts to detect surface tags
/// and emits 3D spatialized footstep audio requests via GameEvents.Audio to the central AudioController Object Pool.
/// </summary>
public class FootstepAudioEmitter : MonoBehaviour
{
    #region Serialized Fields

    [Header("Raycast Ground Detection")]
    [Tooltip("Layer mask filtering valid walkable ground surfaces.")]
    [SerializeField] private LayerMask groundLayerMask = ~0;

    [Tooltip("Raycast check length extending downward from offset origin.")]
    [SerializeField] private float raycastDistance = 1.5f;

    [Tooltip("Vertical position offset from origin to initiate ground raycast.")]
    [SerializeField] private Vector3 raycastOffset = Vector3.up * 0.5f;

    [Header("Pitch & Volume Variation")]
    [Range(0.8f, 1.2f)]
    [SerializeField] private float minPitch = 0.95f;

    [Range(0.8f, 1.2f)]
    [SerializeField] private float maxPitch = 1.05f;

    [Range(0.1f, 1.0f)]
    [SerializeField] private float baseVolume = 0.8f;

    [Range(0.0f, 0.2f)]
    [SerializeField] private float volumeVariance = 0.05f;

    [Header("Default Profile (Fallback)")]
    [Tooltip("Fallback footstep audio profile used if no custom hero/enemy profile is assigned.")]
    [SerializeField] private HeroFootstepProfileSO defaultProfile;

    #endregion

    #region Private Fields

    private Transform cachedTransform;
    private HeroFootstepProfileSO currentProfile;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        cachedTransform = transform;
        currentProfile = defaultProfile;
    }

    #endregion

    #region Public Profile Setter

    /// <summary>
    /// Updates the active footstep profile when the active hero changes or for different enemy types.
    /// </summary>
    public void SetActiveProfile(HeroFootstepProfileSO newProfile)
    {
        currentProfile = (newProfile != null) ? newProfile : defaultProfile;
    }

    #endregion

    #region Animation Event Handler

    /// <summary>
    /// Called directly by Unity Animation Events during walk/run animation cycles.
    /// Emits a spatial 3D audio request to the AudioController Object Pool.
    /// </summary>
    public void OnFootstep()
    {
        if (currentProfile == null) return;

        AudioClip clip = DetectAndSelectClip();
        if (clip == null) return;

        float randomizedPitch = Random.Range(minPitch, maxPitch);
        float randomizedVolume = Mathf.Clamp01(baseVolume + Random.Range(-volumeVariance, volumeVariance));

        // Decoupled 3D spatial SFX request handled by central AudioController Object Pool
        GameEvents.Audio.RequestPlaySFXClipAtPosition(clip, cachedTransform.position, randomizedVolume, randomizedPitch);
    }

    #endregion

    #region Ground Detection Logic

    /// <summary>
    /// Performs downward raycast to resolve surface tag and fetch matching AudioClip from active profile.
    /// </summary>
    private AudioClip DetectAndSelectClip()
    {
        Vector3 origin = cachedTransform.position + raycastOffset;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayerMask))
        {
            string hitTag = hit.collider.gameObject.tag;
            return currentProfile.GetClipForSurface(hitTag);
        }

        return currentProfile.GetClipForSurface(string.Empty);
    }

    #endregion

    #region Editor Debugging Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 origin = transform.position + raycastOffset;
        Gizmos.DrawRay(origin, Vector3.down * raycastDistance);
    }
#endif

    #endregion
}