using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class MinimapController : MonoBehaviour
{
    public enum MinimapMode
    {
        DynamicRotating, // Map rotates with camera, player arrow points UP
        StaticFixedNorth // Map stays North-up, player arrow rotates 360
    }

    [Header("Settings & Target References")]
    [SerializeField] private MinimapMode currentMode = MinimapMode.DynamicRotating;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform mainCameraTransform;
    [SerializeField] private UIDocument hudDocument;

    [Header("UI Element Names")]
    [SerializeField] private string northIndicatorRingName = "minimap_compass_ring";
    [SerializeField] private string playerArrowName = "minimap_player_arrow";

    private Camera targetCamera;
    private bool previousFogState;

    // UI Elements references
    private VisualElement compassRingElement;
    private VisualElement playerArrowElement;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();

        // Auto-find references if not assigned in Inspector
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        if (mainCameraTransform == null && Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }

        InitializeUIReferences();
    }

    private void InitializeUIReferences()
    {
        if (hudDocument == null)
        {
            hudDocument = FindFirstObjectByType<UIDocument>();
        }

        if (hudDocument != null && hudDocument.rootVisualElement != null)
        {
            VisualElement root = hudDocument.rootVisualElement;
            compassRingElement = root.Q<VisualElement>(northIndicatorRingName);
            playerArrowElement = root.Q<VisualElement>(playerArrowName);
        }
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

    private void LateUpdate()
    {
        if (playerTransform == null) return;

        // Position tracking: Keep top-down Y height, follow player X/Z
        Vector3 newPos = playerTransform.position;
        newPos.y = transform.position.y;
        transform.position = newPos;

        // Re-check UI references if lost or initialized late
        if (compassRingElement == null || playerArrowElement == null)
        {
            InitializeUIReferences();
        }

        UpdateMinimapRotation();
    }

    private void UpdateMinimapRotation()
    {
        float playerYaw = playerTransform.eulerAngles.y;
        float cameraYaw = mainCameraTransform != null ? mainCameraTransform.eulerAngles.y : playerYaw;

        if (currentMode == MinimapMode.DynamicRotating)
        {
            // 1. Camera rotates following the main camera or player view
            transform.rotation = Quaternion.Euler(90f, cameraYaw, 0f);

            // 2. Player Arrow remains pointing UP in the center of HUD
            if (playerArrowElement != null)
            {
                float relativeArrowAngle = playerYaw - cameraYaw;
                playerArrowElement.style.rotate = new Rotate(Angle.Degrees(relativeArrowAngle));
            }

            // 3. Compass Ring rotates in reverse to keep North aligned globally
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

            // 3. Player Arrow rotates 360 degrees in the HUD center
            if (playerArrowElement != null)
            {
                playerArrowElement.style.rotate = new Rotate(Angle.Degrees(playerYaw));
            }
        }
    }

    /// <summary>
    /// Public method to change mode dynamically from Settings UI or Manager.
    /// </summary>
    public void SetMinimapMode(MinimapMode newMode)
    {
        currentMode = newMode;
    }
}