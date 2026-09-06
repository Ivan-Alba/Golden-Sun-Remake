using UnityEngine;

public class HybridAudioListener : MonoBehaviour
{
    public static HybridAudioListener Instance { get; private set; }

    [Header("Exploration References (Persistent)")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform exploreCameraTransform;

    // Dynamic Scene-Local References
    private Transform battleCameraTransform;
    private Transform battleCenterTarget;

    // Local GameState cache
    private GameManager.GameState _cachedGameState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        SubscribeToGameEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromGameEvents();
    }

    #region Event Subscriptions

    private void SubscribeToGameEvents()
    {
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;
    }

    private void UnsubscribeFromGameEvents()
    {
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        _cachedGameState = newState;
    }

    #endregion

    private void LateUpdate()
    {
        // --- BATTLE MODE AUDIO ---
        if (_cachedGameState == GameManager.GameState.InBattle)
        {
            if (battleCameraTransform != null)
            {
                // Position at center target (if set) or battle camera, matching battle camera orientation
                transform.position = (battleCenterTarget != null) ? battleCenterTarget.position : battleCameraTransform.position;
                transform.rotation = battleCameraTransform.rotation;
            }
        }
        // --- EXPLORATION MODE AUDIO ---
        else
        {
            if (playerTransform != null && exploreCameraTransform != null)
            {
                // Position at player ears, matching exploration camera rotation
                transform.position = playerTransform.position + (Vector3.up * 1.5f);
                transform.rotation = exploreCameraTransform.rotation;
            }
        }
    }

    // Assigns local battle transforms dynamically when a fight starts
    public void SetBattleReferences(Transform newBattleCam, Transform newBattleCenter = null)
    {
        battleCameraTransform = newBattleCam;
        battleCenterTarget = newBattleCenter;
    }

    // Clears references when exiting battle
    public void ClearBattleReferences()
    {
        battleCameraTransform = null;
        battleCenterTarget = null;
    }
}