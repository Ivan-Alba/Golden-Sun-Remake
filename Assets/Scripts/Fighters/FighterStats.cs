using System;
using System.Collections.Generic;
using UnityEngine;

public class FighterStats : MonoBehaviour
{
    // Events for UI and system updates (passes current value and max value)
    public event Action<int, int> OnHpChanged;
    public event Action<int, int> OnPpChanged;

    [Header("Fighter Identity & Basic Info")]
    [SerializeField] protected string fighterName;
    [SerializeField] protected Sprite turnBarImage;

    [Header("Base Stats Configuration")]
    [SerializeField] protected int baseMaxHp = 100;
    [SerializeField] protected int baseMaxPp = 50;
    [SerializeField] protected int baseAttack = 20;
    [SerializeField] protected int baseDefense = 15;
    [SerializeField] protected int baseAgility = 10;
    [SerializeField] protected int baseLuck = 5;

    [Header("Elemental Power Base Stats")]
    [SerializeField] protected int basePowerVenus = 100;
    [SerializeField] protected int basePowerMars = 100;
    [SerializeField] protected int basePowerJupiter = 100;
    [SerializeField] protected int basePowerMercury = 100;

    [Header("Elemental Resistance Base Stats")]
    [SerializeField] protected int baseResistVenus = 100;
    [SerializeField] protected int baseResistMars = 100;
    [SerializeField] protected int baseResistJupiter = 100;
    [SerializeField] protected int baseResistMercury = 100;

    protected Dictionary<StatType, Stat> statsMap = new Dictionary<StatType, Stat>();
    protected List<PsynergySO> psynergies = new List<PsynergySO>();

    private int currHp;
    private int currPp;

    #region Properties - Identity & Resources

    public string Name { get => fighterName; set => fighterName = value; }
    public Sprite TurnBarImage { get => turnBarImage; set => turnBarImage = value; }
    public List<PsynergySO> Psynergies { get => psynergies; set => psynergies = value; }

    public bool IsDead => currHp <= 0;

    public int CurrHp
    {
        get => currHp;
        set
        {
            int clampedHp = Mathf.Clamp(value, 0, MaxHp);
            if (currHp != clampedHp)
            {
                currHp = clampedHp;
                OnHpChanged?.Invoke(currHp, MaxHp);
            }
        }
    }

    public int CurrPp
    {
        get => currPp;
        set
        {
            int clampedPp = Mathf.Clamp(value, 0, MaxPp);
            if (currPp != clampedPp)
            {
                currPp = clampedPp;
                OnPpChanged?.Invoke(currPp, MaxPp);
            }
        }
    }

    #endregion

    #region Properties - Core Stats Accessors

    public int MaxHp => GetStatValue(StatType.MaxHP);
    public int MaxPp => GetStatValue(StatType.MaxPP);
    public int Attack => GetStatValue(StatType.Attack);
    public int Defense => GetStatValue(StatType.Defense);
    public int Agility => GetStatValue(StatType.Agility);
    public int Luck => GetStatValue(StatType.Luck);

    // Elemental Power Accessors
    public int PowerVenus => GetStatValue(StatType.PowerVenus);
    public int PowerMars => GetStatValue(StatType.PowerMars);
    public int PowerJupiter => GetStatValue(StatType.PowerJupiter);
    public int PowerMercury => GetStatValue(StatType.PowerMercury);

    // Elemental Resistance Accessors
    public int ResistVenus => GetStatValue(StatType.ResistVenus);
    public int ResistMars => GetStatValue(StatType.ResistMars);
    public int ResistJupiter => GetStatValue(StatType.ResistJupiter);
    public int ResistMercury => GetStatValue(StatType.ResistMercury);

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        InitializeStats();
    }

    protected virtual void Start()
    {
        // Set current resources to max on start
        currHp = MaxHp;
        currPp = MaxPp;

        // Force UI initial notification
        OnHpChanged?.Invoke(currHp, MaxHp);
        OnPpChanged?.Invoke(currPp, MaxPp);
    }

    #endregion

    #region Stats Initialization & Helper Methods

    protected virtual void InitializeStats()
    {
        statsMap.Clear();

        // Core Stats
        statsMap.Add(StatType.MaxHP, new Stat(StatType.MaxHP, baseMaxHp));
        statsMap.Add(StatType.MaxPP, new Stat(StatType.MaxPP, baseMaxPp));
        statsMap.Add(StatType.Attack, new Stat(StatType.Attack, baseAttack));
        statsMap.Add(StatType.Defense, new Stat(StatType.Defense, baseDefense));
        statsMap.Add(StatType.Agility, new Stat(StatType.Agility, baseAgility));
        statsMap.Add(StatType.Luck, new Stat(StatType.Luck, baseLuck));

        // Elemental Powers
        statsMap.Add(StatType.PowerVenus, new Stat(StatType.PowerVenus, basePowerVenus));
        statsMap.Add(StatType.PowerMars, new Stat(StatType.PowerMars, basePowerMars));
        statsMap.Add(StatType.PowerJupiter, new Stat(StatType.PowerJupiter, basePowerJupiter));
        statsMap.Add(StatType.PowerMercury, new Stat(StatType.PowerMercury, basePowerMercury));

        // Elemental Resistances
        statsMap.Add(StatType.ResistVenus, new Stat(StatType.ResistVenus, baseResistVenus));
        statsMap.Add(StatType.ResistMars, new Stat(StatType.ResistMars, baseResistMars));
        statsMap.Add(StatType.ResistJupiter, new Stat(StatType.ResistJupiter, baseResistJupiter));
        statsMap.Add(StatType.ResistMercury, new Stat(StatType.ResistMercury, baseResistMercury));
    }

    public Stat GetStat(StatType type)
    {
        if (statsMap.TryGetValue(type, out var stat))
        {
            return stat;
        }

        Debug.LogError($"[FighterStats] StatType '{type}' not found on {gameObject.name}.");
        return null;
    }

    public int GetStatValue(StatType type)
    {
        var stat = GetStat(type);
        return stat != null ? stat.Value : 0;
    }

    /// <summary>
    /// Changes the base value of a stat and automatically updates resources/UI if necessary.
    /// </summary>
    public void SetStatBaseValue(StatType type, int newBaseValue)
    {
        var stat = GetStat(type);
        if (stat != null)
        {
            stat.BaseValue = newBaseValue;

            if (type == StatType.MaxHP)
            {
                CurrHp = Mathf.Clamp(CurrHp, 0, MaxHp);
                OnHpChanged?.Invoke(CurrHp, MaxHp);
            }
            else if (type == StatType.MaxPP)
            {
                CurrPp = Mathf.Clamp(CurrPp, 0, MaxPp);
                OnPpChanged?.Invoke(CurrPp, MaxPp);
            }
        }
    }

    #endregion

    #region Health & Resource Operations

    public void RestoreHP(int amount)
    {
        if (IsDead || amount <= 0) return;
        CurrHp += amount;
    }

    public void RestorePP(int amount)
    {
        if (IsDead || amount <= 0) return;
        CurrPp += amount;
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0) return;
        CurrHp -= amount;
    }

    public void Revive(int healthAmount)
    {
        if (!IsDead) return;
        CurrHp = Mathf.Clamp(healthAmount, 1, MaxHp);
    }

    public void AddPermanentStatBoost(StatType type, int amount)
    {
        var stat = GetStat(type);
        if (stat != null)
        {
            stat.BaseValue += amount;

            if (type == StatType.MaxHP) CurrHp += amount;
            if (type == StatType.MaxPP) CurrPp += amount;
        }
    }

    #endregion

    #region Modifier Operations

    public void AddStatModifier(StatType type, StatModifier modifier)
    {
        var stat = GetStat(type);
        stat?.AddModifier(modifier);

        // Notify UI in case MaxHP/MaxPP changed via equipment/buffs
        if (type == StatType.MaxHP) OnHpChanged?.Invoke(CurrHp, MaxHp);
        if (type == StatType.MaxPP) OnPpChanged?.Invoke(CurrPp, MaxPp);
    }

    public void RemoveModifiersFromSource(object source)
    {
        foreach (var stat in statsMap.Values)
        {
            stat.RemoveAllModifiersFromSource(source);
        }

        OnHpChanged?.Invoke(CurrHp, MaxHp);
        OnPpChanged?.Invoke(CurrPp, MaxPp);
    }

    public void TickTurnModifiers()
    {
        foreach (var stat in statsMap.Values)
        {
            stat.TickTurnModifiers();
        }
    }

    public void RemoveAllTemporaryModifiers()
    {
        foreach (var stat in statsMap.Values)
        {
            stat.RemoveAllTemporaryModifiers();
        }
    }

    #endregion
}