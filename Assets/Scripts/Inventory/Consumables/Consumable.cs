using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Consumable : Item 
{
    /**public Consumable(string _itemSlug, string _itemName, string _itemDescription, string _statBonus, int _value, bool _canSell )
    {

        this.ItemSlug = itemSlug;
        this.ItemName = itemName;
        this.ItemDescription = itemDescription;
        this.statBonus = statBonus;
        this.value = value;
        this.CanSell = canSell;
    }*/
    

    public void Consume(FighterStats target, Action<bool, string> callback)
    {
        Debug.Log($"Consumir {ItemName} que devuelve {BonusValue} de {StatBonus}");
        switch (StatBonus) {
            case "HP":
            if (target.CurrHp != 0 && target.CurrHp < target.MaxHp) {
                GameManager.instance.GetComponent<Sounds>().PlaySound(GameManager.CURE_SOUND);
                target.AddConsumableEffect(StatBonus, BonusValue);
                callback(true, ItemSlug);
            } else { //No se puede usar el ítem
                GameManager.instance.GetComponent<Sounds>().PlaySound(GameManager.NEGATIVE_SOUND);
                callback(false, ItemSlug);
            }
            break;
        }
        callback(false, ItemSlug);
    }

}
