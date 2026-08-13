using UnityEngine;

/// <summary>
/// Interface implemented by any world object that can be targeted or interacted with using Psynergy.
/// </summary>
public interface IPsynergyTargetable
{
    Transform Transform { get; }

    /// <summary>
    /// Checks if the object can currently be targeted by the aim system.
    /// </summary>
    bool CanBeTargeted();

    /// <summary>
    /// Checks if this specific object responds to the given Psynergy.
    /// </summary>
    bool IsCompatibleWith(Psynergy psynergy);

    /// <summary>
    /// Executes the interaction logic if the Psynergy matches.
    /// Returns true if the Psynergy successfully triggered a reaction.
    /// </summary>
    bool ExecutePsynergy(Psynergy psynergy, GameObject caster);

    void OnTargetGained();
    void OnTargetLost();
}