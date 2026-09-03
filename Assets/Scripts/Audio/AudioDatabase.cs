using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject database containing scene BGM mappings and SFX audio clips.
/// Provides O(1) dictionary lookup performance at runtime.
/// </summary>
[CreateAssetMenu(fileName = "AudioDatabase", menuName = "GoldenSun/Audio/Audio Database")]
public class AudioDatabase : ScriptableObject, ISerializationCallbackReceiver
{
    #region Nested Data Structures

    [Serializable]
    public struct SceneBGMMapping
    {
        [Tooltip("Exact name of the target Unity Scene.")]
        public string sceneName;

        [Tooltip("BGM Audio Clip played when entering this scene.")]
        public AudioClip bgmClip;

        [Tooltip("Default volume scale for this track.")]
        [Range(0f, 1f)]
        public float defaultVolume;

        public SceneBGMMapping(string sceneName, AudioClip bgmClip, float defaultVolume = 1f)
        {
            this.sceneName = sceneName;
            this.bgmClip = bgmClip;
            this.defaultVolume = defaultVolume;
        }
    }

    [Serializable]
    public struct SFXMapping
    {
        [Tooltip("Enum identifier for the SFX.")]
        public SFXType sfxType;

        [Tooltip("Audio Clip corresponding to this SFX type.")]
        public AudioClip sfxClip;

        [Tooltip("Default volume scale for this sound effect.")]
        [Range(0f, 1f)]
        public float defaultVolume;

        public SFXMapping(SFXType sfxType, AudioClip sfxClip, float defaultVolume = 1f)
        {
            this.sfxType = sfxType;
            this.sfxClip = sfxClip;
            this.defaultVolume = defaultVolume;
        }
    }

    #endregion

    #region Serialized Fields

    [Header("BGM Scene Mappings")]
    [SerializeField] private List<SceneBGMMapping> sceneBGMList = new List<SceneBGMMapping>();

    [Header("SFX Mappings")]
    [SerializeField] private List<SFXMapping> sfxList = new List<SFXMapping>();

    #endregion

    #region Private Runtime State & Caches

    // Fast O(1) runtime lookup caches
    private Dictionary<string, SceneBGMMapping> bgmCache;
    private Dictionary<SFXType, SFXMapping> sfxCache;

    #endregion

    #region Initialization & Cache Engine

    /// <summary>
    /// Builds runtime lookup dictionaries for fast access.
    /// Preserves capacity to prevent memory re-allocations.
    /// </summary>
    public void Initialize()
    {
        bgmCache = new Dictionary<string, SceneBGMMapping>(sceneBGMList.Count, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sceneBGMList.Count; i++)
        {
            var mapping = sceneBGMList[i];
            if (!string.IsNullOrEmpty(mapping.sceneName) && !bgmCache.ContainsKey(mapping.sceneName))
            {
                bgmCache.Add(mapping.sceneName, mapping);
            }
        }

        sfxCache = new Dictionary<SFXType, SFXMapping>(sfxList.Count);
        for (int i = 0; i < sfxList.Count; i++)
        {
            var mapping = sfxList[i];
            if (mapping.sfxType != SFXType.None && !sfxCache.ContainsKey(mapping.sfxType))
            {
                sfxCache.Add(mapping.sfxType, mapping);
            }
        }
    }

    #endregion

    #region Public Lookup API

    /// <summary>
    /// Attempts to retrieve BGM clip and volume mapping by scene name.
    /// </summary>
    public bool TryGetBGM(string sceneName, out AudioClip clip, out float volume)
    {
        clip = null;
        volume = 1f;

        if (string.IsNullOrEmpty(sceneName)) return false;
        if (bgmCache == null) Initialize();

        if (bgmCache.TryGetValue(sceneName, out var mapping))
        {
            clip = mapping.bgmClip;
            volume = mapping.defaultVolume;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to retrieve SFX clip and volume mapping by SFXType enum.
    /// </summary>
    public bool TryGetSFX(SFXType type, out AudioClip clip, out float volume)
    {
        clip = null;
        volume = 1f;

        if (type == SFXType.None) return false;
        if (sfxCache == null) Initialize();

        if (sfxCache.TryGetValue(type, out var mapping))
        {
            clip = mapping.sfxClip;
            volume = mapping.defaultVolume;
            return true;
        }

        return false;
    }

    #endregion

    #region ISerializationCallbackReceiver Implementation

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        // Forces cache rebuild on domain reload or Inspector editing
        bgmCache = null;
        sfxCache = null;
    }

    #endregion
}