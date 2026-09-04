using System.Collections.Generic;
using UnityEngine;

public enum AttackScalingType
{
    ElementalPower, // Scale based on user's elemental power / affinity
    PhysicalAttack  // Scale based on user's physical attack stat + elemental boost
}

/// <summary>
/// ScriptableObject definition for offensive Psynergies.
/// Combines immediate damage calculations (elemental/physical scaling, area range, HP/PP drain) 
/// with a modular list of secondary status effect applications (debuffs, ailments, doom).
/// </summary>
[CreateAssetMenu(fileName = "NewAttackPsynergy", menuName = "Psynergy/Attack Psynergy")]
public class AttackPsynergySO : PsynergySO
{
    #region Serialized Fields

    [Header("Attack Properties")]
    [SerializeField] private int basePower = 20;
    [SerializeField] private AttackScalingType scalingType = AttackScalingType.ElementalPower;
    [Tooltip("Multiplier applied to base physical attack or total damage (e.g., 1.5 = 150% physical damage).")]
    [SerializeField] private float damageMultiplier = 1.0f;

    [Header("Life / PP Drain (Immediate On Hit)")]
    [SerializeField] private bool drainsHp;
    [SerializeField] private bool drainsPp;
    [Range(0f, 1f)]
    [Tooltip("Percentage of dealt damage restored to caster on hit (0.0 = 0%, 1.0 = 100%).")]
    [SerializeField] private float drainRatio = 0.5f;

    [Header("Secondary Status Effects & Debuffs")]
    [Tooltip("List of status effects (debuffs, ailments, doom, stat modifiers) potentially applied on target hit.")]
    [SerializeField] private List<StatusEffectApplication> secondaryEffects = new List<StatusEffectApplication>();

    #endregion

    #region Public Properties

    public int BasePower => basePower;
    public AttackScalingType ScalingType => scalingType;
    public float DamageMultiplier => damageMultiplier;
    public bool DrainsHp => drainsHp;
    public bool DrainsPp => drainsPp;
    public float DrainRatio => drainRatio;
    public IReadOnlyList<StatusEffectApplication> SecondaryEffects => secondaryEffects;

    #endregion

    #region Editor Validation

    private void OnValidate()
    {
        if (basePower < 0) basePower = 0;
        if (damageMultiplier < 0f) damageMultiplier = 0f;
    }

    #endregion

    #region Execution Methods Override

    public override bool ExecuteBattleEffect(FighterStats caster, IReadOnlyList<FighterStats> targets)
    {
        if (!IsBattlePsynergy)
        {
            Debug.LogWarning($"[Psynergy] '{PsynergyName}' is marked as a non-battle Psynergy and cannot be used in combat.");
            return false;
        }

        if (targets == null || targets.Count == 0) return false;

        Debug.Log($"[Psynergy] {caster.Name} uses attack Psynergy '{PsynergyName}' (Scaling: {scalingType}, Multiplier: {damageMultiplier}x, Base Power: {basePower}) on {targets.Count} target(s).");

        for (int i = 0; i < targets.Count; i++)
        {
            FighterStats target = targets[i];
            if (target == null) continue;

            // TODO: Calculate damage using Golden Sun diminishing area spread formula (AreaRange, HasDiminishingSpread)
            // TODO: Apply calculated damage to target
            // TODO: Process immediate HP/PP draining back to caster if drainsHp / drainsPp flags are active
            // TODO: Iterate secondaryEffects list and perform Random.value <= applicationChance rolls to apply StatusEffectSO
        }

        return true;
    }

    public override bool ExecuteMenuEffect(FighterStats caster, IReadOnlyList<FighterStats> targets)
    {
        Debug.LogWarning($"[Psynergy] '{PsynergyName}' is an offensive Psynergy and cannot be cast from the pause menu.");
        return false;
    }

    #endregion
}