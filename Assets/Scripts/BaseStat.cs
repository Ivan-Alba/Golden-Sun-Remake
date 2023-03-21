using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class BaseStat
{
    private List<StatBonus> baseAdditives;
    private int baseValue, finalValue;
    private string statName, statDescription;

    public string StatName { get => statName; set => statName = value; }
    public List<StatBonus> BaseAdditives { get => baseAdditives; set => baseAdditives = value; }
    public int BaseValue { get => baseValue; set => baseValue = value; }
    public int FinalValue { get => finalValue; set => finalValue = value; }

    public BaseStat(int baseValue, string statName, string statDescription)
    {
        this.BaseAdditives = new List<StatBonus>();
        this.BaseValue = baseValue;
        this.StatName = statName;
        this.statDescription = statDescription;
    }

    public void AddStatBonus(StatBonus statBonus)
    {
        this.BaseAdditives.Add(statBonus);
    }

    public void RemoveStatBonus(StatBonus statBonus)
    {
        StatBonus itemToRemove = this.BaseAdditives.FindLast(x => x.BonusValue == statBonus.BonusValue);
        if (itemToRemove != null)
        {
            this.BaseAdditives.Remove(itemToRemove);
        } else
        {
            StatBonus negativeBonusToAdd = new StatBonus(-statBonus.BonusValue);
            AddStatBonus(negativeBonusToAdd);
        }
    }

    public int GetFinalValue()
    {
        FinalValue = 0;
        foreach (StatBonus statBonus in this.BaseAdditives)
        {
            FinalValue += statBonus.BonusValue;
        }
        FinalValue += BaseValue;
        return FinalValue;
    }


}
