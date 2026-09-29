using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Controls the visual transition curtain (split doors, central sun emblem, and ambient fade overlay) using UI Toolkit.
/// </summary>
public class SceneTransitionUI : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Visual Elements Assets")]
    [SerializeField] private Sprite sunEmblemSprite;

    [Header("Transition Timings")]
    [SerializeField] private float curtainAnimationDuration = 0.8f;

    private VisualElement root;
    private VisualElement fadeOverlay;
    private VisualElement leftDoor;
    private VisualElement rightDoor;
    private VisualElement centerEmblem;

    public float CurtainAnimationDuration => curtainAnimationDuration;

    private void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();

        root = uiDocument.rootVisualElement;
        fadeOverlay = root.Q<VisualElement>("fade-overlay");
        leftDoor = root.Q<VisualElement>("left-door");
        rightDoor = root.Q<VisualElement>("right-door");
        centerEmblem = root.Q<VisualElement>("center-emblem");

        if (sunEmblemSprite != null && centerEmblem != null)
        {
            centerEmblem.style.backgroundImage = new StyleBackground(sunEmblemSprite);
        }

        // Set transition duration dynamically in UI Toolkit styles
        var durationList = new List<TimeValue> { new TimeValue(curtainAnimationDuration, TimeUnit.Second) };
        if (leftDoor != null) leftDoor.style.transitionDuration = new StyleList<TimeValue>(durationList);
        if (rightDoor != null) rightDoor.style.transitionDuration = new StyleList<TimeValue>(durationList);

        // Ensure curtain starts fully open and overlay transparent
        OpenCurtainInstant();
    }

    #region Ambient Fade Overlay Methods

    /// <summary>
    /// Smoothly transitions the ambient overlay opacity to a target alpha value.
    /// </summary>
    public IEnumerator Routine_FadeToBlack(float targetAlpha, float duration)
    {
        if (fadeOverlay == null) yield break;

        // Override CSS transition duration dynamically if requested
        var durationList = new List<TimeValue> { new TimeValue(duration, TimeUnit.Second) };
        fadeOverlay.style.transitionDuration = new StyleList<TimeValue>(durationList);

        // Apply new opacity (triggers UI Toolkit's native CSS transition)
        fadeOverlay.style.opacity = Mathf.Clamp01(targetAlpha);

        yield return new WaitForSecondsRealtime(duration);
    }

    /// <summary>
    /// Instantly resets the ambient fade overlay back to fully transparent (opacity = 0).
    /// </summary>
    public void ResetFadeOverlay()
    {
        if (fadeOverlay == null) return;

        // Clear inline transition duration so style resets immediately
        fadeOverlay.style.transitionDuration = StyleKeyword.Null;
        fadeOverlay.style.opacity = 0f;
    }

    #endregion

    #region Curtain Routines

    /// <summary>
    /// Closes the split doors and shows the center emblem (Covers the screen).
    /// </summary>
    public IEnumerator Routine_CloseCurtain()
    {
        if (leftDoor != null) leftDoor.RemoveFromClassList("door-open-left");
        if (rightDoor != null) rightDoor.RemoveFromClassList("door-open-right");

        // Wait slightly for doors to start closing before popping the emblem
        yield return new WaitForSecondsRealtime(curtainAnimationDuration * 0.4f);
        if (centerEmblem != null) centerEmblem.RemoveFromClassList("emblem-hidden");

        // Wait for full curtain close animation to complete
        yield return new WaitForSecondsRealtime(curtainAnimationDuration * 0.6f);
    }

    /// <summary>
    /// Opens the split doors and hides the center emblem (Reveals the scene).
    /// Also resets ambient fade overlay.
    /// </summary>
    public IEnumerator Routine_OpenCurtain()
    {
        if (centerEmblem != null) centerEmblem.AddToClassList("emblem-hidden");

        yield return new WaitForSecondsRealtime(0.1f);

        if (leftDoor != null) leftDoor.AddToClassList("door-open-left");
        if (rightDoor != null) rightDoor.AddToClassList("door-open-right");

        StartCoroutine(Routine_FadeToBlack(0f, 1.5f));

        yield return new WaitForSecondsRealtime(curtainAnimationDuration);

        // Clear ambient fade overlay when scene is fully revealed
        ResetFadeOverlay();
    }

    private void OpenCurtainInstant()
    {
        ResetFadeOverlay();
        if (leftDoor != null) leftDoor.AddToClassList("door-open-left");
        if (rightDoor != null) rightDoor.AddToClassList("door-open-right");
        if (centerEmblem != null) centerEmblem.AddToClassList("emblem-hidden");
    }

    #endregion
}