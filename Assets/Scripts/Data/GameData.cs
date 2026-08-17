using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

#region Inventory & Equipment DTOs

[System.Serializable]
public class InventorySlotData
{
    public string itemSlug;
    public int quantity;

    public InventorySlotData() { }

    public InventorySlotData(string slug, int qty)
    {
        itemSlug = slug;
        quantity = qty;
    }
}

[System.Serializable]
public class HeroInventoryData
{
    public string heroName;
    public List<InventorySlotData> slots = new List<InventorySlotData>();
}

[System.Serializable]
public class EquipmentSlotData
{
    public EquipmentSlot slotType;
    public string itemSlug;
}

[System.Serializable]
public class HeroEquipmentData
{
    public string heroName;
    public List<EquipmentSlotData> standardEquipment = new List<EquipmentSlotData>();
    public List<string> relicSlugs = new List<string>();
}

#endregion

[System.Serializable]
public class GameData
{
    // Scene Data
    public string currentSceneName;
    public float[] playerPosition = new float[3];
    public float[] playerRotation = new float[3];

    // Refactored Inventories & Equipment
    public List<InventorySlotData> globalBagItems = new List<InventorySlotData>();
    public List<HeroInventoryData> heroesInventories = new List<HeroInventoryData>();
    public List<HeroEquipmentData> heroesEquipment = new List<HeroEquipmentData>();

    // Party & Game Stats
    public int totalGold;
    public float totalTime;
    public List<string> party = new List<string>();
    public List<List<Stat>> heroStats = new List<List<Stat>>();
    public List<int[]> heroLevelAndXp = new List<int[]>();
    public List<int[]> heroCurrHpPp = new List<int[]>();

    // Psynergy Data (Stored as Slugs for Clean JSON Serialization)
    public List<List<string>> heroPsynergySlugs = new List<List<string>>();
    public List<KeyValuePair<string, string[]>> heroShortcuts = new List<KeyValuePair<string, string[]>>();

    // JSON CONSTRUCTOR
    public GameData()
    {

    }

    public GameData(Transform player)
    {
        // ---- SCENE DATA ----
        currentSceneName = SceneManager.GetActiveScene().name;

        // Position
        playerPosition[0] = player.position.x;
        playerPosition[1] = player.position.y;
        playerPosition[2] = player.position.z;

        // Rotation
        playerRotation[0] = player.rotation.eulerAngles.x;
        playerRotation[1] = player.rotation.eulerAngles.y;
        playerRotation[2] = player.rotation.eulerAngles.z;

        // ---- GLOBAL BAG DATA ----
        if (GlobalBag.Instance != null)
        {
            foreach (var slot in GlobalBag.Instance.KeyItemSlots)
            {
                if (!slot.IsEmpty)
                {
                    globalBagItems.Add(new InventorySlotData(slot.Item.ItemSlug, slot.Quantity));
                }
            }
        }

        // ---- GAME & PARTY DATA ----
        if (GameManager.Instance != null)
        {
            totalGold = GameManager.Instance.TotalGold;
            totalTime = GameManager.Instance.TotalPlayed;

            // ---- PARTY DATA ----
            foreach (GameObject hero in GameManager.Instance.GetParty())
            {
                if (hero == null) continue;

                party.Add(hero.name);

                if (hero.TryGetComponent<HeroStats>(out var heroStatsComponent))
                {
                    // Collect all Stat objects for this hero
                    List<Stat> currentHeroStats = new List<Stat>();
                    foreach (StatType statType in Enum.GetValues(typeof(StatType)))
                    {
                        Stat stat = heroStatsComponent.GetStat(statType);
                        if (stat != null)
                        {
                            currentHeroStats.Add(stat);
                        }
                    }
                    heroStats.Add(currentHeroStats);

                    // Level and XP from HeroStats
                    int[] xpLevel = new int[3] { heroStatsComponent.Level, heroStatsComponent.CurrentXp, heroStatsComponent.NextLevelXp };
                    heroLevelAndXp.Add(xpLevel);

                    // Current HP and PP from FighterStats/HeroStats
                    int[] currentPpHp = new int[2] { heroStatsComponent.CurrHp, heroStatsComponent.CurrPp };
                    heroCurrHpPp.Add(currentPpHp);

                    // Convert PsynergySO list to Slugs
                    List<string> psySlugs = heroStatsComponent.Psynergies?
                        .Where(p => p != null)
                        .Select(p => p.SlugName)
                        .ToList() ?? new List<string>();

                    heroPsynergySlugs.Add(psySlugs);
                }

                // Individual Backpack (HeroInventory)
                if (hero.TryGetComponent<HeroInventory>(out var heroInventory))
                {
                    HeroInventoryData invData = new HeroInventoryData { heroName = hero.name };

                    for (int i = 0; i < heroInventory.Slots.Count; i++)
                    {
                        var slot = heroInventory.Slots[i];
                        if (!slot.IsEmpty)
                        {
                            invData.slots.Add(new InventorySlotData(slot.Item.ItemSlug, slot.Quantity));
                        }
                        else
                        {
                            invData.slots.Add(new InventorySlotData(null, 0));
                        }
                    }
                    heroesInventories.Add(invData);
                }

                // Equipment (HeroEquipment)
                if (hero.TryGetComponent<HeroEquipment>(out var heroEquipment))
                {
                    HeroEquipmentData eqData = new HeroEquipmentData { heroName = hero.name };

                    foreach (var kvp in heroEquipment.EquippedItems)
                    {
                        if (kvp.Value != null)
                        {
                            eqData.standardEquipment.Add(new EquipmentSlotData
                            {
                                slotType = kvp.Key,
                                itemSlug = kvp.Value.ItemSlug
                            });
                        }
                    }

                    foreach (var relic in heroEquipment.EquippedRelics)
                    {
                        eqData.relicSlugs.Add(relic != null ? relic.ItemSlug : null);
                    }

                    heroesEquipment.Add(eqData);
                }
            }
        }

        // ---- PSYNERGY SHORTCUTS ----
        if (PsynergyWorldController.Instance != null && GameManager.Instance != null)
        {
            foreach (GameObject hero in GameManager.Instance.GetParty())
            {
                if (hero == null) continue;

                PsynergySO[] shortcuts = PsynergyWorldController.Instance.GetShortcuts(hero);
                if (shortcuts != null)
                {
                    string[] shortcutSlugs = shortcuts
                        .Select(p => p != null ? p.SlugName : null)
                        .ToArray();

                    KeyValuePair<string, string[]> item = new KeyValuePair<string, string[]>(hero.name, shortcutSlugs);
                    heroShortcuts.Add(item);
                }
            }
        }
    }
}