#if false
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipmentUI : MonoBehaviour
{
    [SerializeField] private MenuController menuController;

    List<GameObject> itemList;
    //Equipable[] equippedItems;
    //List<Equipable> equipableList;

    int optionsIndex;
    int slotsIndex;
    int itemListIndex;
    int equipmentSlots;
    GameObject currentHero;

    bool selectingOption;
    bool selectingSlot;
    bool selectingEquipment;
    bool animating;

    // UI ELEMENTS
    Transform[] options;
    List<Transform> slots;
    Transform equipInfo;
    Sprite emptyIcon;
    Transform equipmentListPanel;
    Transform equipScrollContent;

    GameObject gearPiecePrefab;

    private void Awake()
    {
        emptyIcon = Resources.Load<Sprite>("Images/Icons/Mark");
        //equipmentSlots = HeroEquipment.EquipmentSlots;
        itemList = new List<GameObject>();
        //equippedItems = new Equipable[equipmentSlots];
        equipInfo = transform.Find("EquipInfo");
        slots = new List<Transform>();

        /*for (int i = 0; i < HeroEquipment.EquipmentSlots; i++)
        {
            slots.Add(equipInfo.Find("EquippedList").GetChild(i).transform);
        }*/

        equipmentListPanel = transform.Find("ItemsPanel");
        equipScrollContent = equipmentListPanel.Find("ItemPanel").Find("Viewport").Find("Content");
        gearPiecePrefab = Resources.Load<GameObject>("Prefabs/UIComponents/InventoryItem");
    }

    private void OnEnable()
    {
        animating = false;
        selectingOption = true;
        currentHero = GameManager.Instance.GetParty()[0];
        optionsIndex = 0;
        slotsIndex = 0;
        itemListIndex = 0;

        options = new Transform[GameManager.Instance.GetParty().Count];
        for (int i = 0; i < options.Length; i++)
        {
            options[i] = transform.Find("Options").GetChild(i);
            options[i].gameObject.SetActive(true);
            options[i].GetComponent<TextMeshProUGUI>().text = GameManager.Instance.GetParty()[i].name;
        }

        options[optionsIndex].transform.Find("Highlight").gameObject.SetActive(true);
        options[optionsIndex].GetComponent<TextMeshProUGUI>().color = Color.yellow;

        RefreshEquipInfo();
    }

    private void Update()
    {
        /* Code for input handling */
    }

    private void ShowGearForSlot()
    {/*
        // GET ALL EQUIPABLE ON INVENTORY ITEMS AND FILTER BY SLOT
        Dictionary<Equipable, int> filteredGear = new Dictionary<Equipable, int>();

        foreach (KeyValuePair<Item, int> item in InventoryController.Instance.PlayerItems)
        {
            if (item.Key is Equipable equipableItem)
            {
                if (equipableItem.ObjectType == HeroEquipment.SlotsName[slotsIndex])
                {
                    filteredGear.Add(equipableItem, item.Value);
                }
            }
        }

        // ADD TO SCROLL VIEW
        equipableList = new List<Equipable>();
        foreach (KeyValuePair<Equipable, int> gearPiece in filteredGear)
        {
            GameObject newItem = Instantiate(gearPiecePrefab, equipScrollContent);
            newItem.transform.Find("Icon").GetComponent<Image>().sprite = gearPiece.Key.Icon;
            newItem.transform.Find("Name").GetComponent<TextMeshProUGUI>().text = gearPiece.Key.ItemName;
            newItem.transform.Find("Quantity").GetComponent<TextMeshProUGUI>().text = gearPiece.Value.ToString();
            itemList.Add(newItem);
            equipableList.Add(gearPiece.Key);
        }

        if (itemList.Count == 0)
        {
            GameObject newItem = Instantiate(gearPiecePrefab, equipScrollContent);
            newItem.transform.Find("Name").GetComponent<TextMeshProUGUI>().SetText("");
            newItem.transform.Find("Icon").GetComponent<Image>().enabled = false;
            itemList.Add(newItem);
        }*/
    }

    private void ResetContent()
    {
        foreach (GameObject item in itemList)
        {
            Destroy(item);
        }
    }

    private void RefreshEquipInfo()
    {/*
        if (currentHero == null) return;

        equippedItems = currentHero.GetComponent<HeroEquipment>()?.EquippedItems;

        // BACKGROUND IMAGE
        transform.Find("Background").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/MainMenu/{currentHero.name}_full");

        // EQUIPPED ITEMS
        Transform equipedList = equipInfo.Find("EquippedList");

        for (int i = 0; i < equipmentSlots; i++)
        {
            Transform inventoryItem = equipedList.GetChild(i).Find("InventoryItem");

            if (equippedItems != null && equippedItems[i] != null)
            {
                inventoryItem.Find("Icon").GetComponent<Image>().sprite = equippedItems[i].Icon;
                inventoryItem.Find("Name").GetComponent<TextMeshProUGUI>().text = equippedItems[i].ItemName;
            }
            else
            {
                inventoryItem.Find("Icon").GetComponent<Image>().sprite = emptyIcon;
                inventoryItem.Find("Name").GetComponent<TextMeshProUGUI>().text = "Vacío";
            }
        }

        // STATS INFO
        Transform statsInfo = transform.Find("StatsInfo");
        FighterStats fighterStats = currentHero.GetComponent<FighterStats>();

        if (fighterStats != null)
        {
            StatType[] displayStats = (StatType[])Enum.GetValues(typeof(StatType));

            for (int i = 0; i < displayStats.Length && i < statsInfo.childCount; i++)
            {
                StatType statType = displayStats[i];
                TextMeshProUGUI valText = statsInfo.GetChild(i).Find("Value")?.GetComponent<TextMeshProUGUI>();

                if (valText == null) continue;

                if (statType == StatType.MaxHP)
                {
                    valText.text = $"{fighterStats.CurrHp} / {fighterStats.MaxHp}";
                }
                else if (statType == StatType.MaxPP)
                {
                    valText.text = $"{fighterStats.CurrPp} / {fighterStats.MaxPp}";
                }
                else
                {
                    valText.text = $"{fighterStats.GetStatValue(statType)}";
                }

                valText.color = Color.white;
            }
        }*/
    }

    private void ShowStatsDifference()
    {/*
        if (currentHero == null) return;

        Equipable newPiece = null;

        if (equipableList != null && equipableList.Count > itemListIndex)
        {
            newPiece = equipableList[itemListIndex];
        }

        HeroEquipment heroEquipment = currentHero.GetComponent<HeroEquipment>();
        FighterStats fighterStats = currentHero.GetComponent<FighterStats>();

        if (heroEquipment == null || fighterStats == null) return;

        Equipable currentPiece = heroEquipment.EquippedItems[slotsIndex];
        StatType[] displayStats = (StatType[])Enum.GetValues(typeof(StatType));
        Transform statsInfo = transform.Find("StatsInfo");

        for (int i = 0; i < displayStats.Length && i < statsInfo.childCount; i++)
        {
            StatType statType = displayStats[i];
            int currentValue = fighterStats.GetStatValue(statType);

            // CURRENT EQUIP BONUS
            int currentPieceBonus = 0;
            if (currentPiece != null && currentPiece.StatName != null)
            {
                for (int x = 0; x < currentPiece.StatName.Length; x++)
                {
                    if (Enum.TryParse<StatType>(currentPiece.StatName[x], true, out StatType parsedStat) && parsedStat == statType)
                    {
                        if (x < currentPiece.StatValue.Length)
                        {
                            currentPieceBonus = currentPiece.StatValue[x];
                        }
                    }
                }
            }

            // NEW EQUIP BONUS
            int newPieceBonus = 0;
            if (newPiece != null && newPiece.StatName != null)
            {
                for (int x = 0; x < newPiece.StatName.Length; x++)
                {
                    if (Enum.TryParse<StatType>(newPiece.StatName[x], true, out StatType parsedStat) && parsedStat == statType)
                    {
                        if (x < newPiece.StatValue.Length)
                        {
                            newPieceBonus = newPiece.StatValue[x];
                        }
                    }
                }
            }

            int newValue = currentValue - currentPieceBonus + newPieceBonus;

            TextMeshProUGUI valText = statsInfo.GetChild(i).Find("Value")?.GetComponent<TextMeshProUGUI>();
            if (valText == null) continue;

            if (statType == StatType.MaxHP)
            {
                valText.text = $"{fighterStats.CurrHp} / {newValue}";
            }
            else if (statType == StatType.MaxPP)
            {
                valText.text = $"{fighterStats.CurrPp} / {newValue}";
            }
            else
            {
                valText.text = newValue.ToString();
            }

            // Display difference color
            int difference = newValue - currentValue;
            if (difference > 0)
            {
                valText.color = Color.green;
            }
            else if (difference < 0)
            {
                valText.color = Color.red;
            }
            else
            {
                valText.color = Color.white;
            }
        }*/
    }

    private IEnumerator BackToMainMenu()
    {
        gameObject.GetComponent<Animator>().SetTrigger("exit");

        yield return new WaitForSecondsRealtime(0.7f);

        foreach (Transform option in options)
        {
            option.Find("Highlight").gameObject.SetActive(false);
            option.GetComponent<TextMeshProUGUI>().color = Color.white;
        }

        gameObject.SetActive(false);
        menuController.BackFromSubmenu();
    }
}
#endif