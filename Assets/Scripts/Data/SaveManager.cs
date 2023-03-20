using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public static class SaveManager {

    
    public static void Save(Transform player)
    {
        GameData dataToSave = new GameData(player);
        string dataPath = Application.persistentDataPath + "/savedData.gs";
        FileStream fs = new FileStream(dataPath, FileMode.Create);
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(fs, dataToSave);
        fs.Close();
        Debug.Log("Datos guardados");

    }

    public static GameData Load()
    {
        string dataPath = Application.persistentDataPath + "/savedData.gs";
        if (File.Exists(dataPath))
        {
            FileStream fs = new FileStream(dataPath, FileMode.Open);
            BinaryFormatter bf = new BinaryFormatter();
            GameData sceneData = (GameData)bf.Deserialize(fs);
            fs.Close();
            Debug.Log("Datos cargados");
            return sceneData;
        } else
        {
            return null;
        }
    }

}
