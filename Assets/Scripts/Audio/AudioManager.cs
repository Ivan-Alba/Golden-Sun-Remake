// File: AudioManager.cs
// Singleton Audio Manager handling background music crossfading and sound effect execution.

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    #region Serialized Fields

    [Header("Audio Data Asset")]
    [SerializeField] private AudioDatabase audioDatabase;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSourceA;
    [SerializeField] private AudioSource bgmSourceB;
    [SerializeField] private AudioSource sfxSource;

    [Header("Default Settings")]
    [SerializeField] private float defaultCrossfadeDuration = 1.0f;
    [Range(0f, 1f)][SerializeField] private float masterBGMVolume = 1.0f;
    [Range(0f, 1f)][SerializeField] private float masterSFXVolume = 1.0f;

    #endregion

    #region Private Fields

    private AudioSource activeBGMSource;
    private AudioSource inactiveBGMSource;
    private Coroutine activeCrossfadeCoroutine;
    private string currentPlayingScene = string.Empty;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Ensure AudioSources exist dynamically if not assigned in Inspector
        EnsureAudioSources();

        if (audioDatabase != null)
        {
            audioDatabase.Initialize();
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

    #region Scene Event Handlers

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayBGMForScene(scene.name);
    }

    #endregion

    #region BGM Management

    /// <summary>
    /// Plays background music assigned to the specified scene. Continuous playback if music matches.
    /// </summary>
    public void PlayBGMForScene(string sceneName)
    {
        if (audioDatabase == null) return;

        if (audioDatabase.TryGetBGM(sceneName, out AudioClip newClip, out float targetVolume))
        {
            PlayBGM(newClip, targetVolume, defaultCrossfadeDuration);
        }

        currentPlayingScene = sceneName;
    }

    /// <summary>
    /// Plays specified AudioClip as BGM. Performs smooth crossfade if another track is currently playing.
    /// </summary>
    public void PlayBGM(AudioClip newClip, float targetVolume = 1.0f, float fadeDuration = 1.0f)
    {
        if (newClip == null)
        {
            StopBGM(fadeDuration);
            return;
        }

        // Avoid restarting if the exact same clip is already active and playing
        if (activeBGMSource != null && activeBGMSource.clip == newClip && activeBGMSource.isPlaying)
        {
            return;
        }

        if (activeCrossfadeCoroutine != null)
        {
            StopCoroutine(activeCrossfadeCoroutine);
        }

        activeCrossfadeCoroutine = StartCoroutine(CrossfadeBGMRoutine(newClip, targetVolume, fadeDuration));
    }

    public void StopBGM(float fadeDuration = 1.0f)
    {
        if (activeCrossfadeCoroutine != null)
        {
            StopCoroutine(activeCrossfadeCoroutine);
        }

        activeCrossfadeCoroutine = StartCoroutine(FadeOutActiveBGMRoutine(fadeDuration));
    }

    private IEnumerator CrossfadeBGMRoutine(AudioClip newClip, float targetVolume, float duration)
    {
        // Swap sources
        AudioSource oldSource = activeBGMSource;
        AudioSource newSource = inactiveBGMSource;

        activeBGMSource = newSource;
        inactiveBGMSource = oldSource;

        // Configure new active source
        newSource.clip = newClip;
        newSource.loop = true;
        newSource.volume = 0f;
        newSource.Play();

        float finalTargetVolume = targetVolume * masterBGMVolume;
        float startOldVolume = oldSource != null ? oldSource.volume : 0f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = timer / duration;

            if (oldSource != null && oldSource.isPlaying)
            {
                oldSource.volume = Mathf.Lerp(startOldVolume, 0f, progress);
            }

            newSource.volume = Mathf.Lerp(0f, finalTargetVolume, progress);
            yield return null;
        }

        newSource.volume = finalTargetVolume;

        if (oldSource != null)
        {
            oldSource.Stop();
            oldSource.clip = null;
        }

        activeCrossfadeCoroutine = null;
    }

    private IEnumerator FadeOutActiveBGMRoutine(float duration)
    {
        if (activeBGMSource == null || !activeBGMSource.isPlaying) yield break;

        float startVolume = activeBGMSource.volume;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            activeBGMSource.volume = Mathf.Lerp(startVolume, 0f, timer / duration);
            yield return null;
        }

        activeBGMSource.Stop();
        activeBGMSource.clip = null;
        activeCrossfadeCoroutine = null;
    }

    #endregion

    #region SFX Management

    /// <summary>
    /// Plays a sound effect by its SFXType enum identifier.
    /// </summary>
    public void PlaySFX(SFXType type)
    {
        if (type == SFXType.None || audioDatabase == null) return;

        if (audioDatabase.TryGetSFX(type, out AudioClip clip, out float volume))
        {
            PlaySFXClip(clip, volume);
        }
    }

    /// <summary>
    /// Plays an explicit AudioClip directly for one-shot SFX execution.
    /// </summary>
    public void PlaySFXClip(AudioClip clip, float volume = 1.0f)
    {
        if (clip == null || sfxSource == null) return;

        float finalVolume = volume * masterSFXVolume;
        sfxSource.PlayOneShot(clip, finalVolume);
    }

    #endregion

    #region Setup Helpers

    private void EnsureAudioSources()
    {
        if (bgmSourceA == null) bgmSourceA = gameObject.AddComponent<AudioSource>();
        if (bgmSourceB == null) bgmSourceB = gameObject.AddComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();

        ConfigureBGMSource(bgmSourceA);
        ConfigureBGMSource(bgmSourceB);

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;

        activeBGMSource = bgmSourceA;
        inactiveBGMSource = bgmSourceB;
    }

    private void ConfigureBGMSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = true;
        source.volume = 0f;
    }

    #endregion
}