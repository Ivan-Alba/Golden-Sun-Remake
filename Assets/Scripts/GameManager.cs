using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance = null;
    public const int CONFIRM_SOUND = 0;
    public const int MOVE_SOUND = 1;
    public const int NEGATIVE_SOUND = 2;
    public const int CURE_SOUND = 3;
    public const int ITEM_FOUNDED_SOUND = 4;

    private const KeyCode KEY_CONFIRM = KeyCode.E;
    private const KeyCode KEY_CANCEL = KeyCode.R;

    private bool inBattle = false;

    //PARTY INFO
    List<GameObject> party = new List<GameObject>();

    //GAME INFO
    int totalGold;
    float totalPlayed;

    //Getters and setters
    public int TotalGold { get => totalGold; set => totalGold = value; }
    public KeyCode KeyConfirm { get => KEY_CONFIRM; }
    public bool InBattle { get => inBattle; set => inBattle = value; }
    public float TotalPlayed { get => totalPlayed; set => totalPlayed = value; }

    public KeyCode KeyCancel => KEY_CANCEL;

    public List<GameObject> GetParty()
    {
        return party;
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }

        if (!party.Contains(GameObject.Find("Isaac")))
        {
            party.Add(GameObject.Find("Isaac"));
            party.Add(GameObject.Find("Garet"));

        }

        DontDestroyOnLoad(gameObject);

    }

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        TotalPlayed += Time.deltaTime;
    }


}
