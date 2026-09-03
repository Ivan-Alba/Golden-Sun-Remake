using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject defining ground surface footstep sound profiles for heroes or NPCs.
/// Allows fast O(1) runtime lookup by surface tags (e.g. Stone, Grass, Wood).
/// </summary>
[CreateAssetMenu(fileName = "HeroFootstepProfile", menuName = "GoldenSun/Audio/Hero Footstep Profile")]
public class HeroFootstepProfileSO : ScriptableObject
{
    #region Nested Data Structures

    [Serializable]
    public struct SurfaceAudioGroup
    {
        [Tooltip("Ground tag or surface identifier (e.g., Stone, Grass, Wood, Water).")]
        public string surfaceTag;

        [Tooltip("Audio clips played when stepping on this surface.")]
        public AudioClip[] stepClips;
    }

    #endregion

    #region Serialized Fields

    [Header("Surface Sound Mappings")]
    [SerializeField] private List<SurfaceAudioGroup> surfaceGroups = new List<SurfaceAudioGroup>();

    [Header("Fallback Default Clips")]
    [Tooltip("Clips used when stepping on unmapped surfaces or when no surface tag matches.")]
    [SerializeField] private AudioClip[] defaultStepClips;

    #endregion

    #region Private Runtime State

    private Dictionary<string, AudioClip[]> surfaceCache;

    #endregion

    #region Cache Initialization

    /// <summary>
    /// Builds an O(1) fast lookup dictionary for surface tags.
    /// </summary>
    public void Initialize()
    {
        surfaceCache = new Dictionary<string, AudioClip[]>(surfaceGroups.Count, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < surfaceGroups.Count; i++)
        {
            var group = surfaceGroups[i];
            if (!string.IsNullOrEmpty(group.surfaceTag) && group.stepClips != null && group.stepClips.Length > 0)
            {
                if (!surfaceCache.ContainsKey(group.surfaceTag))
                {
                    surfaceCache.Add(group.surfaceTag, group.stepClips);
                }
            }
        }
    }

    #endregion

    #region Public Query API

    /// <summary>
    /// Selects a random audio clip matching the given ground surface tag.
    /// Falls back to defaultStepClips if no surface group is found.
    /// </summary>
    public AudioClip GetClipForSurface(string surfaceTag)
    {
        if (surfaceCache == null) Initialize();

        if (!string.IsNullOrEmpty(surfaceTag) && surfaceCache.TryGetValue(surfaceTag, out AudioClip[] clips) && clips.Length > 0)
        {
            return clips[UnityEngine.Random.Range(0, clips.Length)];
        }

        if (defaultStepClips != null && defaultStepClips.Length > 0)
        {
            return defaultStepClips[UnityEngine.Random.Range(0, defaultStepClips.Length)];
        }

        return null;
    }

    #endregion
}