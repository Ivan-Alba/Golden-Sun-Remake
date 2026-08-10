using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Components")]
    [SerializeField] private Animator logoTransitionAnimator;

    [Header("Settings")]
    [SerializeField] private float transitionDuration = 0.5f;

    // Triggers defined in the Animator Controller
    private const string START_TRIGGER = "start"; // Closes screen (covers view with logo)
    private const string END_TRIGGER = "end";     // Opens screen (reveals new scene)

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
    }

    private void OnEnable()
    {
        // Subscribe to Unity's scene load event
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        // Unsubscribe to avoid memory leaks
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Called automatically by Unity right after a new scene finishes loading
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Automatically play the opening transition when arriving at any scene
        FadeInNewScene();
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
        // 1. Play closing animation (e.g., logo covers the screen)
        if (logoTransitionAnimator != null)
        {
            logoTransitionAnimator.ResetTrigger(END_TRIGGER);
            logoTransitionAnimator.SetTrigger(START_TRIGGER);
        }

        yield return new WaitForSeconds(transitionDuration);

        // 2. Load the target scene asynchronously
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // OnSceneLoaded will automatically be triggered by Unity once asyncLoad finishes,
        // which calls FadeInNewScene() below.
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
    /// Plays a full closing/opening transition and executes an intermediate action while the screen is completely covered.
    /// </summary>
    public IEnumerator PlayFullTransitionRoutine(System.Action onScreenCovered = null)
    {
        GameManager.Instance.SetGameState(GameManager.GameState.Loading);
        // 1. Play closing animation (e.g., logo covers the screen)
        if (logoTransitionAnimator != null)
        {
            logoTransitionAnimator.SetTrigger(START_TRIGGER);
        }

        // Wait until screen is fully covered by the logo
        yield return new WaitForSeconds(transitionDuration);

        // 2. Execute background setup code (e.g., SetOffBattle, camera swaps) while view is hidden
        onScreenCovered?.Invoke();

        // Wait 1 frame to allow Unity to process camera switches and layout refreshes
        yield return null;

        // 3. Play opening animation to reveal the newly configured screen
        if (logoTransitionAnimator != null)
        {
            logoTransitionAnimator.SetTrigger(END_TRIGGER);
        }
    }
}