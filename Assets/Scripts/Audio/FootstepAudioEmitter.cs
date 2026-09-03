using UnityEngine;

/// <summary>
/// Attached to character/enemy root or model hierarchy. Performs ground raycasts to detect surface tags
/// and plays 3D spatialized footstep audio directly at the entity position.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class FootstepAudioEmitter : MonoBehaviour
{
    #region Serialized Fields

    [Header("Raycast Ground Detection")]
    [SerializeField] private LayerMask groundLayerMask = ~0;
    [SerializeField] private float raycastDistance = 1.5f;
    [SerializeField] private Vector3 raycastOffset = Vector3.up * 0.5f;

    [Header("Pitch & Volume Variation")]
    [Range(0.8f, 1.2f)][SerializeField] private float minPitch = 0.95f;
    [Range(0.8f, 1.2f)][SerializeField] private float maxPitch = 1.05f;
    [Range(0.1f, 1.0f)][SerializeField] private float baseVolume = 0.8f;
    [Range(0.0f, 0.2f)][SerializeField] private float volumeVariance = 0.05f;

    [Header("3D Audio Distance Settings")]
    [Tooltip("Distance within which the footstep volume is at 100%.")]
    [SerializeField] private float minAudioDistance = 1f;

    [Tooltip("Maximum distance where footsteps can still be heard.")]
    [SerializeField] private float maxAudioDistance = 18f;

    [Header("Default Profile (Fallback)")]
    [SerializeField] private HeroFootstepProfileSO defaultProfile;

    #endregion

    #region Private Fields

    private Transform cachedTransform;
    private AudioSource spatialAudioSource;
    private HeroFootstepProfileSO currentProfile;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        cachedTransform = transform;
        currentProfile = defaultProfile;

        ConfigureSpatialAudioSource();
    }

    #endregion

    #region AudioSource Setup

    /// <summary>
    /// Configures the local AudioSource for zero-allocation 3D spatial attenuation.
    /// </summary>
    private void ConfigureSpatialAudioSource()
    {
        spatialAudioSource = GetComponent<AudioSource>();
        if (spatialAudioSource == null)
        {
            spatialAudioSource = gameObject.AddComponent<AudioSource>();
        }

        spatialAudioSource.playOnAwake = false;
        spatialAudioSource.spatialBlend = 1.0f; // 100% 3D sound
        spatialAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        spatialAudioSource.minDistance = minAudioDistance;
        spatialAudioSource.maxDistance = maxAudioDistance;
        spatialAudioSource.dopplerLevel = 0f;
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
    /// </summary>
    public void OnFootstep()
    {
        if (currentProfile == null) return;

        AudioClip clip = DetectAndSelectClip();
        if (clip == null) return;

        float randomizedPitch = Random.Range(minPitch, maxPitch);
        float randomizedVolume = Mathf.Clamp01(baseVolume + Random.Range(-volumeVariance, volumeVariance));

        GameEvents.Audio.RequestPlaySFXClipAtPosition(clip, cachedTransform.position, randomizedVolume, randomizedPitch);
    }

    #endregion

    #region Ground Detection Logic

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

    #region Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 origin = transform.position + raycastOffset;
        Gizmos.DrawRay(origin, Vector3.down * raycastDistance);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, maxAudioDistance);
    }
#endif

    #endregion
}