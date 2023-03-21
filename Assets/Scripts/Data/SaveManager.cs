using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public static class SaveManager
{

    public static GameData loadedData = null;
    public static void Save(Transform player)
    {
        GameData dataToSave = new GameData(player);
        string dataPath = Application.persistentDataPath + "/savedData.json";

        string json = JsonConvert.SerializeObject(dataToSave, Formatting.Indented, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            TypeNameAssemblyFormat = FormatterAssemblyStyle.Full,

        });
        Debug.Log("Datos guardados");
        File.WriteAllText(dataPath, json);
    }


    public static GameData Load()
    {
        string dataPath = Application.persistentDataPath + "/savedData.json";
        if (File.Exists(dataPath))
        {
            string json = File.ReadAllText(dataPath);
            GameData gameData = JsonConvert.DeserializeObject<GameData>(json, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                TypeNameAssemblyFormat = FormatterAssemblyStyle.Full,
            });
            Debug.Log("Datos cargados");
            loadedData = gameData;
            return gameData;
        }
        else
        {
            return null;
        }
    }

}
