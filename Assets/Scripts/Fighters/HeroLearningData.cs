using System;
using System.Collections.Generic;

[System.Serializable]
public class HeroAbility
{
    public string name;
    public HeroAbilityItem[] abilities;

    public string GetAbilityName(int level)
    {
        // Buscar la habilidad correspondiente al nivel.
        foreach (HeroAbilityItem ability in abilities)
        {
            if (ability.level == level)
            {
                return ability.name;
            }
        }

        return null;
    }
}

[System.Serializable]
public class HeroAbilityItem
{
    public int level;
    public string name;
}

[System.Serializable]
public class HeroAbilitiesData
{
    public HeroAbility[] heroes;

    public HeroAbility GetHeroAbility(string name)
    {
        // Buscar el héroe por nombre.
        foreach (HeroAbility hero in heroes)
        {
            if (hero.name == name)
            {
                return hero;
            }
        }

        return null;
    }
}