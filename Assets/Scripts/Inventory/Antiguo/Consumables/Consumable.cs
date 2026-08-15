using Newtonsoft.Json;
using System;
using UnityEngine;

[JsonObject(MemberSerialization = MemberSerialization.Fields)]
public class Consumable : Item
{
    private string statBonus;
    private int bonusValue;

    public string StatBonus { get => statBonus; set => statBonus = value; }
    public int BonusValue { get => bonusValue; set => bonusValue = value; }

    public void Consume(FighterStats target, Action<bool, string> callback)
    {
        if (target == null)
        {
            callback?.Invoke(false, ItemSlug);
            return;
        }

        Debug.Log($"Consumir {ItemName} que devuelve {BonusValue} de {StatBonus}");

        switch (StatBonus.ToUpper())
        {
            case "HP":
                // Check if target is alive and not already at max HP
                if (target.CurrHp > 0 && target.CurrHp < target.MaxHp)
                {
                    AudioManager.Instance?.PlaySFX(SFXType.UI_Cure);

                    // Property setter automatically clamps and triggers UI events
                    target.CurrHp += BonusValue;

                    callback?.Invoke(true, ItemSlug);
                    return;
                }
                else
                {
                    AudioManager.Instance?.PlaySFX(SFXType.UI_Cancel);
                    callback?.Invoke(false, ItemSlug);
                    return;
                }

            case "PP":
                // Check if target is alive and not already at max PP
                if (target.CurrHp > 0 && target.CurrPp < target.MaxPp)
                {
                    AudioManager.Instance?.PlaySFX(SFXType.UI_Cure);

                    // Property setter automatically clamps and triggers UI events
                    target.CurrPp += BonusValue;

                    callback?.Invoke(true, ItemSlug);
                    return;
                }
                else
                {
                    AudioManager.Instance?.PlaySFX(SFXType.UI_Cancel);
                    callback?.Invoke(false, ItemSlug);
                    return;
                }

            default:
                // Handle permanent stat boosters (e.g., Apple -> Attack, Mint -> Agility)
                if (Enum.TryParse<StatType>(StatBonus, true, out StatType statType))
                {
                    Stat stat = target.GetStat(statType);
                    if (stat != null)
                    {
                        AudioManager.Instance?.PlaySFX(SFXType.UI_Cure);

                        // Permanently increase base stat value
                        stat.BaseValue += BonusValue;

                        callback?.Invoke(true, ItemSlug);
                        return;
                    }
                }
                break;
        }

        // If item could not be used
        AudioManager.Instance?.PlaySFX(SFXType.UI_Cancel);
        callback?.Invoke(false, ItemSlug);
    }
}