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


    #endregion

    #region Public Aura & Cast FX API

    /// <summary>
    /// Spawns an aura VFX at the caster's feet and manages auto-destruction.
    /// </summary>
    public GameObject PlayCasterAura(Transform casterTransform, GameObject auraPrefab, float autoDestroyDelay = 3.0f)
    {
        if (casterTransform == null || auraPrefab == null) return null;

        GameObject auraInstance = Instantiate(auraPrefab, casterTransform.position, casterTransform.rotation, casterTransform);
        Destroy(auraInstance, autoDestroyDelay);
        return auraInstance;
    }

    /// <summary>
    /// Requests the elemental circle VFX from Database and instantiates it at the caster's position.
    /// </summary>
    public void PlayElementalCastCircle(Transform casterTransform, ElementalType element, float autoDestroyDelay = 2.5f)
    {
        if (casterTransform == null) return;

        GameEvents.Database.RequestElementVFX(element, (vfxPrefab) =>
        {
            if (vfxPrefab != null)
            {
                GameObject instance = Instantiate(vfxPrefab, casterTransform.position, Quaternion.identity, casterTransform);
                Destroy(instance, autoDestroyDelay);
            }
        });
    }

    #endregion

    #region Public Target Impact & Main VFX API

    /// <summary>
    /// Spawns the main sequence VFX at the designated location (Feet, Center, Area, etc.).
    /// </summary>
    public GameObject PlayMainActionVFX(
        GameObject mainVfxPrefab,
        GameObject primaryTarget,
        List<GameObject> areaTargets,
        VFXSpawnLocation spawnLocation = VFXSpawnLocation.TargetCenter,
        float overrideDestroyDelay = -1f)
    {
        if (mainVfxPrefab == null) return null;

        Vector3 spawnPosition = CalculateSpawnPosition(primaryTarget, areaTargets, spawnLocation);
        Quaternion spawnRotation = Quaternion.identity;

        GameObject vfxInstance = Instantiate(mainVfxPrefab, spawnPosition, spawnRotation);

        float destroyDelay = overrideDestroyDelay > 0f ? overrideDestroyDelay : GetVFXDuration(vfxInstance);
        Destroy(vfxInstance, destroyDelay);

        return vfxInstance;
    }

    /// <summary>
    /// Spawns impact hit VFX (standardized HitFXType or Custom prefab) on all target transforms.
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

        StartCoroutine(ExecuteStaggeredHitFX(selectedPrefab, targetObjects, staggerDelay, overrideDestroyDelay, spawnedInstances));

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
            default: return null;
        }
    }

    private Vector3 CalculateSpawnPosition(GameObject primaryTarget, List<GameObject> areaTargets, VFXSpawnLocation spawnLocation)
    {
        if (primaryTarget != null && (spawnLocation == VFXSpawnLocation.TargetCenter || spawnLocation == VFXSpawnLocation.TargetFeet))
        {
            Vector3 targetPos = primaryTarget.transform.position;
            if (spawnLocation == VFXSpawnLocation.TargetCenter && primaryTarget.TryGetComponent<Collider>(out var col))
            {
                targetPos = col.bounds.center;
            }
            return targetPos;
        }

        if (areaTargets != null && areaTargets.Count > 0 && spawnLocation == VFXSpawnLocation.AreaCenter)
        {
            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (var target in areaTargets)
            {
                if (target != null)
                {
                    center += target.transform.position;
                    count++;
                }
            }
            return count > 0 ? center / count : Vector3.zero;
        }

        return primaryTarget != null ? primaryTarget.transform.position : Vector3.zero;
    }

    private IEnumerator ExecuteStaggeredHitFX(
        GameObject vfxPrefab,
        List<GameObject> targets,
        float staggerDelay,
        float overrideDestroyDelay,
        List<GameObject> spawnedInstances)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            GameObject target = targets[i];
            if (target == null) continue;

            Vector3 spawnPos = target.transform.position;
            if (target.TryGetComponent<Collider>(out var col))
            {
                spawnPos = col.bounds.center;
            }

            GameObject instance = Instantiate(vfxPrefab, spawnPos, Quaternion.identity);
            spawnedInstances.Add(instance);

            float destroyDelay = overrideDestroyDelay > 0f ? overrideDestroyDelay : GetVFXDuration(instance);
            Destroy(instance, destroyDelay);

            if (staggerDelay > 0f && i < targets.Count - 1)
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