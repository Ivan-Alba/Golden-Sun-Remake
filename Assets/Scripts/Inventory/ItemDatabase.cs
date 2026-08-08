using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance { get; set; }
    private List<Item> itemList { get; set; }

    private void Awake()
    {

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            BuildDatabase();
        }
    }


    private void BuildDatabase()
    {
        JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
        itemList = JsonConvert.DeserializeObject<List<Item>>(Resources.Load("JSON/Items").ToString(), settings);
        itemList.ForEach(x => x.Icon = Resources.Load<Sprite>($"Images/Icons/Items/{x.ObjectType}/{x.ItemSlug}"));

        //DEBUG ALL ITEMS TO PLAYER

        //itemList.ForEach(x => InventoryController.Instance.AddItem(x.ItemSlug));
    }

    public Item GetItem(string itemSlug)
    {
        foreach(Item item in itemList)
        {
            if (item.ItemSlug == itemSlug) return item;
        }
        Debug.LogWarning("No se encuentra el item " + itemSlug);
        return null;
    }

    public IEnumerable<Item> GetAllItems()
    {
        foreach (Item item in itemList)
        {
            yield return item;
        }
        Debug.LogWarning("No se encuentra el item " + itemList);
    }
}
