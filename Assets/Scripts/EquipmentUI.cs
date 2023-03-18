using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipmentUI : MonoBehaviour
{
    List<Equipable> itemList;
    Equipable[] equippedItems;

    int optionsIndex;
    int equipmentIndex;
    int itemListIndex;
    int equipmentSlots;
    GameObject currentHero;

    bool selectingOption;
    bool selectingSlot;
    bool selectingEquipment;

    //UI ELEMENTS
    Transform[] options;
    Transform equipInfo;

    Sprite emptyIcon;


    private void Awake()
    {
        emptyIcon = Resources.Load<Sprite>("Images/Icons/Mark");
        equipmentSlots = HeroEquipment.EquipmentSlots;
        itemList = new List<Equipable>();
        equippedItems = new Equipable[equipmentSlots];
        equipInfo = transform.Find("EquipInfo");
    }
    private void OnEnable()
    {
        selectingOption = true;
        currentHero = GameManager.instance.GetParty()[0];
        equippedItems = currentHero.GetComponent<HeroEquipment>().EquippedItems;
        optionsIndex = 0;
        equipmentIndex = 0;
        itemListIndex = 0;
        options = new Transform[GameManager.instance.GetParty().Count];
        for (int i = 0; i < options.Length; i++)
        {
            options[i] = transform.Find("Options").GetChild(i);
            options[i].gameObject.SetActive(true);
            options[i].GetComponent<TextMeshProUGUI>().text = GameManager.instance.GetParty()[i].name;
        }
        options[optionsIndex].transform.Find("Highlight").gameObject.SetActive(true);
        options[optionsIndex].GetComponent<TextMeshProUGUI>().color = Color.yellow;

        RefreshEquipInfo();
    }

    private void Update()
    {


        //------PRESS DOWN KEY-------
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            if (selectingOption)
            {
                options[optionsIndex].transform.Find("Highlight").gameObject.SetActive(false);
                GameManager.instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                options[optionsIndex].GetComponent<TextMeshProUGUI>().color = Color.white;
                if (optionsIndex + 1 >= options.Length)
                {
                    optionsIndex = 0;
                }
                else
                {
                    optionsIndex += 1;
                }

                options[optionsIndex].GetComponent<TextMeshProUGUI>().color = Color.yellow;
                options[optionsIndex].transform.Find("Highlight").gameObject.SetActive(true);
                currentHero = GameManager.instance.GetParty()[optionsIndex];
                RefreshEquipInfo();
            }
        }


        //-----PRESS UP KEY-------
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (selectingOption)
            {
                options[optionsIndex].transform.Find("Highlight").gameObject.SetActive(false);
                GameManager.instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                options[optionsIndex].GetComponent<TextMeshProUGUI>().color = Color.white;
                if (optionsIndex - 1 < 0)
                {
                    optionsIndex = options.Length - 1;
                }
                else
                {
                    optionsIndex -= 1;
                }

                options[optionsIndex].transform.Find("Highlight").gameObject.SetActive(true);
                options[optionsIndex].GetComponent<TextMeshProUGUI>().color = Color.yellow;
                currentHero = GameManager.instance.GetParty()[optionsIndex];
                RefreshEquipInfo();
            }
        }
    }

    public void RefreshEquipInfo()
    {
        //BACKGROUND IMAGE
        equipInfo.Find("Background").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/MainMenu/{currentHero.name}_full");

        //EQUIPED ITEMS
        Transform equipedList = equipInfo.Find("EquippedList");

        for (int i = 0; i < equipmentSlots; i++)
        {
            Transform inventoryItem = equipedList.GetChild(i).Find("InventoryItem");

            if (equippedItems[i] != null)
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

        //STATS INFO
        Transform statsInfo = equipInfo.Find("StatsInfo");

        statsInfo.Find("HP").Find("Value").GetComponent<TextMeshProUGUI>().text = $"{currentHero.GetComponent<FighterStats>().CurrHp} / {currentHero.GetComponent<FighterStats>().MaxHp}";
        statsInfo.Find("PP").Find("Value").GetComponent<TextMeshProUGUI>().text = $"{currentHero.GetComponent<FighterStats>().CurrPp} / {currentHero.GetComponent<FighterStats>().MaxPp}";
        statsInfo.Find("Strenght").Find("Value").GetComponent<TextMeshProUGUI>().text = $"{currentHero.GetComponent<FighterStats>().Strenght}";
        statsInfo.Find("Defense").Find("Value").GetComponent<TextMeshProUGUI>().text = $"{currentHero.GetComponent<FighterStats>().Defense}";
        statsInfo.Find("Wisdom").Find("Value").GetComponent<TextMeshProUGUI>().text = $"{currentHero.GetComponent<FighterStats>().Wisdom}";
        statsInfo.Find("Spirit").Find("Value").GetComponent<TextMeshProUGUI>().text = $"{currentHero.GetComponent<FighterStats>().Spirit}";
        statsInfo.Find("Agility").Find("Value").GetComponent<TextMeshProUGUI>().text = $"{currentHero.GetComponent<FighterStats>().Agility}";
        statsInfo.Find("Luck").Find("Value").GetComponent<TextMeshProUGUI>().text = $"{currentHero.GetComponent<FighterStats>().Luck}";

    }
}
