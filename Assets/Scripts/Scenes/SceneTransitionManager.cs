// File: SceneTransitionManager.cs
// Manages scene loading transitions, logo overlays, and ambient screen fades.
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

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
    public float ArrivalWalkDelay => arrivalWalkDelay;

    #endregion

    #region Private Fields

    private const string START_TRIGGER = "start"; // Closes screen (covers view with logo)
    private const string END_TRIGGER = "end";     // Opens screen (reveals new scene)

    private Coroutine activeFadeCoroutine;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

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

    #endregion

    #region Ambient Screen Fade (Black Overlay)

    /// <summary>
    /// Smoothly fades an overlay black panel to a target alpha (e.g., 0.8f or 1f) over a specified duration.
    /// </summary>
    public void FadeToBlack(float targetAlpha = 1f, float duration = 1.0f)
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
    public void ResetFadeOverlay()
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

    /// <summary>
    /// Initiates a scene load covered by the closing transition animation.
    /// </summary>
    public void LoadSceneWithTransition(string sceneName)
    {
        StartCoroutine(Routine_LoadScene(sceneName));
    }

    private IEnumerator Routine_LoadScene(string sceneName)
    {
        // 1. Play closing transition (cover screen)
        if (logoTransitionAnimator != null)
        {
            logoTransitionAnimator.ResetTrigger(END_TRIGGER);
            logoTransitionAnimator.SetTrigger(START_TRIGGER);
        }

        yield return new WaitForSeconds(transitionDuration);

        // 2. Load the scene asynchronously in the background
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }

    /// <summary>
    /// Plays the transition animation to reveal the new scene.
    /// </summary>
    public void FadeInNewScene()
    {
        ResetFadeOverlay();

        if (logoTransitionAnimator != null)
        {
            logoTransitionAnimator.ResetTrigger(START_TRIGGER);
            logoTransitionAnimator.SetTrigger(END_TRIGGER);
        }
    }

    /// <summary>
    /// Plays a full closing/opening transition and executes an intermediate action while covered.
    /// </summary>
    public IEnumerator PlayFullTransitionRoutine(System.Action onScreenCovered = null)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameManager.GameState.Loading);
        }

        if (logoTransitionAnimator != null)
        {
            logoTransitionAnimator.ResetTrigger(END_TRIGGER);
            logoTransitionAnimator.SetTrigger(START_TRIGGER);
        }

        yield return new WaitForSeconds(transitionDuration);

        onScreenCovered?.Invoke();

        yield return null;

        FadeInNewScene();

        yield return new WaitForSeconds(transitionDuration);
    }

    #endregion
}