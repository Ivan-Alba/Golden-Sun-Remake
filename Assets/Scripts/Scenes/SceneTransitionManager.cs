using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages scene loading transitions, visual split-door curtains, ambient screen fades, and async scene activation.
/// Decoupled from Singletons, listens exclusively to GameEvents.Scene, and uses UI Toolkit via SceneTransitionUI.
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    #region Serialized Fields

    [Header("UI Toolkit References")]
    [SerializeField] private SceneTransitionUI sceneTransitionUI;

    [Header("Settings")]
    [SerializeField] private float fallbackTransitionDuration = 0.8f;
    [SerializeField] private float arrivalWalkDelay = 0.2f;

    #endregion

    #region Public Properties

    /// <summary>
    /// Duration in seconds for the transition curtain animation to complete opening or closing.
    /// Reads directly from SceneTransitionUI if available.
    /// </summary>
    public float TransitionDuration => sceneTransitionUI != null ? sceneTransitionUI.CurtainAnimationDuration : fallbackTransitionDuration;

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
        if (sceneTransitionUI == null)
        {
            sceneTransitionUI = GetComponentInChildren<SceneTransitionUI>();
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
        activeTransitionCoroutine = StartCoroutine(Routine_LoadSceneAsync(sceneName));
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

    #region Ambient Screen Fade Operations

    /// <summary>
    /// Triggers a smooth opacity fade on the ambient UI Toolkit overlay panel.
    /// </summary>
    private void FadeToBlack(float targetAlpha = 1f, float duration = 1.0f)
    {
        if (sceneTransitionUI == null) return;

        if (activeFadeCoroutine != null)
        {
            StopCoroutine(activeFadeCoroutine);
        }

        activeFadeCoroutine = StartCoroutine(sceneTransitionUI.Routine_FadeToBlack(targetAlpha, duration));
    }

    /// <summary>
    /// Resets the ambient fade overlay back to fully transparent instantly.
    /// </summary>
    private void ResetFadeOverlay()
    {
        if (activeFadeCoroutine != null)
        {
            StopCoroutine(activeFadeCoroutine);
            activeFadeCoroutine = null;
        }

        if (sceneTransitionUI != null)
        {
            sceneTransitionUI.ResetFadeOverlay();
        }
    }

    #endregion

    #region Async Scene Loading & Transition Routines

    /// <summary>
    /// Handles the full 3-phase async scene transition with controlled activation.
    /// Locks controls to Cutscene state while closing curtain, then switches to Loading once covered.
    /// </summary>
    private IEnumerator Routine_LoadSceneAsync(string sceneName)
    {
        // 1. FORCE CUTSCENE STATE: Lock inputs/HUD as soon as the closing sequence starts
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.Cutscene);

        // Close curtain (World stays rendered until 100% closed)
        if (sceneTransitionUI != null)
        {
            yield return StartCoroutine(sceneTransitionUI.Routine_CloseCurtain());
        }
        else
        {
            yield return new WaitForSecondsRealtime(TransitionDuration);
        }

        // 2. SCREEN IS 100% COVERED: Now switch to Loading state safely
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.Loading);

        // Show global status indicator ("Cargando..." with Djinn)
        GameEvents.UI.TriggerShowStatus("Cargando...", StatusIconType.Djinn);
        GameEvents.Scene.TriggerTransitionCovered();

        // 3. PHASE 2: Controlled background loading with allowSceneActivation = false
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        // Wait until assets finish loading in memory (reaches 90%)
        while (asyncLoad.progress < 0.9f)
        {
            yield return null;
        }

        // 4. PHASE 3: Authorize activation & finalize
        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        yield return null;

        // Give 1-2 frames for VRAM, camera buffers, and Awake/Start logic to settle
        yield return new WaitForEndOfFrame();

        // Hide Djinn status indicator
        GameEvents.UI.TriggerHideStatus();

        // Reveal the newly loaded scene
        FadeInNewScene();

        activeTransitionCoroutine = null;
    }

    /// <summary>
    /// Opens the transition curtain and triggers OnTransitionCompleted ONLY after it is fully open.
    /// </summary>
    private void FadeInNewScene()
    {
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.Cutscene);
        StartCoroutine(Routine_FadeInAndNotify());
    }

    private IEnumerator Routine_FadeInAndNotify()
    {
        if (sceneTransitionUI != null)
        {
            yield return StartCoroutine(sceneTransitionUI.Routine_OpenCurtain());
        }
        else
        {
            yield return new WaitForSecondsRealtime(TransitionDuration);
        }

        GameEvents.Scene.TriggerTransitionCompleted();
    }

    /// <summary>
    /// Executes a visual curtain closure and reopening without changing scenes (e.g., for teleports or cuts).
    /// </summary>
    private IEnumerator Routine_PlayFullTransition(Action onScreenCovered = null)
    {
        // 1. FORCE CUTSCENE STATE during closure
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.Cutscene);

        if (sceneTransitionUI != null)
        {
            yield return StartCoroutine(sceneTransitionUI.Routine_CloseCurtain());
        }
        else
        {
            yield return new WaitForSecondsRealtime(TransitionDuration);
        }

        // Screen is covered, request Loading state for safety during teleport/changes
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.Loading);

        GameEvents.Scene.TriggerTransitionCovered();

        onScreenCovered?.Invoke();

        yield return null;

        FadeInNewScene();

        yield return new WaitForSecondsRealtime(TransitionDuration);

        activeTransitionCoroutine = null;
    }

    #endregion
}