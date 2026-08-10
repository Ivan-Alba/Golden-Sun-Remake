using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

[Serializable]
public class FighterStats : MonoBehaviour
{
    List<BaseStat> characterStats = new List<BaseStat>();
    List<Psynergy> psynergies = new List<Psynergy>();


    private enum Stats
    {
        HP,
        MAXHP,
        PP,
        MAXPP,
        STRENGHT,
        DEFENSE,
        WISDOM,
        SPIRIT,
        AGILITY,
        LUCK
    }

    public const int HP = 0;


    [Header("Fighter Base Stats")]
    [SerializeField] string _name;
    [SerializeField] int level;
    [SerializeField] int maxHp, currHp, maxPp, currPp, strenght, defense, wisdom, spirit, agility, luck;
    [SerializeField] Sprite turnBarImage;
    [SerializeField] int xp;

    //LISTA ITEMS DROP

    //Heros
    int nextLevelXp;

    public string Name { get => _name; set => _name = value; }
    public int Level { get => level; set => level = value; }
    public int MaxHp { get => characterStats[0].GetFinalValue(); set => maxHp = value; }
    public int CurrHp { get => currHp; set => currHp = value; }
    public int MaxPp { get => characterStats[1].GetFinalValue(); set => maxPp = value; }
    public int CurrPp { get => currPp; set => currPp = value; }
    public int Strenght { get => characterStats[2].GetFinalValue(); set => strenght = value; }
    public int Defense { get => characterStats[3].GetFinalValue(); set => defense = value; }
    public int Wisdom { get => characterStats[4].GetFinalValue(); set => wisdom = value; }
    public int Spirit { get => characterStats[5].GetFinalValue(); set => spirit = value; }
    public int Agility { get => characterStats[6].GetFinalValue(); set => agility = value; }
    public int Luck { get => characterStats[7].GetFinalValue(); set => luck = value; }
    public int Xp { get => xp; set => xp = value; }
    public Sprite TurnBarImage { get => turnBarImage; set => turnBarImage = value; }
    public int NextLevelXp { get => nextLevelXp; set => nextLevelXp = value; }
    public List<Psynergy> Psynergies { get => psynergies; set => psynergies = value; }
    public List<BaseStat> CharacterStats { get => characterStats; set => characterStats = value; }

    private void Awake()
    {
        NextLevelXp = level * 125 + (125 * level / 2);
        CharacterStats.Add(new BaseStat(maxHp, ((Stats)1).ToString(), "Puntos de vida máximos"));
        CharacterStats.Add(new BaseStat(maxPp, ((Stats)3).ToString(), "Puntos psinergicos máximos"));
        CharacterStats.Add(new BaseStat(strenght, ((Stats)4).ToString(), "Aumenta el daño físico inflingido"));
        CharacterStats.Add(new BaseStat(defense, ((Stats)5).ToString(), "Disminuye el daño físico recibido"));
        CharacterStats.Add(new BaseStat(wisdom, ((Stats)6).ToString(), "Aumenta el daño mágico inflingido"));
        CharacterStats.Add(new BaseStat(spirit, ((Stats)7).ToString(), "Disminuye el daño mágico recibido"));
        CharacterStats.Add(new BaseStat(agility, ((Stats)8).ToString(), "Influye en la velocidad en batalla"));
        CharacterStats.Add(new BaseStat(luck, ((Stats)9).ToString(), "Influye en la capacidad de crítico"));

      
    }

    private void Start()
    {
       /* psynergies.Add(PsynergyDatabase.Instance.GetPsynergy("Catch"));
        psynergies.Add(PsynergyDatabase.Instance.GetPsynergy("Cure"));
        psynergies.Add(PsynergyDatabase.Instance.GetPsynergy("Flare"));
        psynergies.Add(PsynergyDatabase.Instance.GetPsynergy("Cure"));
        psynergies.Add(PsynergyDatabase.Instance.GetPsynergy("Flare"));
        psynergies.Add(PsynergyDatabase.Instance.GetPsynergy("Cure"));
        psynergies.Add(PsynergyDatabase.Instance.GetPsynergy("Flare"));
       */
    }

    public void addStatBonus(string statName, int bonusValue) 
    {
        characterStats.Find(x => x.StatName == statName).AddStatBonus(new StatBonus(bonusValue));
    }

    public void RemoveStatBonus(string statName, int bonusValue)
    {
        characterStats.Find(x => x.StatName == statName).RemoveStatBonus(new StatBonus(bonusValue));
    }

    public void AddBonusEffect(string stat, int value)
    {
        switch (stat)
        {
            case "HP":
                CurrHp += value;
                if (CurrHp > MaxHp) currHp = MaxHp;
                break;
            case "MAXHP":

                break;
            case "PP":

                break;
            case "MAXPP":

                break;
            case "STRENGHT":

                break;
            case "DEFENSE":

                break;
            case "WISDOM":

                break;
            case "SPIRIT":

                break;
            case "AGILITY":

                break;
            case "LUCK":

                break;
        }
    }

    public string LevelUp()
    {
        //EXPERIENCIA
        level += 1;
        xp = 0;
        NextLevelXp = level * 125 + (125 * level / 2);
        //SUBIR STATS BASE


        //APRENDE ALGUNA HABILIDAD?

        string abilityLearned = LearnAbility();
        if (abilityLearned != null )
        {
            return abilityLearned;
        } else
        {
            return null;
        }
    }

    private string LearnAbility()
    {
        //Cargar Json
        HeroAbilitiesData abilitiesData = JsonUtility.FromJson<HeroAbilitiesData>(Resources.Load("JSON/HeroLearningData").ToString());

        // Buscar el héroe por nombre.
        HeroAbility heroAbility = abilitiesData.GetHeroAbility(_name);

        if (heroAbility != null)
        {
            // Buscar la habilidad correspondiente al nivel del héroe.
            string abilityName = heroAbility.GetAbilityName(level);

            if (abilityName != null)
            {
                //Debug.Log(_name + " aprenderá la habilidad " + abilityName + " en el nivel " + level + ".");
                psynergies.Add(PsynergyDatabase.Instance.GetPsynergy(abilityName));
                return abilityName;
            }
            else
            {
                //Debug.Log(_name + " no aprenderá ninguna habilidad en el nivel " + level + ".");
            }
        }
        else
        {
            //Debug.Log("El héroe " + _name + " no se encontró en los datos de habilidades.");
        }

        return null;

    }

}
