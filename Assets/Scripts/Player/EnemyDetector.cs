using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyDetector : MonoBehaviour
{
    GameObject battleManager;
    GameObject player;
    Collider enemyTouched;
    bool battleLaunched;
    [SerializeField] AudioClip AudioCollision;
    private void Awake()
    {
        battleManager = GameObject.Find("BattleManager").gameObject;
        player = GameObject.Find("Player").gameObject;
    }

    private void Update()
    {

    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Enemy") && !battleLaunched)
        {
            //Debug.Log("Contacto enemigo: " + other.gameObject.name);
            transform.GetComponent<AudioSource>().PlayOneShot(AudioCollision);
            other.gameObject.GetComponent<Ai>().CollissionWithPlayer();
            GameManager.instance.InBattle = true;
            player.transform.GetComponent<CharacterController>().SimpleMove(new Vector3(0,0,0));
            player.transform.LookAt(new Vector3(other.transform.position.x, transform.parent.position.y, other.transform.position.z));
            player.GetComponent<Animator>().SetTrigger("attacked");

            other.gameObject.GetComponent<Animator>().enabled = false;
            enemyTouched = other;

            StartCoroutine(StartBattle());
            battleLaunched = true;

        }
    }


    private IEnumerator StartBattle()
    {
        yield return new WaitForSeconds(0.3f);
        //BATTLE UI CONTROLLER
        battleManager.AddComponent<BattleUIController>();

        //TURN CONTROLLER
        battleManager.AddComponent<TurnController>();
        //BATTLECAMERA CONTROLLER
        //battleManager.AddComponent<BattleCameraController>();

        //TRANSICIÓN A BATALLA
        battleManager.AddComponent<BattleManager>();

        battleManager.GetComponent<BattleManager>().AwakeBatlle(enemyTouched.gameObject, enemyTouched.transform.parent.gameObject);
        battleLaunched = false;
    }
}
