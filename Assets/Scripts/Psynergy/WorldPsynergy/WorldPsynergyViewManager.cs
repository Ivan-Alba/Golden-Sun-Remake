using System;
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
/// Decouples game logic from visual representation.
/// </summary>
public class WorldPsynergyViewManager : MonoBehaviour
{
    #region Serialized Fields & Views

    [Header("Registered Visual Views")]
    [SerializeField] private WorldPsynergyHandView handPsynergyView;

    // Futuras vistas irán aquí asignadas desde la Inspector (ej. AreaPsynergyView, AuraPsynergyView)

    #endregion

    #region Public Execution API

    /// <summary>
    /// Evaluates the Psynergy definition and plays its corresponding visual presentation strategy.
    /// </summary>
    public void PlayPsynergyVisuals(
        PsynergySO psynergy,
        Transform casterTransform,
        IPsynergyTargetable target,
        Action onImpactOrGrasp,
        Action onFinished)
    {
        if (psynergy == null || casterTransform == null) return;

        WorldPsynergyVisualType visualType = DetermineVisualType(psynergy.PsynergySlug);

        switch (visualType)
        {
            case WorldPsynergyVisualType.Hand3D:
                if (handPsynergyView != null)
                {
                    handPsynergyView.Execute(psynergy, casterTransform, target, onImpactOrGrasp, onFinished);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(WorldPsynergyViewManager)}] HandPsynergyView is missing in inspector! Executing fallback callbacks.");
                    onImpactOrGrasp?.Invoke();
                    onFinished?.Invoke();
                }
                break;

            case WorldPsynergyVisualType.AreaVFX:
            case WorldPsynergyVisualType.CasterAura:
                // Marcador de posición para futuras vistas visuales que no utilicen la mano
                Debug.Log($"[{nameof(WorldPsynergyViewManager)}] Visual type '{visualType}' not implemented yet. Triggering default callbacks.");
                onImpactOrGrasp?.Invoke();
                onFinished?.Invoke();
                break;
        }
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