using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a weapon's special passive attack (Unleash/Desatado) triggered probabilistically on basic physical attacks.
/// </summary>
[CreateAssetMenu(fileName = "New_WeaponUnleash", menuName = "Inventory/Combat/Weapon Unleash")]
public class WeaponUnleashSO : ScriptableObject
{
    #region Core Identification

    [Header("Core Identification")]
    [SerializeField] private string unleashName;
    [SerializeField] private string slugName;
    [TextArea(2, 4)]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;

    #endregion

    #region Combat Properties

    [Header("Combat Properties")]
    [SerializeField] private ElementalType element = ElementalType.None;
    [Tooltip("Base damage multiplier applied on top of the attacker's physical hit.")]
    [SerializeField] private float damageMultiplier = 1.5f;
    [Tooltip("Flat bonus damage added to the total unleashed attack calculation.")]
    [SerializeField] private int flatBonusDamage = 0;

    #endregion

    #region Status & Secondary Effects

    [Header("Status & Secondary Effects")]
    [Tooltip("List of status effects applied to the target upon hitting with this unleash.")]
    [SerializeField] private List<StatusEffectSO> statusEffects = new List<StatusEffectSO>();

    [Range(0f, 1f)]
    [Tooltip("Probability (0.0 to 1.0) of applying the secondary status conditions upon hitting the target.")]
    [SerializeField] private float statusChance = 0.2f;

    #endregion

    #region VFX & SFX Assets

    [Header("VFX & SFX Assets")]
    [Tooltip("Impact visual effect type applied on the victim.")]
    [SerializeField] private HitFXType hitFXType = HitFXType.None;
    [Tooltip("Optional custom hit VFX prefab instantiated if HitFXType is set to Custom.")]
    [SerializeField] private GameObject customHitVFX = null;
    [Tooltip("VFX instantiated around the caster when initiating the Unleash.")]
    [SerializeField] private GameObject casterAuraVFX = null;
    [Tooltip("Main attack sequence VFXPrefab executed over the target/area.")]
    [SerializeField] private GameObject unleashMainVFX = null;
    [Tooltip("Audio clip played when the Unleash is activated.")]
    [SerializeField] private AudioClip soundEffect = null;

    #endregion

    #region Public Properties

    public string UnleashName => unleashName;
    public string SlugName => slugName;
    public string Description => description;
    public Sprite Icon => icon;

    public ElementalType Element => element;
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
}