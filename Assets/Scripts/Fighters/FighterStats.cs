using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

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
    [SerializeField] Sprite avatar;
    [SerializeField] int xp;

    //LISTA ITEMS DROP

    //Heros
    int nextLevelXp;

    public string Name { get => _name; set => _name = value; }
    public int Level { get => level; set => level = value; }
    public int MaxHp { get => maxHp; set => maxHp = value; }
    public int CurrHp { get => currHp; set => currHp = value; }
    public int MaxPp { get => maxPp; set => maxPp = value; }
    public int CurrPp { get => currPp; set => currPp = value; }
    public int Strenght { get => strenght; set => strenght = value; }
    public int Defense { get => defense; set => defense = value; }
    public int Wisdom { get => wisdom; set => wisdom = value; }
    public int Spirit { get => spirit; set => spirit = value; }
    public int Agility { get => agility; set => agility = value; }
    public int Luck { get => luck; set => luck = value; }
    public int Xp { get => xp; set => xp = value; }
    public Sprite Avatar { get => avatar; set => avatar = value; }
    public int NextLevelXp { get => nextLevelXp; set => nextLevelXp = value; }
    public List<Psynergy> Psynergies { get => psynergies; set => psynergies = value; }
    public List<BaseStat> CharacterStats { get => characterStats; set => characterStats = value; }

    private void Awake()
    {
        NextLevelXp = level * 125 + (125 * level / 2);

    }

    private void Start()
    {
        CharacterStats.Add(new BaseStat(maxHp, ((Stats)0).ToString(), "Puntos de vida máximos"));
        CharacterStats.Add(new BaseStat(maxPp, ((Stats)2).ToString(), "Puntos psinergicos máximos"));
        CharacterStats.Add(new BaseStat(strenght, ((Stats)4).ToString(), "Aumenta el daño físico inflingido"));
        CharacterStats.Add(new BaseStat(defense, ((Stats)5).ToString(), "Disminuye el daño físico recibido"));
        CharacterStats.Add(new BaseStat(wisdom, ((Stats)6).ToString(), "Aumenta el daño mágico inflingido"));
        CharacterStats.Add(new BaseStat(spirit, ((Stats)7).ToString(), "Disminuye el daño mágico recibido"));
        CharacterStats.Add(new BaseStat(agility, ((Stats)8).ToString(), "Influye en la velocidad en batalla"));
        CharacterStats.Add(new BaseStat(luck, ((Stats)9).ToString(), "Influye en la capacidad de crítico"));

        psynergies.Add(PsynergyDatabase.Instance.GetPsynergy("Cure"));
        psynergies.Add(PsynergyDatabase.Instance.GetPsynergy("Flare"));
        psynergies.Add(PsynergyDatabase.Instance.GetPsynergy("Cure"));
        psynergies.Add(PsynergyDatabase.Instance.GetPsynergy("Flare")); 
        psynergies.Add(PsynergyDatabase.Instance.GetPsynergy("Cure"));
        psynergies.Add(PsynergyDatabase.Instance.GetPsynergy("Flare"));

    }

    public void AddConsumableEffect(string stat, int value)
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

    public void LevelUp()
    {
        //EXPERIENCIA
        level += 1;
        xp = 0;
        NextLevelXp = level * 125 + (125 * level / 2);
        //SUBIR STATS BASE


        //APRENDE ALGUNA HABILIDAD?
        LearnAbility();
    }

    private void LearnAbility()
    {
        switch (level)
        {
            case 2:
                psynergies.Add(PsynergyDatabase.Instance.GetPsynergy("Cure"));
                break;
        }
    }

}
