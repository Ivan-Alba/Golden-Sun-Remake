/// <summary>
/// Defines the runtime tactical state of a Djinn.
/// </summary>
public enum DjinnState
{
    /// <summary>
    /// Djinn is equipped and active. Grants base stats, contributes to hero class, and enables leader passive if chosen.
    /// </summary>
    Set,

    /// <summary>
    /// Djinn has been unleashed or manually put on standby. Loses stat/class bonuses, but serves as a resource for Summons.
    /// </summary>
    Standby,

    /// <summary>
    /// Djinn was consumed by a Summon and is currently exhausted. Automatically recovers to Set over individual turns.
    /// </summary>
    Recovery
}