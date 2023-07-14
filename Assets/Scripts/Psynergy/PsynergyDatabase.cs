using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PsynergyDatabase : MonoBehaviour
{
    public static PsynergyDatabase Instance { get; set; }
    private List<Psynergy> psynergyList { get; set; }
    
    GameObject marsElementCircle;
    GameObject jupiterElementCircle;
    GameObject mercuryElementCircle;
    GameObject venusElementCircle;
    GameObject noElementalCircle;

    Dictionary<string, GameObject> psynergyBattleAnim = new Dictionary<string, GameObject>();


    public GameObject MarsElementCircle { get => marsElementCircle; set => marsElementCircle = value; }
    public GameObject JupiterElementCircle { get => jupiterElementCircle; set => jupiterElementCircle = value; }
    public GameObject MercuryElementCircle { get => mercuryElementCircle; set => mercuryElementCircle = value; }
    public GameObject VenusElementCircle { get => venusElementCircle; set => venusElementCircle = value; }
    public GameObject NoElementalCircle { get => noElementalCircle; set => noElementalCircle = value; }
    public Dictionary<string, GameObject> PsynergyBattleAnim { get => psynergyBattleAnim; set => psynergyBattleAnim = value; }


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
            LoadElementAnimations();
        }
    }


    private void BuildDatabase()
    {
        JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
        psynergyList = JsonConvert.DeserializeObject<List<Psynergy>>(Resources.Load("JSON/Psynergies").ToString(), settings);
        psynergyList.ForEach(x => x.SetElementIcon());
        psynergyList.ForEach(x => PsynergyBattleAnim.Add(x.SlugName, Resources.Load<GameObject>($"Prefabs/PsynergyVFX/{x.SlugName}")));
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

    private void LoadElementAnimations()
    {
        MarsElementCircle = Resources.Load<GameObject>("Prefabs/PsynergyVFX/mars");
        JupiterElementCircle = Resources.Load<GameObject>("Prefabs/PsynergyVFX/jupiter");
        MercuryElementCircle = Resources.Load<GameObject>("Prefabs/PsynergyVFX/mercury");
        VenusElementCircle = Resources.Load<GameObject>("Prefabs/PsynergyVFX/venus");
        NoElementalCircle = Resources.Load<GameObject>("Prefabs/PsynergyVFX/noElemental");
    }

    public GameObject getElementAnimation(Psynergy psynergy)
    {
        switch (psynergy.Element.ToString())
        {
            case "Venus":
                return venusElementCircle;
            case "Mars":
                return marsElementCircle;
            case "Jupiter":
                return jupiterElementCircle;
            case "Mercury":
                return mercuryElementCircle;
            default:
                return NoElementalCircle;
        }
    }
}

