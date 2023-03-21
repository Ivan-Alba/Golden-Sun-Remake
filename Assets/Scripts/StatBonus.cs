using System;


[Serializable]
public class StatBonus 
{
    int bonusValue;

    public StatBonus(int bonusValue)
    {
        this.BonusValue = bonusValue;
    }

    public int BonusValue { get => bonusValue; set => bonusValue = value; }
}
