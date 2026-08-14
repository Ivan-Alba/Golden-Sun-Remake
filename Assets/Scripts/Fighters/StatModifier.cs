using System;

[Serializable]
public class StatModifier
{
    public float Value { get; }
    public StatModifierType Type { get; }
    public object Source { get; }
    public bool IsTemporary { get; }
    public int DurationInTurns { get; private set; }

    /// <summary>
    /// Creates a new stat modifier.
    /// </summary>
    /// <param name="value">The numerical value (positive or negative).</param>
    /// <param name="type">Flat, PercentBase, or PercentTotal calculation type.</param>
    /// <param name="source">Origin object causing this modifier (e.g., equipment, spell, Djinn).</param>
    /// <param name="isTemporary">True if the modifier should be cleared when battle ends.</param>
    /// <param name="durationInTurns">Duration in turns. Set to <= 0 for infinite/battle-long duration.</param>
    public StatModifier(float value, StatModifierType type, object source = null, bool isTemporary = false, int durationInTurns = -1)
    {
        Value = value;
        Type = type;
        Source = source;
        IsTemporary = isTemporary;
        DurationInTurns = durationInTurns;
    }

    /// <summary>
    /// Decrements the remaining turn duration if applicable.
    /// </summary>
    /// <returns>True if the modifier has expired and should be removed; otherwise false.</returns>
    public bool TickTurn()
    {
        if (!IsTemporary || DurationInTurns <= 0) return false;

        DurationInTurns--;
        return DurationInTurns <= 0;
    }
}