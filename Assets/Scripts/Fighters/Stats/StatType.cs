public enum StatType
{
    // Basic Core Stats
    MaxHP,
    MaxPP,
    Attack,
    Defense,
    Agility,
    Luck,

    // Elemental Power (Golden Sun Mechanics)
    PowerVenus,
    PowerMars,
    PowerJupiter,
    PowerMercury,

    // Elemental Resistance (Golden Sun Mechanics)
    ResistVenus,
    ResistMars,
    ResistJupiter,
    ResistMercury
}

public enum StatModifierType
{
    Flat,         // Direct integer value (+10 Attack)
    PercentBase,  // Percentage calculated strictly over BaseValue (+10% of BaseValue)
    PercentTotal  // Percentage calculated over the subtotal sum (Base + PercentBase + Flat)
}