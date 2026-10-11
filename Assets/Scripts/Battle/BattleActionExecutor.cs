using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Domain executor component responsible for orchestrating combat action sequences:
/// physical movement, damage and healing calculations, status effects,
/// camera presentation triggers, and delegating visual and audio FX playback to BattleFXManager.
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

    private IEnumerator ExecuteBasicAttack(FighterController caster, GameObject target)
    {
        if (target == null || !target.TryGetComponent<FighterStats>(out var targetStats))
        {
            yield break;
        }

        FighterStats casterStats = caster.GetComponent<FighterStats>();
        if (casterStats == null) yield break;

        // -------------------------------------------------------------------------
        // TODO: WEAPON UNLEASH EVALUATION (PRE-MOVEMENT)
        // 1. Resolve weapon and check if a Weapon Unleash triggers (Hero only).
        // 2. If Unleash triggers:
        //    a. Switch camera to Hero Focus / Unleash Intro Camera.
        //    b. Play Unleash Aura VFX & SFX while stationary.
        //    c. Execute specialized Unleash Coroutine instead of Basic Attack flow.
        // -------------------------------------------------------------------------

        // 1. Resolve Weapon Properties & Critical Hit Probability EARLY
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

        // 2. Move Caster to Target
        yield return caster.MoveToTargetCoroutine(target.transform.position, stopDistance: 2.5f);

        // 3. Cinematic Camera & Animation Setup (POST-MOVEMENT)
        if (isCritical)
        {
            // Activate dramatic close-up camera for Critical Hit
            if (battleCameraController != null)
            {
                battleCameraController.SetCriticalImpactCamera(caster.gameObject, target);
            }

            // Optional: Trigger specific critical attack animation if available in controller
            caster.PlayAttackAnimation(); // e.g., caster.PlayCriticalAttackAnimation();
        }
        else
        {
            caster.PlayAttackAnimation();
        }


        // 4. Calculate & Apply Physical Damage (with Hit-Stop / Slow-Motion for Criticals)
        if (isCritical)
        {
            Time.timeScale = 0.3f; // Dramatic Slow-Motion frame rate punch
            yield return new WaitForSecondsRealtime(1.5f); // Realtime wait unaffected by timeScale
            Time.timeScale = 0.1f;
            yield return new WaitForSecondsRealtime(0.4f); // Realtime wait unaffected by timeScale
            Time.timeScale = 1.0f;
            List<GameObject> singleTargetList = new List<GameObject> { target };
            battleCameraController.SetActionCamera(caster.gameObject, singleTargetList);
        }
        else
        {
            // Wait for animation impact frame
            yield return new WaitForSeconds(0.5f);
        }

        GameEvents.Audio.RequestPlaySFX(SFXType.Combat_AttackSwing);

        int damage = BattleDamageCalculator.CalculatePhysicalDamage(casterStats, targetStats, weaponElement, isCritical);

        if (target.TryGetComponent<FighterController>(out var targetController))
        {
            targetController.ReceiveDamage(damage, isCritical);
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

        // 5. Trigger Visual & Audio Hit FX
        if (fxManager != null)
        {
            fxManager.PlayBasicAttackHitFX(target, weaponHitFX);
        }

        yield return new WaitForSeconds(0.5f);

        // 6. Return Caster to Start Position
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
            yield return new WaitForSeconds(1.0f);
            ApplyPsynergyDomainEffects(casterStats, psynergy, primaryTargetPosition, areaTargets);
        }

        yield return new WaitForSeconds(0.5f);
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
        // Safe stub: triggers item preparation animation and passes turn safely
        if (caster != null)
        {
            caster.PlayCasterAnimation(CasterAnimationType.UseItem);
        }
        yield return new WaitForSeconds(1.0f);
    }

    private IEnumerator ExecuteDjinnUnleashAction(FighterController caster, DjinnInstance djinn, List<GameObject> targets)
    {
        // Safe stub: triggers Djinn unleash animation and passes turn safely
        if (caster != null)
        {
            caster.PlayCasterAnimation(CasterAnimationType.UseDjinn);
        }
        yield return new WaitForSeconds(1.0f);
    }

    private IEnumerator ExecuteSummonAction(FighterController caster, object summonData, List<GameObject> targets)
    {
        // Safe stub: triggers Summon animation and passes turn safely
        if (caster != null)
        {
            caster.PlayCasterAnimation(CasterAnimationType.UseSummon);
        }
        yield return new WaitForSeconds(1.0f);
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