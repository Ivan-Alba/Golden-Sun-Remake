using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class FighterController : MonoBehaviour
{
    bool goingToTarget;
    bool comingBack;
    bool returnToStartRotation;
    bool isDefending;
    bool actionFinished;
    //FLOATING TEXT
    GameObject floatingTextPrefab;
    Sounds sounds;
    Animator fighterAnim;
    Quaternion startRotation;
    Vector3 startPosition;
    Transform target;
    FighterStats stats;
    FighterController fighterController;

    int nextTurnCD;

    public bool ActionFinished { get => actionFinished; set => actionFinished = value; }
    public int NextTurnCD { get => nextTurnCD; set => nextTurnCD = value; }

    private void Awake()
    {
        fighterAnim = GetComponentInChildren<Animator>();
        sounds = GetComponent<Sounds>();
        stats = gameObject.GetComponent<FighterStats>();
        fighterController = gameObject.GetComponent<FighterController>();
        floatingTextPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FloatingText.prefab");
    }

    // Update is called once per frame
    void Update()
    {
        if (goingToTarget)
        {

            //WHILE FAR
            if (Vector3.Distance(transform.position, target.position) > 3)
            {
                float speed = 2;

                //TODO LERP OR SMOOTH DAMP?
                Vector3 velocity = new Vector3();
                transform.LookAt(new Vector3(target.transform.position.x, transform.position.y, target.transform.position.z));
                transform.position = Vector3.SmoothDamp(transform.position, new Vector3(target.position.x, transform.position.y, target.position.z), ref velocity, speed * Time.deltaTime);
                fighterAnim.SetFloat("Move", speed);
            }
            else //WHEN CLOSE
            {
                goingToTarget = false;
                fighterAnim.SetFloat("Move", 0);
            }
        }

        //Return to start position before attack
        if (comingBack)
        {
            if (Vector3.Distance(transform.position, startPosition) < 0.1)
            {
                comingBack = false;
                fighterAnim.SetFloat("Move", 0);
                transform.position = startPosition;
                returnToStartRotation = true;
            }
            else
            {
                float speed = 3;
                fighterAnim.SetFloat("Move", -speed);
                transform.position = Vector3.Lerp(transform.position, startPosition, speed * Time.deltaTime);
            }
        }

        if (returnToStartRotation)
        {
            // Smoothly rotate towards the target point.
            transform.rotation = Quaternion.Slerp(transform.rotation, startRotation, 4 * Time.deltaTime);
            if (transform.rotation == startRotation)
            {
                returnToStartRotation = false;
            }
        }


    }

    public void StartsTurn()
    {
        if (this.isDefending)
        {
            SetOffDefense();
        }
    }

    public void GoToTarget(Transform target)
    {
        this.target = target;
        startPosition = transform.position;
        startRotation = transform.rotation;
        goingToTarget = true;
    }

    public IEnumerator BasicAttack(GameObject target)
    {
        int attackCD = 50;

        FighterController targetController = target.GetComponent<FighterController>();
        FighterController performerController = gameObject.GetComponent<FighterController>();

        FighterStats targetStats = target.GetComponent<FighterStats>();
        FighterStats performerStats = gameObject.GetComponent<FighterStats>();

        performerController.NextTurnCD += attackCD - performerStats.Agility / 2;

        //WAIT FOR ANIMATION PREPARE ATTACK AND TURN CAMERA HIDE
        //yield return new WaitForSeconds(0.5f);

        //GOING TO TARGET
        GoToTarget(target.transform);
        while (goingToTarget)
        {
            yield return null;
        }

        //ATTACK ANIMATION
        fighterAnim.SetTrigger("attack");
        sounds.PlaySound(1);
        yield return new WaitForSeconds(0.5f);//simulamos tiempo de animacion

        //DO PHYSICAL DAMAGE
        int damage = ((stats.Strenght - targetStats.Defense / 2) * ((20 + stats.Strenght) / 20));
        if (targetController.isDefending) damage = damage / 2;
        damage = damage + (int)Random.Range(damage * 0.3f, damage * 0.5f);


        //DOING DAMAGE
        targetController.ReceiveDamage(damage);
        yield return new WaitForSeconds(0.5f);

        //WAIT ANIMATION CALLS AttackDone()
        while (comingBack || transform.position != startPosition)
        {
            yield return null;
        }

        ActionFinished = true;


    }

    public void ReceiveDamage(int damage)
    {
        fighterAnim.SetTrigger("receiveHit");
        sounds.PlaySound(2);
        stats.CurrHp -= damage;

        GameObject text;
        Vector3 offset;
        if (gameObject.CompareTag("Hero"))
        {
            offset = new Vector3(0, 0, 0.7f);
            text = Instantiate(floatingTextPrefab, transform.position + offset, Quaternion.identity);

        }
        else
        { //TODO Offset en base al collider
            offset = new Vector3(0, gameObject.GetComponent<BoxCollider>().bounds.size.y / 2, 0.2f);
            text = Instantiate(floatingTextPrefab, transform.position + offset, Quaternion.identity);

        }
        text.GetComponent<TextMeshPro>().SetText(damage.ToString());

        if (stats.CurrHp <= 0)
        {
            fighterAnim.SetTrigger("death");
        }
    }

    public void SetUpDefense()
    {
        fighterAnim.SetBool("defense", true);
        this.isDefending = true;
        fighterController.NextTurnCD += 50;
    }

    public void SetOffDefense()
    {
        this.isDefending = false;
        fighterAnim.SetBool("defense", false);
    }

    public GameObject SelectRandomEnemy(List<GameObject> fighters)
    {
        GameObject targetSelected = null;
        if (gameObject.CompareTag("Hero"))
        {
            //TODO IMPLEMENTAR ESTADO "LOCURA"?
        } else
        {
            while (targetSelected == null || targetSelected.GetComponent<FighterStats>().CurrHp <= 0)
            {
                targetSelected = fighters[Random.Range(0, fighters.Count)];
            }

        }
        return targetSelected;
    }

    public void AttackDone() //CALLED BY ANIMATIONS
    {
        //COME BACK TO START POSITION WHEN ANIMATION DONE
        comingBack = true;
    }

}
