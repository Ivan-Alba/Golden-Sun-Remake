using System.Collections;
using UnityEngine;

/// <summary>
/// 3D View/Controller component responsible exclusively for spatial movement, 
/// animation triggers, and receiving impact states.
/// </summary>
public class FighterController : MonoBehaviour
{
    #region Private Fields & References

    private Animator fighterAnim;
    private FighterStats stats;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private bool isDefending;
    private bool actionFinished;
    private bool isReturning;

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

    #region Animation Triggers API (Exposed to BattleActionExecutor)

    public void PlayAttackAnimation()
    {
        if (fighterAnim != null) fighterAnim.SetTrigger("attack");
    }

    public void PlayCastAnimation()
    {
        if (fighterAnim != null) fighterAnim.SetTrigger("usePsynergy");
    }

    #endregion

    #region Health & Status Effects

    public void ReceiveDamage(int damageAmount, bool isCritical = false)
    {
        if (fighterAnim != null) fighterAnim.SetTrigger("receiveHit");

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
            if (fighterAnim != null) fighterAnim.SetTrigger("death");
        }
    }

    public void SetUpDefense()
    {
        if (fighterAnim != null) fighterAnim.SetBool("defense", true);
        isDefending = true;
    }

    public void SetOffDefense()
    {
        isDefending = false;
        if (fighterAnim != null) fighterAnim.SetBool("defense", false);
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

            if (fighterAnim != null) fighterAnim.SetFloat("Move", 6f);
            yield return null;
        }

        if (fighterAnim != null) fighterAnim.SetFloat("Move", 0f);
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
            if (fighterAnim != null) fighterAnim.SetFloat("Move", -8.5f);
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
        if (fighterAnim != null) fighterAnim.SetFloat("Move", 0f);

        isReturning = false;
    }

    public void AttackDone() // Animation Event Hook
    {
        // Reserved for animation sync frame if needed
    }

    #endregion
}