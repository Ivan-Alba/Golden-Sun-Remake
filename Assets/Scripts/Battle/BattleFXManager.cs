using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum VFXSpawnLocation
{
    TargetFeet,
    TargetCenter,
    CasterPosition,
    AreaCenter,
    ScreenCenter
}

/// <summary>
/// Domain controller responsible for spawning, positioning, synchronizing,
/// and managing the lifecycle of battle visual and audio FX based on BattleActionFXConfig.
/// </summary>
public class BattleFXManager : MonoBehaviour
{
    #region Serialized & Singleton Dependencies

    [Header("Arena Area Anchors")]
    [Tooltip("Transform anchor placed at the spatial center of the Hero party formation.")]
    [SerializeField] private Transform heroRowAnchor;

    [Tooltip("Transform anchor placed at the spatial center of the Enemy formation.")]
    [SerializeField] private Transform enemyRowAnchor;

    [Tooltip("Transform anchor placed at the absolute center of the Battle Arena.")]
    [SerializeField] private Transform screenCenterAnchor;

    [Header("Default Hit FX Prefabs")]
    [SerializeField] private GameObject defaultSlashHitVFX;
    [SerializeField] private GameObject defaultBluntHitVFX;
    [SerializeField] private GameObject defaultPiercingHitVFX;

    [SerializeField] private GameObject defaultPhysicalVenusHitVFX;
    [SerializeField] private GameObject defaultPhysicalMarsHitVFX;
    [SerializeField] private GameObject defaultPhysicalJupiterHitVFX;
    [SerializeField] private GameObject defaultPhysicalMercuryHitVFX;
    [SerializeField] private GameObject defaultMagicalVenusHitVFX;
    [SerializeField] private GameObject defaultMagicalMarsHitVFX;
    [SerializeField] private GameObject defaultMagicalJupiterHitVFX;
    [SerializeField] private GameObject defaultMagicalMercuryHitVFX;

    [SerializeField] private GameObject defaultHealingHitVFX;
    [SerializeField] private GameObject fallbackHitVFX;

    #endregion

    #region Public Aura & Cast FX API

    /// <summary>
    /// Triggers the appropriate physical hit FX for a basic attack using the weapon's configured HitFXType.
    /// </summary>
    public void PlayBasicAttackHitFX(GameObject target, HitFXType hitFXType)
    {
        if (target == null) return;

        PlayTargetImpactFX(hitFXType, null, new List<GameObject> { target });
    }

    /// <summary>
    /// Spawns an aura/circle VFX attached to the caster, preserving the prefab's local transform defaults.
    /// </summary>
    public GameObject PlayCasterAura(Transform casterTransform, GameObject auraPrefab, float autoDestroyDelay = 2.5f)
    {
        if (casterTransform == null || auraPrefab == null) return null;

        GameObject instance = Instantiate(auraPrefab, casterTransform);
        Destroy(instance, autoDestroyDelay);
        return instance;
    }

    #endregion

    #region Public Target Impact & Main VFX API

    /// <summary>
    /// Spawns the main sequence VFX attached to the calculated parent Transform anchor (Target, Row, or Screen).
    /// </summary>
    public GameObject PlayMainActionVFX(
        GameObject mainVfxPrefab,
        GameObject primaryTarget,
        List<GameObject> areaTargets,
        VFXSpawnLocation spawnLocation = VFXSpawnLocation.TargetCenter,
        float overrideDestroyDelay = -1f)
    {
        if (mainVfxPrefab == null) return null;

        Transform spawnTransform = CalculateSpawnPosition(primaryTarget, areaTargets, spawnLocation);

        GameObject vfxInstance = Instantiate(mainVfxPrefab, spawnTransform);

        float destroyDelay = overrideDestroyDelay > 0f ? overrideDestroyDelay : GetVFXDuration(vfxInstance);
        Destroy(vfxInstance, destroyDelay);

        return vfxInstance;
    }

    /// <summary>
    /// Spawns impact hit VFX (standardized HitFXType or Custom prefab) at the damage anchor position of each target.
    /// </summary>
    public List<GameObject> PlayTargetImpactFX(
        HitFXType hitFXType,
        GameObject customHitVFX,
        List<GameObject> targetObjects,
        float staggerDelay = 0.0f,
        float overrideDestroyDelay = -1f)
    {
        List<GameObject> spawnedInstances = new List<GameObject>();
        if (targetObjects == null || targetObjects.Count == 0) return spawnedInstances;

        GameObject selectedPrefab = ResolveHitFXPrefab(hitFXType, customHitVFX);
        if (selectedPrefab == null) return spawnedInstances;

        StartCoroutine(ExecuteStaggeredHitFX(selectedPrefab, hitFXType, targetObjects, staggerDelay, overrideDestroyDelay, spawnedInstances));

        return spawnedInstances;
    }

    #endregion

    #region High-Level FX Config Executor

    /// <summary>
    /// Master sequence coroutine that interprets BattleActionFXConfig, playing audio, main VFX, 
    /// and target hit FX with exact synchronization timing.
    /// </summary>
    public IEnumerator ExecuteFXConfigSequence(
        BattleActionFXConfig fxConfig,
        FighterController caster,
        GameObject primaryTarget,
        List<GameObject> areaTargets,
        System.Action onImpactFrame = null)
    {
        // 1. Trigger Caster Animation & Aura
        if (caster != null)
        {
            if (fxConfig.CasterAnimation != CasterAnimationType.None)
            {
                caster.PlayCasterAnimation(fxConfig.CasterAnimation);
            }

            if (fxConfig.CasterAuraVFX != null)
            {
                PlayCasterAura(caster.transform, fxConfig.CasterAuraVFX);
            }
        }

        // Wait for casting setup phase
        if (fxConfig.FallbackCastDuration > 0)
        {
            yield return new WaitForSeconds(fxConfig.FallbackCastDuration);
        }

        // 2. Play Audio & SFX
        PlayAudioFromConfig(fxConfig);

        // 3. Spawn Main Action VFX
        if (fxConfig.MainActionVFX != null)
        {
            PlayMainActionVFX(fxConfig.MainActionVFX, primaryTarget, areaTargets, VFXSpawnLocation.TargetCenter);
        }

        // Wait before applying impact logic
        if (fxConfig.FallbackImpactDelay > 0)
        {
            yield return new WaitForSeconds(fxConfig.FallbackImpactDelay);
        }

        // 4. Fire Impact Event Callback (Apply Damage / Healing in Executor)
        onImpactFrame?.Invoke();

        // 5. Spawn Target Hit Effects (Slashes, Sparks, Cleanses)
        PlayTargetImpactFX(fxConfig.TargetHitFXType, fxConfig.CustomTargetHitVFX, areaTargets);
    }

    #endregion

    #region Helper & Utility Methods

    private void PlayAudioFromConfig(BattleActionFXConfig fxConfig)
    {
        if (fxConfig.DirectAudioClip != null)
        {
            GameEvents.Audio.RequestPlaySFXClip(fxConfig.DirectAudioClip);
        }
        else if (fxConfig.FallbackSFX != SFXType.None)
        {
            GameEvents.Audio.RequestPlaySFX(fxConfig.FallbackSFX);
        }
    }

    /// <summary>
    /// Resolves and triggers the associated impact SFX based on the hit FX category.
    /// </summary>
    private void PlayHitSFXForType(HitFXType hitFXType)
    {
        SFXType sfxToPlay = hitFXType switch
        {
            HitFXType.Slash => SFXType.Combat_HitSlash,
            HitFXType.Blunt => SFXType.Combat_HitBlunt,
            HitFXType.Piercing => SFXType.Combat_HitPierce,
            HitFXType.PhysicalVenus => SFXType.Combat_HitVenus,
            HitFXType.PhysicalMars => SFXType.Combat_HitMars,
            HitFXType.PhysicalJupiter => SFXType.Combat_HitJupiter,
            HitFXType.PhysicalMercury => SFXType.Combat_HitMercury,
            HitFXType.MagicalVenus => SFXType.Combat_MagicVenus,
            HitFXType.MagicalMars => SFXType.Combat_MagicMars,
            HitFXType.MagicalJupiter => SFXType.Combat_MagicJupiter,
            HitFXType.MagicalMercury => SFXType.Combat_MagicMercury,
            HitFXType.Healing => SFXType.Combat_Heal,
            _ => SFXType.Combat_HitBlunt
        };

        GameEvents.Audio.RequestPlaySFX(sfxToPlay);
    }

    private GameObject ResolveHitFXPrefab(HitFXType hitFXType, GameObject customHitVFX)
    {
        switch (hitFXType)
        {
            case HitFXType.Slash: return defaultSlashHitVFX;
            case HitFXType.Blunt: return defaultBluntHitVFX;
            case HitFXType.Piercing: return defaultPiercingHitVFX;
            case HitFXType.PhysicalVenus: return defaultPhysicalVenusHitVFX;
            case HitFXType.PhysicalMars: return defaultPhysicalMarsHitVFX;
            case HitFXType.PhysicalJupiter: return defaultPhysicalJupiterHitVFX;
            case HitFXType.PhysicalMercury: return defaultPhysicalMercuryHitVFX;
            case HitFXType.MagicalVenus: return defaultMagicalVenusHitVFX;
            case HitFXType.MagicalMars: return defaultMagicalMarsHitVFX;
            case HitFXType.MagicalJupiter: return defaultMagicalJupiterHitVFX;
            case HitFXType.MagicalMercury: return defaultMagicalMercuryHitVFX;
            case HitFXType.Healing: return defaultHealingHitVFX;
            case HitFXType.Custom: return customHitVFX;
            default: return fallbackHitVFX;
        }
    }

    /// <summary>
    /// Resolves the parent Transform anchor for Main VFX instantiations based on spawnLocation semantics.
    /// </summary>
    private Transform CalculateSpawnPosition(GameObject primaryTarget, List<GameObject> areaTargets, VFXSpawnLocation spawnLocation)
    {
        switch (spawnLocation)
        {
            case VFXSpawnLocation.TargetFeet:
            case VFXSpawnLocation.TargetCenter:
                return primaryTarget != null ? primaryTarget.transform : transform;

            case VFXSpawnLocation.AreaCenter:
                GameObject referenceTarget = primaryTarget ?? (areaTargets != null && areaTargets.Count > 0 ? areaTargets[0] : null);
                if (referenceTarget != null)
                {
                    bool isHero = referenceTarget.CompareTag("Hero") || referenceTarget.GetComponent<HeroStats>() != null;

                    if (isHero && heroRowAnchor != null)
                        return heroRowAnchor;
                    if (!isHero && enemyRowAnchor != null)
                        return enemyRowAnchor;
                }
                return primaryTarget != null ? primaryTarget.transform : transform;

            case VFXSpawnLocation.ScreenCenter:
                return screenCenterAnchor != null ? screenCenterAnchor : transform;

            case VFXSpawnLocation.CasterPosition:
                return transform;

            default:
                return primaryTarget != null ? primaryTarget.transform : transform;
        }
    }

    /// <summary>
    /// Spawns impact hit VFX at the explicit damage position defined in FighterBattleAnchors,
    /// triggering synchronized impact SFX per target.
    /// </summary>
    private IEnumerator ExecuteStaggeredHitFX(
        GameObject vfxPrefab,
        HitFXType hitFXType,
        List<GameObject> targetObjects,
        float staggerDelay,
        float overrideDestroyDelay,
        List<GameObject> spawnedInstances)
    {
        for (int i = 0; i < targetObjects.Count; i++)
        {
            GameObject target = targetObjects[i];
            if (target == null) continue;

            Vector3 spawnPos = target.TryGetComponent<FighterBattleAnchors>(out var anchors)
                ? anchors.GetDamageWorldPosition()
                : target.transform.position + Vector3.up * 1.5f;

            GameObject instance = Instantiate(vfxPrefab, spawnPos, vfxPrefab.transform.rotation);
            spawnedInstances.Add(instance);

            // Trigger synchronized impact SFX corresponding to this HitFXType
            PlayHitSFXForType(hitFXType);

            float destroyDelay = overrideDestroyDelay > 0f ? overrideDestroyDelay : GetVFXDuration(instance);
            Destroy(instance, destroyDelay);

            if (staggerDelay > 0f && i < targetObjects.Count - 1)
            {
                yield return new WaitForSeconds(staggerDelay);
            }
        }
    }

    private float GetVFXDuration(GameObject vfxInstance)
    {
        if (vfxInstance == null) return 3.0f;

        ParticleSystem ps = vfxInstance.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            return ps.main.duration + ps.main.startLifetime.constantMax;
        }

        ParticleSystem psChild = vfxInstance.GetComponentInChildren<ParticleSystem>();
        if (psChild != null)
        {
            return psChild.main.duration + psChild.main.startLifetime.constantMax;
        }

        return 3.0f; // Default fallback duration
    }

    #endregion
}