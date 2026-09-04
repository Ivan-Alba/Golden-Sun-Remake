using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// ScriptableObject defining the battle unleash action, combat modifiers, 
/// direct HP/PP restoration, multi-hit strike mechanics (e.g., Gust Djinn), 
/// status effects, and visual/audio presentation executed when a Djinn is unleashed in combat.
/// </summary>
[CreateAssetMenu(fileName = "New_DjinnUnleash", menuName = "GoldenSun/Djinn/Djinn Unleash Effect")]
public class DjinnUnleashSO : ScriptableObject
{
    #region Core Identification

    [Header("Core Identification")]
    [Tooltip("Localized display name of the unleashing ability.")]
    [SerializeField] private LocalizedString unleashName;

    [Tooltip("Unique string identifier used for database lookups and save data transactions.")]
    [SerializeField] private string slugName = "flint_unleash";

    [Tooltip("Localized detailed description of the unleash skill and its combat effects.")]
    [SerializeField] private LocalizedString description;

    [Tooltip("UI Icon displayed in battle action selection menus.")]
    [SerializeField] private Sprite icon;

    #endregion

    #region Targeting & Combat Properties

    [Header("Targeting & Primary Attack Properties")]
    [Tooltip("Elemental power alignment of this unleash execution.")]
    [SerializeField] private ElementalType element = ElementalType.None;

    [Tooltip("Does this unleash target allies (e.g. Shield/Heal) or enemies (Attack/Debuff)?")]
    [SerializeField] private bool targetsAlly = false;

    [Tooltip("Does this unleash affect all combatants in the target group?")]
    [SerializeField] private bool isAreaOfEffect = false;

    [Tooltip("Base damage multiplier applied during the primary hit (e.g., 1.5 = +50% physical/elemental damage).")]
    [SerializeField] private float damageMultiplier = 1.0f;

    [Tooltip("Flat bonus damage or power added to the primary attack calculation.")]
    [SerializeField] private int flatBonusDamage = 0;

    #endregion

    #region Multi-Hit / Secondary Strike Mechanics (Gust Effect)

    [Header("Secondary Strike Properties (Multi-Hit / Gust Effect)")]
    [Tooltip("If true, this unleash has a chance to execute an additional follow-up strike.")]
    [SerializeField] private bool hasSecondaryHit = false;

    [Range(0f, 1f)]
    [Tooltip("Normalized probability (0.0 to 1.0) of triggering the secondary follow-up hit (e.g., 0.5 = 50% chance like Gust Djinn).")]
    [SerializeField] private float secondaryHitChance = 0.5f;

    [Tooltip("Damage multiplier applied exclusively to the secondary strike (can be lower, equal, or higher than primary multiplier).")]
    [SerializeField] private float secondaryDamageMultiplier = 1.0f;

    [Tooltip("Flat bonus damage added exclusively to the secondary strike.")]
    [SerializeField] private int secondaryFlatBonusDamage = 0;

    #endregion

    #region Health & Psynergy Restoration

    [Header("HP Restoration")]
    [SerializeField] private bool restoresHp = false;
    [Tooltip("Amount of HP restored. If percentage is true, calculated based on target's Max HP.")]
    [SerializeField] private int healValue = 0;
    [SerializeField] private bool isHpPercentage = false;

    [Header("PP Restoration")]
    [SerializeField] private bool restoresPp = false;
    [Tooltip("Amount of PP restored. If percentage is true, calculated based on target's Max PP.")]
    [SerializeField] private int ppHealValue = 0;
    [SerializeField] private bool isPpPercentage = false;

    [Header("Revive Mechanics")]
    [SerializeField] private bool canRevive = false;
    [Range(0f, 1f)]
    [Tooltip("Probability of successfully reviving a fallen ally (0.0 = 0%, 1.0 = 100%).")]
    [SerializeField] private float reviveSuccessChance = 1.0f;

    #endregion

    #region Secondary Status Effects

    [Header("Secondary Status Effects")]
    [Tooltip("List of status effects with independent application probabilities triggered upon unleashing this Djinn.")]
    [SerializeField] private List<StatusEffectApplication> secondaryEffects = new List<StatusEffectApplication>();

    #endregion

    #region VFX & SFX Assets

    [Header("VFX & SFX Assets")]
    [Tooltip("Impact visual effect type applied on the target.")]
    [SerializeField] private HitFXType hitFXType = HitFXType.None;

    [Tooltip("Optional custom hit VFX prefab instantiated if HitFXType is set to Custom.")]
    [SerializeField] private GameObject customHitVFX;

    [Tooltip("VFX instantiated around the caster when initiating the Djinn Unleash.")]
    [SerializeField] private GameObject casterAuraVFX;

    [Tooltip("Main sequence VFX Prefab executed over the target/area.")]
    [SerializeField] private GameObject unleashMainVFX;

    [Tooltip("Optional direct Audio Clip executed via 3D Audio Pool on unleash execution.")]
    [SerializeField] private AudioClip soundEffect;

    #endregion

    #region Public Properties

    public LocalizedString UnleashNameLocalizedString => unleashName;
    public string UnleashName => unleashName != null ? unleashName.GetLocalizedString() : string.Empty;

    public string SlugName => slugName;

    public LocalizedString DescriptionLocalizedString => description;
    public string Description => description != null ? description.GetLocalizedString() : string.Empty;

    public Sprite Icon => icon;

    // Targeting & Primary Attack
    public ElementalType Element => element;
    public bool TargetsAlly => targetsAlly;
    public bool IsAreaOfEffect => isAreaOfEffect;
    public float DamageMultiplier => damageMultiplier;
    public int FlatBonusDamage => flatBonusDamage;

    // Multi-Hit / Secondary Strike
    public bool HasSecondaryHit => hasSecondaryHit;
    public float SecondaryHitChance => secondaryHitChance;
    public float SecondaryDamageMultiplier => secondaryDamageMultiplier;
    public int SecondaryFlatBonusDamage => secondaryFlatBonusDamage;

    // Direct Restoration
    public bool RestoresHp => restoresHp;
    public int HealValue => healValue;
    public bool IsHpPercentage => isHpPercentage;

    public bool RestoresPp => restoresPp;
    public int PpHealValue => ppHealValue;
    public bool IsPpPercentage => isPpPercentage;

    public bool CanRevive => canRevive;
    public float ReviveSuccessChance => reviveSuccessChance;

    // Status Effects
    public IReadOnlyList<StatusEffectApplication> SecondaryEffects => secondaryEffects;

    // Visual & Audio Assets
    public HitFXType HitFXType => hitFXType;
    public GameObject CustomHitVFX => customHitVFX;
    public GameObject CasterAuraVFX => casterAuraVFX;
    public GameObject UnleashMainVFX => unleashMainVFX;
    public AudioClip SoundEffect => soundEffect;

    #endregion

    #region Unity Editor Validation

    private void OnValidate()
    {
        if (damageMultiplier < 0f) damageMultiplier = 0f;
        if (secondaryDamageMultiplier < 0f) secondaryDamageMultiplier = 0f;
        if (healValue < 0) healValue = 0;
        if (ppHealValue < 0) ppHealValue = 0;

        if (string.IsNullOrWhiteSpace(slugName))
        {
            slugName = name.Replace("New_DjinnUnleash", string.Empty).ToLowerInvariant();
        }
    }

    #endregion
}