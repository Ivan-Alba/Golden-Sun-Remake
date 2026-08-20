using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject defining battle-use effects for non-consumable items, equipment, or artifacts (e.g. Hermes' Wand, Healing Ring).
/// Supports damage, healing, status cures, buff applications, VFX/SFX playback, and item break chance mechanics upon use.
/// </summary>
[CreateAssetMenu(fileName = "BattleEffect_", menuName = "GoldenSun/Items/Item Battle Effect")]
public class ItemBattleEffectSO : ScriptableObject
{
    #region Usage Restrictions & Element

    [Header("Targeting & Element")]
    [SerializeField] private ElementalType element = ElementalType.None;

    [Tooltip("Does this effect target an ally or an enemy?")]
    [SerializeField] private bool targetsAlly = true;

    [Tooltip("Does this effect target all combatants in the target party?")]
    [SerializeField] private bool isAreaOfEffect = false;

    #endregion

    #region Resource Effects (Damage / Healing)

    [Header("Health & Psynergy Effects")]
    [Tooltip("Restores health points (HP) to the target.")]
    [SerializeField] private bool restoresHp = false;

    [Tooltip("Amount of HP restored. If percentage is true, calculated over target's Max HP.")]
    [SerializeField] private int hpAmount = 0;

    [SerializeField] private bool isHpPercentage = false;

    [Space(5)]
    [Tooltip("Restores psynergy points (PP) to the target.")]
    [SerializeField] private bool restoresPp = false;

    [Tooltip("Amount of PP restored. If percentage is true, calculated over target's Max PP.")]
    [SerializeField] private int ppAmount = 0;

    [SerializeField] private bool isPpPercentage = false;

    #endregion

    #region Status Effect Removal & Cures

    [Header("Status Removal & Cures")]
    [Tooltip("If true, clears ALL active status effects on target.")]
    [SerializeField] private bool cureAllStatusEffects = false;

    [Tooltip("Categories of status effects cured by this item effect (e.g. Ailments, Debuffs).")]
    [SerializeField] private List<StatusEffectCategory> categoriesToCure = new List<StatusEffectCategory>();

    [Tooltip("Specific status effect templates cured by this item effect.")]
    [SerializeField] private List<StatusEffectSO> specificStatusToCure = new List<StatusEffectSO>();

    #endregion

    #region Status Effect Application (Buffs / Ailments)

    [Header("Status Effect Application")]
    [Tooltip("Status effects, buffs, or ailments applied to the target upon activation.")]
    [SerializeField] private List<StatusEffectSO> statusEffectsToApply = new List<StatusEffectSO>();

    #endregion

    #region Golden Sun Break Mechanics

    [Header("Break / Degradation Mechanics")]
    [Tooltip("Can this item break or crack when used in battle?")]
    [SerializeField] private bool canBreakOnUse = false;

    [Range(0f, 100f)]
    [Tooltip("Percentage chance (0-100%) that using this item breaks it in the user's inventory.")]
    [SerializeField] private float breakChancePercentage = 10f;

    #endregion

    #region VFX & SFX Assets

    [Header("VFX & SFX Assets")]
    [Tooltip("Impact visual effect type applied on the victim.")]
    [SerializeField] private HitFXType hitFXType = HitFXType.None;
    [Tooltip("Optional custom hit VFX prefab instantiated if HitFXType is set to Custom.")]
    [SerializeField] private GameObject customHitVFX = null;
    [Tooltip("VFX instantiated around the caster when initiating the Item Effect.")]
    [SerializeField] private GameObject casterAuraVFX = null;
    [Tooltip("Main effect sequence VFXPrefab executed over the target/area.")]
    [SerializeField] private GameObject effectMainVFX = null;
    [Tooltip("Audio clip played when the Item Effect is activated.")]
    [SerializeField] private AudioClip soundEffect = null;

    #endregion

    #region Public Properties

    public ElementalType Element => element;
    public bool TargetsAlly => targetsAlly;
    public bool IsAreaOfEffect => isAreaOfEffect;

    public bool RestoresHp => restoresHp;
    public int HpAmount => hpAmount;
    public bool IsHpPercentage => isHpPercentage;

    public bool RestoresPp => restoresPp;
    public int PpAmount => ppAmount;
    public bool IsPpPercentage => isPpPercentage;

    public bool CureAllStatusEffects => cureAllStatusEffects;
    public IReadOnlyList<StatusEffectCategory> CategoriesToCure => categoriesToCure;
    public IReadOnlyList<StatusEffectSO> SpecificStatusToCure => specificStatusToCure;
    public IReadOnlyList<StatusEffectSO> StatusEffectsToApply => statusEffectsToApply;

    public bool CanBreakOnUse => canBreakOnUse;
    public float BreakChancePercentage => breakChancePercentage;

    public HitFXType HitFXType => hitFXType;
    public GameObject CustomHitVFX => customHitVFX;
    public GameObject CasterAuraVFX => casterAuraVFX;
    public GameObject EffectMainVFX => effectMainVFX;
    public AudioClip SoundEffect => soundEffect;

    #endregion

    #region Execution API

    /// <summary>
    /// Executes the special battle effect on a single target.
    /// </summary>
    /// <returns>True if the effect was successfully applied; otherwise false.</returns>
    public virtual bool ExecuteEffect(FighterStats user, FighterStats target, out bool itemBroke)
    {
        itemBroke = false;

        if (target == null || target.IsDead) return false;

        bool effectApplied = false;

        // 1. Handle Resource Restoration (HP)
        if (restoresHp && target.CurrHp < target.MaxHp)
        {
            int hpToRestore = isHpPercentage
                ? Mathf.RoundToInt(target.MaxHp * (hpAmount / 100f))
                : hpAmount;

            if (target.RestoreHP(hpToRestore))
            {
                effectApplied = true;
            }
        }

        // 2. Handle Resource Restoration (PP)
        if (restoresPp && target.CurrPp < target.MaxPp)
        {
            int ppToRestore = isPpPercentage
                ? Mathf.RoundToInt(target.MaxPp * (ppAmount / 100f))
                : ppAmount;

            if (target.RestorePP(ppToRestore))
            {
                effectApplied = true;
            }
        }

        // 3. Handle Status Cures
        if (cureAllStatusEffects && target.ActiveStatusEffects.Count > 0)
        {
            target.ClearAllStatusEffects();
            effectApplied = true;
        }
        else
        {
            foreach (var category in categoriesToCure)
            {
                if (target.PurgeCategory(category) > 0) effectApplied = true;
            }

            foreach (var statusSO in specificStatusToCure)
            {
                if (target.RemoveStatusEffect(statusSO)) effectApplied = true;
            }
        }

        // 4. Apply Status Effects or Buffs
        if (statusEffectsToApply != null)
        {
            foreach (var statusSO in statusEffectsToApply)
            {
                if (statusSO != null && target.ApplyStatusEffect(statusSO, user))
                {
                    effectApplied = true;
                }
            }
        }

        // 5. Evaluate Item Break Chance (Golden Sun mechanic)
        if (effectApplied && canBreakOnUse)
        {
            float roll = Random.Range(0f, 100f);
            if (roll <= breakChancePercentage)
            {
                itemBroke = true;
                Debug.LogWarning($"[ItemBattleEffect] Item effect triggered and item BROKE! Roll: {roll:F1}% <= {breakChancePercentage}%");
            }
        }

        return effectApplied;
    }

    #endregion
}