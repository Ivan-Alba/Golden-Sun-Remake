using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages scene loading transitions, logo overlays, and ambient screen fades.
/// Decoupled from Singletons and listens exclusively to GameEvents.Scene.
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    #region Animator Hashes (Cached for performance)

    private static readonly int StartTriggerHash = Animator.StringToHash("start");
    private static readonly int EndTriggerHash = Animator.StringToHash("end");

    #endregion

    #region Serialized Fields

    [Header("Components")]
    [SerializeField] private Animator logoTransitionAnimator;

    [Header("Ambient Fade Settings")]
    [SerializeField] private CanvasGroup fadeOverlayCanvasGroup;

    [Header("Settings")]
    [SerializeField] private float transitionDuration = 1.0f;
    [SerializeField] private float arrivalWalkDelay = 0.2f;

    #endregion

    #region Public Properties

    /// <summary>
    /// Duration in seconds for the transition animation to complete opening or closing.
    /// </summary>
    public float TransitionDuration => transitionDuration;

    /// <summary>
    /// Delay after arrival for auto-walking or player positioning routines.
    /// </summary>
    public float ArrivalWalkDelay => arrivalWalkDelay;

    #endregion

    #region Private Fields

    private Coroutine activeFadeCoroutine;
    private Coroutine activeTransitionCoroutine;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (logoTransitionAnimator == null)
        {
            logoTransitionAnimator = GetComponentInChildren<Animator>();
        }

        if (fadeOverlayCanvasGroup != null)
        {
            fadeOverlayCanvasGroup.alpha = 0f;
            fadeOverlayCanvasGroup.blocksRaycasts = false;
        }
    }

    private void OnEnable()
    {
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    #endregion

    #region Event Subscriptions

    private void SubscribeToEvents()
    {
        GameEvents.Scene.OnRequestLoadScene += HandleRequestLoadScene;
        GameEvents.Scene.OnRequestFullTransition += HandleRequestFullTransition;
        GameEvents.Scene.OnRequestFadeInScene += HandleRequestFadeInScene;
        GameEvents.Scene.OnRequestFadeToBlack += HandleRequestFadeToBlack;
        GameEvents.Scene.OnRequestResetFade += HandleRequestResetFade;
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.Scene.OnRequestLoadScene -= HandleRequestLoadScene;
        GameEvents.Scene.OnRequestFullTransition -= HandleRequestFullTransition;
        GameEvents.Scene.OnRequestFadeInScene -= HandleRequestFadeInScene;
        GameEvents.Scene.OnRequestFadeToBlack -= HandleRequestFadeToBlack;
        GameEvents.Scene.OnRequestResetFade -= HandleRequestResetFade;
    }

    #endregion

    #region Event Handlers

    private void HandleRequestLoadScene(string sceneName)
    {
        if (activeTransitionCoroutine != null) StopCoroutine(activeTransitionCoroutine);
        activeTransitionCoroutine = StartCoroutine(Routine_LoadScene(sceneName));
    }

    private void HandleRequestFullTransition(Action onScreenCovered)
    {
        if (activeTransitionCoroutine != null) StopCoroutine(activeTransitionCoroutine);
        activeTransitionCoroutine = StartCoroutine(Routine_PlayFullTransition(onScreenCovered));
    }

    private void HandleRequestFadeInScene()
    {
        FadeInNewScene();
    }

    private void HandleRequestFadeToBlack(float targetAlpha, float duration)
    {
        FadeToBlack(targetAlpha, duration);
    }

    private void HandleRequestResetFade()
    {
        ResetFadeOverlay();
    }

    #endregion

    #region Ambient Screen Fade (Black Overlay)

    /// <summary>
    /// Smoothly fades an overlay black panel to a target alpha over a specified duration.
    /// </summary>
    private void FadeToBlack(float targetAlpha = 1f, float duration = 1.0f)
    {
        if (fadeOverlayCanvasGroup == null) return;

        if (activeFadeCoroutine != null)
        {
            StopCoroutine(activeFadeCoroutine);
        }

        activeFadeCoroutine = StartCoroutine(Routine_FadeOverlay(targetAlpha, duration));
    }

    /// <summary>
    /// Resets the ambient fade overlay back to fully transparent.
    /// </summary>
    private void ResetFadeOverlay()
    {
        if (activeFadeCoroutine != null)
        {
            StopCoroutine(activeFadeCoroutine);
            activeFadeCoroutine = null;
        }

        if (fadeOverlayCanvasGroup != null)
        {
            fadeOverlayCanvasGroup.alpha = 0f;
            fadeOverlayCanvasGroup.blocksRaycasts = false;
        }
    }

    private IEnumerator Routine_FadeOverlay(float targetAlpha, float duration)
    {
        float startAlpha = fadeOverlayCanvasGroup.alpha;
        float timer = 0f;

        fadeOverlayCanvasGroup.blocksRaycasts = targetAlpha > 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            fadeOverlayCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / duration);
            yield return null;
        }

        fadeOverlayCanvasGroup.alpha = targetAlpha;
        activeFadeCoroutine = null;
    }

    #endregion

    #region Logo Transition & Scene Loading

    private IEnumerator Routine_LoadScene(string sceneName)
    {
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.Loading);

        // 1. Play closing transition (cover screen)
        if (logoTransitionAnimator != null)
        {
            logoTransitionAnimator.ResetTrigger(EndTriggerHash);
            logoTransitionAnimator.SetTrigger(StartTriggerHash);
        }

        yield return new WaitForSecondsRealtime(transitionDuration);

        GameEvents.Scene.TriggerTransitionCovered();

        // 2. Load the scene asynchronously in the background
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        activeTransitionCoroutine = null;
    }

    /// <summary>
    /// Plays the transition animation to reveal the new scene.
    /// </summary>
    private void FadeInNewScene()
    {
        ResetFadeOverlay();

        if (logoTransitionAnimator != null)
        {
            logoTransitionAnimator.ResetTrigger(StartTriggerHash);
            logoTransitionAnimator.SetTrigger(EndTriggerHash);
        }

        GameEvents.Scene.TriggerTransitionCompleted();
    }

    private IEnumerator Routine_PlayFullTransition(Action onScreenCovered = null)
    {
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.Loading);

        if (logoTransitionAnimator != null)
        {
            logoTransitionAnimator.ResetTrigger(EndTriggerHash);
            logoTransitionAnimator.SetTrigger(StartTriggerHash);
        }

        yield return new WaitForSecondsRealtime(transitionDuration);

        GameEvents.Scene.TriggerTransitionCovered();

        onScreenCovered?.Invoke();

        yield return null;

        FadeInNewScene();

        yield return new WaitForSecondsRealtime(transitionDuration);

        activeTransitionCoroutine = null;
    }

    #endregion
}