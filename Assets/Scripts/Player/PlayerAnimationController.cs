using UnityEngine;

/// <summary>
/// Single Responsibility Component dedicated strictly to managing the root Hero Animator parameters, 
/// cached string hashes, blend trees, overworld interactions, and Psynergy casting animations.
/// Attached directly to the main Player root GameObject alongside the Animator component.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    #region Animator Hashes (Cached for performance)

    // Locomotion & Physics Hashes
    private static readonly int SpeedHash = Animator.StringToHash("speed");
    //private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    //private static readonly int VerticalVelocityHash = Animator.StringToHash("verticalVelocity");

    // Idle & Emotion Hashes
    private static readonly int LongTimeIdleHash = Animator.StringToHash("longTime");

    // Actions, Interactions & Golden Sun Mechanics
    private static readonly int CastPsynergyHash = Animator.StringToHash("castPsynergy");
    private static readonly int InteractHash = Animator.StringToHash("interact");
    private static readonly int OpenChestHash = Animator.StringToHash("openChest");
    private static readonly int PickFloorHash = Animator.StringToHash("pickFloor");
    private static readonly int EnemyHitHash = Animator.StringToHash("attacked");
    private static readonly int IsPushingHash = Animator.StringToHash("isPushing");
    private static readonly int AutoJumpHash = Animator.StringToHash("autoJump");
    private static readonly int ClimbHash = Animator.StringToHash("isClimbing");
    private static readonly int ClimbSpeedHash = Animator.StringToHash("climbSpeed");
    private static readonly int SlideHash = Animator.StringToHash("isSliding");
    private static readonly int FallHash = Animator.StringToHash("fallHole");

    #endregion

    #region Private Fields

    private Animator activeAnimator;
    private FootstepAudioEmitter footstepEmitter;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        activeAnimator = GetComponent<Animator>();
        footstepEmitter = GetComponent<FootstepAudioEmitter>();
    }

    private void OnEnable()
    {
        GameEvents.Player.OnRequestInteractAnimation += TriggerInteract;
        GameEvents.Player.OnRequestOpenChestAnimation += TriggerOpenChest;
        GameEvents.Player.OnRequestPickFloorAnimation += TriggerPickFloor;
        GameEvents.Player.OnRequestEnemyHitAnimation += TriggerEnemyHitReaction;
    }

    private void OnDisable()
    {
        GameEvents.Player.OnRequestInteractAnimation -= TriggerInteract;
        GameEvents.Player.OnRequestOpenChestAnimation -= TriggerOpenChest;
        GameEvents.Player.OnRequestPickFloorAnimation -= TriggerPickFloor;
        GameEvents.Player.OnRequestEnemyHitAnimation -= TriggerEnemyHitReaction;
    }

    #endregion

    #region Animator & Avatar Management

    /// <summary>
    /// Swaps the active Runtime Animator Controller (e.g. applying an AnimatorOverrideController for unique hero clips).
    /// </summary>
    public void ApplyAnimatorOverride(RuntimeAnimatorController overrideController)
    {
        if (activeAnimator != null && overrideController != null)
        {
            activeAnimator.runtimeAnimatorController = overrideController;
        }
    }

    /// <summary>
    /// Updates the Animator's target Humanoid Avatar when swapping hero models.
    /// </summary>
    public void SetAvatar(Avatar newAvatar)
    {
        if (activeAnimator != null && newAvatar != null)
        {
            activeAnimator.avatar = newAvatar;
        }
    }

    #endregion

    #region Locomotion & Movement Animations

    public void UpdateLocomotion(float normalizedSpeed)
    {
        if (activeAnimator == null) return;

        activeAnimator.SetFloat(SpeedHash, normalizedSpeed);
    }

    /*public void UpdateAirborneState(bool isGrounded, float verticalVelocity)
    {
        if (activeAnimator == null) return;

        activeAnimator.SetBool(IsGroundedHash, isGrounded);
        activeAnimator.SetFloat(VerticalVelocityHash, verticalVelocity);
    }*/

    public void ResetLocomotionAnimations()
    {
        if (activeAnimator == null) return;

        activeAnimator.SetFloat(SpeedHash, 0f);
    }

    public void TriggerLongIdle()
    {
        if (activeAnimator != null)
        {
            activeAnimator.SetTrigger(LongTimeIdleHash);
        }
    }

    #endregion

    #region Interactions & Golden Sun Field Actions

    public void TriggerCastPsynergy()
    {
        if (activeAnimator != null)
        {
            activeAnimator.SetTrigger(CastPsynergyHash);
        }
    }

    public void TriggerInteract()
    {
        if (activeAnimator != null)
        {
            activeAnimator.SetTrigger(InteractHash);
        }
    }

    public void TriggerOpenChest()
    {
        if (activeAnimator != null)
        {
            activeAnimator.SetTrigger(OpenChestHash);
        }
    }

    public void TriggerPickFloor()
    {
        if (activeAnimator != null)
        {
            activeAnimator.SetTrigger(PickFloorHash);
        }
    }

    public void TriggerEnemyHitReaction()
    {
        if (activeAnimator != null)
        {
            activeAnimator.SetTrigger(EnemyHitHash);
        }
    }

    public void SetPushingState(bool isPushing)
    {
        if (activeAnimator != null)
        {
            activeAnimator.SetBool(IsPushingHash, isPushing);
        }
    }

    public void TriggerAutoJump()
    {
        if (activeAnimator != null)
        {
            activeAnimator.SetTrigger(AutoJumpHash);
        }
    }

    public void SetClimbingState(bool isClimbing)
    {
        if (activeAnimator != null)
        {
            activeAnimator.SetBool(ClimbHash, isClimbing);
        }
    }

    public void UpdateClimbSpeed(float speed)
    {
        if (activeAnimator != null)
        {
            activeAnimator.SetFloat(ClimbSpeedHash, speed);
        }
    }
    public void SetSlidingState(bool isSliding)
    {
        if (activeAnimator != null)
        {
            activeAnimator.SetBool(SlideHash, isSliding);
        }
    }

    public void TriggerFall()
    {
        if (activeAnimator != null)
        {
            activeAnimator.SetTrigger(FallHash);
        }
    }

    #endregion

    #region Animation Event Callbacks

    public void PlayFootprints()
    {
        if (footstepEmitter != null)
        {
            footstepEmitter.OnFootstep();
        }
    }

    public void playFootprints() => PlayFootprints();

    #endregion
}