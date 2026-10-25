using UnityEngine;

/// <summary>
/// Bridge component attached to the visual model containing the Animator.
/// Relays Animation Events (attack completion, footsteps) to parent combat and audio controllers.
/// </summary>
public class AnimationController : MonoBehaviour
{
    #region Private Fields

    private FighterController fighterController;
    private FootstepAudioEmitter footstepEmitter;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Safely retrieve references from this GameObject or parent hierarchy
        fighterController = GetComponentInParent<FighterController>();
        footstepEmitter = GetComponentInParent<FootstepAudioEmitter>();

        if (fighterController == null)
        {
            Debug.LogWarning($"[AnimationController] No FighterController found in hierarchy for {gameObject.name}.", this);
        }
    }

    #endregion

    #region Animation Event Callbacks

    /// <summary>
    /// Invoked by Animation Events at the end of an attack animation sequence.
    /// Signals the FighterController to complete the current combat turn action.
    /// </summary>
    public void AttackAnimationDone()
    {
        if (fighterController != null)
        {
            fighterController.AttackDone();
        }
    }

    /// <summary>
    /// Invoked directly by Unity Animation Events on character walk/run animations.
    /// Delegates footstep audio detection and execution to FootstepAudioEmitter.
    /// </summary>
    public void playFootprints()
    {
        if (footstepEmitter != null)
        {
            footstepEmitter.OnFootstep();
        }
    }

    #endregion
}