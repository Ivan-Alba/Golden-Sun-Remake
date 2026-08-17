using UnityEngine;

/// <summary>
/// Interface implemented by any world object that can be targeted or interacted with using Psynergy.
/// </summary>
public interface IPsynergyTargetable
{
    Transform Transform { get; }

    bool CanBeTargeted();

    /// <summary>
    /// Checks if this specific object responds to the given PsynergySO.
    /// </summary>
    bool IsCompatibleWith(PsynergySO psynergy);

    /// <summary>
    /// Executes the interaction logic if the Psynergy matches.
    /// </summary>
    bool ExecutePsynergy(PsynergySO psynergy, GameObject caster);

    void OnTargetGained();
    void OnTargetLost();
}