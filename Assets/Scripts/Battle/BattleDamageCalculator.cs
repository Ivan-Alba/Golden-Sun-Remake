using UnityEngine;

public static class BattleDamageCalculator
{
    /// <summary>
    /// Calculates standard physical attack damage following Golden Sun's formula: 
    /// (Attack - Defense) / 2 * ElementalFactor * Variance.
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

        // Small damage variance factor (+/- 10%)
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
    /// Calculates Psynergy attack damage based on AttackPsynergySO configuration.
    /// Supports both Elemental Power (magical) and Physical Attack (physical-elemental) scaling,
    /// as well as diminishing area damage spread.
    /// </summary>
    /// <param name="attacker">Fighter casting the Psynergy.</param>
    /// <param name="defender">Fighter receiving the Psynergy.</param>
    /// <param name="psynergy">Attack Psynergy ScriptableObject containing spell metadata.</param>
    /// <param name="targetDistance">Distance from primary target (0 = primary, 1 = adjacent, 2 = outer).</param>
    public static int CalculateAttackPsynergyDamage(
        FighterStats attacker,
        FighterStats defender,
        AttackPsynergySO psynergy,
        int targetDistance = 0)
    {
        if (attacker == null || defender == null || psynergy == null) return 0;

        float baseDamage = 0f;

        if (psynergy.ScalingType == AttackScalingType.PhysicalAttack)
        {
            // Physical-Elemental Formula: ((Attack * Multiplier - Defense) / 2) + BasePower
            float scaledAttack = attacker.Attack * psynergy.DamageMultiplier;
            float physicalBase = Mathf.Max(1f, (scaledAttack - defender.Defense) / 2f);
            baseDamage = physicalBase + psynergy.BasePower;
        }
        else
        {
            // Pure Magical Formula: Uses Spell Base Power directly
            baseDamage = psynergy.BasePower;
        }

        // Elemental Modifier (Power vs Resistance)
        float elementalModifier = GetElementalModifier(attacker, defender, psynergy.Element);

        // Area Damage Spread Reduction (Golden Sun area damage formula)
        float spreadMultiplier = psynergy.HasDiminishingSpread ? GetSpreadMultiplier(targetDistance) : 1.0f;

        // Small damage variance factor (+/- 10%)
        float variance = Random.Range(0.9f, 1.1f);

        int finalDamage = Mathf.RoundToInt(baseDamage * elementalModifier * spreadMultiplier * variance);

        // Apply defense status reduction (50% reduction if defending)
        if (defender.TryGetComponent<FighterController>(out var defController) && defController.IsDefending)
        {
            finalDamage = Mathf.Max(1, finalDamage / 2);
        }

        return Mathf.Max(1, finalDamage);
    }

    /// <summary>
    /// Generic magical Psynergy damage calculation for raw spell power values.
    /// </summary>
    public static int CalculateRawPsynergyDamage(
        FighterStats attacker,
        FighterStats defender,
        int spellPower,
        ElementalType element,
        int targetDistance = 0)
    {
        if (attacker == null || defender == null) return 0;

        float elementalModifier = GetElementalModifier(attacker, defender, element);
        float spreadMultiplier = GetSpreadMultiplier(targetDistance);
        float variance = Random.Range(0.9f, 1.1f);

        int finalDamage = Mathf.RoundToInt(spellPower * elementalModifier * spreadMultiplier * variance);

        if (defender.TryGetComponent<FighterController>(out var defController) && defController.IsDefending)
        {
            finalDamage = Mathf.Max(1, finalDamage / 2);
        }

        return Mathf.Max(1, finalDamage);
    }

    /// <summary>
    /// Calculates the area damage multiplier based on target distance from the primary target.
    /// Distance 0 = 100%, Distance 1 = ~70%, Distance 2+ = ~40%.
    /// </summary>
    public static float GetSpreadMultiplier(int targetDistance)
    {

        switch (targetDistance)
        {
            case 0: return 1.0f;  // Main target receives 100%
            case 1: return 0.7f;  // Immediate adjacent targets receive ~70%
            case 2: return 0.4f;  // Outer targets receive ~40%
            default: return 0.25f; // Distant targets receive 25% minimum
        }
    }

    /// <summary>
    /// Evaluates physical critical hit chance based on weapon rate and luck.
    /// </summary>
    public static bool CheckCriticalHit(int weaponCritRate, int attackerLuck)
    {
        int chancePercentage = weaponCritRate + (attackerLuck / 2);
        int roll = Random.Range(0, 100);
        return roll < chancePercentage;
    }

    /// <summary>
    /// Calculates the elemental modifier factor following Golden Sun's formula: 
    /// 1 + (Attacker Power - Defender Resistance) / 400.
    /// </summary>
    public static float GetElementalModifier(FighterStats attacker, FighterStats defender, ElementalType element)
    {
        if (element == ElementalType.None) return 1.0f;

        StatType powerType;
        StatType resistType;

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
}