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
    public List<GameObject> party = new List<GameObject>();


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


        //---PARTY DATA----

        //total gold
        totalGold = GameManager.Instance.TotalGold;
        //total time played
        totalTime = GameManager.Instance.TotalPlayed;
        //party


        //----HERO DATA ---

        //stats




    }


}
