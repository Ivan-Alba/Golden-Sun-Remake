using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Orchestrates 3D Overworld UI elements projected onto the 2D UI Toolkit panel during exploration and cutscenes.
/// Manages interaction prompts, aim hover target names, floating emote balloons, and proximity entity indicators (Enemies/NPCs/Signs).
/// </summary>
public class OverworldWorldSpaceUIController : MonoBehaviour
{
    #region Serialized Fields

    [Header("UI Document References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private string containerName = "world_interaction_prompts_container";

    [Header("UI Templates")]
    [SerializeField] private VisualTreeAsset interactionPromptTemplate;

    [Header("Proximity Entity Templates")]
    [SerializeField] private VisualTreeAsset enemyBarTemplate;
    [SerializeField] private VisualTreeAsset npcNameplateTemplate;
    [SerializeField] private VisualTreeAsset simpleSignTemplate;

    [Header("Camera Settings Override")]
    [Tooltip("Optional camera override. If null, dynamically syncs via GameEvents.Camera.OnActiveCameraChanged.")]
    [SerializeField] private Camera overrideCamera;

    #endregion

    #region Private Fields

    private Camera cachedActiveCamera;

    // --- Layer 1: Interaction Prompt Elements ---
    private VisualElement promptContainer;
    private TemplateContainer promptTemplateInstance;
    private VisualElement promptRootVisualElement;
    private Label keyLabel;
    private Label actionLabel;

    private Interactable currentInteractionTarget;
    private bool isPromptVisible;

    // --- Layer 2: Aim / Hover Target Info Elements (Prepared) ---
    private GameObject currentAimTarget;
    private string currentAimName;
    private bool isAimInfoVisible;

    // --- Layer 3: Floating Emote Elements (Prepared) ---
    private Transform currentEmoteTarget;
    private bool isEmoteVisible;

    // --- Layer 4: Proximity World-Space Entities (Enemies, NPCs, Signs) ---
    private readonly Dictionary<GameObject, TrackedProximityUI> activeProximityElements = new Dictionary<GameObject, TrackedProximityUI>();

    #endregion

    #region Internal Tracking Structure

    private class TrackedProximityUI
    {
        public TemplateContainer TemplateInstance;
        public WorldSpaceUIProvider Provider;
        public Label NameLabel;
        public Label SubTitleLabel;
        public Label LevelLabel;
        public ProgressBar ThreatBar;
        public VisualElement AlertIcon;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the currently active camera used for 3D-to-2D panel coordinate projections.
    /// </summary>
    public Camera ActiveCamera
    {
        get
        {
            if (overrideCamera != null && overrideCamera.isActiveAndEnabled)
            {
                return overrideCamera;
            }
            return cachedActiveCamera;
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

        CacheInitialCamera();
    }

    private void OnEnable()
    {
        InitializeUI();
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
        CleanupUI();
        ClearAllProximityElements();
    }

    private void LateUpdate()
    {
        if (ActiveCamera == null) return;

        if (isPromptVisible && currentInteractionTarget != null)
        {
            UpdatePromptPosition();
        }

        if (isAimInfoVisible && currentAimTarget != null)
        {
            UpdateAimInfoPosition();
        }

        if (isEmoteVisible && currentEmoteTarget != null)
        {
            UpdateEmotePosition();
        }

        // Update positions for active proximity elements (Enemies, NPCs, Signs)
        UpdateProximityElementsPosition();
    }

    #endregion

    #region Initialization & Cleanup

    private void CacheInitialCamera()
    {
        if (overrideCamera != null)
        {
            cachedActiveCamera = overrideCamera;
        }
        else if (Camera.main != null)
        {
            cachedActiveCamera = Camera.main;
        }
    }

    /// <summary>
    /// Finds the target UI Toolkit container and instantiates the World Interaction Prompt Template.
    /// </summary>
    private void InitializeUI()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        promptContainer = uiDocument.rootVisualElement.Q<VisualElement>(containerName);

        if (promptContainer != null && interactionPromptTemplate != null)
        {
            promptTemplateInstance = interactionPromptTemplate.Instantiate();
            promptContainer.Add(promptTemplateInstance);

            promptRootVisualElement = promptTemplateInstance.Q<VisualElement>("world_prompt_root");
            keyLabel = promptTemplateInstance.Q<Label>("label_prompt_key");
            actionLabel = promptTemplateInstance.Q<Label>("label_action_text");

            if (promptRootVisualElement != null)
            {
                promptRootVisualElement.RegisterCallback<TransitionEndEvent>(OnFadeOutFinished);
            }

            SetPromptVisibility(false, immediate: true);
        }
        else
        {
            Debug.LogWarning($"[OverworldWorldSpaceUIController] Could not find container '{containerName}' or template is missing.");
        }
    }

    private void CleanupUI()
    {
        if (promptRootVisualElement != null)
        {
            promptRootVisualElement.UnregisterCallback<TransitionEndEvent>(OnFadeOutFinished);
        }
    }

    #endregion

    #region Event Subscriptions

    private void SubscribeToEvents()
    {
        GameEvents.Camera.OnActiveCameraChanged += HandleActiveCameraChanged;
        GameEvents.World.OnInteractableTargetChanged += HandleInteractableTargetChanged;
        GameEvents.World.OnAimTargetChanged += HandleAimTargetChanged;
        GameEvents.World.OnShowEmoteRequested += HandleShowEmoteRequested;

        // Proximity & Enemy Threat Event Subscriptions
        GameEvents.World.OnEntityEnteredProximity += HandleEntityEnteredProximity;
        GameEvents.World.OnEntityExitedProximity += HandleEntityExitedProximity;
        GameEvents.World.OnEnemyThreatLevelChanged += HandleEnemyThreatLevelChanged;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.Camera.OnActiveCameraChanged -= HandleActiveCameraChanged;
        GameEvents.World.OnInteractableTargetChanged -= HandleInteractableTargetChanged;
        GameEvents.World.OnAimTargetChanged -= HandleAimTargetChanged;
        GameEvents.World.OnShowEmoteRequested -= HandleShowEmoteRequested;

        // Proximity & Enemy Threat Event Unsubscriptions
        GameEvents.World.OnEntityEnteredProximity -= HandleEntityEnteredProximity;
        GameEvents.World.OnEntityExitedProximity -= HandleEntityExitedProximity;
        GameEvents.World.OnEnemyThreatLevelChanged -= HandleEnemyThreatLevelChanged;
    }

    #endregion

    #region Event Handlers

    private void HandleActiveCameraChanged(Camera newCamera)
    {
        if (newCamera != null)
        {
            cachedActiveCamera = newCamera;
        }
    }

    /// <summary>
    /// Triggered when the player interaction detector focuses on a new interactable target or loses target.
    /// </summary>
    private void HandleInteractableTargetChanged(Interactable newTarget)
    {
        currentInteractionTarget = newTarget;

        if (currentInteractionTarget != null)
        {
            if (keyLabel != null) keyLabel.text = currentInteractionTarget.KeyPrompt;
            if (actionLabel != null) actionLabel.text = currentInteractionTarget.ActionText;

            UpdatePromptPosition();
            SetPromptVisibility(true);
        }
        else
        {
            SetPromptVisibility(false);
        }
    }

    /// <summary>
    /// Triggered when the player reticle passes over a targetable object in the world.
    /// </summary>
    private void HandleAimTargetChanged(GameObject targetObj, string targetName)
    {
        currentAimTarget = targetObj;
        currentAimName = targetName;
        isAimInfoVisible = (currentAimTarget != null && !string.IsNullOrEmpty(targetName));

        // TODO: Update and show hovering reticle name tag UI element
    }

    /// <summary>
    /// Triggered during dialogues or cutscenes to spawn an expressional balloon over a character.
    /// </summary>
    private void HandleShowEmoteRequested(Transform targetHead, EmoteType emoteType, float duration)
    {
        currentEmoteTarget = targetHead;
        isEmoteVisible = (currentEmoteTarget != null);

        // TODO: Instantiate or display expressional balloon corresponding to emoteType
    }

    #endregion

    #region Proximity & Enemy Handlers

    private void HandleEntityEnteredProximity(GameObject entity)
    {
        if (entity == null || activeProximityElements.ContainsKey(entity)) return;

        if (!entity.TryGetComponent<WorldSpaceUIProvider>(out var provider)) return;

        VisualTreeAsset selectedTemplate = GetTemplateForType(provider.UIType);
        if (selectedTemplate == null || promptContainer == null) return;

        // Instantiate template and add to container
        TemplateContainer instance = selectedTemplate.Instantiate();
        promptContainer.Add(instance);

        TrackedProximityUI tracked = new TrackedProximityUI
        {
            TemplateInstance = instance,
            Provider = provider,
            NameLabel = instance.Q<Label>("entity-name"),
            SubTitleLabel = instance.Q<Label>("entity-subtitle"),
            LevelLabel = instance.Q<Label>("entity-level"),
            ThreatBar = instance.Q<ProgressBar>("threat-bar"),
            AlertIcon = instance.Q<VisualElement>("alert-icon")
        };

        // Initialize values
        if (tracked.NameLabel != null) tracked.NameLabel.text = provider.EntityName;
        if (tracked.SubTitleLabel != null)
        {
            tracked.SubTitleLabel.text = provider.EntitySubTitle;
            tracked.SubTitleLabel.style.display = string.IsNullOrEmpty(provider.EntitySubTitle) ? DisplayStyle.None : DisplayStyle.Flex;
        }
        if (tracked.LevelLabel != null)
        {
            tracked.LevelLabel.text = $"Lv. {provider.EntityLevel}";
            tracked.LevelLabel.style.display = provider.EntityLevel > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        activeProximityElements.Add(entity, tracked);
    }

    private void HandleEntityExitedProximity(GameObject entity)
    {
        if (entity == null) return;

        if (activeProximityElements.TryGetValue(entity, out TrackedProximityUI tracked))
        {
            if (tracked.TemplateInstance != null && promptContainer != null)
            {
                promptContainer.Remove(tracked.TemplateInstance);
            }
            activeProximityElements.Remove(entity);
        }
    }

    private void HandleEnemyThreatLevelChanged(GameObject enemyObj, float threatValue, EnemyAlertState state)
    {
        if (enemyObj != null && activeProximityElements.TryGetValue(enemyObj, out TrackedProximityUI tracked))
        {
            // TODO: Update threat progress bar and alert icon display in UXML instance
            if (tracked.ThreatBar != null)
            {
                tracked.ThreatBar.value = threatValue * 100f;
            }

            if (tracked.AlertIcon != null)
            {
                bool isAlerted = (state == EnemyAlertState.Suspicious || state == EnemyAlertState.Chasing);
                tracked.AlertIcon.style.display = isAlerted ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }

    #endregion

    #region Projection & Positioning Logic

    /// <summary>
    /// Converts the interaction target's 3D World Position into UI Toolkit screen space coordinates.
    /// </summary>
    private void UpdatePromptPosition()
    {
        Camera cam = ActiveCamera;
        if (cam == null || promptTemplateInstance == null || currentInteractionTarget == null) return;

        Vector3 worldPos = currentInteractionTarget.PromptWorldPosition;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        // Hide if object is behind camera clipping plane
        if (screenPos.z < 0)
        {
            promptTemplateInstance.style.display = DisplayStyle.None;
            return;
        }

        if (isPromptVisible)
        {
            promptTemplateInstance.style.display = DisplayStyle.Flex;
        }

        Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
            promptContainer.panel,
            worldPos,
            cam
        );

        promptTemplateInstance.style.left = panelPos.x;
        promptTemplateInstance.style.top = panelPos.y;
    }

    private void UpdateAimInfoPosition()
    {
        // TODO: Calculate 3D to 2D screen coordinates for the aim target label
    }

    private void UpdateEmotePosition()
    {
        // TODO: Calculate 3D to 2D screen coordinates for the emotional balloon container
    }

    private void UpdateProximityElementsPosition()
    {
        Camera cam = ActiveCamera;
        if (cam == null || promptContainer == null) return;

        List<GameObject> toRemove = null;

        foreach (var kvp in activeProximityElements)
        {
            GameObject entity = kvp.Key;
            TrackedProximityUI tracked = kvp.Value;

            if (entity == null || !entity.activeInHierarchy)
            {
                toRemove ??= new List<GameObject>();
                toRemove.Add(entity);
                continue;
            }

            Vector3 worldPos = tracked.Provider.AnchorPosition;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            bool isVisible = screenPos.z > 0;
            tracked.TemplateInstance.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;

            if (isVisible)
            {
                Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
                    promptContainer.panel,
                    worldPos,
                    cam
                );

                tracked.TemplateInstance.style.position = Position.Absolute;
                tracked.TemplateInstance.style.left = panelPos.x;
                tracked.TemplateInstance.style.top = panelPos.y;
            }
        }

        if (toRemove != null)
        {
            for (int i = 0; i < toRemove.Count; i++)
            {
                HandleEntityExitedProximity(toRemove[i]);
            }
        }
    }

    private VisualTreeAsset GetTemplateForType(WorldSpaceUIType type)
    {
        return type switch
        {
            WorldSpaceUIType.EnemyBar => enemyBarTemplate,
            WorldSpaceUIType.NPCIndicator => npcNameplateTemplate,
            WorldSpaceUIType.SimpleSign => simpleSignTemplate,
            _ => null
        };
    }

    private void ClearAllProximityElements()
    {
        foreach (var tracked in activeProximityElements.Values)
        {
            if (tracked.TemplateInstance != null && promptContainer != null)
            {
                promptContainer.Remove(tracked.TemplateInstance);
            }
        }
        activeProximityElements.Clear();
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
            promptRootVisualElement.AddToClassList("prompt-hidden");

            if (immediate)
            {
                promptTemplateInstance.style.display = DisplayStyle.None;
            }
        }
    }

    private void OnFadeOutFinished(TransitionEndEvent evt)
    {
        if (promptTemplateInstance == null) return;

        if (!isPromptVisible)
        {
            promptTemplateInstance.style.display = DisplayStyle.None;
        }
    }

    /// <summary>
    /// Allows external system override for the active rendering camera.
    /// </summary>
    public void SetOverrideCamera(Camera camera)
    {
        overrideCamera = camera;
    }

    #endregion
}