using UnityEngine;

public static class BattleDamageCalculator
{
    /// <summary>
    /// Calculates physical attack damage following Golden Sun's formula: (Attack - Defense) / 2 * ElementalFactor * Variance.
    /// Note: Attacker's total Attack already includes weapon modifiers from HeroEquipment.
    /// </summary>
    public static int CalculatePhysicalDamage(
        FighterStats attacker,
        FighterStats defender,
        ElementalType attackElement = ElementalType.None,
        bool isCritical = false,
        int bonusPower = 0)
    {
        if (attacker == null || defender == null) return 0;

        float attackVal = attacker.Attack;
        float defenseVal = defender.Defense;

        // Base Damage = (Attacker Total Attack - Defender Defense) / 2 + Bonus Power
        float baseDamage = Mathf.Max(1f, (attackVal - defenseVal) / 2f) + bonusPower;

        // Critical Hit Multiplier (1.5x damage in Golden Sun)
        if (isCritical)
        {
            baseDamage *= 1.5f;
        }

        // Elemental Power vs Resistance multiplier
        float elementalModifier = GetElementalModifier(attacker, defender, attackElement);

        // Small damage variance factor (+/- 10%) to prevent static values
        float variance = Random.Range(0.9f, 1.1f);

        int finalDamage = Mathf.RoundToInt(baseDamage * elementalModifier * variance);

        // Apply defense status reduction (50% reduction if defending)
        if (defender.TryGetComponent<FighterController>(out var defController) && defController.IsDefending)
        {
            finalDamage = Mathf.Max(1, finalDamage / 2);
        }

        return Mathf.Max(1, finalDamage);
    }

    /// <summary>
    /// Calculates Psynergy magical damage based on Spell Base Power and Elemental Power/Resistance difference.
    /// Formula: SpellPower * ElementalFactor * Variance
    /// </summary>
    public static int CalculatePsynergyDamage(
        FighterStats attacker,
        FighterStats defender,
        int spellPower,
        ElementalType element)
    {
        if (attacker == null || defender == null) return 0;

        float elementalModifier = GetElementalModifier(attacker, defender, element);

        // Small damage variance factor (+/- 10%)
        float variance = Random.Range(0.9f, 1.1f);

        int finalDamage = Mathf.RoundToInt(spellPower * elementalModifier * variance);

        if (defender.TryGetComponent<FighterController>(out var defController) && defController.IsDefending)
        {
            finalDamage = Mathf.Max(1, finalDamage / 2);
        }

        return Mathf.Max(1, finalDamage);
    }

    /// <summary>
    /// Overload for PsynergyDamage taking Psynergy.Elements for backward compatibility.
    /// </summary>
    public static int CalculatePsynergyDamage(
        FighterStats attacker,
        FighterStats defender,
        int spellPower,
        Psynergy.Elements element)
    {
        return CalculatePsynergyDamage(attacker, defender, spellPower, ConvertPsynergyElementToElementalType(element));
    }

    public static bool CheckCriticalHit(int weaponCritRate, int attackerLuck)
    {
        int chancePercentage = weaponCritRate + (attackerLuck / 2);
        int roll = Random.Range(0, 100);
        return roll < chancePercentage;
    }

    public static float GetElementalModifier(FighterStats attacker, FighterStats defender, ElementalType element)
    {
        if (element == ElementalType.None) return 1.0f;

        StatType powerType = StatType.PowerVenus;
        StatType resistType = StatType.ResistVenus;

        switch (element)
        {
            case ElementalType.Venus:
                powerType = StatType.PowerVenus;
                resistType = StatType.ResistVenus;
                break;
            case ElementalType.Mars:
                powerType = StatType.PowerMars;
                resistType = StatType.ResistMars;
                break;
            case ElementalType.Jupiter:
                powerType = StatType.PowerJupiter;
                resistType = StatType.ResistJupiter;
                break;
            case ElementalType.Mercury:
                powerType = StatType.PowerMercury;
                resistType = StatType.ResistMercury;
                break;
            default:
                return 1.0f;
        }

        int power = attacker.GetStatValue(powerType);
        int resistance = defender.GetStatValue(resistType);

        // Golden Sun Elemental Factor Formula: 1 + (Attacker Power - Defender Resistance) / 400
        float factor = 1.0f + ((power - resistance) / 400.0f);
        return Mathf.Max(0.2f, factor); // Minimum 20% damage guaranteed
    }

    public static ElementalType ConvertPsynergyElementToElementalType(Psynergy.Elements elem)
    {
        switch (elem)
        {
            case Psynergy.Elements.Venus: return ElementalType.Venus;
            case Psynergy.Elements.Mars: return ElementalType.Mars;
            case Psynergy.Elements.Jupiter: return ElementalType.Jupiter;
            case Psynergy.Elements.Mercury: return ElementalType.Mercury;
            default: return ElementalType.None;
        }
    }
}