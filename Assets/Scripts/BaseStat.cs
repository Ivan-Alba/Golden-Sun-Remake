using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BaseStat
{
    private List<StatBonus> baseAdditives;
    private int baseValue, finalValue;
    private string statName, statDescription;

    public string StatName { get => statName; set => statName = value; }

    public BaseStat(int baseValue, string statName, string statDescription)
    {
        this.baseAdditives = new List<StatBonus>();
        this.baseValue = baseValue;
        this.StatName = statName;
        this.statDescription = statDescription;
    }

    public void AddStatBonus(StatBonus statBonus)
    {
        this.baseAdditives.Add(statBonus);
    }

    public void RemoveStatBonus(StatBonus statBonus)
    {
        StatBonus itemToRemove = this.baseAdditives.FindLast(x => x.BonusValue == statBonus.BonusValue);
        if (itemToRemove != null)
        {
            this.baseAdditives.Remove(itemToRemove);
        } else
        {
            StatBonus negativeBonusToAdd = new StatBonus(-statBonus.BonusValue);
            AddStatBonus(negativeBonusToAdd);
        }
    }

    public int GetFinalValue()
    {
        finalValue = 0;
        foreach (StatBonus statBonus in this.baseAdditives)
        {
            finalValue += statBonus.BonusValue;
        }
        finalValue += baseValue;
        return finalValue;
    }


}
