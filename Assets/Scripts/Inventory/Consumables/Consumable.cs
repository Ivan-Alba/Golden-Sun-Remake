using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[JsonObject(MemberSerialization = MemberSerialization.Fields)]
public class Consumable : Item 
{

    string statBonus;
    int bonusValue;

    public string StatBonus { get => statBonus; set => statBonus = value; }
    public int BonusValue { get => bonusValue; set => bonusValue = value; }

    public void Consume(FighterStats target, Action<bool, string> callback)
    {
        Debug.Log($"Consumir {ItemName} que devuelve {BonusValue} de {StatBonus}");
        switch (StatBonus) {
            case "HP":
            if (target.CurrHp != 0 && target.CurrHp < target.MaxHp) {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.CURE_SOUND);
                target.AddBonusEffect(StatBonus, BonusValue);
                callback(true, ItemSlug);
            } else { //No se puede usar el ítem
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.NEGATIVE_SOUND);
                callback(false, ItemSlug);
            }
            break;
        }
        callback(false, ItemSlug);
    }

}
