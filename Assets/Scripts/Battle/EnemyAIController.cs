using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

#region Enums & Data Structures

/// <summary>
/// Defines the activation condition evaluated by the Enemy AI before executing an action.
/// </summary>
public enum AIConditionType
{
    Always,               // Standard fallback or weighted random action
    SelfHpBelowPercent,   // Triggered when self HP falls below threshold (e.g. self-healing)
    AllyHpBelowPercent,   // Triggered when an ally's HP falls below threshold (e.g. party heal)
    TurnInterval          // Triggered periodically every X turns (TODO: implement turn counter)
}

/// <summary>
/// Serializable configuration container defining an enemy's action rule and its triggering criteria.
/// Configured directly in the Unity Inspector.
/// </summary>
[Serializable]
public class EnemyActionRule
{
    [Header("Action Definition")]
    public BattleCommandType CommandType = BattleCommandType.Attack;
    public PsynergySO Psynergy;
    public ItemSO Item;

    [Header("Trigger Conditions")]
    public AIConditionType Condition = AIConditionType.Always;

    [Range(0, 100)]
    [Tooltip("HP percentage threshold required to trigger this condition.")]
    public float HpThresholdPercent = 30f;

    [Range(1, 100)]
    [Tooltip("Selection weight during weighted random evaluation. Higher values increase chance of execution.")]
    public int SelectionWeight = 10;
}

/// <summary>
/// Renders consumable items finite per battle instance for enemy combatants.
/// </summary>
[Serializable]
public class EnemyItemStock
{
    public ItemSO Item;
    [Min(1)] public int MaxUses = 1;

    [HideInInspector] public int RemainingUses;

    public void Initialize()
    {
        RemainingUses = MaxUses;
    }
}

#endregion

/// <summary>
/// Domain component responsible for evaluating enemy tactical decisions during combat,
/// managing consumable item stock, selecting targets, and building complete BattleActionRequest payloads.
/// </summary>
public class EnemyAIController : MonoBehaviour
{
    [Header("AI Action Profile")]
    [Tooltip("List of tactical rules available to this enemy, evaluated in priority order.")]
    [SerializeField] private List<EnemyActionRule> actionRules = new List<EnemyActionRule>();

    [Header("Consumable Items Stock")]
    [Tooltip("Finite items available to this enemy during the current battle instance.")]
    [SerializeField] private List<EnemyItemStock> itemStock = new List<EnemyItemStock>();

    private FighterStats _myStats;
    private FighterController _myFighterController;

    private void Awake()
    {
        _myStats = GetComponent<FighterStats>();
        _myFighterController = GetComponent<FighterController>();

        // Initialize item stock counts for this combat instance
        foreach (var stock in itemStock)
        {
            stock?.Initialize();
        }
    }

    /// <summary>
    /// Evaluates current battlefield state and builds a complete BattleActionRequest for the enemy's turn.
    /// </summary>
    public BattleActionRequest BuildEnemyActionRequest(IReadOnlyList<GameObject> heroes, List<GameObject> enemies)
    {
        // 1. Filter action rules that satisfy current conditions
        List<EnemyActionRule> validRules = FilterValidRules(enemies);

        // 2. Select winning rule via weighted probability
        EnemyActionRule selectedRule = SelectRuleByWeight(validRules);

        // 3. Resolve targets and construct unified action payload
        return ConstructRequestFromRule(selectedRule, heroes, enemies);
    }

    #region AI Rule Evaluation Logic

    private List<EnemyActionRule> FilterValidRules(List<GameObject> enemies)
    {
        List<EnemyActionRule> valid = new List<EnemyActionRule>();

        // TODO: Check if character has a Silence/Seal Psynergy StatusEffect.
        // If sealed, skip all rules where CommandType == BattleCommandType.Psynergy.
        bool isPsynergySealed = false;

        foreach (var rule in actionRules)
        {
            if (rule == null) continue;

            // Validate Psynergy rules (Sealed status & PP Cost check)
            if (rule.CommandType == BattleCommandType.Psynergy)
            {
                if (isPsynergySealed) continue;

                if (rule.Psynergy == null) continue;

                // Check if the enemy has enough PP to cast this psynergy
                if (_myStats != null && _myStats.CurrPp < rule.Psynergy.PpCost) continue;
            }

            // Skip Item actions if stock is depleted
            if (rule.CommandType == BattleCommandType.Item)
            {
                if (rule.Item == null || !HasAvailableItemStock(rule.Item)) continue;
            }

            // Evaluate specific trigger condition
            switch (rule.Condition)
            {
                case AIConditionType.Always:
                    valid.Add(rule);
                    break;

                case AIConditionType.SelfHpBelowPercent:
                    if (_myStats != null && _myStats.MaxHp > 0)
                    {
                        float myHpPercent = ((float)_myStats.CurrHp / _myStats.MaxHp) * 100f;
                        if (myHpPercent <= rule.HpThresholdPercent) valid.Add(rule);
                    }
                    break;

                case AIConditionType.AllyHpBelowPercent:
                    if (HasLowHpAlly(enemies, rule.HpThresholdPercent)) valid.Add(rule);
                    break;
            }
        }

        // Fallback: If no conditions met or rules configured, default to basic attack
        if (valid.Count == 0)
        {
            valid.Add(new EnemyActionRule
            {
                CommandType = BattleCommandType.Attack,
                SelectionWeight = 1
            });
        }

        return valid;
    }

    private EnemyActionRule SelectRuleByWeight(List<EnemyActionRule> rules)
    {
        int totalWeight = 0;
        foreach (var r in rules) totalWeight += Mathf.Max(1, r.SelectionWeight);

        int randomRoll = Random.Range(0, totalWeight);
        int currentSum = 0;

        foreach (var r in rules)
        {
            currentSum += Mathf.Max(1, r.SelectionWeight);
            if (randomRoll < currentSum) return r;
        }

        return rules[0];
    }

    private BattleActionRequest ConstructRequestFromRule(
        EnemyActionRule rule,
        IReadOnlyList<GameObject> heroes,
        List<GameObject> enemies)
    {
        object payload = null;
        ActionTargetType targetType = ActionTargetType.SingleEnemy;
        int areaRange = 0;

        switch (rule.CommandType)
        {
            case BattleCommandType.Psynergy:
                if (rule.Psynergy != null)
                {
                    payload = rule.Psynergy;
                    targetType = rule.Psynergy.TargetType;
                    areaRange = rule.Psynergy.AreaRange;
                }
                break;

            case BattleCommandType.Item:
                if (rule.Item != null)
                {
                    payload = rule.Item;
                    ConsumeItemStock(rule.Item);
                    targetType = ActionTargetType.SingleAlly;
                    areaRange = 0;
                }
                break;

            case BattleCommandType.Attack:
            default:
                targetType = ActionTargetType.SingleEnemy;
                areaRange = 0;
                break;
        }

        var (primaryTarget, affectedTargets) = ResolveTargetsForAction(targetType, areaRange, heroes, enemies);

        return new BattleActionRequest
        {
            Caster = _myFighterController,
            CommandType = rule.CommandType,
            ActionPayload = payload,
            PrimaryTarget = primaryTarget,
            AffectedTargets = affectedTargets
        };
    }

    #endregion

    #region Target Selection Helpers

    /// <summary>
    /// Calculates primary and secondary affected targets matching TargetSelectionController's payload rules.
    /// Global actions (AllAllies / AllEnemies) strictly set primaryTarget to null.
    /// </summary>
    private (GameObject primaryTarget, List<GameObject> affectedTargets) ResolveTargetsForAction(
        ActionTargetType targetType,
        int areaRange,
        IReadOnlyList<GameObject> heroes,
        List<GameObject> enemies)
    {
        List<GameObject> affectedTargets = new List<GameObject>();
        GameObject primaryTarget = null;

        bool isAlly = IsAllyTargetType(targetType);

        if (isAlly)
        {
            switch (targetType)
            {
                case ActionTargetType.Self:
                    primaryTarget = gameObject;
                    if (primaryTarget != null) affectedTargets.Add(primaryTarget);
                    break;

                case ActionTargetType.SingleAlly:
                    primaryTarget = SelectAllyTarget(enemies);
                    if (primaryTarget != null) affectedTargets.Add(primaryTarget);
                    break;

                case ActionTargetType.AllAllies:
                    primaryTarget = null; // Global action: no single primary focus
                    if (enemies != null)
                    {
                        foreach (GameObject enemy in enemies)
                        {
                            if (enemy != null && enemy.TryGetComponent<FighterStats>(out var s) && s.CurrHp > 0)
                            {
                                affectedTargets.Add(enemy);
                            }
                        }
                    }
                    break;
            }
        }
        else
        {
            switch (targetType)
            {
                case ActionTargetType.SingleEnemy:
                    primaryTarget = SelectHeroTarget(heroes);
                    if (primaryTarget != null) affectedTargets.Add(primaryTarget);
                    break;

                case ActionTargetType.MultiEnemyArea:
                    primaryTarget = SelectHeroTarget(heroes);
                    if (primaryTarget != null && heroes != null)
                    {
                        affectedTargets.Add(primaryTarget);

                        if (areaRange >= 1)
                        {
                            int primaryIdx = -1;
                            for (int i = 0; i < heroes.Count; i++)
                            {
                                if (heroes[i] == primaryTarget)
                                {
                                    primaryIdx = i;
                                    break;
                                }
                            }

                            if (primaryIdx >= 0)
                            {
                                for (int offset = 1; offset <= areaRange; offset++)
                                {
                                    int rightIndex = primaryIdx + offset;
                                    int leftIndex = primaryIdx - offset;

                                    if (rightIndex < heroes.Count && heroes[rightIndex] != null)
                                    {
                                        GameObject rightTarget = heroes[rightIndex];
                                        if (rightTarget.TryGetComponent<FighterStats>(out var sRight) && sRight.CurrHp > 0)
                                            affectedTargets.Add(rightTarget);
                                    }

                                    if (leftIndex >= 0 && heroes[leftIndex] != null)
                                    {
                                        GameObject leftTarget = heroes[leftIndex];
                                        if (leftTarget.TryGetComponent<FighterStats>(out var sLeft) && sLeft.CurrHp > 0)
                                            affectedTargets.Add(leftTarget);
                                    }
                                }
                            }
                        }
                    }
                    break;

                case ActionTargetType.AllEnemies:
                    primaryTarget = null; // Global action: no single primary focus
                    if (heroes != null)
                    {
                        foreach (GameObject hero in heroes)
                        {
                            if (hero != null && hero.TryGetComponent<FighterStats>(out var s) && s.CurrHp > 0)
                            {
                                affectedTargets.Add(hero);
                            }
                        }
                    }
                    break;
            }
        }

        return (primaryTarget, affectedTargets);
    }

    /// <summary>
    /// Selects a random living hero target from active candidates.
    /// </summary>
    public GameObject SelectHeroTarget(IReadOnlyList<GameObject> heroes)
    {
        List<GameObject> validTargets = new List<GameObject>();

        if (heroes != null)
        {
            foreach (GameObject hero in heroes)
            {
                if (hero != null && hero.TryGetComponent<FighterStats>(out var stats) && stats.CurrHp > 0)
                {
                    validTargets.Add(hero);
                }
            }
        }

        if (validTargets.Count == 0) return null;
        return validTargets[Random.Range(0, validTargets.Count)];
    }

    /// <summary>
    /// Selects the allied enemy with the lowest HP percentage for healing/buffs.
    /// </summary>
    public GameObject SelectAllyTarget(List<GameObject> enemies)
    {
        GameObject lowestAlly = null;
        float lowestPercent = 100f;

        if (enemies != null)
        {
            foreach (GameObject enemy in enemies)
            {
                if (enemy == null) continue;

                if (enemy.TryGetComponent<FighterStats>(out var stats) && stats.CurrHp > 0)
                {
                    float hpPercent = ((float)stats.CurrHp / stats.MaxHp) * 100f;
                    if (hpPercent < lowestPercent)
                    {
                        lowestPercent = hpPercent;
                        lowestAlly = enemy;
                    }
                }
            }
        }

        return lowestAlly ?? gameObject;
    }

    private bool HasLowHpAlly(List<GameObject> enemies, float thresholdPercent)
    {
        if (enemies == null) return false;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null) continue;

            if (enemy.TryGetComponent<FighterStats>(out var stats) && stats.CurrHp > 0)
            {
                float hpPercent = ((float)stats.CurrHp / stats.MaxHp) * 100f;
                if (hpPercent <= thresholdPercent) return true;
            }
        }
        return false;
    }

    private bool IsAllyTargetType(ActionTargetType targetType)
    {
        return targetType == ActionTargetType.SingleAlly ||
               targetType == ActionTargetType.AllAllies ||
               targetType == ActionTargetType.Self;
    }

    #endregion

    #region Consumable Stock Management

    private bool HasAvailableItemStock(ItemSO item)
    {
        foreach (var stock in itemStock)
        {
            if (stock != null && stock.Item == item && stock.RemainingUses > 0)
            {
                return true;
            }
        }
        return false;
    }

    private void ConsumeItemStock(ItemSO item)
    {
        foreach (var stock in itemStock)
        {
            if (stock != null && stock.Item == item && stock.RemainingUses > 0)
            {
                stock.RemainingUses--;
                break;
            }
        }
    }

    #endregion
}