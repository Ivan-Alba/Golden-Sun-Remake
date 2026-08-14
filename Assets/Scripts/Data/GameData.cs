using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class GameData
{
    // Scene
    public string currentSceneName;
    public float[] playerPosition = new float[3];
    public float[] playerRotation = new float[3];

    // Inventory
    public List<KeyValuePair<Item, int>> playerItems = new List<KeyValuePair<Item, int>>();

    // Party
    public int totalGold;
    public float totalTime;
    public List<string> party = new List<string>();
    public List<List<Stat>> heroStats = new List<List<Stat>>();
    public List<int[]> heroLevelAndXp = new List<int[]>();
    public List<int[]> heroCurrHpPp = new List<int[]>();
    public List<Equipable[]> heroEquippedGear = new List<Equipable[]>();
    public List<List<Psynergy>> heroPsynergy = new List<List<Psynergy>>();
    public List<KeyValuePair<string, Psynergy[]>> heroShortcuts = new List<KeyValuePair<string, Psynergy[]>>();

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

        // ---- INVENTORY DATA ----
        if (InventoryController.Instance != null && InventoryController.Instance.PlayerItems != null)
        {
            playerItems = InventoryController.Instance.PlayerItems.ToList();
        }

        // ---- GAME DATA ----
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

                    // Psynergy
                    heroPsynergy.Add(heroStatsComponent.Psynergies);
                }

                // Equipment
                if (hero.TryGetComponent<HeroEquipment>(out var equipmentComponent))
                {
                    heroEquippedGear.Add(equipmentComponent.EquippedItems);
                }
            }
        }

        // PsynergyShortcuts
        if (PsynergyWorldController.Instance != null && PsynergyWorldController.Instance.HeroShortcuts != null)
        {
            foreach (KeyValuePair<GameObject, Psynergy[]> heroShortcut in PsynergyWorldController.Instance.HeroShortcuts)
            {
                if (heroShortcut.Key != null)
                {
                    KeyValuePair<string, Psynergy[]> item = new KeyValuePair<string, Psynergy[]>(heroShortcut.Key.name, heroShortcut.Value);
                    heroShortcuts.Add(item);
                }
            }
        }
    }
}