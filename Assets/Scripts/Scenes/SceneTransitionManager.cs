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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    #endregion

    #region Scene Load Handlers

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Reset overlay alpha on new scene load
        ResetFadeOverlay();
        FadeInNewScene();
    }

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

    /// <summary>
    /// Loads a scene asynchronously with a smooth logo transition effect.
    /// </summary>
    public void LoadSceneWithTransition(string sceneName)
    {
        StartCoroutine(Routine_LoadScene(sceneName));
    }

    private IEnumerator Routine_LoadScene(string sceneName)
    {
        if (logoTransitionAnimator != null)
        {
            logoTransitionAnimator.ResetTrigger(END_TRIGGER);
            logoTransitionAnimator.SetTrigger(START_TRIGGER);
        }

        yield return new WaitForSeconds(transitionDuration);

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }

    /// <summary>
    /// Plays the transition to reveal the current scene.
    /// </summary>
    public void FadeInNewScene()
    {
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
        GameManager.Instance.SetGameState(GameManager.GameState.Loading);

        if (logoTransitionAnimator != null)
        {
            logoTransitionAnimator.SetTrigger(START_TRIGGER);
        }

        yield return new WaitForSeconds(transitionDuration);

        onScreenCovered?.Invoke();

        yield return null;

        if (logoTransitionAnimator != null)
        {
            logoTransitionAnimator.SetTrigger(END_TRIGGER);
        }
    }

    #endregion
}