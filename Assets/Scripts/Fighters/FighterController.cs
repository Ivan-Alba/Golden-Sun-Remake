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
        floatingTextPrefab = Resources.Load("Prefabs/UIComponents/FloatingText") as GameObject;
    }

    // Update is called once per frame
    void Update()
    {
        if (goingToTarget)
        {

            //WHILE FAR
            if (Vector3.Distance(transform.position, target.position) > 3)
            {
                float speed = 6f;

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
                float speed = 8.5f;
                fighterAnim.SetFloat("Move", -speed);
                transform.position = Vector3.MoveTowards(transform.position, startPosition, speed * Time.deltaTime);
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

        //CALCULATE DAMAGE
        int damage;
        if (gameObject.tag == "Hero" && gameObject.GetComponent<HeroEquipment>().EquippedItems[0] != null)
        {
            Equipable weapon = gameObject.GetComponent<HeroEquipment>().EquippedItems[0];
            damage = CalculateDamage("Physical", weapon.BasePower, targetStats, weapon.Element);

        }
        else
        {
            damage = CalculateDamage("Physical", 20, targetStats, Psynergy.Elements.NoElemental);
        }

        //ATTACK ANIMATION
        fighterAnim.SetTrigger("attack");
        sounds.PlaySound(1);

        yield return new WaitForSeconds(0.5f);//simulamos tiempo de animacion

        //DO PHYSICAL DAMAGE
        if (targetController.isDefending) damage = damage / 2;
        targetController.ReceiveDamage(damage);

        //yield return new WaitForSeconds(0.5f);

        //WAIT ANIMATION CALLS AttackDone()
        while (comingBack || transform.position != startPosition)
        {
            yield return null;
        }

        ActionFinished = true;


    }

    public IEnumerator UsePsynergy(Psynergy psynergyToUse, GameObject actualTarget, List<GameObject> targets)
    {
        int psynergyCD = 60;

        FighterController performerController = gameObject.GetComponent<FighterController>();
        FighterStats performerStats = gameObject.GetComponent<FighterStats>();

        //PP Cost
        performerStats.CurrPp -= psynergyToUse.PpCost;

        performerController.NextTurnCD += psynergyCD - performerStats.Agility / 2;

        //USE PSYNERGY ANIMATION
        fighterAnim.SetTrigger("usePsynergy");
        GameObject elementAnimation = Instantiate(PsynergyDatabase.Instance.getElementAnimation(psynergyToUse), performerController.transform);

        yield return new WaitForSeconds(2f);


        GameObject animation = Instantiate(PsynergyDatabase.Instance.PsynergyBattleAnim[psynergyToUse.SlugName], actualTarget.transform);

        yield return new WaitForSeconds(2f);

        if (psynergyToUse.Type == Psynergy.Types.ATTACK)
        {
            //DAMAGE CALC

            foreach (GameObject target in targets)
            {
                int magicalDamage = CalculateDamage("Psynergy", psynergyToUse.Power, target.GetComponent<FighterStats>(), psynergyToUse.Element);
                target.GetComponent<FighterController>().ReceiveDamage(magicalDamage);
            }
        }
        else if (psynergyToUse.Type == Psynergy.Types.HEALING)
        {
            Healing(psynergyToUse.Power, targets);
        }

        yield return new WaitForSeconds(1f);

        Destroy(animation);
        Destroy(elementAnimation);

        ActionFinished = true;
    }


    public int CalculateDamage(string attackType, int basePower, FighterStats targetStats, Psynergy.Elements attackElement = Psynergy.Elements.NoElemental)
    {
        switch (attackType)
        {
            case "Physical":

                int criticalRate = 1;


                // Si el ataque es de un elemento compatible con el arma, aumentar su poder
                Equipable weapon = null;
                Psynergy.Elements weaponElement;

                if (gameObject.tag == "Hero" && gameObject.GetComponent<HeroEquipment>().EquippedItems[0] != null)
                {
                    weapon = gameObject.GetComponent<HeroEquipment>().EquippedItems[0];
                    weaponElement = weapon.Element;
                    criticalRate = weapon.CriticalRate;

                    if (attackElement == weaponElement)
                    {
                        basePower += weapon.ElementalPower;
                    }
                }

                // Si el ataque es un golpe crítico, duplicar el poder base
                if (IsCriticalHit(criticalRate, stats.Luck))
                {
                    basePower *= 2;
                }

                // Calcular el modificador de daño en función del elemento del ataque
                float modificadorElemento = 1.0f;
                /*
                if (defensor.ResistenciaElemento.ContainsKey(elemento))
                {
                    modificadorElemento = defensor.ResistenciaElemento[elemento];
                }*/


                // Calcular el modificador de daño en función de la defensa del defensor
                float modificadorDefensa = targetStats.Defense / (targetStats.Defense + 5.0f);

                // Calcular el daño final
                int attackerLevel = stats.Level;
                int attackerStrenght = stats.Strenght;
                int targetDefense = stats.Defense;

                float damageNotRounded = (((2 * attackerLevel + 10) / 30.0f) * (attackerStrenght / targetDefense) * basePower + 2) * modificadorElemento;
                int finalDamage = Mathf.RoundToInt(damageNotRounded);

                Debug.Log("Damage not rounded: " +damageNotRounded + " Final damage: " + finalDamage);
                return finalDamage;


            case "Psynergy":
                float multiplier = 1;
                int damage = Mathf.RoundToInt(stats.Wisdom * multiplier * basePower / targetStats.Spirit);

                return damage;

            default:
                return 0;
        }

    }

    public bool IsCriticalHit(int criticalRate, int charLuck, int otherFactors = 0)
    {
        // Calculate probability of critical hit
        int critProb = criticalRate + charLuck / 2 + otherFactors;

        // Generate a random number between 0 and 99
        int rand = Random.Range(0, 100);

        // Check if the attack is a critical hit
        return rand <= critProb;
    }


    private void Healing(int power, List<GameObject> targets)
    {
        foreach (GameObject target in targets)
        {
            int beforeHealingHp = target.GetComponent<FighterStats>().CurrHp;
            target.GetComponent<FighterStats>().AddBonusEffect("HP", power);
            int hpRestored = target.GetComponent<FighterStats>().CurrHp - beforeHealingHp;
            target.GetComponent<FighterController>().InstantiateFloatingText(hpRestored.ToString(), Color.green);
        }
    }

    public void ReceiveDamage(int damage)
    {
        fighterAnim.SetTrigger("receiveHit");
        sounds.PlaySound(2);
        stats.CurrHp -= damage;

        if (stats.CurrHp < 0)
        {
            stats.CurrHp = 0;
        }

        InstantiateFloatingText(damage.ToString(), Color.white);

        if (stats.CurrHp <= 0)
        {
            fighterAnim.SetTrigger("death");
        }
    }

    private void InstantiateFloatingText(string number, Color textColor)
    {
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
        text.GetComponent<TextMeshPro>().SetText(number.ToString());
        text.GetComponent<TextMeshPro>().color = textColor;


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

    public GameObject SelectRandomTarget(List<GameObject> fighters)
    {
        GameObject targetSelected = null;
        if (gameObject.CompareTag("Hero"))
        {
            //TODO IMPLEMENTAR ESTADO "LOCURA"?
        }
        else
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
        StartCoroutine(WaitAfterAttack());
    }

    private IEnumerator WaitAfterAttack()
    {
        //TIME AFTER ATTACK TIL COMING BACK
        yield return new WaitForSeconds(0.5f);
        comingBack = true;
    }


}
