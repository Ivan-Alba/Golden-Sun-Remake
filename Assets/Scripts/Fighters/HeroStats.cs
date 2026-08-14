using System.Collections.Generic;
using UnityEngine;

public class HeroStats : FighterStats
{
    [Header("Hero Progression")]
    [SerializeField] private int level = 1;
    [SerializeField] private int currentXp = 0;

    private int nextLevelXp;

    public int Level { get => level; private set => level = value; }
    public int CurrentXp { get => currentXp; private set => currentXp = value; }
    public int NextLevelXp { get => nextLevelXp; private set => nextLevelXp = value; }

    protected override void Awake()
    {
        base.Awake();
        CalculateNextLevelXp();
    }

    private void CalculateNextLevelXp()
    {
        nextLevelXp = level * 125 + (125 * level / 2);
    }

    /// <summary>
    /// Adds experience to the hero, handles potential level-ups and returns a list of newly learned ability slugs.
    /// </summary>
    public List<string> AddExperience(int amount)
    {
        List<string> learnedAbilities = new List<string>();
        currentXp += amount;

        while (currentXp >= nextLevelXp)
        {
            string newAbility = LevelUp();
            if (!string.IsNullOrEmpty(newAbility))
            {
                learnedAbilities.Add(newAbility);
            }
        }

        return learnedAbilities;
    }

    public string LevelUp()
    {
        level++;
        currentXp -= nextLevelXp;
        CalculateNextLevelXp();

        return LearnAbility();
    }

    private string LearnAbility()
    {
        HeroAbilitiesData abilitiesData = JsonUtility.FromJson<HeroAbilitiesData>(
            Resources.Load<TextAsset>("JSON/HeroLearningData").text
        );

        if (abilitiesData == null) return null;

        HeroAbility heroAbility = abilitiesData.GetHeroAbility(fighterName);
        if (heroAbility != null)
        {
            string abilityName = heroAbility.GetAbilityName(level);
            if (!string.IsNullOrEmpty(abilityName))
            {
                Debug.Log($"{fighterName} learned {abilityName} at level {level}.");
                psynergies.Add(PsynergyDatabase.Instance.GetPsynergy(abilityName));
                return abilityName;
            }
        }

        return null;
    }

    public void LoadProgression(int savedLevel, int savedXp)
    {
        level = savedLevel;
        currentXp = savedXp;
        CalculateNextLevelXp();
    }

}