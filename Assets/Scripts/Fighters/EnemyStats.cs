using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data structure defining the drop chance for a specific item reward upon defeating an enemy.
/// </summary>
[Serializable]
public struct ItemDropRate
{
    public string itemSlug;
    [Range(0f, 1f)] public float dropChancePercentage;
}

/// <summary>
/// Represents stats and reward data specifically for enemy units in combat encounters.
/// </summary>
public class EnemyStats : FighterStats
{
    #region Serialized Fields

    [Header("Enemy Rewards")]
    [SerializeField] private int xpReward = 50;
    [SerializeField] private int coinsReward = 20;

    [Header("Loot Configuration")]
    [SerializeField] private List<ItemDropRate> possibleDrops = new List<ItemDropRate>();

    #endregion

    #region Properties

    public override bool IsHero => false;
    public int XpReward => xpReward;
    public int CoinsReward => coinsReward;
    public List<ItemDropRate> PossibleDrops => possibleDrops;

    #endregion
}