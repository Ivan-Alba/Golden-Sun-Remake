using System;
using System.Collections;
using UnityEngine;

public enum WorldPsynergyVisualType
{
    Hand3D,      // Mover, Coger, Fuerza...
    AreaVFX,     // Lluvia, Revelar...
    CasterAura   // Lectura, Mente...
}

/// <summary>
/// Bridge manager responsible for routing field Psynergy visual execution requests
/// to their corresponding IWorldPsynergyView strategies (Hand3D, AreaVFX, CasterAura, etc.).
/// Manages caster aura particle VFX instantiation and lifecycle attached to the caster during field psynergy execution.
/// </summary>
public class WorldPsynergyViewManager : MonoBehaviour
{
    #region Serialized Fields & Views

    [Header("Caster Particle Effect Config")]
    [Tooltip("Particle system prefab instantiated around the caster during world Psynergy execution.")]
    [SerializeField] private GameObject casterAuraPrefab;

    [Tooltip("Offset applied to the instantiated caster aura relative to the caster's transform root.")]
    [SerializeField] private Vector3 auraSpawnOffset = new Vector3(0f, 0.1f, 0f);

    [Tooltip("Extra delay in seconds after stopping particle emission before destroying the instance.")]
    [SerializeField] private float vfxCleanupDelay = 1.0f;

    [Header("Registered Visual Views")]
    [SerializeField] private WorldPsynergyHandView handPsynergyView;

    #endregion

    #region Private Fields

    private GameObject currentAuraInstance;

    #endregion

    #region Public Execution API

    /// <summary>
    /// Evaluates the Psynergy definition, instantiates aura particle effects attached to the caster,
    /// and plays its corresponding visual presentation strategy.
    /// </summary>
    public void PlayPsynergyVisuals(
        PsynergySO psynergy,
        Transform casterTransform,
        IPsynergyTargetable target,
        Action onImpactOrGrasp,
        Action onFinished)
    {
        if (psynergy == null || casterTransform == null) return;

        // 1. Spawnea las partículas de casteo emparentadas al personaje
        SpawnCasterAura(casterTransform);

        // 2. Encapsula la llamada a onFinished para detener suavemente la emisión de partículas
        Action wrappedOnFinished = () =>
        {
            StopAndDestroyCasterAura();
            onFinished?.Invoke();
        };

        WorldPsynergyVisualType visualType = DetermineVisualType(psynergy.PsynergySlug);

        switch (visualType)
        {
            case WorldPsynergyVisualType.Hand3D:
                if (handPsynergyView != null)
                {
                    handPsynergyView.Execute(psynergy, casterTransform, target, onImpactOrGrasp, wrappedOnFinished);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(WorldPsynergyViewManager)}] HandPsynergyView is missing in inspector! Executing fallback callbacks.");
                    onImpactOrGrasp?.Invoke();
                    wrappedOnFinished.Invoke();
                }
                break;

            case WorldPsynergyVisualType.AreaVFX:
            case WorldPsynergyVisualType.CasterAura:
                Debug.Log($"[{nameof(WorldPsynergyViewManager)}] Visual type '{visualType}' not implemented yet. Triggering default callbacks.");
                onImpactOrGrasp?.Invoke();
                wrappedOnFinished.Invoke();
                break;
        }
    }

    /// <summary>
    /// Forcefully stops any active caster aura particles (useful on interruption or sequence cancels).
    /// </summary>
    public void StopCasterAura()
    {
        StopAndDestroyCasterAura();
    }

    #endregion

    #region Particle LifeCycle Helpers

    private void SpawnCasterAura(Transform casterTransform)
    {
        StopAndDestroyCasterAura(); // Limpia cualquier aura previa residual

        if (casterAuraPrefab != null && casterTransform != null)
        {
            Vector3 spawnPosition = casterTransform.position + auraSpawnOffset;
            currentAuraInstance = Instantiate(casterAuraPrefab, spawnPosition, casterTransform.rotation, casterTransform);
        }
    }

    private void StopAndDestroyCasterAura()
    {
        if (currentAuraInstance == null) return;

        GameObject instanceToDestroy = currentAuraInstance;
        currentAuraInstance = null; // Desvincula la referencia activa

        // Detiene la emisión de partículas suavemente manteniendo la jerarquía bajo el personaje
        ParticleSystem[] particleSystems = instanceToDestroy.GetComponentsInChildren<ParticleSystem>();
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // Destruye el GameObject tras consumir el tiempo de vida de las partículas lanzadas
        Destroy(instanceToDestroy, vfxCleanupDelay);
    }

    #endregion

    #region Helper Strategy Selector

    private WorldPsynergyVisualType DetermineVisualType(string psynergySlug)
    {
        string slug = psynergySlug != null ? psynergySlug.ToLowerInvariant() : string.Empty;

        switch (slug)
        {
            case "mover":
            case "move":
            case "coger":
            case "catch":
            case "fuerza":
            case "force":
                return WorldPsynergyVisualType.Hand3D;

            case "lluvia":
            case "rain":
            case "revelar":
            case "reveal":
                return WorldPsynergyVisualType.AreaVFX;

            case "lectura":
            case "mindread":
                return WorldPsynergyVisualType.CasterAura;

            default:
                return WorldPsynergyVisualType.Hand3D;
        }
    }

    #endregion
}