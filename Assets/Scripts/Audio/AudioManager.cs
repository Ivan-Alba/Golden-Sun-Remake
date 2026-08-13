// File: AudioManager.cs
// Singleton Audio Manager handling background music crossfading, battle BGM state stack, and sound effect execution.
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

    // Saved State for Resuming Music after Battles
    private AudioClip savedExplorerClip;
    private float savedExplorerTime;
    private float savedExplorerTargetVolume = 1.0f;

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
    public void PlayBGM(AudioClip newClip, float targetVolume = 1.0f, float fadeDuration = 1.0f, float startTime = 0f)
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

        activeCrossfadeCoroutine = StartCoroutine(CrossfadeBGMRoutine(newClip, targetVolume, fadeDuration, startTime));
    }

    public void StopBGM(float fadeDuration = 1.0f)
    {
        if (activeCrossfadeCoroutine != null)
        {
            StopCoroutine(activeCrossfadeCoroutine);
        }

        activeCrossfadeCoroutine = StartCoroutine(FadeOutActiveBGMRoutine(fadeDuration));
    }

    private IEnumerator CrossfadeBGMRoutine(AudioClip newClip, float targetVolume, float duration, float startTime = 0f)
    {
        // Swap sources
        AudioSource oldSource = activeBGMSource;
        AudioSource newSource = inactiveBGMSource;

        activeBGMSource = newSource;
        inactiveBGMSource = oldSource;

        // Configure new active source
        newSource.clip = newClip;
        newSource.loop = true;
        newSource.time = startTime < newClip.length ? startTime : 0f;
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
        if (inactiveBGMSource != null)
        {
            inactiveBGMSource.Stop();
            inactiveBGMSource.clip = null;
        }
        activeCrossfadeCoroutine = null;
    }

    #endregion

    #region Battle & Victory BGM Control

    /// <summary>
    /// Saves current BGM track and timestamp, then plays intro (if available) seamlessly transitioning to main loop.
    /// </summary>
    public void PlayBattleBGM(bool isBossBattle = false, float fadeDuration = 0.5f)
    {
        if (audioDatabase == null) return;

        // Save current track state before switching to battle
        SaveCurrentBGMState();

        string introKey = isBossBattle ? "BossBattleIntroBGM" : "BattleIntroBGM";
        string battleKey = isBossBattle ? "BossBattleBGM" : "BattleBGM";

        bool hasIntro = audioDatabase.TryGetBGM(introKey, out AudioClip introClip, out float introVolume);
        bool hasMain = audioDatabase.TryGetBGM(battleKey, out AudioClip mainClip, out float mainVolume);

        if (hasIntro && hasMain)
        {
            // If intro exists, use seamless scheduled transition
            PlayIntroAndLoopBGM(introClip, mainClip, mainVolume, fadeDuration);
        }
        else if (hasMain)
        {
            // Fallback: Just play main track directly if intro isn't defined
            PlayBGM(mainClip, mainVolume, fadeDuration);
        }
        else
        {
            Debug.LogWarning($"[AudioManager] BGM key '{battleKey}' not found in AudioDatabase!");
        }
    }

    /// <summary>
    /// Plays Victory BGM track. Uses scheduled intro transition if both 'VictoryBGM' and 'VictoryLoopBGM' exist.
    /// </summary>
    public void PlayVictoryBGM(float fadeDuration = 0.5f)
    {
        if (audioDatabase == null) return;

        bool hasIntro = audioDatabase.TryGetBGM("VictoryBGM", out AudioClip introClip, out float introVolume);
        bool hasLoop = audioDatabase.TryGetBGM("VictoryLoopBGM", out AudioClip loopClip, out float loopVolume);

        if (hasIntro && hasLoop)
        {
            // If both intro and loop exist, use seamless scheduled transition
            PlayIntroAndLoopBGM(introClip, loopClip, loopVolume, fadeDuration);
        }
        else if (hasIntro)
        {
            // Fallback: Play single track if loop isn't defined
            PlayBGM(introClip, introVolume, fadeDuration);
        }
        else if (hasLoop)
        {
            // Fallback: Play loop directly if intro isn't defined
            PlayBGM(loopClip, loopVolume, fadeDuration);
        }
        else
        {
            Debug.LogWarning("[AudioManager] Neither 'VictoryBGM' nor 'VictoryLoopBGM' were found in AudioDatabase!");
        }
    }

    /// <summary>
    /// Plays GameOver BGM track.
    /// </summary>
    public void PlayGameOverBGM(float fadeDuration = 1.0f)
    {
        if (audioDatabase == null) return;

        if (audioDatabase.TryGetBGM("GameOverBGM", out AudioClip gameOverClip, out float targetVolume))
        {
            PlayBGM(gameOverClip, targetVolume, fadeDuration);
        }
    }

    /// <summary>
    /// Restores the exploration BGM from the exact timestamp where it was paused before entering battle.
    /// </summary>
    public void ResumeBGM(float fadeDuration = 1.0f)
    {
        if (savedExplorerClip != null)
        {
            PlayBGM(savedExplorerClip, savedExplorerTargetVolume, fadeDuration, savedExplorerTime);
            savedExplorerClip = null;
            savedExplorerTime = 0f;
        }
        else
        {
            // Fallback if no saved clip exists
            PlayBGMForScene(currentPlayingScene);
        }
    }

    /// <summary>
    /// Seamlessly plays an intro clip followed immediately by a looping main track using DSP scheduling.
    /// </summary>
    public void PlayIntroAndLoopBGM(AudioClip introClip, AudioClip mainLoopClip, float targetVolume = 1.0f, float fadeDuration = 0.5f)
    {
        if (activeCrossfadeCoroutine != null)
        {
            StopCoroutine(activeCrossfadeCoroutine);
        }

        activeCrossfadeCoroutine = StartCoroutine(PlayIntroAndLoopRoutine(introClip, mainLoopClip, targetVolume, fadeDuration));
    }

    private IEnumerator PlayIntroAndLoopRoutine(AudioClip introClip, AudioClip mainLoopClip, float targetVolume, float fadeDuration)
    {
        AudioSource oldSource = activeBGMSource;
        AudioSource introSource = bgmSourceA;
        AudioSource loopSource = bgmSourceB;

        // 1. Stop completely and reset the state of both AudioSources
        introSource.Stop();
        loopSource.Stop();

        // 2. Assign the clips
        introSource.clip = introClip;
        loopSource.clip = mainLoopClip;

        // 3. Force both clips to start strictly at time zero
        introSource.time = 0f;
        loopSource.time = 0f;

        // 4. Configure looping settings
        introSource.loop = false;
        loopSource.loop = true;

        // 5. Adjust initial volumes
        introSource.volume = 0f;
        loopSource.volume = 0f;

        // 6. Schedule precise playback using dspTime
        double scheduledStartTime = AudioSettings.dspTime + 0.05; // 50ms buffer for hardware synchronization
        double scheduledLoopTime = scheduledStartTime + ((double)introClip.samples / introClip.frequency);

        introSource.PlayScheduled(scheduledStartTime);
        loopSource.PlayScheduled(scheduledLoopTime);

        // 7. Update active/inactive source references
        activeBGMSource = loopSource;
        inactiveBGMSource = introSource;

        float finalTargetVolume = targetVolume * masterBGMVolume;
        float startOldVolume = oldSource != null ? oldSource.volume : 0f;
        float timer = 0f;

        // 8. Perform smooth crossfade (Fade in new BGM / Fade out previous music)
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = timer / fadeDuration;

            if (oldSource != null && oldSource != introSource && oldSource != loopSource && oldSource.isPlaying)
            {
                oldSource.volume = Mathf.Lerp(startOldVolume, 0f, progress);
            }

            introSource.volume = Mathf.Lerp(0f, finalTargetVolume, progress);
            loopSource.volume = Mathf.Lerp(0f, finalTargetVolume, progress);
            yield return null;
        }

        introSource.volume = finalTargetVolume;
        loopSource.volume = finalTargetVolume;

        if (oldSource != null && oldSource != introSource && oldSource != loopSource)
        {
            oldSource.Stop();
            oldSource.clip = null;
        }

        activeCrossfadeCoroutine = null;
    }

    private void SaveCurrentBGMState()
    {
        if (activeBGMSource != null && activeBGMSource.isPlaying)
        {
            savedExplorerClip = activeBGMSource.clip;
            savedExplorerTime = activeBGMSource.time;
        }
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