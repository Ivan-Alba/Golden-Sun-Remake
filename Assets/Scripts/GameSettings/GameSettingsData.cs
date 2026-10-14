using System;
using UnityEngine;

public enum TextSpeed
{
    Normal,
    Fast
}

public enum DisplayModeOption
{
    ExclusiveFullscreen,
    BorderlessWindow,
    Windowed
}

public enum FrameRateTarget
{
    FPS_30 = 30,
    FPS_60 = 60,
    FPS_120 = 120,
    Unlimited = -1
}

/// <summary>
/// Data Transfer Object (DTO) holding all persistent game configuration state.
/// </summary>
[Serializable]
public class GameSettingsData
{
    // A. Gameplay / Sistema de Batalla & Localización
    public BattleSystemMode BattleMode = BattleSystemMode.GoldenSunClassic;
    public TextSpeed DialogueTextSpeed = TextSpeed.Normal;
    public string LanguageCode = "es"; // "es" (Spanish), "en" (English)

    // B. Audio (Valores normalizados de 0.0f a 1.0f)
    public float MasterVolume = 1.0f;
    public float BgmVolume = 0.8f;
    public float SfxVolume = 0.8f;
    public bool MuteOnFocusLoss = true;

    // C. Gráficos / Calidad
    public int QualityPresetIndex = 1; // 0: Medium, 1: High, 2: Ultra
    public DisplayModeOption DisplayMode = DisplayModeOption.BorderlessWindow;
    public int ResolutionWidth = 1920;
    public int ResolutionHeight = 1080;
    public bool VSyncEnabled = true;
    public FrameRateTarget TargetFrameRate = FrameRateTarget.FPS_60;

    /// <summary>
    /// Default empty constructor used by JsonUtility.
    /// </summary>
    public GameSettingsData() { }

    /// <summary>
    /// Factory method to safely generate default settings with current monitor resolution.
    /// Must be called during Unity lifecycle (Awake/Start), never in field initializers.
    /// </summary>
    public static GameSettingsData CreateDefault()
    {
        var settings = new GameSettingsData();
        Resolution currentRes = Screen.currentResolution;

        if (currentRes.width > 0 && currentRes.height > 0)
        {
            settings.ResolutionWidth = currentRes.width;
            settings.ResolutionHeight = currentRes.height;
        }

        return settings;
    }

    /// <summary>
    /// Creates a deep clone of the settings object to safely edit in options UI before applying.
    /// </summary>
    public GameSettingsData Clone()
    {
        return new GameSettingsData
        {
            BattleMode = this.BattleMode,
            DialogueTextSpeed = this.DialogueTextSpeed,
            LanguageCode = this.LanguageCode,
            MasterVolume = this.MasterVolume,
            BgmVolume = this.BgmVolume,
            SfxVolume = this.SfxVolume,
            MuteOnFocusLoss = this.MuteOnFocusLoss,
            QualityPresetIndex = this.QualityPresetIndex,
            DisplayMode = this.DisplayMode,
            ResolutionWidth = this.ResolutionWidth,
            ResolutionHeight = this.ResolutionHeight,
            VSyncEnabled = this.VSyncEnabled,
            TargetFrameRate = this.TargetFrameRate
        };
    }
}