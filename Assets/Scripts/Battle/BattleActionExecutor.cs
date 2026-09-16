using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Domain executor component responsible for orchestrating the combat action sequence:
/// movement/animation triggers, damage and healing calculations, status effects,
/// VFX/SFX playback, and UI event notifications.
/// </summary>
public class BattleActionExecutor : MonoBehaviour
{
    #region Serialized & Local Dependencies

    [Header("Optional Dependencies")]
    [SerializeField] private BattleFXManager fxManager;

    private void Awake()
    {
        if (fxManager == null)
        {
            fxManager = GetComponent<BattleFXManager>() ?? GetComponentInParent<BattleFXManager>();
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
                caster.SetUpDefense();
                yield return new WaitForSeconds(1.2f);
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
    /// Executes a physical basic attack sequence with movement, weapon properties, damage calculation, and animations.
    /// </summary>
    private IEnumerator ExecuteBasicAttack(FighterController caster, GameObject target)
    {
        if (target == null || !target.TryGetComponent<FighterStats>(out var targetStats))
        {
            yield break;
        }

        FighterStats casterStats = caster.GetComponent<FighterStats>();
        if (casterStats == null) yield break;

        // 1. Move Caster to Target
        yield return caster.MoveToTargetCoroutine(target.transform.position, stopDistance: 2.5f);

        // 2. Resolve Weapon Properties & Critical Hit Probability
        ElementalType weaponElement = ElementalType.None;
        int critRateBonus = 1;

        if (caster.TryGetComponent<HeroEquipment>(out var heroEq))
        {
            WeaponSO weapon = heroEq.GetEquippedWeapon();
            if (weapon != null)
            {
                weaponElement = weapon.Element;
                critRateBonus = weapon.CriticalRateBonus;
            }
        }

        bool isCritical = BattleDamageCalculator.CheckCriticalHit(critRateBonus, casterStats.Luck);

        // 3. Play Attack Animation
        caster.PlayAttackAnimation();
        yield return new WaitForSeconds(0.4f);

        // 4. Calculate & Apply Physical Damage
        int damage = BattleDamageCalculator.CalculatePhysicalDamage(casterStats, targetStats, weaponElement, isCritical);

        if (target.TryGetComponent<FighterController>(out var targetController))
        {
            targetController.ReceiveDamage(damage, isCritical);
        }
        else
        {
            targetStats.CurrHp -= damage;
            string textDisplay = isCritical ? $"CRITICAL!\n{damage}" : damage.ToString();
            var textType = isCritical
                ? DamageNumbersPresenter.DamageTextType.CriticalDamage
                : DamageNumbersPresenter.DamageTextType.NormalDamage;

            GameEvents.Battle.TriggerDamageNumber(target, textDisplay, textType);
        }

        // 5. Play Hit SFX
        GameEvents.Audio.RequestPlaySFX(SFXType.Combat_AttackSwing);

        yield return new WaitForSeconds(0.7f);

        // 6. Return Caster to Start Position
        yield return caster.ReturnToOriginCoroutine();
    }

    /// <summary>
    /// Executes a Psynergy action supporting Attack, Heal, and Support Psynergy subtypes.
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

        // 1. Consume PP
        casterStats.CurrPp = Mathf.Max(0, casterStats.CurrPp - psynergy.PpCost);

        // 2. Play Cast Animation
        caster.PlayCastAnimation();

        GameEvents.Battle.RequestActionBanner(psynergy.name, psynergy.Description);

        // 3. Instantiate Elemental Circle Cast VFX
        GameObject castVfxPrefab = null;
        GameEvents.Database.RequestElementVFX(psynergy.Element, (vfx) => castVfxPrefab = vfx);

        GameObject castVfxInstance = null;
        if (castVfxPrefab != null)
        {
            castVfxInstance = Instantiate(castVfxPrefab, caster.transform);
        }

        yield return new WaitForSeconds(1.8f);

        if (castVfxInstance != null) Destroy(castVfxInstance, 2.0f);

        // 4. Instantiate Main Impact VFX
        GameObject impactVfxPrefab = psynergy.BattleFXConfig.MainActionVFX;
        GameObject[] impactVfxInstances = new GameObject[areaTargets.Count];

        if (primaryTargetPosition != null && impactVfxPrefab != null)
        {
            impactVfxInstances[0] = Instantiate(impactVfxPrefab, primaryTargetPosition.transform);
            GameEvents.Audio.RequestPlaySFXClip(psynergy.BattleFXConfig.DirectAudioClip);
        }
        else if (impactVfxPrefab != null)
        {
            for (int i = 0; i < areaTargets.Count; i++)
            {
                impactVfxInstances[i] = Instantiate(impactVfxPrefab, areaTargets[i].transform);
                GameEvents.Audio.RequestPlaySFXClip(psynergy.BattleFXConfig.DirectAudioClip);
                yield return new WaitForSeconds(0.2f);
            }
        }

        yield return new WaitForSeconds(1.5f);

        // 5. Process Psynergy Subtype Execution
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
                    targetStats.CurrHp -= damage;
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

        yield return new WaitForSeconds(0.8f);

        if (impactVfxInstances != null)
        {
            foreach (GameObject vfx in impactVfxInstances)
            {
                if (vfx != null) Destroy(vfx, 3.0f);
            }
        }
    }

    /// <summary>
    /// Executes Heal Psynergy restoring HP and displaying green numbers.
    /// </summary>
    private void ExecuteHealing(FighterStats casterStats, HealPsynergySO healPsynergy, List<GameObject> targets)
    {
        List<FighterStats> targetStatsList = new List<FighterStats>();

        foreach (GameObject targetObj in targets)
        {
            if (targetObj != null && targetObj.TryGetComponent<FighterStats>(out var targetStats))
            {
                targetStatsList.Add(targetStats);
            }
        }

        // Execute SO healing, revive, and cleanse logic
        healPsynergy.ExecuteBattleEffect(casterStats, targetStatsList);
    }

    private IEnumerator ExecuteItemAction(FighterController caster, ItemSO item, List<GameObject> targets)
    {
        // Item execution sequence stub
        yield return new WaitForSeconds(1.0f);
    }

    private IEnumerator ExecuteDjinnUnleashAction(FighterController caster, DjinnInstance djinn, List<GameObject> targets)
    {
        // Djinn unleash execution sequence stub
        yield return new WaitForSeconds(1.0f);
    }

    private IEnumerator ExecuteSummonAction(FighterController caster, object summonData, List<GameObject> targets)
    {
        // Summon execution sequence stub
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