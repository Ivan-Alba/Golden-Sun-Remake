using System.Collections;
using UnityEngine;

/// <summary>
/// 3D View/Controller component responsible exclusively for spatial movement, 
/// animation triggers, and receiving impact states.
/// </summary>
public class FighterController : MonoBehaviour
{
    #region Private Fields & References

    [SerializeField] private BattleSelectionPointer selectionPointer;

    private Animator fighterAnim;
    private FighterStats stats;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private bool isDefending;
    private bool actionFinished;
    private bool isReturning;

    #endregion

    #region Animator Hashes (Performance Optimization)

    private static readonly int HashUsePsynergy = Animator.StringToHash("usePsynergy");
    private static readonly int HashUseItem = Animator.StringToHash("useItem");
    private static readonly int HashUseDjinn = Animator.StringToHash("useDjinn");
    private static readonly int HashUseSummon = Animator.StringToHash("useSummon");

    private static readonly int HashAttack = Animator.StringToHash("attack");
    private static readonly int HashReceiveHit = Animator.StringToHash("receiveHit");
    private static readonly int HashDeath = Animator.StringToHash("death");
    private static readonly int HashVictoryPose = Animator.StringToHash("victoryPose");
    private static readonly int HashDefense = Animator.StringToHash("defense");
    private static readonly int HashMove = Animator.StringToHash("Move");

    #endregion

    #region Properties

    public bool ActionFinished { get => actionFinished; set => actionFinished = value; }
    public bool IsDefending => isDefending;

    // Cooldown accumulated value managed by TurnController
    public int NextTurnCD { get; set; }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        fighterAnim = GetComponentInChildren<Animator>();
        stats = GetComponent<FighterStats>();
    }

    #endregion

    #region Turn State Methods

    public void StartsTurn()
    {
        if (isDefending)
        {
            SetOffDefense();
        }

        // Decrement active turn modifiers on stats
        stats?.TickTurnModifiers();
    }

    #endregion

    #region Caster Animation API (FX Config Synchronization)

    /// <summary>
    /// Executes the caster's preparation/cast animation based on the CasterAnimationType enum.
    /// </summary>
    public void PlayCasterAnimation(CasterAnimationType animType)
    {
        if (fighterAnim == null || animType == CasterAnimationType.None) return;

        switch (animType)
        {
            case CasterAnimationType.UsePsynergy:
                fighterAnim.SetTrigger(HashUsePsynergy);
                break;

            case CasterAnimationType.UseItem:
                fighterAnim.SetTrigger(HashUseItem);
                break;

            case CasterAnimationType.UseDjinn:
                fighterAnim.SetTrigger(HashUseDjinn);
                break;

            case CasterAnimationType.UseSummon:
                fighterAnim.SetTrigger(HashUseSummon);
                break;

            case CasterAnimationType.AttackPreparation:
                // Reservoir hook: Reserved for future pre-attack stance/dash animations.
                break;
        }
    }

    #endregion

    #region Animation Triggers API (Exposed to BattleActionExecutor & BattleManager)

    public void PlayAttackAnimation()
    {
        if (fighterAnim != null) fighterAnim.SetTrigger(HashAttack);
    }

    public void PlayCastAnimation()
    {
        if (fighterAnim != null) fighterAnim.SetTrigger(HashUsePsynergy);
    }

    /// <summary>
    /// Triggers victory animation pose if the fighter is currently alive.
    /// </summary>
    public void PlayVictoryPose()
    {
        if (stats != null && stats.CurrHp <= 0) return;
        if (fighterAnim != null) fighterAnim.SetBool(HashVictoryPose, true);
    }

    /// <summary>
    /// Stops the victory animation pose.
    /// </summary>
    public void StopVictoryPose()
    {
        if (fighterAnim != null) fighterAnim.SetBool(HashVictoryPose, false);
    }

    #endregion

    #region Health & Status Effects

    public void ReceiveDamage(int damageAmount, bool isCritical = false)
    {
        if (fighterAnim != null) fighterAnim.SetTrigger(HashReceiveHit);

        if (stats != null)
        {
            stats.CurrHp -= damageAmount;
        }

        // Trigger floating number UI Event
        string textDisplay = isCritical ? $"CRITICAL!\n{damageAmount}" : damageAmount.ToString();
        var textType = isCritical
            ? DamageNumbersPresenter.DamageTextType.CriticalDamage
            : DamageNumbersPresenter.DamageTextType.NormalDamage;

        GameEvents.Battle.TriggerDamageNumber(gameObject, textDisplay, textType);

        if (stats != null && stats.CurrHp <= 0)
        {
            if (fighterAnim != null) fighterAnim.SetTrigger(HashDeath);
        }
    }

    public void SetUpDefense()
    {
        if (fighterAnim != null) fighterAnim.SetBool(HashDefense, true);
        isDefending = true;
    }

    public void SetOffDefense()
    {
        isDefending = false;
        if (fighterAnim != null) fighterAnim.SetBool(HashDefense, false);
    }

    #endregion

    #region Movement Helpers & Coroutines (Exposed)

    /// <summary>
    /// Coroutine wrapper to move towards a target 3D point.
    /// </summary>
    public IEnumerator MoveToTargetCoroutine(Vector3 destinationPoint, float stopDistance = 2.5f)
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        Vector3 targetPosHorizontal = new Vector3(destinationPoint.x, transform.position.y, destinationPoint.z);

        while (Vector3.Distance(transform.position, targetPosHorizontal) > stopDistance)
        {
            transform.LookAt(targetPosHorizontal);
            transform.position = Vector3.MoveTowards(transform.position, targetPosHorizontal, 8f * Time.deltaTime);

            if (fighterAnim != null) fighterAnim.SetFloat(HashMove, 6f);
            yield return null;
        }

        if (fighterAnim != null) fighterAnim.SetFloat(HashMove, 0f);
    }

    /// <summary>
    /// Coroutine wrapper to return to original spatial slot anchor position.
    /// </summary>
    public IEnumerator ReturnToOriginCoroutine()
    {
        isReturning = true;

        while (Vector3.Distance(transform.position, startPosition) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, startPosition, 8.5f * Time.deltaTime);
            if (fighterAnim != null) fighterAnim.SetFloat(HashMove, -8.5f);
            yield return null;
        }

        transform.position = startPosition;

        // Smoothly return to start rotation
        while (Quaternion.Angle(transform.rotation, startRotation) > 1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, startRotation, 10f * Time.deltaTime);
            yield return null;
        }

        transform.rotation = startRotation;
        if (fighterAnim != null) fighterAnim.SetFloat(HashMove, 0f);

        isReturning = false;
    }

    public void AttackDone() // Animation Event Hook
    {
        // Reserved for animation sync frame if needed
    }

    #endregion

    /// <summary>
    /// Controls 3D target selection pointer visibility, dynamic scale, and animation synchronization.
    /// </summary>
    public void SetSelectionPointerVisible(bool visible, bool isPrimary = false, float primaryScale = 1.0f, float secondaryScale = 0.7f)
    {
        if (selectionPointer == null) return;

        if (visible)
        {
            float targetScale = isPrimary ? primaryScale : secondaryScale;
            selectionPointer.transform.localScale = Vector3.one * targetScale;
            selectionPointer.SetVisible(true);
            selectionPointer.ResetAnimation();
        }
        else
        {
            selectionPointer.SetVisible(false);
        }
    }

    /// <summary>
    /// Resets fighter battle state (defense status, turn CDs, animators) when combat ends. Regions, etc.
    /// </summary>
    public void OnBattleEnded()
    {
        NextTurnCD = 0;
        SetOffDefense();
        StopVictoryPose();
    }
}