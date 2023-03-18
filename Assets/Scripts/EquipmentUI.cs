using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

public class EquipmentUI : MonoBehaviour
{
    List<GameObject> itemList;
    Equipable[] equippedItems;

    int optionsIndex;
    int slotsIndex;
    int itemListIndex;
    int equipmentSlots;
    GameObject currentHero;

    bool selectingOption;
    bool selectingSlot;
    bool selectingEquipment;
    bool animating;

    //UI ELEMENTS
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
        equipmentSlots = HeroEquipment.EquipmentSlots;
        itemList = new List<GameObject>();
        equippedItems = new Equipable[equipmentSlots];
        equipInfo = transform.Find("EquipInfo");
        slots = new List<Transform>();
        for (int i = 0; i < HeroEquipment.EquipmentSlots; i++)
        {
            slots.Add(equipInfo.Find("EquippedList").GetChild(i).transform);
        }
        equipmentListPanel = transform.Find("ItemsPanel");
        equipScrollContent = equipmentListPanel.Find("ItemPanel").Find("Viewport").Find("Content");
        gearPiecePrefab = Resources.Load<GameObject>("Prefabs/InventoryItem");
    }

    private void OnEnable()
    {
        animating = false;
        selectingOption = true;
        currentHero = GameManager.instance.GetParty()[0];
        equippedItems = currentHero.GetComponent<HeroEquipment>().EquippedItems;
        optionsIndex = 0;
        slotsIndex = 0;
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
        if (animating)
        {
            return;
        }
        //-----PRESS CONFIRM KEY------
        if (Input.GetKeyDown(GameManager.instance.KeyConfirm))
        {
            if (selectingEquipment)
            {
                //SELECTIN EQUIP FOR A SLOT
                selectingEquipment = false;
                RefreshEquipInfo();
                equipmentListPanel.gameObject.SetActive(false);
                ResetContent();
                itemList = new List<GameObject>();
                slots[slotsIndex].GetChild(0).Find("Highlight").GetComponent<Animator>().enabled = true;
                selectingSlot = true;
            }
            else if (selectingSlot)
            {
                //SELECTING GEAR SLOT
                selectingSlot = false;
                SetEquipmentListForSlot();
                slots[slotsIndex].GetChild(0).Find("Highlight").GetComponent<Animator>().enabled = false;
                equipmentListPanel.gameObject.SetActive(true);
                itemListIndex = 0;
                if (itemList.Count > 0)
                {
                    itemList[itemListIndex].transform.Find("Highlight").gameObject.SetActive(true);
                } else
                {
                    //AÑADIR HUECO VACÍO/QUITAR ITEM
                }
                selectingEquipment = true;
            }
            else if (selectingOption)
            {
                //SELECTING HERO
                selectingOption = false;
                slotsIndex = 0;
                options[optionsIndex].transform.Find("Highlight").GetComponent<Animator>().enabled = false;
                slots[slotsIndex].GetChild(0).Find("Highlight").gameObject.SetActive(true);
                selectingSlot = true;
            }
        }


        //-----PRESS BACK KEY------
        if (Input.GetKeyDown(GameManager.instance.KeyCancel))
        {
            if (selectingEquipment)
            {
                //SELECTIN EQUIP FOR A SLOT
                selectingEquipment = false;
                slots[slotsIndex].GetChild(0).Find("Highlight").GetComponent<Animator>().enabled = true;
                equipmentListPanel.gameObject.SetActive(false);
                ResetContent();
                itemList = new List<GameObject>();
                selectingSlot = true;
            }
            else if (selectingSlot)
            {
                //SELECTING GEAR SLOT
                selectingSlot = false;
                slots[slotsIndex].GetChild(0).Find("Highlight").gameObject.SetActive(false);
                options[optionsIndex].Find("Highlight").GetComponent<Animator>().enabled = true;
                selectingOption = true;
            }
            else if (selectingOption)
            {
                //BACK TO MAIN MENU
                animating = true;
                StartCoroutine(BackToMainMenu());
            }
        }


        //------PRESS DOWN KEY-------
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            if (selectingEquipment)
            {

            }
            else if (selectingSlot)
            {
                slots[slotsIndex].GetChild(0).Find("Highlight").gameObject.SetActive(false);
                GameManager.instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                slots[slotsIndex].GetChild(0).Find("Name").GetComponent<TextMeshProUGUI>().color = Color.white;
                if (slotsIndex + 1 >= slots.Count)
                {
                    slotsIndex = 0;
                }
                else
                {
                    slotsIndex += 1;
                }

                slots[slotsIndex].GetChild(0).Find("Name").GetComponent<TextMeshProUGUI>().color = Color.yellow;
                slots[slotsIndex].GetChild(0).Find("Highlight").gameObject.SetActive(true);
            }
            else if (selectingOption)
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
            if (selectingEquipment)
            {

            }
            else if (selectingSlot)
            {
                slots[slotsIndex].GetChild(0).Find("Highlight").gameObject.SetActive(false);
                GameManager.instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                slots[slotsIndex].GetChild(0).Find("Name").GetComponent<TextMeshProUGUI>().color = Color.white;
                if (slotsIndex - 1 < 0)
                {
                    slotsIndex = slots.Count - 1;
                }
                else
                {
                    slotsIndex -= 1;
                }

                slots[slotsIndex].GetChild(0).Find("Name").GetComponent<TextMeshProUGUI>().color = Color.yellow;
                slots[slotsIndex].GetChild(0).Find("Highlight").gameObject.SetActive(true);
            }
            else if (selectingOption)
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

    private void SetEquipmentListForSlot()
    {
        //GET ALL EQUIPABLE ON INVENTORY ITEMS AND FILTER BY SLOT
        Dictionary<Equipable, int> filteredGear = new Dictionary<Equipable, int>();
        foreach (KeyValuePair<Item, int> item in InventoryController.Instance.PlayerItems)
        {
            if (item.Key.GetType() == typeof(Equipable))
            {
                if (((Equipable)item.Key).ObjectType == HeroEquipment.SlotsName[slotsIndex])
                {
                    filteredGear.Add((Equipable)item.Key, item.Value);
                }
            }
        }

        //ADD TO SCROLL VIEW
        foreach (KeyValuePair<Equipable, int> gearPiece in filteredGear)
        {
            GameObject newItem = Instantiate(gearPiecePrefab, equipScrollContent);
            newItem.transform.Find("Icon").GetComponent<Image>().sprite = gearPiece.Key.Icon;
            newItem.transform.Find("Name").GetComponent<TextMeshProUGUI>().text = gearPiece.Key.ItemName;
            //No mostramos cantidad en pantalla de equipo
            newItem.transform.Find("Quantity").GetComponent<TextMeshProUGUI>().text = gearPiece.Value.ToString();
            itemList.Add(newItem);
        }

        if (itemList.Count == 0)
        {
            GameObject newItem = Instantiate(gearPiecePrefab, equipScrollContent);
            newItem.transform.Find("Name").GetComponent<TextMeshProUGUI>().SetText("");
            newItem.transform.Find("Icon").GetComponent<Image>().enabled = false;
            itemList.Add(newItem);
        }

    }

    private void ResetContent()
    {
       foreach (GameObject item in itemList)
        {
            Destroy(item);
        }
    }
    private void RefreshEquipInfo()
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
        transform.parent.GetComponent<MenuController>().BackFromSubmenu();
    }

}
