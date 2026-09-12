using System;
using UnityEngine;

/// <summary>
/// Attached to VFX prefabs to notify the BattleActionExecutor when critical visual moments occur
/// (e.g. projectile hit frame, explosion peak) via Unity Animation Events or Timeline.
/// </summary>
public class BattleVFXNotifier : MonoBehaviour
{
    public event Action OnVFXImpactTriggered;
    public event Action OnVFXSequenceCompleted;

    /// <summary>
    /// Invoked via Animation Event inside the VFX clip or Particle System callback.
    /// </summary>
    public void TriggerImpactEvent()
    {
        OnVFXImpactTriggered?.Invoke();
    }

    /// <summary>
    /// Invoked via Animation Event when the VFX finishes playing its active phase.
    /// </summary>
    public void TriggerSequenceCompleted()
    {
        OnVFXSequenceCompleted?.Invoke();
    }
}