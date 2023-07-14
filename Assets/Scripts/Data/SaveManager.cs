using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public static class SaveManager
{

    public static GameData[] loadedData = null;
    public static void Save(int fileIndex)
    {
        Transform player = GameObject.FindGameObjectWithTag("Player").transform;

        GameData dataToSave = new GameData(player);
        string dataPath = Application.persistentDataPath + $"/savedData{fileIndex}.json";

        string json = JsonConvert.SerializeObject(dataToSave, Formatting.Indented, new JsonSerializerSettings
        {
           //TypeNameHandling = TypeNameHandling.All,
            //TypeNameAssemblyFormat = FormatterAssemblyStyle.Full,

        });
        Debug.Log("Datos guardados");
        File.WriteAllText(dataPath, json);
    }


    public static GameData Load(int fileIndex)
    {
        string dataPath = Application.persistentDataPath + $"/savedData{fileIndex}.json";
        if (File.Exists(dataPath))
        {
            string json = File.ReadAllText(dataPath);
            GameData gameData = JsonConvert.DeserializeObject<GameData>(json, new JsonSerializerSettings
            {
                //TypeNameHandling = TypeNameHandling.All,
                //TypeNameAssemblyFormat = FormatterAssemblyStyle.Full,
            });
            Debug.Log("Datos cargados");
            loadedData = new GameData[1];
            loadedData[0] = gameData;
            return gameData;
        }
        else
        {
            return null;
        }
    }

    public static int CountFiles()
    {
        int fileCount = 0;
        while (File.Exists(Application.persistentDataPath + $"/savedData{fileCount}.json"))
        {
            fileCount++;
        }

        return fileCount;
    }
        

}
