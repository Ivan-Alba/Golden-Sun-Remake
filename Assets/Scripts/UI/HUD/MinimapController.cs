using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

/// <summary>
/// Controls the top-down minimap camera tracking and updates UI Toolkit compass/arrow rotations.
/// Render adjustments (such as disabling fog for the minimap pass) are hooked directly to SRP.
/// </summary>
public class MinimapController : MonoBehaviour
{
    #region Enumerations

    public enum MinimapMode
    {
        DynamicRotating, // Map rotates with main camera view; player arrow points UP.
        StaticFixedNorth // Map remains fixed North-up; player arrow rotates 360 degrees.
    }

    #endregion

    #region Serialized Fields

    [Header("Settings & Target References")]
    [SerializeField] private MinimapMode currentMode = MinimapMode.DynamicRotating;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform mainCameraTransform;
    [SerializeField] private UIDocument hudDocument;

    [Header("UI Element Names")]
    [SerializeField] private string northIndicatorRingName = "minimap_compass_ring";
    [SerializeField] private string playerArrowName = "minimap_player_arrow";

    #endregion

    #region Private Fields

    private Camera targetCamera;
    private bool previousFogState;

    // Cached UI Elements references
    private VisualElement compassRingElement;
    private VisualElement playerArrowElement;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
    }

    private void Start()
    {
        InitializeUIReferences();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }

    private void LateUpdate()
    {
        if (playerTransform == null) return;

        // Position tracking: Maintain top-down height (Y), align with Player X/Z
        Vector3 currentPosition = transform.position;
        Vector3 targetPosition = playerTransform.position;
        transform.position = new Vector3(targetPosition.x, currentPosition.y, targetPosition.z);

        UpdateMinimapRotation();
    }

    #endregion

    #region Render Pipeline Callbacks

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == targetCamera)
        {
            previousFogState = RenderSettings.fog;
            RenderSettings.fog = false;
        }
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == targetCamera)
        {
            RenderSettings.fog = previousFogState;
        }
    }

    #endregion

    #region UI Initialization

    /// <summary>
    /// Binds UI Toolkit visual elements for the compass ring and player arrow from the HUD document.
    /// Executed once at initialization without frame polling.
    /// </summary>
    public void InitializeUIReferences()
    {
        if (hudDocument == null || hudDocument.rootVisualElement == null) return;

        VisualElement root = hudDocument.rootVisualElement;
        compassRingElement = root.Q<VisualElement>(northIndicatorRingName);
        playerArrowElement = root.Q<VisualElement>(playerArrowName);
    }

    #endregion

    #region Minimap Operations

    private void UpdateMinimapRotation()
    {
        float playerYaw = playerTransform.eulerAngles.y;
        float cameraYaw = mainCameraTransform != null ? mainCameraTransform.eulerAngles.y : playerYaw;

        if (currentMode == MinimapMode.DynamicRotating)
        {
            // 1. Camera rotates following the main camera/view direction
            transform.rotation = Quaternion.Euler(90f, cameraYaw, 0f);

            // 2. Player Arrow points relative to camera forward angle
            if (playerArrowElement != null)
            {
                float relativeArrowAngle = playerYaw - cameraYaw;
                playerArrowElement.style.rotate = new Rotate(Angle.Degrees(relativeArrowAngle));
            }

            // 3. Compass Ring rotates in reverse to keep North aligned
            if (compassRingElement != null)
            {
                compassRingElement.style.rotate = new Rotate(Angle.Degrees(-cameraYaw));
            }
        }
        else // StaticFixedNorth
        {
            // 1. Camera faces strictly North
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // 2. Compass Ring stays fixed facing North (0 degrees)
            if (compassRingElement != null)
            {
                compassRingElement.style.rotate = new Rotate(Angle.Degrees(0f));
            }

            // 3. Player Arrow rotates full 360 degrees in the HUD
            if (playerArrowElement != null)
            {
                playerArrowElement.style.rotate = new Rotate(Angle.Degrees(playerYaw));
            }
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Dynamically changes the minimap rotation behavior (e.g. called from settings menu).
    /// </summary>
    /// <param name="newMode">Target MinimapMode setting.</param>
    public void SetMinimapMode(MinimapMode newMode)
    {
        currentMode = newMode;
    }

    #endregion
}