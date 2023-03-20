using Palmmedia.ReportGenerator.Core.Reporting.Builders.Rendering;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class GameData
{
    public float[] playerPosition = new float[3];
    public float[] playerRotation = new float[3];
    public int totalGold;
    public float totalTime;
    public string currentSceneName;

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
