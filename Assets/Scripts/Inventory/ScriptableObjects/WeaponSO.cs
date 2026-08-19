using UnityEngine;

[CreateAssetMenu(fileName = "New_Weapon", menuName = "Inventory/Items/Weapon")]
public class WeaponSO : EquipableSO
{
    [Header("3D Model & Mesh Assets")]
    [Tooltip("3D Prefab model instantiated and attached to the hero's hand bone when equipped.")]
    [SerializeField] private GameObject weaponModelPrefab = null;

    [Header("Physical Hit Visuals & Audio")]
    [Tooltip("Default physical impact effect executed on standard basic attacks.")]
    [SerializeField] private HitFXType hitFXType = HitFXType.Slash;
    [Tooltip("Optional custom hit VFX prefab instantiated if HitFXType is set to Custom.")]
    [SerializeField] private GameObject customHitVFX = null;
    [Tooltip("Audio clip played when swinging this weapon.")]
    [SerializeField] private AudioClip swingSoundEffect = null;

    [Header("Weapon Unleash (Ataque Especial Pasivo / Artefacto)")]
    [SerializeField] private bool hasUnleash = false;
    [Range(0f, 1f)]
    [Tooltip("Base probability (0.35 = 35% in Golden Sun) of triggering the unleash on a basic physical attack.")]
    [SerializeField] private float unleashProcRate = 0.35f;
    [SerializeField] private WeaponUnleashSO unleashEffect = null;

    [Header("Weapon Stats Extra")]
    [Tooltip("Increases Unleash proc chance / critical hit rate in combat formulas.")]
    [SerializeField] private int criticalRateBonus = 0;

    #region Public Properties

    public GameObject WeaponModelPrefab => weaponModelPrefab;

    public HitFXType HitFXType => hitFXType;
    public GameObject CustomHitVFX => customHitVFX;
    public AudioClip SwingSoundEffect => swingSoundEffect;

    public bool HasUnleash => hasUnleash;
    public float UnleashProcRate => unleashProcRate;
    public WeaponUnleashSO UnleashEffect => unleashEffect;

    public int CriticalRateBonus => criticalRateBonus;

    #endregion
}