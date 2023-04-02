using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditorInternal.Profiling.Memory.Experimental;
using UnityEngine;
using UnityEngine.UI;

public class BattleUIController : MonoBehaviour
{

    //UI ELEMENTS
    GameObject battleCanvas;
    GameObject turnsPanel;
    GameObject ordersPanel;
    GameObject contentScrollView;
    GameObject contentItemPrefab;
    GameObject helpPanel;
    List<GameObject> contentItems = new List<GameObject>();
    int contentCursorIndex;
    string lastContent;


    
    List<GameObject> ordersButtons = new List<GameObject>();
    GameObject[] heroInfoPanels = new GameObject[4];

    //Turns settings
    TurnController turnController;
    List<Transform> turnsUIElements = new List<Transform>();
    List<KeyValuePair<GameObject, int>> turnsOrdered;
    int maxTurnBoxes;

    GameObject commandInfo;
    private int commandSelected;

    string[] commandsText = new string[8] { "Atacar", "Psinergía", "Djinn", "Invocar", "Defensa", "Objeto", "Estado", "Huir" };

    public int CommandSelected { get => commandSelected; set => commandSelected = value; }

    private void Awake()
    {
        //Awake UI elements
        battleCanvas = transform.Find("BattleCanvas").gameObject;
        turnsPanel = battleCanvas.transform.Find("TurnPanel").gameObject;
        ordersPanel = battleCanvas.transform.Find("OrdersPanel").gameObject;
        commandInfo = (ordersPanel.transform.Find("CommandInfoBorder").gameObject).transform.Find("CommandInfo").gameObject;
        helpPanel = battleCanvas.transform.Find("HelpPanel").gameObject;

        contentScrollView = battleCanvas.transform.Find("ContentPanel").GetChild(0).GetChild(0).GetChild(0).gameObject;
        contentItemPrefab = Resources.Load("Prefabs/ContentItem") as GameObject;
        foreach (Transform button in ordersPanel.transform.GetComponentInChildren<Transform>())
        {
            if (button.gameObject.name.Contains("Button"))
            {
                ordersButtons.Add(button.gameObject);
            }
        }

        //GUARDAMOS LOS GAMEOBJECT DE LOS CUADROS DE TURNO DE LA UI
        foreach (Transform turn in turnsPanel.GetComponentsInChildren<Transform>())
        {
            if (!turn.gameObject.Equals(turnsPanel) && !turn.gameObject.name.StartsWith("Highlight")) turnsUIElements.Add(turn);
        }


        //NUMBER OF TURN BOXES
        maxTurnBoxes = turnsUIElements.Count;
    }

    public void SetHeroInfoPanel(int i)
    {
        heroInfoPanels[i] = battleCanvas.transform.Find($"Hero{i}Panel").gameObject;
        heroInfoPanels[i].SetActive(true);
    }

    public void SetTurnsPanel(List<KeyValuePair<GameObject, int>> turnsOrdered)
    {
        this.turnsOrdered = turnsOrdered;
        //SET IMAGES TURNS PANEL
        int i = 0;
        foreach (Transform turnUIElement in turnsUIElements)
        {
            turnUIElement.gameObject.GetComponent<Image>().sprite = turnsOrdered.ElementAt(i++).Key.GetComponent<FighterStats>().TurnBarImage;
        }
    }

    public void ShowTurnsPanel(bool choice)
    {
        turnsPanel.SetActive(choice);
    }
    public void ShowSelectedTarget(GameObject enemyPointed)
    {
        //SHOW ON TURN BAR THE ENEMY TARGET
        for (int i = 0; i < maxTurnBoxes; i++)
        {
            if (turnsOrdered.ElementAt(i).Key.Equals(enemyPointed))
            {
                turnsPanel.transform.GetChild(i).Find("Highlight").gameObject.SetActive(true);
            }
            else
            {
                turnsPanel.transform.GetChild(i).Find("Highlight").gameObject.SetActive(false);
            }
        }
    }
    public void HideSelectedTarget()
    {
        for (int i = 0; i < maxTurnBoxes; i++)
        {
            turnsPanel.transform.GetChild(i).Find("Highlight").gameObject.SetActive(false);
        }
    }

    public void ordersPanelMove(string direction)
    {
        if (direction == "RIGHT")
        {
            if (commandSelected != ordersPanel.transform.childCount - 2)
            {
                commandSelected += 1; //Move a command on right direction
            }
            else
            {
                commandSelected = 0; //last command at right returns to 0
            }
        }
        else if (direction == "LEFT")
        {
            if (commandSelected == 0)
            {
                commandSelected = ordersPanel.transform.childCount - 2;
            }
            else
            {
                commandSelected -= 1;
            }
        }

        SetInfoText();
        RefreshButtonAnimation();
        GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
    }

    public void ShowOrdersPanel(bool choice)
    {
        ordersPanel.SetActive(choice);

        if (choice)
        {
            commandSelected = 0;
            SetInfoText();
            RefreshButtonAnimation();
        }

    }

    public void ShowBattleCanvas(bool choice)
    {
        battleCanvas.SetActive(choice);
    }

    public void RefreshHeroInfoPanel()
    {
        List<GameObject> heroParty = GameManager.Instance.GetParty();
        for (int i = 0; i < heroParty.Count; i++)
        {
            GameObject hp = heroInfoPanels[i].transform.Find("Hp").gameObject;
            GameObject pp = heroInfoPanels[i].transform.Find("Pp").gameObject;
            GameObject name = heroInfoPanels[i].transform.Find("Name").gameObject;

            FighterStats heroStats = heroParty[i].GetComponent<FighterStats>();

            if (heroStats.CurrHp <= 0)
            {
                hp.GetComponent<TextMeshProUGUI>().color = Color.red;
                hp.GetComponent<TextMeshProUGUI>().SetText($"HP 0 / {heroStats.MaxHp}");

            }
            else if (heroStats.CurrHp < heroStats.MaxHp * 0.3f)
            {
                hp.GetComponent<TextMeshProUGUI>().color = Color.yellow;
                hp.GetComponent<TextMeshProUGUI>().SetText($"HP {heroStats.CurrHp} / {heroStats.MaxHp}");

            }
            else
            {

                hp.GetComponent<TextMeshProUGUI>().color = Color.white;
                hp.GetComponent<TextMeshProUGUI>().SetText($"HP {heroStats.CurrHp} / {heroStats.MaxHp}");
            }

            pp.GetComponent<TextMeshProUGUI>().SetText($"PP {heroStats.CurrPp} / {heroStats.MaxPp}");
            name.GetComponent<TextMeshProUGUI>().SetText($"{heroStats.Name}");

        }
    }

    private void SetInfoText()
    {
        commandInfo.GetComponent<TextMeshProUGUI>().SetText(commandsText[commandSelected]);
    }

    private void RefreshButtonAnimation()
    {
        for (int i = 0; i < ordersButtons.Count; i++)
        {
            if (i == commandSelected)
            {
                ordersButtons[i].GetComponent<Animator>().SetBool("Selected", true);


            }
            else
            {
                ordersButtons[i].GetComponent<Animator>().SetBool("Selected", false);
            }
        }
    }

    public void SetContentPanel(string order)
    {
        turnController = gameObject.GetComponent<TurnController>();
        switch (order)
        {
            case "Psynergy":
                lastContent = "Psynergy";
                foreach (Psynergy psynergy in turnController.CurrentFighter.GetComponent<FighterStats>().Psynergies)
                {
                    GameObject newItem = Instantiate(contentItemPrefab, contentScrollView.transform);
                    newItem.transform.Find("Name").GetComponent<TextMeshProUGUI>().SetText(psynergy.Name);
                    newItem.transform.Find("Number").GetComponent<TextMeshProUGUI>().SetText($"{psynergy.PpCost} PP");
                    newItem.transform.Find("Icon").GetComponent<Image>().sprite = psynergy.Icon;
                    newItem.transform.Find("ElementIcon").GetComponent<Image>().sprite = psynergy.ElementIcon;
                    newItem.name = psynergy.SlugName;
                    if (turnController.CurrentFighter.GetComponent<FighterStats>().CurrPp < psynergy.PpCost) //Not enough PP to use a psynergy
                    {
                        foreach (TextMeshProUGUI text in newItem.GetComponentsInChildren<TextMeshProUGUI>()) {
                            text.color = Color.grey;
                        }
                    }
                    contentItems.Add(newItem);
                }


                contentCursorIndex = 0;
                helpPanel.GetComponentInChildren<TextMeshProUGUI>().SetText(turnController.CurrentFighter.GetComponent<FighterStats>().Psynergies[0].Description);
                contentItems[0].transform.Find("Highlight").GetComponent<Image>().enabled = true;
                break;
        }
    }
    public void ShowContentPanel(bool choice)
    {
        if (choice)
        {
            contentScrollView.transform.parent.parent.parent.gameObject.SetActive(true);
            helpPanel.SetActive(true);
        }
        else
        {
            contentScrollView.transform.parent.parent.parent.gameObject.SetActive(false);
            helpPanel.SetActive(false);

            for (int i = 0; i < contentScrollView.transform.childCount; i++)
            {
                Destroy(contentScrollView.transform.GetChild(i).gameObject);
                contentItems = new List<GameObject>();
            }
        }
    }

    public void MoveContentPanel(string direction)
    {
        if (direction == "UP")
        {
            if (contentCursorIndex != 0)
            {
                contentCursorIndex--;
            } else
            {
                contentCursorIndex = contentItems.Count - 1;
            }

            foreach (GameObject item in contentItems)
            {
                item.transform.Find("Highlight").GetComponent<Image>().enabled = false;
            }

            contentItems[contentCursorIndex].transform.Find("Highlight").GetComponent<Image>().enabled = true;
        } else if (direction == "DOWN") 
        {
           
            
                if (contentCursorIndex != contentItems.Count -1)
                {
                    contentCursorIndex++;
                }
                else
                {
                contentCursorIndex = 0;
                }

                foreach (GameObject item in contentItems)
                {
                    item.transform.Find("Highlight").GetComponent<Image>().enabled = false;
                }

                contentItems[contentCursorIndex].transform.Find("Highlight").GetComponent<Image>().enabled = true;
            
        }

        helpPanel.GetComponentInChildren<TextMeshProUGUI>().SetText(turnController.CurrentFighter.GetComponent<FighterStats>().Psynergies[contentCursorIndex].Description);



    }

    public string GetContentItem ()
    {
        switch (lastContent)
        {
            case "Psynergy":
                if (contentItems[contentCursorIndex].GetComponentInChildren<TextMeshProUGUI>().color != Color.grey)
                {
                    GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.CONFIRM_SOUND);
                    return contentItems[contentCursorIndex].name;
                } else
                {
                    GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.NEGATIVE_SOUND);
                    return null;
                }
            default:
                return null;
        }
    }
}
