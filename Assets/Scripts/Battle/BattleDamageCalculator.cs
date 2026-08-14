using UnityEngine;

public static class BattleDamageCalculator
{
    /// <summary>
    /// Calculates physical attack damage following Golden Sun's formula: (Attack - Defense) / 2 * ElementalFactor
    /// </summary>
    public static int CalculatePhysicalDamage(FighterStats attacker, FighterStats defender, int basePower, Psynergy.Elements attackElement, bool isCritical = false)
    {
        if (attacker == null || defender == null) return 0;

        float attackVal = attacker.Attack;
        float defenseVal = defender.Defense;

        // Base Damage = (Attacker Attack - Defender Defense) / 2
        float baseDamage = Mathf.Max(1f, (attackVal - defenseVal) / 2f);

        // Add base power from action/weapon
        baseDamage += basePower;

        // Critical Hit Multiplier (Golden Sun doubles base power/damage)
        if (isCritical)
        {
            baseDamage *= 1.5f;
        }

        // Elemental Power vs Resistance multiplier
        float elementalModifier = GetElementalModifier(attacker, defender, attackElement);

        int finalDamage = Mathf.RoundToInt(baseDamage * elementalModifier);

        // Apply defense status reduction (50% reduction if defending)
        if (defender.TryGetComponent<FighterController>(out var defController) && defController.IsDefending)
        {
            finalDamage = Mathf.Max(1, finalDamage / 2);
        }

        return Mathf.Max(1, finalDamage);
    }

    /// <summary>
    /// Calculates Psynergy magical damage based on Spell Base Power and Elemental Power/Resistance difference.
    /// Formula: BasePower * (1 + (Power - Resistance) / 400)
    /// </summary>
    public static int CalculatePsynergyDamage(FighterStats attacker, FighterStats defender, int spellPower, Psynergy.Elements element)
    {
        if (attacker == null || defender == null) return 0;

        float elementalModifier = GetElementalModifier(attacker, defender, element);
        int finalDamage = Mathf.RoundToInt(spellPower * elementalModifier);

        if (defender.TryGetComponent<FighterController>(out var defController) && defController.IsDefending)
        {
            finalDamage = Mathf.Max(1, finalDamage / 2);
        }

        return Mathf.Max(1, finalDamage);
    }

    public static bool CheckCriticalHit(int weaponCritRate, int attackerLuck)
    {
        int chancePercentage = weaponCritRate + (attackerLuck / 2);
        int roll = Random.Range(0, 100);
        return roll < chancePercentage;
    }

    private static float GetElementalModifier(FighterStats attacker, FighterStats defender, Psynergy.Elements element)
    {
        if (element == Psynergy.Elements.NoElemental) return 1.0f;

        StatType powerType = StatType.PowerVenus;
        StatType resistType = StatType.ResistVenus;

        switch (element)
        {
            case Psynergy.Elements.Venus:
                powerType = StatType.PowerVenus;
                resistType = StatType.ResistVenus;
                break;
            case Psynergy.Elements.Mars:
                powerType = StatType.PowerMars;
                resistType = StatType.ResistMars;
                break;
            case Psynergy.Elements.Jupiter:
                powerType = StatType.PowerJupiter;
                resistType = StatType.ResistJupiter;
                break;
            case Psynergy.Elements.Mercury:
                powerType = StatType.PowerMercury;
                resistType = StatType.ResistMercury;
                break;
        }

        int power = attacker.GetStatValue(powerType);
        int resistance = defender.GetStatValue(resistType);

        // Golden Sun Elemental Factor Formula: 1 + (Attacker Power - Defender Resistance) / 400
        float factor = 1.0f + ((power - resistance) / 400.0f);
        return Mathf.Max(0.2f, factor); // Minimum 20% damage guaranteed
    }
}