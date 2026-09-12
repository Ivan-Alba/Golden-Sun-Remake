using System.Collections.Generic;
using UnityEngine;

#region Data Transfer Object (DTO)

/// <summary>
/// Data container holding the consolidated rewards earned upon completing a battle.
/// </summary>
public struct BattleRewardSummary
{
    public int TotalXpEarned { get; set; }
    public int TotalGoldEarned { get; set; }
    public int TotalPhEarned { get; set; }
    public IReadOnlyDictionary<ItemSO, int> ItemsObtained { get; set; }
}

#endregion

/// <summary>
/// Domain component responsible for tracking, calculating, and consolidating 
/// experience, gold, and item loot drops when enemies are defeated in combat.
/// </summary>
public class BattleRewardTracker : MonoBehaviour
{
    private int _totalXpEarned;
    private int _totalGoldEarned;
    private int _totalPhEarned;
    private readonly Dictionary<ItemSO, int> _totalItemsObtained = new Dictionary<ItemSO, int>();

    /// <summary>
    /// Resets all accumulated rewards to zero. Should be invoked at the start of a battle setup.
    /// </summary>
    public void ResetTracker()
    {
        _totalXpEarned = 0;
        _totalGoldEarned = 0;
        _totalPhEarned = 0;
        _totalItemsObtained.Clear();
    }

    /// <summary>
    /// Tracks an enemy defeat event, accumulating XP/Gold rewards and evaluating item drop rates.
    /// </summary>
    public void TrackEnemyDefeated(EnemyStats enemyStats)
    {
        if (enemyStats == null) return;

        // Accumulate currency and experience
        _totalXpEarned += enemyStats.XpReward;
        _totalGoldEarned += enemyStats.CoinsReward;
        _totalPhEarned += enemyStats.CoinsReward;

        // Process item loot table
        EvaluateItemLoot(enemyStats);
    }

    /// <summary>
    /// Evaluates probability rolls for the enemy's drop rates and adds items to the reward list.
    /// </summary>
    private void EvaluateItemLoot(EnemyStats enemyStats)
    {
        if (enemyStats.PossibleDrops == null || enemyStats.PossibleDrops.Count == 0) return;

        List<ItemDropRate> drops = enemyStats.PossibleDrops;

        foreach (ItemDropRate drop in drops)
        {
            if (string.IsNullOrEmpty(drop.itemSlug)) continue;

            // Roll percentage drop chance (dropChancePercentage ranges from 0.0f to 1.0f)
            float roll = Random.value;
            if (roll <= drop.dropChancePercentage)
            {
                ItemSO item = null;
                GameEvents.Database.RequestItemBySlug(drop.itemSlug, (foundItem) => item = foundItem);

                if (item != null)
                {
                    if (_totalItemsObtained.ContainsKey(item))
                    {
                        _totalItemsObtained[item] += 1;
                    }
                    else
                    {
                        _totalItemsObtained.Add(item, 1);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Consolidates all tracked rewards into an immutable summary object.
    /// </summary>
    public BattleRewardSummary GetRewardSummary()
    {
        return new BattleRewardSummary
        {
            TotalXpEarned = _totalXpEarned,
            TotalGoldEarned = _totalGoldEarned,
            TotalPhEarned = _totalPhEarned,
            ItemsObtained = new Dictionary<ItemSO, int>(_totalItemsObtained)
        };
    }
}