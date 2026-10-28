using UnityEngine;
using System;

/// <summary>
/// Defines the 7 standard physical equipment slots corresponding to Golden Sun mechanics.
/// </summary>
public enum EquipmentSlot
{
    None,
    Weapon,
    Body,
    Head,
    Hands,
    UnderShirt,
    Boots,
    Ring,
    PsynergyRelic
}

[Serializable]
public class EquipmentSlotSpritePair
{
    public EquipmentSlot slotType;
    public Sprite slotSprite;
}