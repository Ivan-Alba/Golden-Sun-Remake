// File: AudioDatabase.cs
// ScriptableObject data asset containing scene BGM mappings and SFX audio clips.

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AudioDatabase", menuName = "GoldenSun/Audio/Audio Database")]
public class AudioDatabase : ScriptableObject
{
    [System.Serializable]
    public struct SceneBGMMapping
    {
        [Tooltip("Exact name of the Unity Scene")]
        public string sceneName;
        public AudioClip bgmClip;
        [Range(0f, 1f)] public float defaultVolume;
    }

    [System.Serializable]
    public struct SFXMapping
    {
        public SFXType sfxType;
        public AudioClip sfxClip;
        [Range(0f, 1f)] public float defaultVolume;
    }

    [Header("BGM Scene Mappings")]
    [SerializeField] private List<SceneBGMMapping> sceneBGMList = new List<SceneBGMMapping>();

    [Header("SFX Mappings")]
    [SerializeField] private List<SFXMapping> sfxList = new List<SFXMapping>();

    // Dictionary caches for O(1) fast runtime lookup
    private Dictionary<string, SceneBGMMapping> bgmCache;
    private Dictionary<SFXType, SFXMapping> sfxCache;

    public void Initialize()
    {
        bgmCache = new Dictionary<string, SceneBGMMapping>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in sceneBGMList)
        {
            if (!string.IsNullOrEmpty(mapping.sceneName) && !bgmCache.ContainsKey(mapping.sceneName))
            {
                bgmCache.Add(mapping.sceneName, mapping);
            }
        }

        sfxCache = new Dictionary<SFXType, SFXMapping>();
        foreach (var mapping in sfxList)
        {
            if (mapping.sfxType != SFXType.None && !sfxCache.ContainsKey(mapping.sfxType))
            {
                sfxCache.Add(mapping.sfxType, mapping);
            }
        }
    }

    public bool TryGetBGM(string sceneName, out AudioClip clip, out float volume)
    {
        clip = null;
        volume = 1f;

        if (bgmCache == null) Initialize();

        if (bgmCache.TryGetValue(sceneName, out var mapping))
        {
            clip = mapping.bgmClip;
            volume = mapping.defaultVolume > 0 ? mapping.defaultVolume : 1f;
            return true;
        }

        return false;
    }

    public bool TryGetSFX(SFXType type, out AudioClip clip, out float volume)
    {
        clip = null;
        volume = 1f;

        if (sfxCache == null) Initialize();

        if (sfxCache.TryGetValue(type, out var mapping))
        {
            clip = mapping.sfxClip;
            volume = mapping.defaultVolume > 0 ? mapping.defaultVolume : 1f;
            return true;
        }

        return false;
    }
}