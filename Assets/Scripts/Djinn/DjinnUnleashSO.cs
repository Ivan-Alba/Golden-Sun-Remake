using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// ScriptableObject defining the battle unleash action, combat modifiers, 
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

    [Header("Targeting & Combat Properties")]
    [Tooltip("Elemental power alignment of this unleash execution.")]
    [SerializeField] private ElementalType element = ElementalType.None;

    [Tooltip("Does this unleash target allies (e.g. Shield/Heal) or enemies (Attack/Debuff)?")]
    [SerializeField] private bool targetsAlly = false;

    [Tooltip("Does this unleash affect all combatants in the target group?")]
    [SerializeField] private bool isAreaOfEffect = false;

    [Tooltip("Base damage multiplier applied during the unleash hit (e.g., 1.5 = +50% physical/elemental damage).")]
    [SerializeField] private float damageMultiplier = 1.0f;

    [Tooltip("Flat bonus damage or power added to the total unleash calculation.")]
    [SerializeField] private int flatBonusDamage = 0;

    #endregion

    #region Status & Secondary Effects

    [Header("Status & Secondary Effects")]
    [Tooltip("List of status effects, ailments, or buffs applied to targets upon unleashing this Djinn.")]
    [SerializeField] private List<StatusEffectSO> statusEffects = new List<StatusEffectSO>();

    [Range(0f, 1f)]
    [Tooltip("Probability (0.0 to 1.0) of applying secondary status conditions upon executing the unleash.")]
    [SerializeField] private float statusChance = 1.0f;

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

    public ElementalType Element => element;
    public bool TargetsAlly => targetsAlly;
    public bool IsAreaOfEffect => isAreaOfEffect;
    public float DamageMultiplier => damageMultiplier;
    public int FlatBonusDamage => flatBonusDamage;

    public IReadOnlyList<StatusEffectSO> StatusEffects => statusEffects;
    public float StatusChance => statusChance;

    public HitFXType HitFXType => hitFXType;
    public GameObject CustomHitVFX => customHitVFX;
    public GameObject CasterAuraVFX => casterAuraVFX;
    public GameObject UnleashMainVFX => unleashMainVFX;
    public AudioClip SoundEffect => soundEffect;

    #endregion

    #region Unity Editor Validation

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (damageMultiplier < 0f)
        {
            damageMultiplier = 0f;
        }

        statusChance = Mathf.Clamp01(statusChance);

        if (string.IsNullOrWhiteSpace(slugName))
        {
            slugName = name.Replace("New_DjinnUnleash", string.Empty).ToLowerInvariant();
        }
    }
#endif

    #endregion
}