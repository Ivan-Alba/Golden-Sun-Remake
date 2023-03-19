using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.Progress;

public class HeroEquipment : MonoBehaviour
{
    private static int equipmentSlots = 7;
    private Equipable[] equippedItems;

    public static int EquipmentSlots { get => equipmentSlots; set => equipmentSlots = value; }
    public Equipable[] EquippedItems { get => equippedItems; set => equippedItems = value; }
    public static string[] SlotsName { get => slotsName; set => slotsName = value; }

    public enum slotsIndex
    {
        WEAPON,
        SHIELD,
        HELMET,
        ARMOR,
        HANDS,
        BOOTS,
        SPECIAL
    }
    private static string[] slotsName = new string[] {
        "Weapon",
        "Shield",
        "Helmet",
        "Armor",
        "Hands",
        "Boots",
        "Accessory"
    };

    private void Awake()
    {
        EquippedItems = new Equipable[EquipmentSlots];
    }

    public void SetEquipment(int slot, Equipable item)
    {
        if (EquippedItems[slot] != null)
        {
            //QUITAMOS ITEM YA EQUIPADO
            RemoveEquipment(slot);
        }

        //Restamos item del inventario
        InventoryController.Instance.LoseItem(item.ItemSlug, 1);

        //AÑADIMOS NUEVO ITEM
        EquippedItems[slot] = item;
        //AÑADIMOS STAT BONUS
        for (int i = 0; i < item.StatName.Length; i++)
        {
            gameObject.GetComponent<FighterStats>().addStatBonus(item.StatName[i], item.StatValue[i]);
        }

    }

    public void RemoveEquipment(int slot)
    {
        if (EquippedItems[slot] != null)
        {

            for (int i = 0; i < EquippedItems[slot].StatName.Length; i++)
            {
                //QUITAR STATBONUS DE EQUIPO ACTUAL
                gameObject.GetComponent<FighterStats>().RemoveStatBonus(EquippedItems[slot].StatName[i], EquippedItems[slot].StatValue[i]);
                //Devolvemos ítem al inventario
            }
            InventoryController.Instance.AddItem(EquippedItems[slot].ItemSlug);
            EquippedItems[slot] = null;
        }
    }
}
