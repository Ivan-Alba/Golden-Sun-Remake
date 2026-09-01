using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages 3D World-Space UI elements projected onto the 2D UI Toolkit panel.
/// Converts 3D world positions into screen coordinates for interaction prompts, damage numbers, etc.
/// </summary>
public class WorldSpaceUIController : MonoBehaviour
{
    [Header("UI Document References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset interactionPromptTemplate;
    [SerializeField] private string containerName = "world_interaction_prompts_container";

    [Header("Camera Settings")]
    [Tooltip("Optional camera override. If null, automatically falls back to Camera.main.")]
    [SerializeField] private Camera targetCamera;

    #region Private Fields

    private VisualElement promptContainer;
    private TemplateContainer promptTemplateInstance;
    private VisualElement promptRootVisualElement;

    private Label keyLabel;
    private Label actionLabel;

    private Interactable currentTarget;
    private bool isPromptVisible;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the currently active camera used for World-to-Screen coordinate projection.
    /// </summary>
    public Camera ActiveCamera
    {
        get
        {
            if (targetCamera != null && targetCamera.isActiveAndEnabled)
            {
                return targetCamera;
            }
            return Camera.main;
        }
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }
    }

    private void OnEnable()
    {
        InitializeUI();
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void LateUpdate()
    {
        if (isPromptVisible && currentTarget != null)
        {
            UpdatePromptPosition();
        }
    }

    #endregion

    #region Initialization & Subscriptions

    /// <summary>
    /// Finds the target UI Toolkit container and instantiates the World Interaction Prompt Template.
    /// </summary>
    private void InitializeUI()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        promptContainer = uiDocument.rootVisualElement.Q<VisualElement>(containerName);

        if (promptContainer != null && interactionPromptTemplate != null)
        {
            // Instantiate prompt template into the container
            promptTemplateInstance = interactionPromptTemplate.Instantiate();
            promptContainer.Add(promptTemplateInstance);

            // Fetch actual root VisualElement and internal labels
            promptRootVisualElement = promptTemplateInstance.Q<VisualElement>("world_prompt_root");
            keyLabel = promptTemplateInstance.Q<Label>("label_prompt_key");
            actionLabel = promptTemplateInstance.Q<Label>("label_action_text");

            // Register USS transition completion listener on the correct element
            if (promptRootVisualElement != null)
            {
                promptRootVisualElement.RegisterCallback<TransitionEndEvent>(OnFadeOutFinished);
            }

            // Hide initially
            SetPromptVisibility(false, immediate: true);
        }
        else
        {
            Debug.LogWarning($"[WorldSpaceUIController] Could not find container '{containerName}' or template is missing.");
        }
    }

    private void SubscribeToEvents()
    {
        PlayerInteractionDetector.OnInteractableTargetChanged += HandleTargetChanged;
    }

    private void UnsubscribeFromEvents()
    {
        PlayerInteractionDetector.OnInteractableTargetChanged -= HandleTargetChanged;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Triggered when the player interaction detector focuses on a new interactable target or loses target.
    /// </summary>
    private void HandleTargetChanged(Interactable newTarget)
    {
        currentTarget = newTarget;

        if (currentTarget != null)
        {
            // Update labels
            if (keyLabel != null) keyLabel.text = currentTarget.KeyPrompt;
            if (actionLabel != null) actionLabel.text = currentTarget.ActionText;

            // Snap position before showing to avoid 1-frame jump
            UpdatePromptPosition();
            SetPromptVisibility(true);
        }
        else
        {
            SetPromptVisibility(false);
        }
    }

    #endregion

    #region Projection & Positioning Logic

    /// <summary>
    /// Converts the target's 3D World Position into UI Toolkit screen space coordinates.
    /// </summary>
    private void UpdatePromptPosition()
    {
        Camera cam = ActiveCamera;
        if (cam == null || promptTemplateInstance == null || currentTarget == null) return;

        // Convert 3D world position to Screen Space (pixels)
        Vector3 worldPos = currentTarget.PromptWorldPosition;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        // Hide if object is behind the camera frustum
        if (screenPos.z < 0)
        {
            promptTemplateInstance.style.display = DisplayStyle.None;
            return;
        }

        if (isPromptVisible)
        {
            promptTemplateInstance.style.display = DisplayStyle.Flex;
        }

        // Convert Unity Screen coordinates to UI Toolkit Panel coordinates
        Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
            promptContainer.panel,
            worldPos,
            cam
        );

        // Apply position to template wrapper
        promptTemplateInstance.style.left = panelPos.x;
        promptTemplateInstance.style.top = panelPos.y;
    }

    /// <summary>
    /// Toggles visibility state and applies USS transition classes for smooth fade-in/out.
    /// </summary>
    private void SetPromptVisibility(bool visible, bool immediate = false)
    {
        isPromptVisible = visible;

        if (promptTemplateInstance == null || promptRootVisualElement == null) return;

        if (visible)
        {
            promptTemplateInstance.style.display = DisplayStyle.Flex;
            promptRootVisualElement.RemoveFromClassList("prompt-hidden");
        }
        else
        {
            // Trigger CSS transition via class
            promptRootVisualElement.AddToClassList("prompt-hidden");

            if (immediate)
            {
                promptTemplateInstance.style.display = DisplayStyle.None;
            }
        }
    }

    /// <summary>
    /// Callback triggered when the UI Toolkit USS transition completes.
    /// </summary>
    private void OnFadeOutFinished(TransitionEndEvent evt)
    {
        if (promptTemplateInstance == null) return;

        // Hide wrapper container once opacity/scale transition completes
        if (!isPromptVisible)
        {
            promptTemplateInstance.style.display = DisplayStyle.None;
        }
    }

    /// <summary>
    /// Allows external systems (e.g., BattleManager) to explicitly set an active camera for UI projection.
    /// </summary>
    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
    }

    #endregion
}