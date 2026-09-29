using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Controls the decoupled global status indicator (e.g., "Loading...", "Saving...") 
/// with sprite animations in UI Toolkit.
/// </summary>
public class GlobalStatusIndicatorUI : MonoBehaviour
{
    [Header("UI Toolkit Document")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Sprite Animation Settings")]
    [SerializeField] private List<Sprite> djinnWalkingSprites;
    [SerializeField] private float frameRate = 0.12f; // Seconds per frame

    private VisualElement statusContainer;
    private VisualElement statusIcon;
    private Label statusLabel;

    private Coroutine activeAnimationCoroutine;

    private void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();

        var root = uiDocument.rootVisualElement;
        statusContainer = root.Q<VisualElement>("status-container");
        statusIcon = root.Q<VisualElement>("status-icon");
        statusLabel = root.Q<Label>("status-label");

        // Hide by default on awake
        HideStatus();
    }

    private void OnEnable()
    {
        GameEvents.UI.OnShowStatusRequested += HandleShowStatus;
        GameEvents.UI.OnHideStatusRequested += HandleHideStatus;
    }

    private void OnDisable()
    {
        GameEvents.UI.OnShowStatusRequested -= HandleShowStatus;
        GameEvents.UI.OnHideStatusRequested -= HandleHideStatus;
    }

    private void HandleShowStatus(string message, StatusIconType iconType)
    {
        ShowStatus(message, iconType);
    }

    private void HandleHideStatus()
    {
        HideStatus();
    }

    public void ShowStatus(string message, StatusIconType iconType = StatusIconType.Djinn)
    {
        if (statusLabel != null) statusLabel.text = message;

        if (statusContainer != null)
        {
            statusContainer.RemoveFromClassList("status-hidden");
        }

        if (activeAnimationCoroutine != null) StopCoroutine(activeAnimationCoroutine);

        if (iconType == StatusIconType.Djinn && djinnWalkingSprites != null && djinnWalkingSprites.Count > 0)
        {
            activeAnimationCoroutine = StartCoroutine(Routine_AnimateSpriteSheet());
        }
    }

    public void HideStatus()
    {
        if (activeAnimationCoroutine != null)
        {
            StopCoroutine(activeAnimationCoroutine);
            activeAnimationCoroutine = null;
        }

        if (statusContainer != null)
        {
            statusContainer.AddToClassList("status-hidden");
        }
    }

    private IEnumerator Routine_AnimateSpriteSheet()
    {
        int currentFrame = 0;

        while (true)
        {
            if (djinnWalkingSprites.Count > 0 && statusIcon != null)
            {
                // Assign current sprite to UI Toolkit background-image
                statusIcon.style.backgroundImage = new StyleBackground(djinnWalkingSprites[currentFrame]);
                currentFrame = (currentFrame + 1) % djinnWalkingSprites.Count;
            }

            yield return new WaitForSecondsRealtime(frameRate);
        }
    }
}

public enum StatusIconType
{
    None,
    Djinn,
    SaveDisk
}