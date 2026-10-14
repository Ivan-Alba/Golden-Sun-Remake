using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Domain manager responsible for persisting game settings to disk, applying 
/// engine/audio/graphics/localization configurations, and broadcasting dirty-state change events.
/// Decoupled from Singletons: operates purely via Event-Driven Architecture (GameEvents).
/// </summary>
public class GameSettingsManager : MonoBehaviour
{
    #region Serialized Fields & Dependencies

    [Header("Audio Mixer Reference")]
    [SerializeField] private AudioMixer mainAudioMixer;

    [Header("Exposed Mixer Parameters")]
    [SerializeField] private string masterVolumeParam = "MasterVolume";
    [SerializeField] private string bgmVolumeParam = "BGMVolume";
    [SerializeField] private string sfxVolumeParam = "SFXVolume";

    #endregion

    #region Private State

    public GameSettingsData CurrentSettings { get; private set; }
    private string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeSettingsData();
    }

    private void Start()
    {
        // Apply engine settings on Start
        ApplyAllEngineSettings(CurrentSettings);

        // Broadcast initial state to gameplay subsystems (like BattleManager)
        BroadcastInitialSettingsState();
    }

    private void OnEnable()
    {
        GameEvents.Settings.RequestApplySettings += HandleRequestApplySettings;
        GameEvents.Settings.RequestLoadSettings += LoadSettingsAndApply;
        GameEvents.Settings.OnRequestCurrentSettings += HandleRequestCurrentSettings;
    }

    private void OnDisable()
    {
        GameEvents.Settings.RequestApplySettings -= HandleRequestApplySettings;
        GameEvents.Settings.RequestLoadSettings -= LoadSettingsAndApply;
        GameEvents.Settings.OnRequestCurrentSettings -= HandleRequestCurrentSettings;
    }

    #endregion

    #region Persistence & Processing

    private void InitializeSettingsData()
    {
        if (File.Exists(FilePath))
        {
            try
            {
                string json = File.ReadAllText(FilePath);
                CurrentSettings = JsonUtility.FromJson<GameSettingsData>(json) ?? GameSettingsData.CreateDefault();
                Debug.Log($"[GameSettingsManager] Settings loaded successfully from: {FilePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameSettingsManager] Error loading settings file, resetting to defaults: {ex.Message}");
                CurrentSettings = GameSettingsData.CreateDefault();
                SaveSettingsToDisk();
            }
        }
        else
        {
            Debug.Log($"[GameSettingsManager] No settings file found. Creating default settings file at: {FilePath}");
            CurrentSettings = GameSettingsData.CreateDefault();
            SaveSettingsToDisk();
        }
    }

    public void LoadSettingsAndApply()
    {
        InitializeSettingsData();
        ApplyAllEngineSettings(CurrentSettings);
        BroadcastInitialSettingsState();
    }

    private void SaveSettingsToDisk()
    {
        try
        {
            string json = JsonUtility.ToJson(CurrentSettings, true);
            File.WriteAllText(FilePath, json);
            Debug.Log($"[GameSettingsManager] Settings successfully saved to disk: {FilePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameSettingsManager] Error saving settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets all settings to factory defaults and applies them immediately across all systems.
    /// </summary>
    public void ResetToDefaults()
    {
        Debug.Log("[GameSettingsManager] Resetting configuration to factory defaults.");
        GameSettingsData defaultSettings = GameSettingsData.CreateDefault();
        HandleRequestApplySettings(defaultSettings);
    }

    private void BroadcastInitialSettingsState()
    {
        GameEvents.Settings.TriggerSettingsLoaded(CurrentSettings);
        GameEvents.Settings.TriggerBattleModeChanged(CurrentSettings.BattleMode);
        GameEvents.Settings.TriggerLanguageChanged(CurrentSettings.LanguageCode);
    }

    #endregion

    #region Event Handlers & Dirty Checking

    private void HandleRequestCurrentSettings(Action<GameSettingsData> callback)
    {
        if (CurrentSettings == null)
        {
            InitializeSettingsData();
        }

        callback?.Invoke(CurrentSettings.Clone());
    }

    private void HandleRequestApplySettings(GameSettingsData newSettings)
    {
        if (newSettings == null) return;

        bool hasBattleModeChanged = CurrentSettings.BattleMode != newSettings.BattleMode;
        bool hasLanguageChanged = CurrentSettings.LanguageCode != newSettings.LanguageCode;
        bool hasTextSpeedChanged = CurrentSettings.DialogueTextSpeed != newSettings.DialogueTextSpeed;

        bool hasAudioChanged = !Mathf.Approximately(CurrentSettings.MasterVolume, newSettings.MasterVolume) ||
                               !Mathf.Approximately(CurrentSettings.BgmVolume, newSettings.BgmVolume) ||
                               !Mathf.Approximately(CurrentSettings.SfxVolume, newSettings.SfxVolume) ||
                               CurrentSettings.MuteOnFocusLoss != newSettings.MuteOnFocusLoss;

        bool hasGraphicsChanged = CurrentSettings.QualityPresetIndex != newSettings.QualityPresetIndex ||
                                  CurrentSettings.DisplayMode != newSettings.DisplayMode ||
                                  CurrentSettings.ResolutionWidth != newSettings.ResolutionWidth ||
                                  CurrentSettings.ResolutionHeight != newSettings.ResolutionHeight ||
                                  CurrentSettings.VSyncEnabled != newSettings.VSyncEnabled ||
                                  CurrentSettings.TargetFrameRate != newSettings.TargetFrameRate;

        // Update internal persistent state
        CurrentSettings = newSettings.Clone();
        SaveSettingsToDisk();

        // Apply engine-level modifications
        if (hasAudioChanged) ApplyAudioSettings(CurrentSettings);
        if (hasGraphicsChanged) ApplyGraphicSettings(CurrentSettings);
        if (hasLanguageChanged) SetLocalizationLocale(CurrentSettings.LanguageCode);

#if !UNITY_EDITOR
        Application.runInBackground = !CurrentSettings.MuteOnFocusLoss;
#endif

        // Broadcast gameplay events
        if (hasBattleModeChanged)
        {
            GameEvents.Settings.TriggerBattleModeChanged(CurrentSettings.BattleMode);
        }

        if (hasLanguageChanged)
        {
            GameEvents.Settings.TriggerLanguageChanged(CurrentSettings.LanguageCode);
        }

        if (hasTextSpeedChanged)
        {
            GameEvents.Settings.TriggerTextSpeedChanged(CurrentSettings.DialogueTextSpeed);
        }

        GameEvents.Settings.TriggerSettingsLoaded(CurrentSettings);
    }

    #endregion

    #region Engine Appliers (Bypassed entirely in Unity Editor)

    private void ApplyAllEngineSettings(GameSettingsData settings)
    {
        ApplyAudioSettings(settings);
        ApplyGraphicSettings(settings);
        SetLocalizationLocale(settings.LanguageCode);

#if !UNITY_EDITOR
        Application.runInBackground = !settings.MuteOnFocusLoss;
#endif
    }

    private void ApplyAudioSettings(GameSettingsData settings)
    {
#if !UNITY_EDITOR
        if (mainAudioMixer == null) return;

        SetMixerVolume(masterVolumeParam, settings.MasterVolume);
        SetMixerVolume(bgmVolumeParam, settings.BgmVolume);
        SetMixerVolume(sfxVolumeParam, settings.SfxVolume);
#endif
    }

    private void SetMixerVolume(string parameterName, float normalizedVolume)
    {
        float clamped = Mathf.Clamp(normalizedVolume, 0.0001f, 1.0f);
        float db = Mathf.Log10(clamped) * 20f;
        mainAudioMixer.SetFloat(parameterName, db);
    }

    private void ApplyGraphicSettings(GameSettingsData settings)
    {
#if !UNITY_EDITOR
        QualitySettings.SetQualityLevel(settings.QualityPresetIndex, true);
        QualitySettings.vSyncCount = settings.VSyncEnabled ? 1 : 0;
        Application.targetFrameRate = (int)settings.TargetFrameRate;

        FullScreenMode unityDisplayMode = settings.DisplayMode switch
        {
            DisplayModeOption.ExclusiveFullscreen => FullScreenMode.ExclusiveFullScreen,
            DisplayModeOption.BorderlessWindow => FullScreenMode.FullScreenWindow,
            _ => FullScreenMode.Windowed
        };

        Screen.SetResolution(settings.ResolutionWidth, settings.ResolutionHeight, unityDisplayMode);
#endif
    }

    private void SetLocalizationLocale(string languageCode)
    {
#if !UNITY_EDITOR
        StartCoroutine(SetLocaleRoutine(languageCode));
#endif
    }

    private IEnumerator SetLocaleRoutine(string languageCode)
    {
        yield return LocalizationSettings.InitializationOperation;

        var locales = LocalizationSettings.AvailableLocales.Locales;
        Locale targetLocale = null;

        for (int i = 0; i < locales.Count; i++)
        {
            if (locales[i].Identifier.Code.StartsWith(languageCode, StringComparison.OrdinalIgnoreCase))
            {
                targetLocale = locales[i];
                break;
            }
        }

        if (targetLocale != null)
        {
            LocalizationSettings.SelectedLocale = targetLocale;
            Debug.Log($"[GameSettingsManager] Localization Locale updated to: {targetLocale.Identifier.Code}");
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
#if !UNITY_EDITOR
        if (CurrentSettings != null && CurrentSettings.MuteOnFocusLoss && mainAudioMixer != null)
        {
            AudioListener.pause = !hasFocus;
        }
#endif
    }

    #endregion
}