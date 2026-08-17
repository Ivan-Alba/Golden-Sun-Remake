using System.Collections.Generic;
using UnityEngine;

public enum AttackScalingType
{
    ElementalPower, // Scale based on user's elemental power / affinity
    PhysicalAttack  // Scale based on user's physical attack stat + elemental boost
}

[CreateAssetMenu(fileName = "NewAttackPsynergy", menuName = "Psynergy/Attack Psynergy")]
public class AttackPsynergySO : PsynergySO
{
    [Header("Attack Properties")]
    [SerializeField] private int basePower = 20;
    [SerializeField] private AttackScalingType scalingType = AttackScalingType.ElementalPower;
    [Tooltip("Multiplier applied to base physical attack or total damage (e.g., 1.5 = 150% physical damage).")]
    [SerializeField] private float damageMultiplier = 1.0f;

    [Header("Life / PP Drain (Optional)")]
    [SerializeField] private bool drainsHp;
    [SerializeField] private bool drainsPp;
    [Range(0f, 1f)]
    [SerializeField] private float drainRatio = 0.5f; // Percentage of dealt damage restored to caster

    [Header("Instant KO / Death Effect (Optional)")]
    [SerializeField] private bool isInstantKill;
    [Range(0f, 1f)]
    [SerializeField] private float instantKillChance = 0.2f;

    [Header("Secondary Stat Effects (Optional)")]
    [SerializeField] private bool appliesSecondaryStatEffect;
    [SerializeField] private StatType statToModify;
    [SerializeField] private float statModifierValue;
    [SerializeField] private StatModifierType modifierType = StatModifierType.PercentBase;
    [SerializeField] private int modifierDurationTurns = 3;
    [Range(0f, 1f)]
    [SerializeField] private float secondaryEffectChance = 1f;

    #region Public Properties

    public int BasePower => basePower;
    public AttackScalingType ScalingType => scalingType;
    public float DamageMultiplier => damageMultiplier;
    public bool DrainsHp => drainsHp;
    public bool DrainsPp => drainsPp;
    public float DrainRatio => drainRatio;
    public bool IsInstantKill => isInstantKill;
    public float InstantKillChance => instantKillChance;
    public bool AppliesSecondaryStatEffect => appliesSecondaryStatEffect;
    public StatType StatToModify => statToModify;
    public float StatModifierValue => statModifierValue;
    public StatModifierType ModifierType => modifierType;
    public int ModifierDurationTurns => modifierDurationTurns;
    public float SecondaryEffectChance => secondaryEffectChance;

    #endregion

    public override void ExecuteBattleEffect(FighterStats caster, List<FighterStats> targets)
    {
        base.ExecuteBattleEffect(caster, targets);

        if (targets == null || targets.Count == 0) return;

        Debug.Log($"[Psynergy] {caster.Name} casts attack Psynergy '{PsynergyName}' (Scaling: {scalingType}, Multiplier: {damageMultiplier}x, Base Power: {basePower}) on {targets.Count} target(s).");

        // TODO: Integrate actual damage calculation formula when combat manager executes actions
        // TODO: Handle HP/PP draining after damage calculation
        // TODO: Handle instant kill probability check against target resistances
        // TODO: Apply secondary StatModifier/StatusEffect if secondaryEffectChance roll succeeds
    }
}