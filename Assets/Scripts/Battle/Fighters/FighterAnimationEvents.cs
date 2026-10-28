using System;
using UnityEngine;

/// <summary>
/// Bridge component attached directly to the 3D model with the Animator.
/// Captures Unity Animation Events and converts them into C# delegates for FighterController, 
/// action executors, and audio emitters.
/// </summary>
public class FighterAnimationEvents : MonoBehaviour
{
    #region Events

    /// <summary>
    /// Triggered at the exact frame of Melee physical impact (applies damage, target hit animation, and impact SFX).
    /// </summary>
    public event Action OnHitImpact;

    /// <summary>
    /// Triggered at the exact frame when casting a Psynergy, Item, Djinn, or Summon 
    /// (instantiates magic projectiles/VFX at the hand or anchor).
    /// </summary>
    public event Action OnCastRelease;

    /// <summary>
    /// Triggered at the final frame of active action clips (Attack, Cast, Item Use) 
    /// signaling completion of the animation sequence.
    /// </summary>
    public event Action OnAnimationCompleted;

    /// <summary>
    /// Triggered at the final frame of the defeat/death animation (initiates URP dissolve effect on enemies).
    /// </summary>
    public event Action OnDeathAnimationFinished;

    #endregion

    #region Private References

    [Header("Optional Component Integrations")]
    [Tooltip("Reference to the footstep audio emitter for movement animation sync.")]
    [SerializeField] private FootstepAudioEmitter footstepEmitter;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (footstepEmitter == null)
        {
            footstepEmitter = GetComponent<FootstepAudioEmitter>();

            if (footstepEmitter == null)
                footstepEmitter = GetComponentInParent<FootstepAudioEmitter>();
        }
    }

    #endregion

    #region Unity Mecanim Animation Event Hooks

    /// <summary>
    /// Animation Event Hook: Add to the exact frame where a physical Melee attack makes contact with the target.
    /// </summary>
    public void AnimationEvent_OnHitImpact()
    {
        OnHitImpact?.Invoke();
    }

    /// <summary>
    /// Animation Event Hook: Add to the exact frame where a spell/item/summon is released (e.g., hand extended forward).
    /// </summary>
    public void AnimationEvent_OnCastRelease()
    {
        OnCastRelease?.Invoke();
    }

    /// <summary>
    /// Animation Event Hook: Add to the end of attack, cast, or item use animations.
    /// </summary>
    public void AnimationEvent_OnAnimationCompleted()
    {
        OnAnimationCompleted?.Invoke();
    }

    /// <summary>
    /// Animation Event Hook: Add to the final frame of the defeat/death animation sequence.
    /// </summary>
    public void AnimationEvent_OnDeathAnimationFinished()
    {
        OnDeathAnimationFinished?.Invoke();
    }

    /// <summary>
    /// Animation Event Hook: Add to walk/run animation clip frames to trigger surface footstep audio.
    /// </summary>
    public void AnimationEvent_OnFootstep()
    {
        if (footstepEmitter != null)
        {
            footstepEmitter.OnFootstep();
        }
    }

    #endregion
}