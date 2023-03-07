using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyDetector : MonoBehaviour
{
    GameObject battleManager;

    private void Awake()
    {
        battleManager = GameObject.Find("BattleManager").gameObject;
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Enemy"))
        {
            //Debug.Log("Contacto enemigo: " + other.gameObject.name);

            //BATTLE UI CONTROLLER
            battleManager.AddComponent<BattleUIController>();

            //TURN CONTROLLER
            battleManager.AddComponent<TurnController>();
            //BATTLECAMERA CONTROLLER
            //battleManager.AddComponent<BattleCameraController>();



            //TRANSICIÓN A BATALLA
            battleManager.AddComponent<BattleManager>();



            other.gameObject.GetComponent<Animator>().enabled = false;
            battleManager.GetComponent<BattleManager>().AwakeBatlle(other.gameObject, other.transform.parent.gameObject);
        }
    }
}
