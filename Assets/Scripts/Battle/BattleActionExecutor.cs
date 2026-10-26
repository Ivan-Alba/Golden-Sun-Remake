using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Domain executor component responsible for orchestrating combat action sequences:
/// physical movement, damage and healing calculations, status effects,
/// camera presentation triggers, and delegating visual and audio FX playback to BattleFXManager.
/// Fully synchronized with FighterAnimationEvents.
/// </summary>
public class BattleActionExecutor : MonoBehaviour
{
    #region Serialized & Local Dependencies

    [Header("Optional Dependencies")]
    [SerializeField] private BattleFXManager fxManager;
    [SerializeField] private BattleCameraController battleCameraController;

    private void Awake()
    {
        if (fxManager == null)
        {
            fxManager = GetComponent<BattleFXManager>() ?? GetComponentInParent<BattleFXManager>();
        }

        if (battleCameraController == null)
        {
            battleCameraController = GetComponent<BattleCameraController>() ?? GetComponentInParent<BattleCameraController>();
        }
    }

    #endregion

    #region Main Public Execution API

    /// <summary>
    /// Calculates and returns the CTB turn delay cost for a given action request.
    /// </summary>
    public int GetActionCost(BattleActionRequest request)
    {
        switch (request.CommandType)
        {
            case BattleCommandType.Attack: return 50;
            case BattleCommandType.Psynergy: return 60;
            case BattleCommandType.Defend: return 30;
            case BattleCommandType.Item: return 40;
            case BattleCommandType.Djinn: return 50;
            case BattleCommandType.Summon: return 60;
            default: return 50;
        }
    }

    /// <summary>
    /// Executes a selected battle action sequence using the unified BattleActionRequest DTO.
    /// </summary>
    public IEnumerator ExecuteActionSequence(BattleActionRequest request)
    {
        if (request.Caster == null)
        {
            yield break;
        }

        FighterController caster = request.Caster;
        List<GameObject> targetList = request.AffectedTargets ?? new List<GameObject>();
        GameObject primaryTarget = request.PrimaryTarget;

        switch (request.CommandType)
        {
            case BattleCommandType.Attack:
                if (targetList.Count > 0 && targetList[0] != null)
                {
                    yield return ExecuteBasicAttack(caster, targetList[0]);
                }
                break;

            case BattleCommandType.Psynergy:
                if (request.ActionPayload is PsynergySO psynergy)
                {
                    yield return ExecutePsynergyAction(caster, psynergy, primaryTarget, targetList);
                }
                break;

            case BattleCommandType.Defend:
                GameEvents.Battle.RequestActionBanner($"{caster.name} is defending.");
                caster.SetUpDefense();
                yield return new WaitForSeconds(1.0f);
                break;

            case BattleCommandType.Item:
                if (request.ActionPayload is ItemSO item)
                {
                    yield return ExecuteItemAction(caster, item, targetList);
                }
                break;

            case BattleCommandType.Djinn:
                if (request.ActionPayload is DjinnInstance djinn)
                {
                    yield return ExecuteDjinnUnleashAction(caster, djinn, targetList);
                }
                break;

            case BattleCommandType.Summon:
                yield return ExecuteSummonAction(caster, request.ActionPayload, targetList);
                break;
        }
    }

    #endregion

    #region Action Implementations

    /// <summary>
    /// Executes physical Melee basic attack sequence: smooth dash approach, camera setup,
    /// triggering physical attack animation, executing impact logic on FighterAnimationEvents callback,
    /// and returning home upon animation completion.
    /// </summary>
    private IEnumerator ExecuteBasicAttack(FighterController caster, GameObject target)
    {
        if (target == null || !target.TryGetComponent<FighterStats>(out var targetStats))
        {
            yield break;
        }

        FighterStats casterStats = caster.GetComponent<FighterStats>();
        if (casterStats == null) yield break;

        // 1. Resolve Weapon Properties & Critical Hit Probability
        ElementalType weaponElement = ElementalType.None;
        int critRateBonus = 1;
        HitFXType weaponHitFX = HitFXType.Blunt;

        if (caster.TryGetComponent<HeroEquipment>(out var heroEq))
        {
            WeaponSO weapon = heroEq.GetEquippedWeapon();
            if (weapon != null)
            {
                weaponElement = weapon.Element;
                critRateBonus = weapon.CriticalRateBonus;
                weaponHitFX = weapon.HitFXType;
            }
        }

        bool isCritical = BattleDamageCalculator.CheckCriticalHit(critRateBonus, casterStats.Luck);

        // 2. Move Caster smoothly to Target stopping position
        yield return caster.MoveToTargetCoroutine(target);

        // 3. Setup Impact Event Callback
        FighterAnimationEvents animEvents = caster.GetComponentInChildren<FighterAnimationEvents>();

        void HandleImpactFrame()
        {
            // Executed at the precise frame Mecanim fires OnHitImpact
            GameEvents.Audio.RequestPlaySFX(SFXType.Combat_AttackSwing);

            int damage = BattleDamageCalculator.CalculatePhysicalDamage(casterStats, targetStats, weaponElement, isCritical);

            if (isCritical)
            {
                // Dynamic Hit-Stop & Camera Trigger directly bound to impact frame
                if (battleCameraController != null)
                {
                    battleCameraController.SetCriticalImpactCamera(caster.gameObject, target);
                }
            }

            if (target.TryGetComponent<FighterController>(out var targetCtrl))
            {
                targetCtrl.ReceiveDamage(damage, isCritical);
            }
            else
            {
                targetStats.CurrHp = Mathf.Max(0, targetStats.CurrHp - damage);
                string textDisplay = isCritical ? $"CRITICAL!\n{damage}" : damage.ToString();
                var textType = isCritical
                    ? DamageNumbersPresenter.DamageTextType.CriticalDamage
                    : DamageNumbersPresenter.DamageTextType.NormalDamage;

                GameEvents.Battle.TriggerDamageNumber(target, textDisplay, textType);
            }

            if (fxManager != null)
            {
                fxManager.PlayBasicAttackHitFX(target, weaponHitFX);
            }
        }

        // 4. Subscribe to Event & Play Animation
        if (animEvents != null)
        {
            animEvents.OnHitImpact += HandleImpactFrame;
        }

        caster.PlayAttackAnimation();

        // 5. Wait until Animation completes naturally via OnAnimationCompleted event
        yield return new WaitUntil(() => caster.ActionFinished);

        // 6. Cleanup Event Subscription
        if (animEvents != null)
        {
            animEvents.OnHitImpact -= HandleImpactFrame;
        }

        // Reset camera if critical frame altered target perspective
        if (isCritical && battleCameraController != null)
        {
            battleCameraController.SetActionCamera(caster.gameObject, new List<GameObject> { target });
        }

        // 7. Return Caster smoothly back to slot home anchor
        yield return caster.ReturnToOriginCoroutine();
    }

    /// <summary>
    /// Executes a Psynergy action supporting Attack, Heal, and Support Psynergy subtypes.
    /// Delegates visual sequences, VFX, and SFX to BattleFXManager using BattleActionFXConfig.
    /// </summary>
    private IEnumerator ExecutePsynergyAction(
        FighterController caster,
        PsynergySO psynergy,
        GameObject primaryTargetPosition,
        List<GameObject> areaTargets)
    {
        if (psynergy == null) yield break;

        FighterStats casterStats = caster.GetComponent<FighterStats>();
        if (casterStats == null) yield break;

        // 1. Consume PP & Show Action Banner
        casterStats.CurrPp = Mathf.Max(0, casterStats.CurrPp - psynergy.PpCost);
        GameEvents.Battle.RequestActionBanner(psynergy.name, psynergy.Description);

        // 2. Execute FX Sequence & Apply Domain Logic on Impact Frame Callback
        if (fxManager != null)
        {
            yield return fxManager.ExecuteFXConfigSequence(
                psynergy.BattleFXConfig,
                caster,
                primaryTargetPosition,
                areaTargets,
                onImpactFrame: () =>
                {
                    ApplyPsynergyDomainEffects(casterStats, psynergy, primaryTargetPosition, areaTargets);
                }
            );
        }
        else
        {
            // Fallback timing if fxManager is missing in scene
            caster.PlayCasterAnimation(CasterAnimationType.UsePsynergy);
            yield return new WaitUntil(() => caster.ActionFinished);
            ApplyPsynergyDomainEffects(casterStats, psynergy, primaryTargetPosition, areaTargets);
        }

        yield return new WaitForSeconds(0.3f);
    }

    /// <summary>
    /// Evaluates and applies the mathematical/domain impact of a Psynergy SO (Attack, Heal, Support).
    /// </summary>
    private void ApplyPsynergyDomainEffects(
        FighterStats casterStats,
        PsynergySO psynergy,
        GameObject primaryTargetPosition,
        List<GameObject> areaTargets)
    {
        if (psynergy is AttackPsynergySO attackPsynergy)
        {
            GameObject centerTarget = primaryTargetPosition ?? (areaTargets != null && areaTargets.Count > 0 ? areaTargets[0] : null);

            for (int i = 0; i < areaTargets.Count; i++)
            {
                GameObject targetObj = areaTargets[i];
                if (targetObj == null || !targetObj.TryGetComponent<FighterStats>(out var targetStats)) continue;

                int distanceIndex = (targetObj == centerTarget) ? 0 : Mathf.Abs(areaTargets.IndexOf(centerTarget) - i);
                if (distanceIndex < 0) distanceIndex = 1;

                int damage = BattleDamageCalculator.CalculateAttackPsynergyDamage(casterStats, targetStats, attackPsynergy, distanceIndex);

                if (targetObj.TryGetComponent<FighterController>(out var targetController))
                {
                    targetController.ReceiveDamage(damage);
                }
                else
                {
                    targetStats.CurrHp = Mathf.Max(0, targetStats.CurrHp - damage);
                    GameEvents.Battle.TriggerDamageNumber(
                        targetObj,
                        damage.ToString(),
                        DamageNumbersPresenter.DamageTextType.NormalDamage
                    );
                }
            }
        }
        else if (psynergy is HealPsynergySO healPsynergy)
        {
            ExecuteHealing(casterStats, healPsynergy, areaTargets);
        }
        else if (psynergy is SupportPsynergySO supportPsynergy)
        {
            List<FighterStats> targetStatsList = ExtractFighterStatsList(areaTargets);
            supportPsynergy.ExecuteBattleEffect(casterStats, targetStatsList);
        }
    }

    /// <summary>
    /// Executes Heal Psynergy restoring HP and displaying green numbers.
    /// </summary>
    private void ExecuteHealing(FighterStats casterStats, HealPsynergySO healPsynergy, List<GameObject> targets)
    {
        List<FighterStats> targetStatsList = ExtractFighterStatsList(targets);
        healPsynergy.ExecuteBattleEffect(casterStats, targetStatsList);
    }

    private IEnumerator ExecuteItemAction(FighterController caster, ItemSO item, List<GameObject> targets)
    {
        if (caster != null)
        {
            caster.PlayCasterAnimation(CasterAnimationType.UseItem);
            yield return new WaitUntil(() => caster.ActionFinished);
        }
    }

    private IEnumerator ExecuteDjinnUnleashAction(FighterController caster, DjinnInstance djinn, List<GameObject> targets)
    {
        if (caster != null)
        {
            caster.PlayCasterAnimation(CasterAnimationType.UseDjinn);
            yield return new WaitUntil(() => caster.ActionFinished);
        }
    }

    private IEnumerator ExecuteSummonAction(FighterController caster, object summonData, List<GameObject> targets)
    {
        if (caster != null)
        {
            caster.PlayCasterAnimation(CasterAnimationType.UseSummon);
            yield return new WaitUntil(() => caster.ActionFinished);
        }
    }

    #endregion

    #region Helper Utility Methods

    private List<FighterStats> ExtractFighterStatsList(List<GameObject> gameObjects)
    {
        List<FighterStats> statsList = new List<FighterStats>();
        if (gameObjects == null) return statsList;

        foreach (GameObject obj in gameObjects)
        {
            if (obj != null && obj.TryGetComponent<FighterStats>(out var stats))
            {
                statsList.Add(stats);
            }
        }

        return statsList;
    }

    #endregion
}