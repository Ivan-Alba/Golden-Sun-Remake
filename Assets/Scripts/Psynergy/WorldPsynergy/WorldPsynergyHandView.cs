using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Controls the 3D Psynergy Hand visual presentation.
/// Handles camera-aligned spawn transforms, multi-turn spiral entrance animations,
/// gesture completion synchronization, and return-to-caster motion.
/// </summary>
public class WorldPsynergyHandView : MonoBehaviour, IWorldPsynergyView
{
    #region Serialized Fields & References

    [Header("Components")]
    [SerializeField] private Animator handAnimator;
    [SerializeField] private ParticleSystem handEnergyAuraVFX;

    [Header("Code Spawn Animation Settings")]
    [Tooltip("Duration in seconds for the hand to scale up and rotate into position.")]
    [SerializeField] private float spawnDuration = 0.75f;

    [Tooltip("Number of full 360-degree rotations during spawn expansion.")]
    [SerializeField] private float fullRotationTurns = 2.0f;

    [Tooltip("Duration in seconds for the hand to scale down and disappear.")]
    [SerializeField] private float despawnDuration = 0.35f;

    [Tooltip("Duration in seconds for the hand to return to the player (used in Catch/Coger).")]
    [SerializeField] private float returnToCasterDuration = 0.55f;

    [Tooltip("Initial spawn scale relative to base transform scale.")]
    [SerializeField] private float startScaleMultiplier = 0.05f;

    [Tooltip("Distance ahead of the player/camera when no target is highlighted.")]
    [SerializeField] private float targetlessDistance = 3.5f;

    [Header("Code Spawn Animation Settings")]
    [Tooltip("Duration in seconds to hold the closed fist pose before starting return travel.")]
    [SerializeField] private float holdAfterGraspDuration = 0.3f;

    #endregion

    #region Private State & Hashes

    private static readonly int AnimTriggerMove = Animator.StringToHash("Cast_Mover");
    private static readonly int AnimTriggerCatch = Animator.StringToHash("Cast_Catch");
    private static readonly int AnimTriggerForce = Animator.StringToHash("Cast_Fuerza");

    private Action _onImpactOrGraspCallback;
    private Action _onSequenceFinishedCallback;

    private Vector3 _defaultScale;
    private Coroutine _activeSequenceCoroutine;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (handAnimator == null)
            handAnimator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();

        _defaultScale = transform.localScale;
        gameObject.SetActive(false);
    }

    #endregion

    #region IWorldPsynergyView Implementation

    public void Execute(
        PsynergySO psynergy,
        Transform casterTransform,
        IPsynergyTargetable target,
        Action onImpactOrGrasp,
        Action onFinished)
    {
        if (casterTransform == null || psynergy == null) return;

        _onImpactOrGraspCallback = onImpactOrGrasp;
        _onSequenceFinishedCallback = onFinished;

        if (_activeSequenceCoroutine != null)
        {
            StopCoroutine(_activeSequenceCoroutine);
        }

        gameObject.SetActive(true);

        if (handEnergyAuraVFX != null)
        {
            handEnergyAuraVFX.Play();
        }

        _activeSequenceCoroutine = StartCoroutine(Routine_ExecuteHandSequence(psynergy.PsynergySlug, casterTransform, target));
    }

    #endregion

    #region Trajectory, Animation & Return-Despawn Sequence

    private IEnumerator Routine_ExecuteHandSequence(string psynergySlug, Transform casterTransform, IPsynergyTargetable target)
    {
        string slug = psynergySlug != null ? psynergySlug.ToLowerInvariant() : string.Empty;
        bool isCatchPsynergy = slug is "coger" or "catch";

        // 1. Determine orientation direction (Facing camera or target)
        Vector3 castDirection = casterTransform.forward;
        Camera mainCam = Camera.main;

        if (mainCam != null)
        {
            Vector3 camForward = Vector3.Scale(mainCam.transform.forward, new Vector3(1f, 0f, 1f)).normalized;
            if (camForward.sqrMagnitude > 0.001f)
            {
                castDirection = camForward;
            }
        }

        // Target placement calculation
        Vector3 targetPosition = target != null
            ? target.Transform.position
            : casterTransform.position + Vector3.up * 1.2f + castDirection * targetlessDistance;

        Quaternion finalRotation = Quaternion.LookRotation(castDirection, Vector3.up);
        Vector3 minScale = _defaultScale * startScaleMultiplier;

        // 2. PHASE 1: MULTI-TURN SPIRAL SPAWN ANIMATION (Rotates 360 * turns while scaling 0 -> 1)
        float elapsed = 0f;
        transform.position = targetPosition;

        while (elapsed < spawnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / spawnDuration);

            // Scale expansion with smooth acceleration/deceleration
            float scaleT = Mathf.SmoothStep(0f, 1f, t);

            // Angle decreases to 0 as t approaches 1, aligning cleanly with finalRotation
            float currentYAngleOffset = (1f - t) * (360f * fullRotationTurns);
            Quaternion currentRotation = finalRotation * Quaternion.Euler(0f, -currentYAngleOffset, 0f);

            transform.localScale = Vector3.Lerp(minScale, _defaultScale, scaleT);
            transform.rotation = currentRotation;

            yield return null;
        }

        transform.localScale = _defaultScale;
        transform.rotation = finalRotation;

        // 3. PHASE 2: TRIGGER AND WAIT FOR ANIMATOR GESTURE COMPLETION
        TriggerHandAnimation(slug);

        // Wait until Animator state updates and completes its full clip duration
        yield return StartCoroutine(Routine_WaitForAnimationCompletion());

        // Pause briefly with fist closed to emphasize the grasp impact
        if (holdAfterGraspDuration > 0f)
        {
            yield return new WaitForSeconds(holdAfterGraspDuration);
        }

        // 4. PHASE 3: RETURN TO CASTER (Catch exclusive) AND GRADUAL DESPAWN NEAR HERO
        if (isCatchPsynergy)
        {
            yield return StartCoroutine(Routine_ReturnToCasterAndDespawn(casterTransform, minScale));
        }
        else
        {
            yield return StartCoroutine(Routine_StandardInPlaceDespawn(finalRotation, minScale));
        }

        // 5. CLEANUP & FINALIZATION
        if (handEnergyAuraVFX != null)
        {
            handEnergyAuraVFX.Stop();
        }

        gameObject.SetActive(false);

        _onSequenceFinishedCallback?.Invoke();
        _onSequenceFinishedCallback = null;
        _activeSequenceCoroutine = null;
    }

    private IEnumerator Routine_WaitForAnimationCompletion()
    {
        if (handAnimator == null) yield break;

        // Wait small delay for state transition to engage
        yield return new WaitForSeconds(0.05f);

        // Wait until transition completes if transitioning
        while (handAnimator.IsInTransition(0))
        {
            yield return null;
        }

        // Wait until normalized time completes the first clip cycle (>= 1.0)
        AnimatorStateInfo stateInfo = handAnimator.GetCurrentAnimatorStateInfo(0);
        while (stateInfo.normalizedTime < 1.0f)
        {
            yield return null;
            stateInfo = handAnimator.GetCurrentAnimatorStateInfo(0);

            // Guard against looping animations to prevent infinite loop
            if (stateInfo.loop && stateInfo.normalizedTime >= 0.95f)
            {
                break;
            }
        }
    }

    private IEnumerator Routine_ReturnToCasterAndDespawn(Transform casterTransform, Vector3 minScale)
    {
        float elapsed = 0f;
        Vector3 startReturnPos = transform.position;

        while (elapsed < returnToCasterDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / returnToCasterDuration);

            // Dynamically recalculate caster center to smoothly track moving hero
            Vector3 targetPlayerCenter = casterTransform.position + Vector3.up * 1.0f;
            transform.position = Vector3.Lerp(startReturnPos, targetPlayerCenter, Mathf.SmoothStep(0f, 1f, t));

            // Start scaling down to 0 in the final 35% of the return travel
            if (t >= 0.65f)
            {
                float fadeT = (t - 0.65f) / 0.35f;
                transform.localScale = Vector3.Lerp(_defaultScale, minScale, fadeT);
            }

            yield return null;
        }
    }

    private IEnumerator Routine_StandardInPlaceDespawn(Quaternion currentRotation, Vector3 minScale)
    {
        float elapsed = 0f;
        Quaternion endRotation = currentRotation * Quaternion.Euler(0f, -120f, 0f);

        while (elapsed < despawnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / despawnDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            transform.localScale = Vector3.Lerp(_defaultScale, minScale, smoothT);
            transform.rotation = Quaternion.Slerp(currentRotation, endRotation, smoothT);

            yield return null;
        }
    }

    private void TriggerHandAnimation(string slug)
    {
        if (handAnimator == null) return;

        switch (slug)
        {
            case "mover":
            case "move":
                handAnimator.SetTrigger(AnimTriggerMove);
                break;

            case "coger":
            case "catch":
                handAnimator.SetTrigger(AnimTriggerCatch);
                break;

            case "fuerza":
            case "force":
                handAnimator.SetTrigger(AnimTriggerForce);
                break;

            default:
                Debug.LogWarning($"[WorldPsynergyHandView] Unhandled slug '{slug}'. Fallback to Catch animation.");
                handAnimator.SetTrigger(AnimTriggerCatch);
                break;
        }
    }

    #endregion

    #region Animation Events

    /// <summary>
    /// Animation Event: Fired at the keyframe where the hand grabs, touches, or pushes.
    /// </summary>
    public void AE_OnImpactOrGrasp()
    {
        _onImpactOrGraspCallback?.Invoke();
        _onImpactOrGraspCallback = null;
    }

    #endregion
}