using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System;

[Serializable]
public class Item 
{

    string itemSlug, itemName, itemDescription;
    int itemPrice;
    string objectType;
    bool canSell, useInMenu;
    [NonSerialized]
    Sprite icon;

    public string ItemName { get => itemName; set => itemName = value; }
    public string ItemDescription { get => itemDescription; set => itemDescription = value; }
    public string ItemSlug { get => itemSlug; set => itemSlug = value; }
    public int ItemPrice { get => itemPrice; set => itemPrice = value; }
    public bool CanSell { get => canSell; set => canSell = value; }
    public bool UseInMenu { get => useInMenu; set => useInMenu = value; }
    public string ObjectType { get => objectType; set => objectType = value; }
    public Sprite Icon { get => icon; set => icon = value; }
}
