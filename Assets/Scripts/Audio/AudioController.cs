using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fully decoupled Audio Manager executing background music transitions, state stacks,
/// intro/loop DSP scheduling, 2D UI SFX, and spatialized 3D SFX Object Pooling via GameEvents.Audio.
/// </summary>
public class AudioController : MonoBehaviour
{
    #region Nested Data Structures

    /// <summary>
    /// Encapsulates a snapshot of a BGM track to allow pushing/popping music states on a stack.
    /// </summary>
    private struct BGMState
    {
        public AudioClip clip;
        public float time;
        public float targetVolume;

        public BGMState(AudioClip clip, float time, float targetVolume)
        {
            this.clip = clip;
            this.time = time;
            this.targetVolume = targetVolume;
        }
    }

    #endregion

    #region Serialized Fields

    [Header("Audio Data Asset")]
    [SerializeField] private AudioDatabase audioDatabase;

    [Header("BGM Audio Sources")]
    [SerializeField] private AudioSource bgmSourceA;
    [SerializeField] private AudioSource bgmSourceB;

    [Header("2D SFX Audio Source")]
    [SerializeField] private AudioSource sfxSource2D;

    [Header("3D SFX Pool Settings")]
    [SerializeField] private int sfx3DPoolSize = 15;
    [SerializeField] private float defaultMin3DDistance = 1f;
    [SerializeField] private float defaultMax3DDistance = 18f;

    [Header("Default Volume Settings")]
    [SerializeField] private float defaultCrossfadeDuration = 1.0f;
    [Range(0f, 1f)][SerializeField] private float masterBGMVolume = 1.0f;
    [Range(0f, 1f)][SerializeField] private float masterSFXVolume = 1.0f;

    #endregion

    #region Private Runtime State

    private AudioSource activeBGMSource;
    private AudioSource inactiveBGMSource;
    private Coroutine activeCrossfadeCoroutine;
    private string currentPlayingScene = string.Empty;

    // BGM State Stack for seamless multi-level music transitions
    private readonly Stack<BGMState> bgmStateStack = new Stack<BGMState>();

    // 3D Spatialized Audio Source Object Pool
    private AudioSource[] sfx3DPool;
    private int next3DPoolIndex;

    #endregion

    #region Properties

    public float MasterBGMVolume => masterBGMVolume;
    public float MasterSFXVolume => masterSFXVolume;

    #endregion

    #region Unity Lifecycle & Initialization

    private void Awake()
    {
        // Enforce single instance per scene setup
        var managers = FindObjectsByType<AudioController>(FindObjectsSortMode.None);
        if (managers.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        EnsureAudioSources();
        Initialize3DPool();

        if (audioDatabase != null)
        {
            audioDatabase.Initialize();
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;

        // Register 2D SFX Event Listeners
        GameEvents.Audio.OnPlaySFXRequested += HandlePlaySFX;
        GameEvents.Audio.OnPlaySFXClipRequested += HandlePlaySFXClip;

        // Register 3D Spatialized SFX Event Listeners
        GameEvents.Audio.OnPlaySFXAtPositionRequested += HandlePlaySFXAtPosition;
        GameEvents.Audio.OnPlaySFXClipAtPositionRequested += HandlePlaySFXClipAtPosition;

        // Register BGM Event Listeners
        GameEvents.Audio.OnPlayBGMForSceneRequested += HandlePlayBGMForScene;
        GameEvents.Audio.OnPlayBGMRequested += HandlePlayBGM;
        GameEvents.Audio.OnStopBGMRequested += HandleStopBGM;
        GameEvents.Audio.OnPlayBattleBGMRequested += HandlePlayBattleBGM;
        GameEvents.Audio.OnPlayVictoryBGMRequested += HandlePlayVictoryBGM;
        GameEvents.Audio.OnPlayGameOverBGMRequested += HandlePlayGameOverBGM;
        GameEvents.Audio.OnResumeBGMRequested += HandleResumeBGM;
        GameEvents.Audio.OnSetBGMVolumeRequested += HandleSetBGMVolume;
        GameEvents.Audio.OnSetSFXVolumeRequested += HandleSetSFXVolume;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        // Unregister 2D SFX Event Listeners
        GameEvents.Audio.OnPlaySFXRequested -= HandlePlaySFX;
        GameEvents.Audio.OnPlaySFXClipRequested -= HandlePlaySFXClip;

        // Unregister 3D Spatialized SFX Event Listeners
        GameEvents.Audio.OnPlaySFXAtPositionRequested -= HandlePlaySFXAtPosition;
        GameEvents.Audio.OnPlaySFXClipAtPositionRequested -= HandlePlaySFXClipAtPosition;

        // Unregister BGM Event Listeners
        GameEvents.Audio.OnPlayBGMForSceneRequested -= HandlePlayBGMForScene;
        GameEvents.Audio.OnPlayBGMRequested -= HandlePlayBGM;
        GameEvents.Audio.OnStopBGMRequested -= HandleStopBGM;
        GameEvents.Audio.OnPlayBattleBGMRequested -= HandlePlayBattleBGM;
        GameEvents.Audio.OnPlayVictoryBGMRequested -= HandlePlayVictoryBGM;
        GameEvents.Audio.OnPlayGameOverBGMRequested -= HandlePlayGameOverBGM;
        GameEvents.Audio.OnResumeBGMRequested -= HandleResumeBGM;
        GameEvents.Audio.OnSetBGMVolumeRequested -= HandleSetBGMVolume;
        GameEvents.Audio.OnSetSFXVolumeRequested -= HandleSetSFXVolume;
    }

    #endregion

    #region Scene Event Handlers

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HandlePlayBGMForScene(scene.name);
    }

    #endregion

    #region Event Handlers - Core BGM Management

    private void HandlePlayBGMForScene(string sceneName)
    {
        if (audioDatabase == null) return;

        if (audioDatabase.TryGetBGM(sceneName, out AudioClip newClip, out float targetVolume))
        {
            HandlePlayBGM(newClip, targetVolume, defaultCrossfadeDuration, 0f);
        }

        currentPlayingScene = sceneName;
    }

    private void HandlePlayBGM(AudioClip newClip, float targetVolume, float fadeDuration, float startTime)
    {
        if (newClip == null)
        {
            HandleStopBGM(fadeDuration);
            return;
        }

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

    private void HandleStopBGM(float fadeDuration)
    {
        if (activeCrossfadeCoroutine != null)
        {
            StopCoroutine(activeCrossfadeCoroutine);
        }

        activeCrossfadeCoroutine = StartCoroutine(FadeOutActiveBGMRoutine(fadeDuration));
    }

    #endregion

    #region Event Handlers - Battle, Victory & BGM Stack Control

    private void HandlePlayBattleBGM(bool isBossBattle, float fadeDuration)
    {
        if (audioDatabase == null) return;

        PushCurrentBGMState();

        string introKey = isBossBattle ? "BossBattleIntroBGM" : "BattleIntroBGM";
        string battleKey = isBossBattle ? "BossBattleBGM" : "BattleBGM";

        bool hasIntro = audioDatabase.TryGetBGM(introKey, out AudioClip introClip, out float introVolume);
        bool hasMain = audioDatabase.TryGetBGM(battleKey, out AudioClip mainClip, out float mainVolume);

        if (hasIntro && hasMain)
        {
            PlayIntroAndLoopBGM(introClip, mainClip, mainVolume, fadeDuration);
        }
        else if (hasMain)
        {
            HandlePlayBGM(mainClip, mainVolume, fadeDuration, 0f);
        }
        else
        {
            Debug.LogWarning($"[AudioController] BGM key '{battleKey}' not found in AudioDatabase!");
        }
    }

    private void HandlePlayVictoryBGM(float fadeDuration)
    {
        if (audioDatabase == null) return;

        bool hasIntro = audioDatabase.TryGetBGM("VictoryBGM", out AudioClip introClip, out float introVolume);
        bool hasLoop = audioDatabase.TryGetBGM("VictoryLoopBGM", out AudioClip loopClip, out float loopVolume);

        if (hasIntro && hasLoop)
        {
            PlayIntroAndLoopBGM(introClip, loopClip, loopVolume, fadeDuration);
        }
        else if (hasIntro)
        {
            HandlePlayBGM(introClip, introVolume, fadeDuration, 0f);
        }
        else if (hasLoop)
        {
            HandlePlayBGM(loopClip, loopVolume, fadeDuration, 0f);
        }
        else
        {
            Debug.LogWarning("[AudioController] Neither 'VictoryBGM' nor 'VictoryLoopBGM' found in AudioDatabase!");
        }
    }

    private void HandlePlayGameOverBGM(float fadeDuration)
    {
        if (audioDatabase == null) return;

        if (audioDatabase.TryGetBGM("GameOverBGM", out AudioClip gameOverClip, out float targetVolume))
        {
            HandlePlayBGM(gameOverClip, targetVolume, fadeDuration, 0f);
        }
    }

    private void HandleResumeBGM(float fadeDuration)
    {
        if (bgmStateStack.Count > 0)
        {
            BGMState previousState = bgmStateStack.Pop();
            HandlePlayBGM(previousState.clip, previousState.targetVolume, fadeDuration, previousState.time);
        }
        else
        {
            HandlePlayBGMForScene(currentPlayingScene);
        }
    }

    #endregion

    #region Event Handlers - Dynamic Volume Controls

    private void HandleSetBGMVolume(float volume)
    {
        masterBGMVolume = Mathf.Clamp01(volume);

        if (activeBGMSource != null && activeBGMSource.isPlaying)
        {
            activeBGMSource.volume = masterBGMVolume;
        }
    }

    private void HandleSetSFXVolume(float volume)
    {
        masterSFXVolume = Mathf.Clamp01(volume);
    }

    #endregion

    #region Event Handlers - 2D SFX Execution

    private void HandlePlaySFX(SFXType type)
    {
        if (type == SFXType.None || audioDatabase == null) return;

        if (audioDatabase.TryGetSFX(type, out AudioClip clip, out float volume))
        {
            HandlePlaySFXClip(clip, volume, 1.0f);
        }
    }

    private void HandlePlaySFXClip(AudioClip clip, float volume, float pitch = 1.0f)
    {
        if (clip == null || sfxSource2D == null) return;

        float finalVolume = volume * masterSFXVolume;

        sfxSource2D.pitch = pitch;
        sfxSource2D.PlayOneShot(clip, finalVolume);
        sfxSource2D.pitch = 1.0f;
    }

    #endregion

    #region Event Handlers - 3D Spatialized SFX Execution (Object Pool)

    private void HandlePlaySFXAtPosition(SFXType type, Vector3 position)
    {
        if (type == SFXType.None || audioDatabase == null) return;

        if (audioDatabase.TryGetSFX(type, out AudioClip clip, out float volume))
        {
            HandlePlaySFXClipAtPosition(clip, position, volume, 1.0f);
        }
    }

    private void HandlePlaySFXClipAtPosition(AudioClip clip, Vector3 position, float volume, float pitch = 1.0f)
    {
        if (clip == null) return;

        AudioSource source3D = GetNextAvailable3DAudioSource();
        if (source3D == null) return;

        source3D.transform.position = position;
        source3D.pitch = pitch;

        float finalVolume = volume * masterSFXVolume;
        source3D.PlayOneShot(clip, finalVolume);
    }

    private AudioSource GetNextAvailable3DAudioSource()
    {
        if (sfx3DPool == null || sfx3DPool.Length == 0) return null;

        // 1. Search for a currently non-playing AudioSource in the pool
        for (int i = 0; i < sfx3DPool.Length; i++)
        {
            int index = (next3DPoolIndex + i) % sfx3DPool.Length;
            if (!sfx3DPool[index].isPlaying)
            {
                next3DPoolIndex = (index + 1) % sfx3DPool.Length;
                return sfx3DPool[index];
            }
        }

        // 2. Fallback: If all 15 sources are busy, recycle the oldest one via round-robin
        AudioSource recycledSource = sfx3DPool[next3DPoolIndex];
        next3DPoolIndex = (next3DPoolIndex + 1) % sfx3DPool.Length;
        recycledSource.Stop();

        return recycledSource;
    }

    #endregion

    #region Internal BGM Crossfade & Scheduling Engine

    private IEnumerator CrossfadeBGMRoutine(AudioClip newClip, float targetVolume, float duration, float startTime = 0f)
    {
        AudioSource oldSource = activeBGMSource;
        AudioSource newSource = inactiveBGMSource;

        activeBGMSource = newSource;
        inactiveBGMSource = oldSource;

        newSource.clip = newClip;
        newSource.loop = true;
        newSource.time = (startTime < newClip.length) ? startTime : 0f;
        newSource.volume = 0f;
        newSource.Play();

        float finalTargetVolume = targetVolume * masterBGMVolume;
        float startOldVolume = (oldSource != null) ? oldSource.volume : 0f;
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

    private void PlayIntroAndLoopBGM(AudioClip introClip, AudioClip mainLoopClip, float targetVolume, float fadeDuration)
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

        introSource.Stop();
        loopSource.Stop();

        introSource.clip = introClip;
        loopSource.clip = mainLoopClip;

        introSource.time = 0f;
        loopSource.time = 0f;

        introSource.loop = false;
        loopSource.loop = true;

        introSource.volume = 0f;
        loopSource.volume = 0f;

        // DSP Scheduling hardware synchronization buffer
        double scheduledStartTime = AudioSettings.dspTime + 0.05;
        double scheduledLoopTime = scheduledStartTime + ((double)introClip.samples / introClip.frequency);

        introSource.PlayScheduled(scheduledStartTime);
        loopSource.PlayScheduled(scheduledLoopTime);

        activeBGMSource = loopSource;
        inactiveBGMSource = introSource;

        float finalTargetVolume = targetVolume * masterBGMVolume;
        float startOldVolume = (oldSource != null) ? oldSource.volume : 0f;
        float timer = 0f;

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

    private void PushCurrentBGMState()
    {
        if (activeBGMSource != null && activeBGMSource.isPlaying && activeBGMSource.clip != null)
        {
            BGMState state = new BGMState(activeBGMSource.clip, activeBGMSource.time, activeBGMSource.volume / Mathf.Max(0.001f, masterBGMVolume));
            bgmStateStack.Push(state);
        }
    }

    #endregion

    #region Setup Helpers & Object Pooling

    private void EnsureAudioSources()
    {
        if (bgmSourceA == null) bgmSourceA = gameObject.AddComponent<AudioSource>();
        if (bgmSourceB == null) bgmSourceB = gameObject.AddComponent<AudioSource>();
        if (sfxSource2D == null) sfxSource2D = gameObject.AddComponent<AudioSource>();

        ConfigureBGMSource(bgmSourceA);
        ConfigureBGMSource(bgmSourceB);

        sfxSource2D.playOnAwake = false;
        sfxSource2D.loop = false;
        sfxSource2D.spatialBlend = 0f; // 2D flat sound

        activeBGMSource = bgmSourceA;
        inactiveBGMSource = bgmSourceB;
    }

    private void ConfigureBGMSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = true;
        source.volume = 0f;
        source.spatialBlend = 0f;
    }

    private void Initialize3DPool()
    {
        sfx3DPool = new AudioSource[sfx3DPoolSize];
        GameObject poolContainer = new GameObject("SFX3D_Pool_Container");
        poolContainer.transform.SetParent(transform);

        for (int i = 0; i < sfx3DPoolSize; i++)
        {
            GameObject node = new GameObject($"SFX3D_Node_{i}");
            node.transform.SetParent(poolContainer.transform);

            AudioSource source = node.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1.0f; // 100% 3D spatial sound
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = defaultMin3DDistance;
            source.maxDistance = defaultMax3DDistance;
            source.dopplerLevel = 0f;

            sfx3DPool[i] = source;
        }
    }

    #endregion
}