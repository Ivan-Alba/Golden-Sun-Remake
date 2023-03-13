using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PsynergyDatabase : MonoBehaviour
{
    public static PsynergyDatabase Instance { get; set; }
    private List<Psynergy> psynergyList { get; set; }


    // Start is called before the first frame update
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            BuildDatabase();
        }
    }


    private void BuildDatabase()
    {
        JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
        psynergyList = JsonConvert.DeserializeObject<List<Psynergy>>(Resources.Load("JSON/Psynergies").ToString(), settings);
        //psynergyList.ForEach(x => Debug.Log(x.ToString()));
        psynergyList.ForEach(x => x.SetElementIcon());
    }

    public Psynergy GetPsynergy(string psynergySlug)
    {
        foreach (Psynergy psynergy in psynergyList)
        {
            if (psynergy.SlugName == psynergySlug) return psynergy;
        }

        Debug.LogWarning("No se encuentra la psinergía: " + psynergySlug);
        return null;
    }
}

