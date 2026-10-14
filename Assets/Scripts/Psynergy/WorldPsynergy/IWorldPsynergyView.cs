using System;
using UnityEngine;

/// <summary>
/// Contract for any visual representation of a field Psynergy in the world (Hand 3D, Area VFX, Auras, etc.).
/// Defines a unified execution interface for both targeted and targetless field casts.
/// </summary>
public interface IWorldPsynergyView
{
    /// <summary>
    /// Executes the visual sequence for a specific field Psynergy.
    /// </summary>
    /// <param name="psynergy">The ScriptableObject definition of the casted Psynergy.</param>
    /// <param name="casterTransform">Transform of the player casting the Psynergy.</param>
    /// <param name="target">Target object in the world (null if targetless cast).</param>
    /// <param name="onImpactOrGrasp">Callback fired at the key frame (e.g., grabbing, pushing, or impact moment).</param>
    /// <param name="onFinished">Callback fired when the entire sequence completes and the view disappears.</param>
    void Execute(
        PsynergySO psynergy,
        Transform casterTransform,
        IPsynergyTargetable target,
        Action onImpactOrGrasp,
        Action onFinished);
}