using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FighterController : MonoBehaviour
{
    #region Private Fields & References

    private Animator fighterAnim;
    private Sounds sounds;
    private FighterStats stats;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private bool isDefending;
    private bool actionFinished;
    private bool isReturning;

    #endregion

    #region Properties

    public bool ActionFinished { get => actionFinished; set => actionFinished = value; }
    public bool IsDefending => isDefending;

    // Cooldown accumulated value managed by TurnController
    public int NextTurnCD { get; set; }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        fighterAnim = GetComponentInChildren<Animator>();
        sounds = GetComponent<Sounds>();
        stats = GetComponent<FighterStats>();
    }

    #endregion

    #region Turn State Methods

    public void StartsTurn()
    {
        if (isDefending)
        {
            SetOffDefense();
        }

        // Decrement active turn modifiers on stats
        stats?.TickTurnModifiers();
    }

    #endregion

    #region Combat Actions (Coroutines)

    public IEnumerator BasicAttack(GameObject target)
    {
        actionFinished = false;

        if (target == null || !target.TryGetComponent<FighterStats>(out var targetStats))
        {
            actionFinished = true;
            yield break;
        }

        startPosition = transform.position;
        startRotation = transform.rotation;

        // 1. Move to Target
        yield return StartCoroutine(Routine_MoveToTarget(target.transform.position, 2.5f));

        // 2. Determine Attack Parameters
        ElementalType weaponElement = ElementalType.None;
        int critRate = 1;

        // Get equipped weapon using the new HeroEquipment slot accessor
        if (TryGetComponent<HeroEquipment>(out var heroEq))
        {
            EquipableSO weapon = heroEq.GetEquippedItem(EquipmentSlot.Weapon);
            if (weapon != null)
            {
                weaponElement = weapon.Element;
                critRate = weapon.CriticalRateBonus;
            }
        }

        bool isCritical = BattleDamageCalculator.CheckCriticalHit(critRate, stats.Luck);

        // 3. Play Attack Animation
        if (fighterAnim != null) fighterAnim.SetTrigger("attack");
        sounds?.PlaySound(1);

        yield return new WaitForSeconds(0.4f);

        // 4. Calculate and Apply Damage
        // Note: stats.Attack already includes base hero attack + equipped weapon attack bonus
        int damage = BattleDamageCalculator.CalculatePhysicalDamage(stats, targetStats, weaponElement, isCritical);
        if (target.TryGetComponent<FighterController>(out var targetController))
        {
            targetController.ReceiveDamage(damage, isCritical);
        }

        // 5. Wait for Animation and Return to Origin
        yield return new WaitForSeconds(0.4f);
        yield return StartCoroutine(Routine_ReturnToOrigin());

        actionFinished = true;
    }

    public IEnumerator UsePsynergy(Psynergy psynergyToUse, GameObject primaryTarget, List<GameObject> areaTargets)
    {
        actionFinished = false;

        if (psynergyToUse == null)
        {
            actionFinished = true;
            yield break;
        }

        // Consume PP
        stats.CurrPp -= psynergyToUse.PpCost;

        // Cast Animation
        if (fighterAnim != null) fighterAnim.SetTrigger("usePsynergy");

        // Instantiate Cast VFX
        GameObject castVfxPrefab = PsynergyDatabase.Instance?.getElementAnimation(psynergyToUse);
        GameObject castVfxInstance = null;
        if (castVfxPrefab != null)
        {
            castVfxInstance = Instantiate(castVfxPrefab, transform);
        }

        yield return new WaitForSeconds(1.5f);

        // Instantiate Impact VFX on target
        GameObject impactVfxPrefab = PsynergyDatabase.Instance?.PsynergyBattleAnim[psynergyToUse.SlugName];
        GameObject impactVfxInstance = null;
        if (impactVfxPrefab != null && primaryTarget != null)
        {
            impactVfxInstance = Instantiate(impactVfxPrefab, primaryTarget.transform);
        }

        yield return new WaitForSeconds(1.5f);

        // Process Effect according to Psynergy Type
        if (psynergyToUse.Type == Psynergy.Types.ATTACK)
        {
            foreach (GameObject targetObj in areaTargets)
            {
                if (targetObj != null && targetObj.TryGetComponent<FighterStats>(out var targetStats))
                {
                    int damage = BattleDamageCalculator.CalculatePsynergyDamage(stats, targetStats, psynergyToUse.Power, psynergyToUse.Element);
                    if (targetObj.TryGetComponent<FighterController>(out var targetController))
                    {
                        targetController.ReceiveDamage(damage);
                    }
                }
            }
        }
        else if (psynergyToUse.Type == Psynergy.Types.HEALING)
        {
            ExecuteHealing(psynergyToUse.Power, areaTargets);
        }

        yield return new WaitForSeconds(0.5f);

        // Clean Up VFX Instances
        if (impactVfxInstance != null) Destroy(impactVfxInstance);
        if (castVfxInstance != null) Destroy(castVfxInstance);

        actionFinished = true;
    }

    #endregion

    #region Health & Status Effects

    public void ReceiveDamage(int damageAmount, bool isCritical = false)
    {
        if (fighterAnim != null) fighterAnim.SetTrigger("receiveHit");
        sounds?.PlaySound(2);

        stats.CurrHp -= damageAmount;

        // Display Floating Text via FX Manager
        string textDisplay = isCritical ? $"CRITICAL!\n{damageAmount}" : damageAmount.ToString();
        Color textColour = isCritical ? Color.yellow : Color.white;

        if (BattleFXManager.Instance != null)
        {
            BattleFXManager.Instance.SpawnFloatingText(transform, textDisplay, textColour);
        }

        if (stats.CurrHp <= 0)
        {
            if (fighterAnim != null) fighterAnim.SetTrigger("death");
        }
    }

    private void ExecuteHealing(int healPower, List<GameObject> targets)
    {
        foreach (GameObject targetObj in targets)
        {
            if (targetObj != null && targetObj.TryGetComponent<FighterStats>(out var targetStats))
            {
                int beforeHp = targetStats.CurrHp;
                targetStats.CurrHp += healPower;
                int restoredHp = targetStats.CurrHp - beforeHp;

                if (BattleFXManager.Instance != null)
                {
                    BattleFXManager.Instance.SpawnFloatingText(targetObj.transform, restoredHp.ToString(), Color.green);
                }
            }
        }
    }

    public void SetUpDefense()
    {
        if (fighterAnim != null) fighterAnim.SetBool("defense", true);
        isDefending = true;
    }

    public void SetOffDefense()
    {
        isDefending = false;
        if (fighterAnim != null) fighterAnim.SetBool("defense", false);
    }

    #endregion

    #region Movement Helpers (Coroutines)

    private IEnumerator Routine_MoveToTarget(Vector3 destinationPoint, float stopDistance)
    {
        Vector3 targetPosHorizontal = new Vector3(destinationPoint.x, transform.position.y, destinationPoint.z);

        while (Vector3.Distance(transform.position, targetPosHorizontal) > stopDistance)
        {
            transform.LookAt(targetPosHorizontal);
            transform.position = Vector3.MoveTowards(transform.position, targetPosHorizontal, 8f * Time.deltaTime);

            if (fighterAnim != null) fighterAnim.SetFloat("Move", 6f);
            yield return null;
        }

        if (fighterAnim != null) fighterAnim.SetFloat("Move", 0f);
    }

    private IEnumerator Routine_ReturnToOrigin()
    {
        isReturning = true;

        while (Vector3.Distance(transform.position, startPosition) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, startPosition, 8.5f * Time.deltaTime);
            if (fighterAnim != null) fighterAnim.SetFloat("Move", -8.5f);
            yield return null;
        }

        transform.position = startPosition;

        // Smoothly return to start rotation
        while (Quaternion.Angle(transform.rotation, startRotation) > 1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, startRotation, 10f * Time.deltaTime);
            yield return null;
        }

        transform.rotation = startRotation;
        if (fighterAnim != null) fighterAnim.SetFloat("Move", 0f);

        isReturning = false;
    }

    public void AttackDone() // Animation Event Trigger
    {
        // Event hook reserved for animation sync if needed
    }

    #endregion
}