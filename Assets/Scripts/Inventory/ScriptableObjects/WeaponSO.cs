using UnityEngine;

/// <summary>
/// ScriptableObject defining weapon equipment in Golden Sun (Longswords, Light Blades, Axes, Maces, Staffs).
/// Inherits core equipment logic from EquipableSO and adds 3D hand mesh prefabs, swing/hit audio-visual FX,
/// weapon unleash mechanics (artifact special moves), and critical rate modifiers.
/// </summary>
[CreateAssetMenu(fileName = "Weapon_", menuName = "GoldenSun/Items/Weapon")]
public class WeaponSO : EquipableSO
{
    #region 3D Model & Mesh Assets

    [Header("3D Model & Mesh Assets")]
    [Tooltip("3D Prefab model instantiated and attached to the hero's weapon bone when equipped.")]
    [SerializeField] private GameObject weaponModelPrefab = null;

    #endregion

    #region Physical Hit Visuals & Audio

    [Header("Physical Hit Visuals & Audio")]
    [Tooltip("Default physical impact effect executed on standard basic attacks.")]
    [SerializeField] private HitFXType hitFXType = HitFXType.Slash;

    [Tooltip("Optional custom hit VFX prefab instantiated if HitFXType is set to Custom.")]
    [SerializeField] private GameObject customHitVFX = null;

    [Tooltip("Audio clip played when swinging this weapon in combat.")]
    [SerializeField] private AudioClip swingSoundEffect = null;

    [Tooltip("Fallback SFX type used when swingSoundEffect is unassigned.")]
    [SerializeField] private SFXType fallbackSwingSFX = SFXType.Combat_AttackSwing;

    #endregion

    #region Weapon Unleash (Artefact Special Move)

    [Header("Weapon Unleash (Special Artifact Attack)")]
    [Tooltip("Does this weapon possess a unique Unleash effect (e.g., Megiddo on Sol Blade)?")]
    [SerializeField] private bool hasUnleash = false;

    [Range(0f, 1f)]
    [Tooltip("Base probability (0.35 = 35% in Golden Sun) of triggering the unleash on a physical strike.")]
    [SerializeField] private float unleashProcRate = 0.35f;

    [Tooltip("ScriptableObject containing the technical logic, animations, and damage multipliers of the unleash.")]
    [SerializeField] private WeaponUnleashSO unleashEffect = null;

    #endregion

    #region Weapon Stats Extra

    [Header("Weapon Combat Extras")]
    [Tooltip("Bonus added to Unleash proc chance / critical hit rate calculations in damage formulas.")]
    [SerializeField] private int criticalRateBonus = 0;

    #endregion

    #region Public Properties

    public GameObject WeaponModelPrefab => weaponModelPrefab;

    public HitFXType HitFXType => hitFXType;
    public GameObject CustomHitVFX => customHitVFX;
    public AudioClip SwingSoundEffect => swingSoundEffect;
    public SFXType FallbackSwingSFX => fallbackSwingSFX;

    public bool HasUnleash => hasUnleash;
    public float UnleashProcRate => unleashProcRate;
    public WeaponUnleashSO UnleashEffect => unleashEffect;

    public int CriticalRateBonus => criticalRateBonus;

    #endregion

    #region Unity Editor Validation

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Enforce Weapon EquipmentSlot
        var slotProp = typeof(EquipableSO).GetField("equipmentSlot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (slotProp != null && EquipmentSlot != EquipmentSlot.Weapon)
        {
            slotProp.SetValue(this, EquipmentSlot.Weapon);
        }

        // Ensure default weapon subtype if set to a non-weapon subtype
        if (EquipmentSubtype < EquipmentSubtype.Longsword || EquipmentSubtype > EquipmentSubtype.Staff)
        {
            var subtypeProp = typeof(EquipableSO).GetField("equipmentSubtype", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (subtypeProp != null)
            {
                subtypeProp.SetValue(this, EquipmentSubtype.Longsword);
            }
        }

        if (criticalRateBonus < 0) criticalRateBonus = 0;
        if (unleashProcRate < 0f) unleashProcRate = 0f;
        if (unleashProcRate > 1f) unleashProcRate = 1f;
    }
#endif

    #endregion
}