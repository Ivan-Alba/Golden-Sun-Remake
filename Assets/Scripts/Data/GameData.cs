using JetBrains.Annotations;
using Palmmedia.ReportGenerator.Core.Reporting.Builders.Rendering;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class GameData
{
    //Scene
    public string currentSceneName;
    public float[] playerPosition = new float[3];
    public float[] playerRotation = new float[3];

    //Inventory
    public List<KeyValuePair<Item, int>> playerItems = new List<KeyValuePair<Item, int>>();

    //Party
    public int totalGold;
    public float totalTime;
    public List<string> party = new List<string>();
    public List<List<BaseStat>> heroStats = new List<List<BaseStat>>();
    public List<int[]> heroLevelAndXp = new List<int[]>();
    public List<int[]> heroCurrHpPp = new List<int[]>();
    public List<Equipable[]> heroEquippedGear = new List<Equipable[]>();
    public List<List<Psynergy>> heroPsynergy = new List<List<Psynergy>>();
    public List<KeyValuePair<string, Psynergy[]>> heroShortcuts = new List<KeyValuePair<string, Psynergy[]>>();

    //JSON CONSTRUCTOR
    public GameData()
    {

    }

    public GameData(Transform player)
    {

        //----SCENE DATA----
        currentSceneName = SceneManager.GetActiveScene().name;
        //position
        playerPosition[0] = player.position.x;
        playerPosition[1] = player.position.y;
        playerPosition[2] = player.position.z;
        //rotation
        playerRotation[0] = player.rotation.eulerAngles.x;
        playerRotation[1] = player.rotation.eulerAngles.y;
        playerRotation[2] = player.rotation.eulerAngles.z;

        //---INVENTORY DATA---

       playerItems = InventoryController.Instance.PlayerItems.ToList();

        //---GAME DATA----

        //total gold
        totalGold = GameManager.Instance.TotalGold;
        //total time played
        totalTime = GameManager.Instance.TotalPlayed;

        //---PARTY DATA----
        int i = 0;
        foreach(GameObject hero in GameManager.Instance.GetParty())
        {
            party.Add(hero.name);
            heroStats.Add(hero.GetComponent<FighterStats>().CharacterStats);
            //Level and XP
            int[] xpLevel = new int[3] { hero.GetComponent<FighterStats>().Level, hero.GetComponent<FighterStats>().Xp, hero.GetComponent<FighterStats>().NextLevelXp };
            heroLevelAndXp.Add(xpLevel);
            //Current HP and PP
            int[] currentPpHp = new int[2] { hero.GetComponent<FighterStats>().CurrHp, hero.GetComponent<FighterStats>().CurrPp };
            heroCurrHpPp.Add(currentPpHp);

            //Psynergy
            heroPsynergy.Add(hero.GetComponent<FighterStats>().Psynergies);
            /*foreach(Psynergy psy in heroPsynergy[i])
            {
                psy.Icon = null;
                psy.ElementIcon = null;
            }*/

            //Equipment
            heroEquippedGear.Add(hero.GetComponent<HeroEquipment>().EquippedItems);
        }

        //PsynergyShortcuts
        foreach(KeyValuePair<GameObject, Psynergy[]> heroShortcut in PsynergyWorldController.Instance.HeroShortcuts)
        {
            KeyValuePair<string, Psynergy[]> item = new KeyValuePair<string, Psynergy[]>(heroShortcut.Key.name, heroShortcut.Value);
            heroShortcuts.Add(item);
        }


    }


}
