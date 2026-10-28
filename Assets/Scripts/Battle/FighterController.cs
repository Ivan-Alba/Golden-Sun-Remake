using System.Collections;
using UnityEngine;

/// <summary>
/// 3D View/Controller component responsible exclusively for spatial movement, 
/// smooth Melee dash/return routines, animation triggers, and receiving impact states.
/// </summary>
public class FighterController : MonoBehaviour
{
    #region Serialized Fields & References

    [Header("Visual Representation")]
    [Tooltip("Reference to the 3D model GameObject (with Animator/Renderers) to toggle visibility in Overworld vs Battle.")]
    [SerializeField] private GameObject battleVisualModel;

    [Tooltip("Direct reference to the fighter's Animator component (works even if the visual model starts inactive).")]
    [SerializeField] private Animator fighterAnim;

    [Header("Visual & Animation Setup")]
    [SerializeField] private FighterAnimationEvents animationEvents;

    [SerializeField] private BattleSelectionPointer selectionPointer;

    [Header("Movement Settings")]
    [Tooltip("Speed multiplier for Melee approach dash.")]
    [SerializeField] private float approachSpeed = 12f;

    [Tooltip("Speed multiplier for returning back to home slot position.")]
    [SerializeField] private float returnSpeed = 10f;

    [Tooltip("Rotation interpolation speed during movement transitions.")]
    [SerializeField] private float rotationSpeed = 12f;

    #endregion

    #region Private Fields

    private FighterStats stats;

    private Vector3 homePosition;
    private Quaternion homeRotation;
    private bool hasStoredHomeTransform;

    private bool isDefending;
    private bool actionFinished;
    private bool isReturning;
    private bool isInHitReaction;

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

    /// <summary>
    /// Flag set to true by FighterAnimationEvents when the active clip finishes playing.
    /// </summary>
    public bool ActionFinished { get => actionFinished; set => actionFinished = value; }

    /// <summary>
    /// Indicates whether the fighter is currently playing a hit reaction/damage animation.
    /// </summary>
    public bool IsInHitReaction => isInHitReaction;

    /// <summary>
    /// Indicates whether the fighter is currently holding a defensive stance.
    /// </summary>
    public bool IsDefending => isDefending;

    /// <summary>
    /// Cooldown accumulated value managed by TurnController.
    /// </summary>
    public int NextTurnCD { get; set; }

    #endregion

    #region Unity Lifecycle & Initialization

    private void Awake()
    {
        if (fighterAnim == null)
        {
            fighterAnim = GetComponentInChildren<Animator>(true);
        }

        if (animationEvents == null)
        {
            animationEvents = GetComponentInChildren<FighterAnimationEvents>(true);
        }

        SubscribeAnimationEvents();
        stats = GetComponent<FighterStats>();
    }

    private void OnDestroy()
    {
        UnsubscribeAnimationEvents();
    }

    /// <summary>
    /// Explicitly registers the fighter's designated battle slot position and rotation.
    /// Called directly by BattleFieldSpawner upon arena layout calculation.
    /// </summary>
    public void SetHomeTransform(Vector3 position, Quaternion rotation)
    {
        homePosition = position;
        homeRotation = rotation;
        hasStoredHomeTransform = true;
    }

    #endregion

    #region Animation Events Subscriptions

    private void SubscribeAnimationEvents()
    {
        if (animationEvents == null) return;

        animationEvents.OnHitImpact += HandleHitImpact;
        animationEvents.OnCastRelease += HandleCastRelease;
        animationEvents.OnAnimationCompleted += HandleAnimationCompleted;
        animationEvents.OnDeathAnimationFinished += OnDeathAnimationFinished;
    }

    private void UnsubscribeAnimationEvents()
    {
        if (animationEvents == null) return;

        animationEvents.OnHitImpact -= HandleHitImpact;
        animationEvents.OnCastRelease -= HandleCastRelease;
        animationEvents.OnAnimationCompleted -= HandleAnimationCompleted;
        animationEvents.OnDeathAnimationFinished -= OnDeathAnimationFinished;
    }

    private void HandleHitImpact()
    {
        // Reserved hook for local hit feedback / particles
    }

    private void HandleCastRelease()
    {
        // Reserved hook for local magic cast feedback / particles
    }

    private void HandleAnimationCompleted()
    {
        actionFinished = true;
        isInHitReaction = false;
    }

    #endregion

    #region Turn State Methods

    public void StartsTurn()
    {
        if (isDefending)
        {
            SetOffDefense();
        }

        stats?.TickTurnModifiers();
    }

    public void SetBattleVisualsActive(bool active)
    {
        if (battleVisualModel != null)
        {
            battleVisualModel.SetActive(active);
        }
        else
        {
            gameObject.SetActive(active);
        }
    }

    #endregion

    #region Caster & Attack Animation API

    /// <summary>
    /// Prepares and triggers the corresponding spell or item cast animation clip.
    /// Resets action completion flag.
    /// </summary>
    public void PlayCasterAnimation(CasterAnimationType animType)
    {
        actionFinished = false;
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
                break;
        }
    }

    /// <summary>
    /// Triggers physical attack animation clip. Resets action completion flag.
    /// </summary>
    public void PlayAttackAnimation()
    {
        actionFinished = false;
        if (fighterAnim != null) fighterAnim.SetTrigger(HashAttack);
    }

    public void PlayCastAnimation()
    {
        actionFinished = false;
        if (fighterAnim != null) fighterAnim.SetTrigger(HashUsePsynergy);
    }

    public void PlayVictoryPose()
    {
        if (stats != null && stats.CurrHp <= 0) return;
        if (fighterAnim != null) fighterAnim.SetBool(HashVictoryPose, true);
    }

    public void StopVictoryPose()
    {
        if (fighterAnim != null) fighterAnim.SetBool(HashVictoryPose, false);
    }

    #endregion

    #region Health & Status Effects

    public void ReceiveDamage(int damageAmount, bool isCritical = false)
    {
        if (fighterAnim != null)
        {
            isInHitReaction = true;
            actionFinished = false;
            fighterAnim.SetTrigger(HashReceiveHit);
        }

        if (stats != null)
        {
            stats.CurrHp = Mathf.Max(0, stats.CurrHp - damageAmount);
        }

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

    #region Movement Routines (Smooth Melee Dash & Backwards Return)

    /// <summary>
    /// Calculates the optimal Melee stopping position in front of a target GameObject.
    /// </summary>
    public Vector3 CalculateMeleeStoppingPosition(GameObject targetObj, float fallbackOffset = 2.2f)
    {
        if (targetObj == null) return transform.position;

        if (targetObj.TryGetComponent<FighterBattleAnchors>(out var anchors))
        {
            return anchors.GetMeleeAttackWorldPosition();
        }

        Vector3 targetPos = targetObj.transform.position;
        Vector3 directionFromTarget = (transform.position - targetPos).normalized;
        directionFromTarget.y = 0;

        float offsetDistance = fallbackOffset;
        if (targetObj.TryGetComponent<Collider>(out var col))
        {
            offsetDistance = Mathf.Max(fallbackOffset, col.bounds.extents.magnitude * 0.9f);
        }

        return targetPos + (directionFromTarget * offsetDistance);
    }

    /// <summary>
    /// Smooth coroutine moving the fighter towards a target Melee position using smooth lerp and progressive rotation.
    /// </summary>
    public IEnumerator MoveToTargetCoroutine(GameObject targetObj, float stopDistanceOffset = 2.2f)
    {
        if (!hasStoredHomeTransform)
        {
            SetHomeTransform(transform.position, transform.rotation);
        }

        Vector3 destinationPoint = CalculateMeleeStoppingPosition(targetObj, stopDistanceOffset);
        destinationPoint.y = transform.position.y;

        Vector3 startPos = transform.position;
        float totalDistance = Vector3.Distance(startPos, destinationPoint);

        if (totalDistance < 0.1f) yield break;

        float travelTime = totalDistance / approachSpeed;
        float elapsedTime = 0f;

        if (fighterAnim != null) fighterAnim.SetFloat(HashMove, 6f);

        while (elapsedTime < travelTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / travelTime;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(startPos, destinationPoint, smoothT);

            Vector3 lookDir = (destinationPoint - transform.position).normalized;
            if (lookDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }

            yield return null;
        }

        transform.position = destinationPoint;

        Vector3 finalLookDir = (targetObj.transform.position - transform.position).normalized;
        finalLookDir.y = 0;
        if (finalLookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(finalLookDir);
        }

        if (fighterAnim != null) fighterAnim.SetFloat(HashMove, 0f);
    }

    /// <summary>
    /// Smooth coroutine returning the fighter back to home slot position moving BACKWARDS (facing the arena).
    /// </summary>
    public IEnumerator ReturnToOriginCoroutine()
    {
        isReturning = true;

        Vector3 startPos = transform.position;
        float totalDistance = Vector3.Distance(startPos, homePosition);

        if (totalDistance > 0.1f)
        {
            float travelTime = totalDistance / returnSpeed;
            float elapsedTime = 0f;

            if (fighterAnim != null) fighterAnim.SetFloat(HashMove, -6f);

            while (elapsedTime < travelTime)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / travelTime;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                transform.position = Vector3.Lerp(startPos, homePosition, smoothT);

                yield return null;
            }
        }

        transform.position = homePosition;

        while (Quaternion.Angle(transform.rotation, homeRotation) > 0.5f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, homeRotation, rotationSpeed * Time.deltaTime);
            yield return null;
        }

        transform.rotation = homeRotation;

        if (fighterAnim != null) fighterAnim.SetFloat(HashMove, 0f);

        isReturning = false;
    }

    #endregion

    #region Death Dissolve Logic

    public void OnDeathAnimationFinished()
    {
        if (stats != null && !stats.IsHero)
        {
            StartCoroutine(FFVIIDeathEffectCoroutine(1.2f));
        }
    }

    private IEnumerator FFVIIDeathEffectCoroutine(float duration)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Destroy(gameObject);
            yield break;
        }

        Material[] materials = new Material[renderers.Length];
        Color[] originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            materials[i] = renderers[i].material;

            if (materials[i].HasProperty("_BaseColor"))
            {
                originalColors[i] = materials[i].GetColor("_BaseColor");
            }
            else if (materials[i].HasProperty("_Color"))
            {
                originalColors[i] = materials[i].GetColor("_Color");
            }
            else
            {
                originalColors[i] = Color.white;
            }
        }

        float elapsedTime = 0f;
        float flashDuration = duration * 0.3f;
        float fadeDuration = duration * 0.7f;

        while (elapsedTime < flashDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / flashDuration;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;

                Color targetRed = Color.Lerp(originalColors[i], new Color(1f, 0.1f, 0.1f, originalColors[i].a), t);
                SetMaterialColor(materials[i], targetRed);
            }

            yield return null;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null) continue;
            EnableURPTransparency(materials[i]);
        }

        elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;

                Color currentColor = new Color(1f, 0.1f, 0.1f, alpha);
                SetMaterialColor(materials[i], currentColor);
            }

            yield return null;
        }

        gameObject.SetActive(false);
        Destroy(gameObject, 0.2f);
    }

    private void EnableURPTransparency(Material mat)
    {
        if (mat == null) return;

        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);
        }

        mat.SetFloat("_Blend", 0f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void SetMaterialColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }
    }

    #endregion

    #region Selection Pointer API & Cleanup

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

    public void OnBattleEnded()
    {
        NextTurnCD = 0;
        SetOffDefense();
        StopVictoryPose();

        SetBattleVisualsActive(false);
    }

    #endregion
}