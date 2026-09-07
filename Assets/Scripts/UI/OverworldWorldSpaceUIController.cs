using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Orchestrates 3D Overworld UI elements projected onto the 2D UI Toolkit panel during exploration and cutscenes.
/// Manages interaction prompts, aim hover target names, floating emote balloons, and proximity entity indicators (Enemies/NPCs).
/// </summary>
public class OverworldWorldSpaceUIController : MonoBehaviour
{
    #region Serialized Fields

    [Header("UI Document References")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Generic Interaction Prompt Template")]
    [Tooltip("Template used for interactables without a permanent nameplate (e.g. Chests, Doors, Loot).")]
    [SerializeField] private VisualTreeAsset genericPromptTemplate;

    [Header("Proximity Entity Templates")]
    [SerializeField] private VisualTreeAsset enemyNameplateTemplate;
    [SerializeField] private VisualTreeAsset npcNameplateTemplate;

    [Header("Camera Settings Override")]
    [Tooltip("Optional camera override. If null, dynamically syncs via GameEvents.Camera.OnActiveCameraChanged.")]
    [SerializeField] private Camera overrideCamera;

    #endregion

    #region Private Fields

    private Camera cachedActiveCamera;
    private VisualElement rootContainer;

    // --- Layer 1: Generic Interaction Prompt (Chests, Loot, Doors) ---
    private Interactable currentInteractionTarget;
    private TrackedProximityUI genericPromptTracked;
    private bool isGenericPromptVisible;

    // --- Layer 2: Aim / Hover Target Info Elements (Prepared) ---
    private GameObject currentAimTarget;
    private string currentAimName;
    private bool isAimInfoVisible;

    // --- Layer 3: Floating Emote Elements (Prepared) ---
    private Transform currentEmoteTarget;
    private bool isEmoteVisible;

    // --- Layer 4: Proximity World-Space Entities (Enemies, NPCs) ---
    private readonly Dictionary<GameObject, TrackedProximityUI> activeProximityElements = new Dictionary<GameObject, TrackedProximityUI>();

    #endregion

    #region Internal Tracking Structure

    private class TrackedProximityUI
    {
        public WorldSpaceUIWidgetPresenter Presenter { get; }
        public WorldSpaceUIProvider Provider { get; }

        public TrackedProximityUI(TemplateContainer templateInstance, WorldSpaceUIProvider provider)
        {
            Provider = provider;
            Presenter = new WorldSpaceUIWidgetPresenter(templateInstance);
        }
    }

    #endregion

    #region Properties

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
        ClearGenericPrompt();
        ClearAllProximityElements();
    }

    private void LateUpdate()
    {
        if (ActiveCamera == null) return;

        if (isGenericPromptVisible && currentInteractionTarget != null && genericPromptTracked != null)
        {
            UpdateGenericPromptPosition();
        }

        if (isAimInfoVisible && currentAimTarget != null)
        {
            UpdateAimInfoPosition();
        }

        if (isEmoteVisible && currentEmoteTarget != null)
        {
            UpdateEmotePosition();
        }

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

    private void InitializeUI()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;
        rootContainer = uiDocument.rootVisualElement;
    }

    #endregion

    #region Event Subscriptions

    private void SubscribeToEvents()
    {
        GameEvents.Camera.OnActiveCameraChanged += HandleActiveCameraChanged;
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;

        GameEvents.World.OnInteractableTargetChanged += HandleInteractableTargetChanged;
        GameEvents.World.OnShowEmoteRequested += HandleShowEmoteRequested;

        GameEvents.World.OnEntityEnteredProximity += HandleEntityEnteredProximity;
        GameEvents.World.OnEntityExitedProximity += HandleEntityExitedProximity;
        GameEvents.World.OnEnemyThreatLevelChanged += HandleEnemyThreatLevelChanged;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.Camera.OnActiveCameraChanged -= HandleActiveCameraChanged;
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;

        GameEvents.World.OnInteractableTargetChanged -= HandleInteractableTargetChanged;
        GameEvents.World.OnShowEmoteRequested -= HandleShowEmoteRequested;

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

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        //Debug.Log($"<color=#3498db>[UIController] HandleGameStateChanged: {previousState} -> {newState}</color>");

        if (newState != GameManager.GameState.WorldExploration)
        {
            currentInteractionTarget = null;
            ClearGenericPrompt();
            ClearAllProximityElements();
        }
    }

    /// <summary>
    /// Triggered when the player interaction detector focuses on a new interactable target or loses target.
    /// Strictly toggles the embedded prompt inside existing Nameplates, preserving the main entity UI.
    /// </summary>
    private void HandleInteractableTargetChanged(Interactable newTarget)
    {
        string prevName = currentInteractionTarget != null ? currentInteractionTarget.gameObject.name : "null";
        string newName = newTarget != null ? newTarget.gameObject.name : "null";
        //Debug.Log($"<color=#f1c40f>[UIController] HandleInteractableTargetChanged | Prev: {prevName} -> New: {newName}</color>");

        // 1. Clean up previous target's interaction state safely
        if (currentInteractionTarget != null)
        {
            GameObject prevObj = currentInteractionTarget.gameObject;
            if (activeProximityElements.TryGetValue(prevObj, out TrackedProximityUI prevTracked))
            {
                //Debug.Log($"<color=#f39c12>[UIController] Disabling embedded prompt on Proximity Nameplate for: {prevObj.name}</color>");
                prevTracked.Presenter.SetInteractionPrompt(false);
            }
            else
            {
                //Debug.LogWarning($"<color=#e67e22>[UIController] Prev target '{prevObj.name}' NOT found in activeProximityElements! Clearing generic prompt instead.</color>");
                ClearGenericPrompt();
            }
        }

        currentInteractionTarget = newTarget;

        // 2. If focus is completely lost, clear standalone prompts and exit
        if (currentInteractionTarget == null)
        {
            //Debug.Log("<color=#d35400>[UIController] Target is null -> Clearing generic prompt.</color>");
            ClearGenericPrompt();
            return;
        }

        GameObject targetObj = currentInteractionTarget.gameObject;

        // 3. Case A: Target has an active Proximity Nameplate (NPC/Sign) -> Show embedded sub-prompt
        if (activeProximityElements.TryGetValue(targetObj, out TrackedProximityUI activeTracked))
        {
            //Debug.Log($"<color=#2ecc71>[UIController] Enabling embedded prompt on Proximity Nameplate for: {targetObj.name}</color>");
            ClearGenericPrompt(); // Ensure standalone floating prompt is cleared
            activeTracked.Presenter.SetInteractionPrompt(true, currentInteractionTarget.KeyPrompt, currentInteractionTarget.ActionText);
        }
        // 4. Case B: Target has no Proximity Nameplate (Chest/Door/Loot) -> Instantiate Standalone Generic Prompt
        else
        {
            //Debug.Log($"<color=#3498db>[UIController] Target '{targetObj.name}' has no active Proximity Nameplate -> Creating Generic Standalone Prompt.</color>");
            SetupGenericPrompt(currentInteractionTarget.KeyPrompt, currentInteractionTarget.ActionText);
        }
    }

    private void HandleShowEmoteRequested(Transform targetHead, EmoteType emoteType, float duration)
    {
        currentEmoteTarget = targetHead;
        isEmoteVisible = (currentEmoteTarget != null);
    }

    #endregion

    #region Proximity & Enemy Handlers

    private void HandleEntityEnteredProximity(GameObject entity)
    {
        if (entity == null) return;

        if (activeProximityElements.ContainsKey(entity)) return;

        if (!entity.TryGetComponent<WorldSpaceUIProvider>(out var provider)) return;

        // If the provider type is None, this entity acts purely as a spatial anchor (e.g. Chests, Doors) 
        // and does not require a persistent proximity nameplate.
        if (provider.UIType == WorldSpaceUIType.None) return;

        VisualTreeAsset selectedTemplate = GetTemplateForType(provider.UIType);
        if (selectedTemplate == null || rootContainer == null) return;

        TemplateContainer instance = selectedTemplate.Instantiate();
        rootContainer.Add(instance);

        TrackedProximityUI tracked = new TrackedProximityUI(instance, provider);
        tracked.Presenter.Setup(provider.EntityName, provider.EntitySubTitle, provider.EntityLevel);

        activeProximityElements.Add(entity, tracked);

        // Handle embedded prompt if player is already targeting this entity
        if (currentInteractionTarget != null && currentInteractionTarget.gameObject == entity)
        {
            ClearGenericPrompt();
            tracked.Presenter.SetInteractionPrompt(true, currentInteractionTarget.KeyPrompt, currentInteractionTarget.ActionText);
        }
    }

    private void HandleEntityExitedProximity(GameObject entity)
    {
        if (entity == null) return;

        //Debug.Log($"<color=#e74c3c>[UIController] HandleEntityExitedProximity: {entity.name}</color>");

        if (activeProximityElements.TryGetValue(entity, out TrackedProximityUI tracked))
        {
            if (tracked.Presenter?.Root != null && rootContainer != null)
            {
                rootContainer.Remove(tracked.Presenter.Root);
            }
            activeProximityElements.Remove(entity);
            //Debug.Log($"<color=#c0392b>[UIController] Successfully removed Proximity Nameplate for: {entity.name} (Total tracked: {activeProximityElements.Count})</color>");
        }
        else
        {
            Debug.LogWarning($"[UIController] HandleEntityExitedProximity received for '{entity.name}', but it was NOT in activeProximityElements!");
        }
    }

    private void HandleEnemyThreatLevelChanged(GameObject enemyObj, float threatValue, EnemyAlertState state)
    {
        if (enemyObj != null && activeProximityElements.TryGetValue(enemyObj, out TrackedProximityUI tracked))
        {
            tracked.Presenter.UpdateThreat(threatValue, state);
        }
    }

    #endregion

    #region Projection & Positioning Logic

    private void SetupGenericPrompt(string keyPrompt, string actionText)
    {
        if (genericPromptTemplate == null || rootContainer == null) return;

        if (genericPromptTracked == null)
        {
            TemplateContainer instance = genericPromptTemplate.Instantiate();
            rootContainer.Add(instance);
            genericPromptTracked = new TrackedProximityUI(instance, null);
        }

        genericPromptTracked.Presenter.SetInteractionPrompt(true, keyPrompt, actionText);
        isGenericPromptVisible = true;
        UpdateGenericPromptPosition();
    }

    private void ClearGenericPrompt()
    {
        isGenericPromptVisible = false;

        if (genericPromptTracked?.Presenter?.Root != null && rootContainer != null)
        {
            rootContainer.Remove(genericPromptTracked.Presenter.Root);
            //Debug.Log("<color=#9b59b6>[UIController] Generic Prompt Removed from RootContainer.</color>");
        }

        genericPromptTracked = null;
    }

    private void UpdateGenericPromptPosition()
    {
        Camera cam = ActiveCamera;
        if (cam == null || currentInteractionTarget == null || genericPromptTracked == null || rootContainer == null || rootContainer.panel == null) return;

        // Resolve world position uniformly via WorldSpaceUIProvider if available, otherwise fallback to object transform position
        Vector3 worldPos;
        if (currentInteractionTarget.TryGetComponent<WorldSpaceUIProvider>(out var provider))
        {
            worldPos = provider.AnchorPosition;
        }
        else
        {
            worldPos = currentInteractionTarget.transform.position + Vector3.up * 1.5f; // Fallback height offset
        }

        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        bool isVisible = screenPos.z > 0;
        genericPromptTracked.Presenter.Root.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;

        if (isVisible)
        {
            Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
                rootContainer.panel,
                worldPos,
                cam
            );

            genericPromptTracked.Presenter.SetPosition(panelPos.x, panelPos.y);
        }
    }

    private void UpdateAimInfoPosition()
    {
        // TODO: Aim info projection
    }

    private void UpdateEmotePosition()
    {
        // TODO: Emote balloon projection
    }

    private void UpdateProximityElementsPosition()
    {
        Camera cam = ActiveCamera;
        if (cam == null || rootContainer == null || rootContainer.panel == null) return;

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
            tracked.Presenter.Root.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;

            if (isVisible)
            {
                Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
                    rootContainer.panel,
                    worldPos,
                    cam
                );

                tracked.Presenter.SetPosition(panelPos.x, panelPos.y);
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
            WorldSpaceUIType.EnemyIndicator => enemyNameplateTemplate,
            WorldSpaceUIType.NPCIndicator => npcNameplateTemplate,
            _ => null
        };
    }

    private void ClearAllProximityElements()
    {
        foreach (var tracked in activeProximityElements.Values)
        {
            if (tracked.Presenter?.Root != null && rootContainer != null)
            {
                rootContainer.Remove(tracked.Presenter.Root);
            }
        }
        activeProximityElements.Clear();
        //Debug.Log("<color=#95a5a6>[UIController] All Proximity Elements Cleared.</color>");
    }

    public void SetOverrideCamera(Camera camera)
    {
        overrideCamera = camera;
    }

    #endregion
}