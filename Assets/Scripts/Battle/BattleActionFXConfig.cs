using System;
using UnityEngine;


public enum CasterAnimationType
{
    None,
    AttackPreparation,
    UsePsynergy,
    UseItem,
    UseDjinn,
    UseSummon
}

/// <summary>
/// Unified configuration container for all Battle Action Visuals & Audio FX.
/// Shared across Psynergies, Djinn Unleashes, Consumables, Weapon Unleashes, and Item Battle Effects.
/// Synchronizes visual sequences via Animation Events with fallback timing support.
/// </summary>
[Serializable]
public struct BattleActionFXConfig
{
    [Header("Caster Animations & Origin FX")]
    [Tooltip("Type of animation to play for the caster.")]
    public CasterAnimationType CasterAnimation;

    [Tooltip("Optional custom aura/circle VFX instantiated around the caster's feet upon initiating the action.")]
    public GameObject CasterAuraVFX;

    [Header("Main Sequence & Area Visuals")]
    [Tooltip("Main action sequence VFX Prefab executed over the target area (e.g., Fireball, Healing Pillar).")]
    public GameObject MainActionVFX;

    [Header("Target Impact Visuals")]
    [Tooltip("Standardized hit effect type spawned on each affected target (e.g., Slash, Pierce, MagicSpark).")]
    public HitFXType TargetHitFXType;

    [Tooltip("Custom hit VFX prefab instantiated on targets if TargetHitFXType is set to Custom.")]
    public GameObject CustomTargetHitVFX;

    [Header("Audio & Sound FX")]
    [Tooltip("Optional direct 3D AudioClip executed during action sequence.")]
    public AudioClip DirectAudioClip;

    [Tooltip("Fallback system SFXType triggered if no direct AudioClip is assigned.")]
    public SFXType FallbackSFX;

    [Header("Sequence Delays & Fallback Timings (Seconds)")]
    [Tooltip("Fallback duration for the casting phase if no Animation Event is triggered.")]
    public float FallbackCastDuration;

    [Tooltip("Fallback delay before applying damage/healing impact if the VFX does not emit an impact event.")]
    public float FallbackImpactDelay;
}