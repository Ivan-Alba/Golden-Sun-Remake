using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

#region Data Transfer Objects (DTOs)

/// <summary>
/// Details about a specific item dropped by an enemy in battle, 
/// including quantity dropped, overflow amount, source enemy, and recipient hero.
/// </summary>
public struct ObtainedItemEntry
{
    public ItemSO Item { get; set; }
    public int QuantityDropped { get; set; }
    public int QuantityOverflow { get; set; }
    public string DroppedByEnemyName { get; set; }
    public string RecipientHeroName { get; set; }

    /// <summary>
    /// True if all dropped units fit into inventory without overflow.
    /// </summary>
    public bool AddedFully => QuantityOverflow == 0;

    /// <summary>
    /// True if at least 1 unit was successfully stored in inventory.
    /// </summary>
    public bool AddedPartialOrFull => QuantityOverflow < QuantityDropped;
}

/// <summary>
/// Data container holding the consolidated rewards earned upon completing a battle.
/// </summary>
public struct BattleRewardSummary
{
    public int TotalXpEarned { get; set; }
    public int TotalGoldEarned { get; set; }
    public int TotalPhEarned { get; set; }
    public IReadOnlyList<ObtainedItemEntry> ItemsObtained { get; set; }
}

#endregion

/// <summary>
/// Domain component responsible for tracking, calculating, consolidating, 
/// and applying experience, gold, and item loot drops when enemies are defeated in combat.
/// </summary>
public class BattleRewardTracker : MonoBehaviour
{
    private int _totalXpEarned;
    private int _totalGoldEarned;
    private int _totalPhEarned;

    // Internal list preserving individual drops and enemy source attribution
    private readonly List<ObtainedItemEntry> _trackedLootDrops = new List<ObtainedItemEntry>();

    /// <summary>
    /// Resets all accumulated rewards to zero. Should be invoked at the start of a battle setup.
    /// </summary>
    public void ResetTracker()
    {
        _totalXpEarned = 0;
        _totalGoldEarned = 0;
        _totalPhEarned = 0;
        _trackedLootDrops.Clear();
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
    /// Evaluates probability rolls for the enemy's drop rates and adds items to the pending loot list.
    /// </summary>
    private void EvaluateItemLoot(EnemyStats enemyStats)
    {
        if (enemyStats.PossibleDrops == null || enemyStats.PossibleDrops.Count == 0) return;

        List<ItemDropRate> drops = enemyStats.PossibleDrops;
        string enemyName = !string.IsNullOrEmpty(enemyStats.Name) ? enemyStats.Name : enemyStats.gameObject.name;

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
                    _trackedLootDrops.Add(new ObtainedItemEntry
                    {
                        Item = item,
                        QuantityDropped = 1, // Standard single drop per enemy entry
                        QuantityOverflow = 1,
                        DroppedByEnemyName = enemyName,
                        RecipientHeroName = string.Empty
                    });
                }
            }
        }
    }

    /// <summary>
    /// Applies non-XP rewards to the global game state (Gold & Inventory) and returns the final immutable summary.
    /// </summary>
    public BattleRewardSummary ApplyAndGetRewardSummary()
    {
        // 1. Grant Global Gold
        if (_totalGoldEarned > 0)
        {
            GameEvents.Party.RequestModifyGold(_totalGoldEarned);
        }

        // 2. Process Item Drops into Inventory
        List<ObtainedItemEntry> processedLoot = new List<ObtainedItemEntry>();

        for (int i = 0; i < _trackedLootDrops.Count; i++)
        {
            ObtainedItemEntry entry = _trackedLootDrops[i];

            int initialQuantity = entry.QuantityDropped > 0 ? entry.QuantityDropped : 1;
            int remainingOverflow = initialQuantity;
            string recipient = string.Empty;

            // Request adding item to party inventory
            GameEvents.Inventory.RequestAddItem(entry.Item, initialQuantity, (overflow, recipientHeroName) =>
            {
                remainingOverflow = overflow;
                recipient = recipientHeroName;
            });

            entry.QuantityDropped = initialQuantity;
            entry.QuantityOverflow = remainingOverflow;
            entry.RecipientHeroName = recipient;

            processedLoot.Add(entry);
        }

        return new BattleRewardSummary
        {
            TotalXpEarned = _totalXpEarned,
            TotalGoldEarned = _totalGoldEarned,
            TotalPhEarned = _totalPhEarned,
            ItemsObtained = processedLoot
        };
    }
}