using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ItemDropRate
{
    public string itemSlug;
    [Range(0f, 100f)] public float dropChancePercentage;
}

public class EnemyStats : FighterStats
{
    [Header("Enemy Rewards")]
    [SerializeField] private int xpReward = 50;
    [SerializeField] private int coinsReward = 20;

    [Header("Loot Configuration")]
    [SerializeField] private List<ItemDropRate> possibleDrops = new List<ItemDropRate>();

    public int XpReward => xpReward;
    public int CoinsReward => coinsReward;
    public List<ItemDropRate> PossibleDrops => possibleDrops;
}