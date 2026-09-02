#if false
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{

    [SerializeField] private MenuController menuController;

    GameObject content;
    GameObject itemPrefab;
    //List<Item> itemsList = new List<Item>(); //List with items on category


    //UI ELEMENTS
    GameObject itemInfo; //ItemDescription Window
    List<GameObject> inventoryOptionsUI = new List<GameObject>(); //Inventory options
    List<GameObject> elementsList; //Content children
    List<GameObject> herosList;
    GameObject heroUseItemPanel;


    int menuIndex;
    int itemIndex;
    int heroIndex;

    bool onItemList;
    bool animating;
    bool selectingItemTarget;

    private void Awake()
    {
        elementsList = new List<GameObject>();
        itemPrefab = Resources.Load<GameObject>("Prefabs/UIComponents/InventoryItem");
        content = gameObject.transform.GetComponentInChildren<GridLayoutGroup>().gameObject;
        itemInfo = gameObject.transform.Find("ItemsPanel").Find("ItemInfo").gameObject;
        heroUseItemPanel = gameObject.transform.Find("HeroPanel").gameObject;
        herosList = new List<GameObject>();
        for (int i = 0; i < GameManager.Instance.GetParty().Count; i++)
        {
            heroUseItemPanel.transform.GetChild(i).gameObject.SetActive(true);
            herosList.Add(heroUseItemPanel.transform.GetChild(i).gameObject);
        }

        //InventoryOptions
        for (int i = 0; i < gameObject.transform.Find("Options").childCount; i++)
        {
            inventoryOptionsUI.Add(gameObject.transform.Find("Options").GetChild(i).gameObject);
        }
    }

    // Update is called once per frame
    void Update()
    {/*
        if (animating) return;

        //---------PRESS DOWN ARROW KEY----------
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            if (onItemList)
            {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                ItemListGoDown();
            }
            else if (selectingItemTarget)
            {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                if (heroIndex < herosList.Count - 1)
                {
                    heroIndex++;
                }
                else
                {
                    heroIndex = 0;
                }
                SetHeroHighlight();
            }
            else
            {
                inventoryOptionsUI[menuIndex].transform.Find("Highlight").gameObject.SetActive(false);
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                inventoryOptionsUI[menuIndex].GetComponent<TextMeshProUGUI>().color = Color.white;
                if (menuIndex + 1 >= inventoryOptionsUI.Count)
                {
                    menuIndex = 0;
                }
                else
                {
                    menuIndex += 1;
                }

                inventoryOptionsUI[menuIndex].GetComponent<TextMeshProUGUI>().color = Color.yellow;
                inventoryOptionsUI[menuIndex].transform.Find("Highlight").gameObject.SetActive(true);
                Reset();
                ShowItemsByType(menuIndex);
            }

        }

        //---------PRESS UP ARROW KEY----------
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (onItemList)
            {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                ItemListGoUp();
            }
            else if (selectingItemTarget)
            {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                if (heroIndex != 0)
                {
                    heroIndex--;
                }
                else
                {
                    heroIndex = herosList.Count - 1;
                }
                SetHeroHighlight();
            }
            else
            {
                inventoryOptionsUI[menuIndex].transform.Find("Highlight").gameObject.SetActive(false);
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                inventoryOptionsUI[menuIndex].GetComponent<TextMeshProUGUI>().color = Color.white;
                if (menuIndex - 1 < 0)
                {
                    menuIndex = inventoryOptionsUI.Count - 1;
                }
                else
                {
                    menuIndex -= 1;
                }

                inventoryOptionsUI[menuIndex].transform.Find("Highlight").gameObject.SetActive(true);
                inventoryOptionsUI[menuIndex].GetComponent<TextMeshProUGUI>().color = Color.yellow;
                Reset();
                ShowItemsByType(menuIndex);
            }
        }

        //------------PRESS KEY CONFIRM-------------------------
        if (Input.GetKeyDown(GameManager.Instance.KeyConfirm))
        {

            if (onItemList) //ON ITEM SELECTION
            {
                //USE ITEM IF USABLE ON MENU
                if (itemsList.Count > 0)
                {
                    if (itemsList[itemIndex].UseInMenu)
                    {
                        Debug.Log($"Objeto seleccionado {itemsList[itemIndex].ItemName}");
                        elementsList[itemIndex].transform.Find("Highlight").GetComponent<Animator>().enabled = false;
                        //MOSTRAR PERSONAJES SOBRE EL QUE USAR OBJETO
                        GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.CONFIRM_SOUND);
                        if (itemIndex % 2 == 0)
                        {
                            //HERO PANEL A LA DERECHA
                            heroUseItemPanel.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 319, 0);
                            heroUseItemPanel.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Right, -319, 0);
                        }
                        else
                        {
                            //HERO PANEL A LA IZQUIERDA
                            heroUseItemPanel.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 102, 0);
                            heroUseItemPanel.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Right, -102, 0);
                        }
                        RefreshHeroPanel();
                        heroUseItemPanel.SetActive(true);
                        selectingItemTarget = true;
                        onItemList = false;
                        heroIndex = 0;
                        SetHeroHighlight();
                    }
                }
            }
            else if (selectingItemTarget)
            {
                //USE ITEM ON CHARACTER
                InventoryController.Instance.ConsumeItem((Consumable)itemsList[itemIndex], GameManager.Instance.GetParty()[heroIndex].GetComponent<FighterStats>());
                RefreshHeroPanel();
                RefreshInventory();
                Debug.Log($"Vas a usar {itemsList[itemIndex].ItemName} en {GameManager.Instance.GetParty()[heroIndex].gameObject.name}");

            }
            else //ON MENU OPTIONS
            {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.CONFIRM_SOUND);
                inventoryOptionsUI[menuIndex].GetComponentInChildren<Animator>().enabled = false;
                SetElementHighlight();
                onItemList = true;

                if (elementsList[0].transform.Find("Name").GetComponent<TextMeshProUGUI>().text != "")
                {
                    //Show item description window
                    StartCoroutine(ShowItemInfoWindow(true));
                    //Set itemInfo window
                    SetItemInfoWindow(0);
                }
                Debug.Log($"Opcion seleccionada: {inventoryOptionsUI[menuIndex].GetComponent<TextMeshProUGUI>().text}");
            }
        }

        //--------------PRESS BACK KEY-----------------
        if (Input.GetKeyDown(GameManager.Instance.KeyCancel))
        {
            GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.NEGATIVE_SOUND);

            if (onItemList) //BACK TO MENU OPTIONS
            {
                foreach (GameObject element in elementsList)
                {
                    element.transform.Find("Highlight").gameObject.SetActive(false);
                }

                if (elementsList[0].transform.Find("Name").GetComponent<TextMeshProUGUI>().text != "")
                {
                    StartCoroutine(ShowItemInfoWindow(false));
                }
                inventoryOptionsUI[menuIndex].GetComponentInChildren<Animator>().enabled = true;
                onItemList = false;
                itemIndex = 0;
            }
            else if (selectingItemTarget)
            {
                heroUseItemPanel.SetActive(false);
                selectingItemTarget = false;
                onItemList = true;
                elementsList[itemIndex].transform.Find("Highlight").GetComponent<Animator>().enabled = true;
            }
            else //BACK TO MAIN MENU
            {
                StartCoroutine(BackToMainMenu());
                animating = true;
            }
        }


        //----------PRESS RIGHT KEY------------
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (onItemList)
            {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                ItemListGoRight();
            }
        }

        //--------------PRESS LEFT KEY---------
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if (onItemList)
            {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                ItemListGoLeft();
            }
        }*/

    }

    private void ShowItemsByType(int objectType)
    {
        /*

        switch (objectType)
        {
            case 0: //Objects
                foreach (Item item in InventoryController.Instance.PlayerItems.Keys)
                {
                    if (item.ObjectType == "Item")
                    {
                        SetElement(item);
                    }
                }
                break;

            case 1: //Weapons
                foreach (Item item in InventoryController.Instance.PlayerItems.Keys)
                {
                    if (item.ObjectType == "Weapon")
                    {
                        SetElement(item);
                    }
                }
                break;

            case 2: //Armors
                foreach (Item item in InventoryController.Instance.PlayerItems.Keys)
                {
                    if (item.ObjectType == "Armor" || item.ObjectType == "Hands" || item.ObjectType == "Boots" || item.ObjectType == "Shield")
                    {
                        SetElement(item);
                    }
                }
                break;

            case 3: //Accessories
                foreach (Item item in InventoryController.Instance.PlayerItems.Keys)
                {
                    if (item.ObjectType == "Accessory")
                    {
                        SetElement(item);
                    }
                }
                break;

            case 4: //Resources
                foreach (Item item in InventoryController.Instance.PlayerItems.Keys)
                {
                    if (item.ObjectType == "Resource")
                    {
                        SetElement(item);
                    }
                }
                break;

            case 5: //Special
                foreach (Item item in InventoryController.Instance.PlayerItems.Keys)
                {
                    if (item.ObjectType == "Special")
                    {
                        SetElement(item);
                    }
                }
                break;
        }

        //If no elements, insert empty element
        if (elementsList.Count == 0)
        {
            GameObject newItem = Instantiate(itemPrefab, content.transform);
            newItem.transform.Find("Name").GetComponent<TextMeshProUGUI>().SetText("");
            newItem.transform.Find("Icon").GetComponent<Image>().enabled = false;
            elementsList.Add(newItem);
        }*/

    }

    /*private void SetElement(Item item)
    {/*
        GameObject newItem = Instantiate(itemPrefab, content.transform);
        newItem.transform.Find("Name").GetComponent<TextMeshProUGUI>().SetText(item.ItemName);
        newItem.transform.Find("Icon").GetComponent<Image>().sprite = item.Icon;

        if (item.ObjectType != "Special")
            newItem.transform.Find("Quantity").GetComponent<TextMeshProUGUI>().SetText(InventoryController.Instance.PlayerItems[item].ToString());

        if (!item.UseInMenu && item.ObjectType == "Item")
            newItem.transform.Find("Name").GetComponent<TextMeshProUGUI>().color = Color.gray;

        itemsList.Add(item);
        elementsList.Add(newItem);
    }*/


    private void RefreshInventory()
    {
        /*Item currentItemSelected = itemsList[itemIndex];
        Reset();
        ShowItemsByType(menuIndex);
        Item afterItemSelected = itemsList[itemIndex];
        if (currentItemSelected != afterItemSelected)
        {
            heroUseItemPanel.SetActive(false);
            selectingItemTarget = false;
            onItemList = true;
            SetElementHighlight();
        }
        else
        {
            SetElementHighlight();
            elementsList[itemIndex].GetComponentInChildren<Animator>().enabled = false;
        }*/
    }

    private void RefreshHeroPanel()
    {
        for (int i = 0; i < herosList.Count; i++)
        {
            herosList[i].transform.Find("Image").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/MainMenu/{GameManager.Instance.GetParty()[i].name}_avatar");
            herosList[i].transform.Find("Name").GetComponent<TextMeshProUGUI>().SetText(GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().Name);
            herosList[i].transform.Find("HP").GetComponent<TextMeshProUGUI>()
                .SetText($"{GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().CurrHp}/{GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().MaxHp}");
            herosList[i].transform.Find("PP").GetComponent<TextMeshProUGUI>().SetText
                              ($"{GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().CurrPp}/{GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().MaxPp}");
            herosList[i].transform.Find("Level").GetComponent<TextMeshProUGUI>().SetText($"Nivel: {GameManager.Instance.GetParty()[i].GetComponent<HeroStats>().Level}");

            //Bars
            herosList[i].transform.Find("HPBar").GetComponent<Slider>().maxValue = GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().MaxHp;
            herosList[i].transform.Find("HPBar").GetComponent<Slider>().value = GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().CurrHp;
            herosList[i].transform.Find("PPBar").GetComponent<Slider>().maxValue = GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().MaxPp;
            herosList[i].transform.Find("PPBar").GetComponent<Slider>().value = GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().CurrPp;
        }

    }

    private IEnumerator ShowItemInfoWindow(bool choice)
    {
        if (choice)
        {
            itemInfo.SetActive(true);

        }
        else
        {
            itemInfo.GetComponent<Animator>().SetTrigger("exit"); //Hide ItemInfo window
            yield return new WaitForSecondsRealtime(0.5f);
            itemInfo.SetActive(false);
        }
    }

    private void SetItemInfoWindow(int itemCursorIndex)
    {
        /*if (itemsList.Count != 0)
        {
            itemInfo.transform.Find("Name").GetComponent<TextMeshProUGUI>().SetText(itemsList[itemCursorIndex].ItemName);
            itemInfo.transform.Find("Description").GetComponent<TextMeshProUGUI>().SetText(itemsList[itemCursorIndex].ItemDescription);
            itemInfo.transform.Find("Image").GetComponent<Image>().sprite = itemsList[itemCursorIndex].Icon;
        }*/

    }

    private IEnumerator BackToMainMenu()
    {
        gameObject.GetComponent<Animator>().SetTrigger("exit");

        yield return new WaitForSecondsRealtime(0.7f);

        foreach (GameObject option in inventoryOptionsUI)
        {
            option.transform.Find("Highlight").gameObject.SetActive(false);
            option.GetComponent<TextMeshProUGUI>().color = Color.white;
        }
        gameObject.SetActive(false);
        menuController.BackFromSubmenu();
    }

    private void ItemListGoDown()
    {
        /*if (itemsList.Count != 0)
        {
            if (itemIndex != itemsList.Count - 1) //if cursor NOT last position
            {
                if (itemIndex + 2 < itemsList.Count)
                {
                    itemIndex += 2;
                }
                else
                {
                    itemIndex += 1;
                }
                SetElementHighlight();
                SetItemInfoWindow(itemIndex);
            }
        }*/
    }

    private void ItemListGoUp()
    {/*
        if (itemsList.Count != 0)
        {
            if (itemIndex != 0) //if cursor NOT first position
            {
                if (itemIndex - 2 >= 0)
                {
                    itemIndex -= 2;
                }
                else
                {
                    itemIndex = 0;
                }
                SetElementHighlight();
                SetItemInfoWindow(itemIndex);
            }
        }*/
    }

    private void ItemListGoRight()
    {/*
        if (itemsList.Count != 0)
        {
            if (itemIndex != itemsList.Count - 1)
            {
                itemIndex++;
                SetElementHighlight();
                SetItemInfoWindow(itemIndex);
            }

        }*/

    }

    private void ItemListGoLeft()
    {
        /*if (itemsList.Count != 0)
        {
            if (itemIndex != 0)
            {
                itemIndex--;
                SetElementHighlight();
                SetItemInfoWindow(itemIndex);
            }

        }*/
    }

    private void SetElementHighlight()
    {
        elementsList.ForEach(x => x.transform.Find("Highlight").gameObject.SetActive(false));
        elementsList[itemIndex].transform.Find("Highlight").gameObject.SetActive(true);
    }

    private void SetHeroHighlight()
    {
        herosList.ForEach(x => x.transform.Find("Highlight").gameObject.SetActive(false));
        herosList[heroIndex].transform.Find("Highlight").gameObject.SetActive(true);
    }

    //ENABLE SETTINGS
    public void OnEnable()
    {
        ShowItemsByType(0);
        menuIndex = 0;
        itemIndex = 0;
        inventoryOptionsUI[menuIndex].transform.Find("Highlight").gameObject.SetActive(true);
        inventoryOptionsUI[menuIndex].GetComponent<TextMeshProUGUI>().color = Color.yellow;
    }

    //DISABLE SETTINGS
    private void OnDisable()
    {
        animating = false;
        Reset();
    }

    //RESET DEFAULT VALUES
    private void Reset()
    {
        foreach (GameObject element in elementsList)
        {
            Destroy(element);
        }
        //itemsList = new List<Item>();
        elementsList = new List<GameObject>();
    }
}
#endif