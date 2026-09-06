using UnityEngine;

/// <summary>
/// Interface implemented by any overworld object that can be detected by proximity,
/// targeted by the aiming reticle, or interacted with using Psynergy abilities (e.g., Move, Frost, Growth).
/// </summary>
public interface IPsynergyTargetable
{
    /// <summary>
    /// Gets the transform component of the targetable world object.
    /// </summary>
    Transform Transform { get; }

    /// <summary>
    /// Determines whether this object is currently eligible for targeting or interaction in its present state.
    /// </summary>
    /// <returns>True if the object can be targeted; otherwise, false.</returns>
    bool CanBeTargeted();

    /// <summary>
    /// Checks if this specific environmental object responds to the provided Psynergy ability data.
    /// </summary>
    /// <param name="psynergy">The PsynergySO asset representing the cast ability.</param>
    /// <returns>True if compatible; otherwise, false.</returns>
    bool IsCompatibleWith(PsynergySO psynergy);

    /// <summary>
    /// Executes the puzzle or world interaction logic if the cast Psynergy matches object requirements.
    /// </summary>
    /// <param name="psynergy">The PsynergySO asset used for interaction.</param>
    /// <param name="caster">The GameObject casting the Psynergy (usually the Player).</param>
    /// <returns>True if the interaction was successfully executed; otherwise, false.</returns>
    bool ExecutePsynergy(PsynergySO psynergy, GameObject caster);

    #region Proximity & Visual Feedback (Shader Highlights)

    /// <summary>
    /// Called when the player enters the proximity sensor range of this Psynergy object.
    /// Ideal for enabling visual feedback shaders (e.g., outline/glow effect).
    /// </summary>
    void OnProximityEntered();

    /// <summary>
    /// Called when the player exits the proximity sensor range of this Psynergy object.
    /// Ideal for disabling visual feedback shaders.
    /// </summary>
    void OnProximityExited();

    #endregion

    #region Aiming & Lock-On States

    /// <summary>
    /// Called when the aiming reticle focuses directly on this object.
    /// </summary>
    void OnTargetGained();

    /// <summary>
    /// Called when the aiming reticle loses focus from this object.
    /// </summary>
    void OnTargetLost();

    #endregion
}