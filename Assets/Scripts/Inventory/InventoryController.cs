using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryController : MonoBehaviour
{
    public static InventoryController Instance { get; set; }
    public Dictionary<Item, int> PlayerItems { get => playerItems; set => playerItems = value; }

    //List<Item> playerItems = new List<Item>();
    //public List<Item> PlayerItems { get => playerItems; set => playerItems = value; }

    Dictionary<Item, int> playerItems = new Dictionary<Item, int>();

    void Awake()
    {

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);

        }
        else
        {
            Instance = this;
        }
    }

    public void AddItem(string itemSlug, int quantity = 1)
    {
        Debug.Log($"Obtenido {itemSlug}");

        Item itemToAdd = ItemDatabase.Instance.GetItem(itemSlug);
        if (playerItems.ContainsKey(itemToAdd))
        {
            playerItems[itemToAdd] += quantity;
        }
        else
        {
            playerItems.Add(itemToAdd, quantity);
        }

        Debug.Log($"{PlayerItems.Count} objetos en el inventario");

    }

    public void LoseItem(string itemSlug, int quantity)
    {
        Item itemToLose = ItemDatabase.Instance.GetItem(itemSlug);
        if (playerItems.ContainsKey(itemToLose))
        {
            playerItems[itemToLose] -= quantity;
            if (playerItems[itemToLose] <= 0)
            {
                playerItems.Remove(itemToLose);
            }
        }
    }


    public void ConsumeItem(Consumable itemToConsume, FighterStats target)
    {
        itemToConsume.Consume((target), resultConsume);
    }

    public void resultConsume(bool result, string itemSlug)
    {
        if (result)
        {
            LoseItem(itemSlug, 1);
        }
    }

}
