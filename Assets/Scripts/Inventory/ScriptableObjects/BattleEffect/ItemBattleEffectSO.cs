using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the complete active effect executed when using an equipment piece or item during combat.
/// Supports restoration, status condition cure/infliction, temporary stat buffs/debuffs, and dispels.
/// </summary>
[CreateAssetMenu(fileName = "New_ItemBattleEffect", menuName = "Inventory/Combat/Item Battle Effect")]
public class ItemBattleEffectSO : ScriptableObject
{
    [Header("Core Identification")]
    [SerializeField] private string effectName;
    [TextArea(2, 4)]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;

    [Header("Targeting Settings")]
    [SerializeField] private PsynergyTargetType targetType = PsynergyTargetType.SingleAlly;

    [Header("1. Restoration Effects (HP / PP / Revive)")]
    [SerializeField] private bool hasRestorationEffect = true;
    [SerializeField] private ConsumableEffectType restorationType = ConsumableEffectType.RestoreHp;
    [SerializeField] private int restorationValue = 50;

    [Header("2. Status Condition Effects")]
    [SerializeField] private bool curesStatusCondition = false;
    //[SerializeField] private StatusEffectType statusToCure = StatusEffectType.None;

    [SerializeField] private bool inflictsStatusCondition = false;
    //[SerializeField] private StatusEffectType statusToInflict = StatusEffectType.None;
    [Range(0f, 1f)]
    [SerializeField] private float statusInflictChance = 1f;

    [Header("3. Temporary Stat Buffs / Debuffs")]
    [SerializeField] private bool appliesStatModifier = false;
    [SerializeField] private StatType statToModify = StatType.Attack;
    [Tooltip("e.g. +0.25 (+25% Attack) for buffs or -0.25 (-25% Attack) for debuffs.")]
    [SerializeField] private float modifierValue = 0.25f;
    [SerializeField] private StatModifierType modifierType = StatModifierType.PercentBase;
    [SerializeField] private int durationInTurns = 3;

    [Header("4. Dispel Enemy Buffs (Break / Anular)")]
    [Tooltip("If true, removes positive stat modifiers (buffs) applied to the target.")]
    [SerializeField] private bool dispelsEnemyBuffs = false;

    [Header("Break & Degradation Rules (Golden Sun Style)")]
    [Tooltip("If true, the item has a chance to break or degrade after usage in battle.")]
    [SerializeField] private bool canBreakOnUse = false;
    [Range(0f, 1f)]
    [Tooltip("Probability (0.0 to 1.0) of breaking/degrading the item upon usage.")]
    [SerializeField] private float breakChance = 0.1f;

    [Header("VFX & SFX Assets")]
    [Tooltip("Type of impact visual effect spawned on each target.")]
    [SerializeField] private HitFXType hitFXType = HitFXType.None;
    [Tooltip("Optional custom hit VFX prefab instantiated if HitFXType is set to Custom.")]
    [SerializeField] private GameObject customHitVFX = null;
    [Tooltip("VFX instantiated over the user when activating the item.")]
    [SerializeField] private GameObject userVFX = null;
    [Tooltip("Main target effect VFX Prefab.")]
    [SerializeField] private GameObject targetVFX = null;
    [Tooltip("Audio clip played when activating this item effect.")]
    [SerializeField] private AudioClip soundEffect = null;

    #region Public Properties

    public string EffectName => effectName;
    public string Description => description;
    public Sprite Icon => icon;
    public PsynergyTargetType TargetType => targetType;

    // Restoration Properties
    public bool HasRestorationEffect => hasRestorationEffect;
    public ConsumableEffectType RestorationType => restorationType;
    public int RestorationValue => restorationValue;

    // Status Condition Properties
    public bool CuresStatusCondition => curesStatusCondition;
    //public StatusEffectType StatusToCure => statusToCure;
    public bool InflictsStatusCondition => inflictsStatusCondition;
    //public StatusEffectType StatusToInflict => statusToInflict;
    public float StatusInflictChance => statusInflictChance;

    // Stat Modifier Properties
    public bool AppliesStatModifier => appliesStatModifier;
    public StatType StatToModify => statToModify;
    public float ModifierValue => modifierValue;
    public StatModifierType ModifierType => modifierType;
    public int DurationInTurns => durationInTurns;

    // Dispel Properties
    public bool DispelsEnemyBuffs => dispelsEnemyBuffs;

    // Item Degradation Properties
    public bool CanBreakOnUse => canBreakOnUse;
    public float BreakChance => breakChance;

    // Visual & Audio Assets
    public HitFXType HitFXType => hitFXType;
    public GameObject CustomHitVFX => customHitVFX;
    public GameObject UserVFX => userVFX;
    public GameObject TargetVFX => targetVFX;
    public AudioClip SoundEffect => soundEffect;

    #endregion
}