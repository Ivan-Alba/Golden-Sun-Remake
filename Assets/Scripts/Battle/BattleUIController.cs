using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleUIController : MonoBehaviour
{

    [Header("UI Canvas & Panels")]
    [SerializeField] private GameObject battleCanvas;
    [SerializeField] private GameObject turnsPanel;
    [SerializeField] private GameObject ordersPanel;
    [SerializeField] private TextMeshProUGUI commandInfoText;
    [SerializeField] private GameObject helpPanel;
    [SerializeField] private GameObject contentScrollView;

    [Header("UI Prefabs & Collections")]
    [SerializeField] private GameObject contentItemPrefab;
    [SerializeField] private List<GameObject> ordersButtons = new List<GameObject>();
    [SerializeField] private GameObject[] heroInfoPanels = new GameObject[4];

    List<GameObject> contentItems;
    int contentCursorIndex;
    string lastContent;

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
        // --- POPULATE TURN BOXES FROM PANELS ---
        turnsUIElements.Clear();
        if (turnsPanel != null)
        {
            foreach (Transform turn in turnsPanel.GetComponentsInChildren<Transform>())
            {
                if (!turn.gameObject.Equals(turnsPanel) && !turn.gameObject.name.StartsWith("Highlight"))
                {
                    turnsUIElements.Add(turn);
                }
            }
        }

        maxTurnBoxes = turnsUIElements.Count;

        // --- CACHE HERO PANELS IN PREFAB ---
        for (int i = 0; i < 4; i++)
        {
            if (heroInfoPanels[i] == null && battleCanvas != null)
            {
                Transform panelTrans = battleCanvas.transform.Find($"Hero{i}Panel");
                if (panelTrans != null)
                {
                    heroInfoPanels[i] = panelTrans.gameObject;
                }
            }
        }
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
    public void ShowSelectedTarget(List<GameObject> enemiesPointed)
    {
        //SHOW ON TURN BAR THE ENEMY TARGET
        for (int i = 0; i < maxTurnBoxes; i++)
        {
            turnsPanel.transform.GetChild(i).Find("Highlight").gameObject.SetActive(false);

            foreach (GameObject enemy in enemiesPointed)
            {
                if (turnsOrdered.ElementAt(i).Key.Equals(enemy))
                {
                    turnsPanel.transform.GetChild(i).Find("Highlight").gameObject.SetActive(true);
                }
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
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SFXType.UI_Move);
        }
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

            //HP
            if (heroStats.CurrHp <= 0)
            {
                hp.GetComponent<TextMeshProUGUI>().color = Color.red;
                hp.GetComponent<TextMeshProUGUI>().SetText($"HP 0 / {heroStats.MaxHp}");
            }
            else if (heroStats.CurrHp <= heroStats.MaxHp * 0.3f)
            {
                hp.GetComponent<TextMeshProUGUI>().color = Color.yellow;
                hp.GetComponent<TextMeshProUGUI>().SetText($"HP {heroStats.CurrHp} / {heroStats.MaxHp}");
            }
            else
            {
                hp.GetComponent<TextMeshProUGUI>().color = Color.white;
                hp.GetComponent<TextMeshProUGUI>().SetText($"HP {heroStats.CurrHp} / {heroStats.MaxHp}");
            }

            //PP
            if (heroStats.CurrPp <= 0)
            {
                pp.GetComponent<TextMeshProUGUI>().color = Color.red;
                pp.GetComponent<TextMeshProUGUI>().SetText($"PP 0 / {heroStats.MaxPp}");
            }
            else if (heroStats.CurrPp <= heroStats.MaxPp * 0.3f)
            {
                pp.GetComponent<TextMeshProUGUI>().color = Color.yellow;
                pp.GetComponent<TextMeshProUGUI>().SetText($"PP {heroStats.CurrPp} / {heroStats.MaxPp}");
            }
            else
            {
                pp.GetComponent<TextMeshProUGUI>().color = Color.white;
                pp.GetComponent<TextMeshProUGUI>().SetText($"PP {heroStats.CurrPp} / {heroStats.MaxPp}");
            }


            name.GetComponent<TextMeshProUGUI>().SetText($"{heroStats.Name}");

        }
    }

    private void SetInfoText()
    {
        if (commandInfoText != null)
        {
            commandInfoText.SetText(commandsText[commandSelected]);
        }
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
        contentItems = new List<GameObject>();
        switch (order)
        {
            case "Psynergy":
                lastContent = "Psynergy";

                if (turnController.CurrentFighter.GetComponent<FighterStats>().Psynergies.Count != 0)
                {
                    foreach (Psynergy psynergy in turnController.CurrentFighter.GetComponent<FighterStats>().Psynergies)
                    {
                        if (psynergy.IsBattlePsynergy)
                        {
                            GameObject newItem = Instantiate(contentItemPrefab, contentScrollView.transform);
                            newItem.transform.Find("Name").GetComponent<TextMeshProUGUI>().SetText(psynergy.Name);
                            newItem.transform.Find("Number").GetComponent<TextMeshProUGUI>().SetText($"{psynergy.PpCost} PP");
                            newItem.transform.Find("Icon").GetComponent<Image>().sprite = psynergy.Icon;
                            newItem.transform.Find("ElementIcon").GetComponent<Image>().sprite = psynergy.ElementIcon;
                            newItem.name = psynergy.SlugName;
                            if (turnController.CurrentFighter.GetComponent<FighterStats>().CurrPp < psynergy.PpCost) //Not enough PP to use a psynergy
                            {
                                foreach (TextMeshProUGUI text in newItem.GetComponentsInChildren<TextMeshProUGUI>())
                                {
                                    text.color = Color.grey;
                                }
                            }
                            contentItems.Add(newItem);
                        }
                    }
                    helpPanel.GetComponentInChildren<TextMeshProUGUI>().SetText(turnController.CurrentFighter.GetComponent<FighterStats>().Psynergies[0].Description);


                } else
                {
                    //NONE PSYNERGY
                    GameObject newItem = Instantiate(contentItemPrefab, contentScrollView.transform);
                    newItem.transform.Find("Name").GetComponent<TextMeshProUGUI>().SetText("");
                    newItem.transform.Find("Number").GetComponent<TextMeshProUGUI>().SetText("");
                    newItem.transform.Find("Icon").GetComponent<Image>().enabled = false;
                    newItem.transform.Find("ElementIcon").GetComponent<Image>().enabled = false;
                    newItem.name = "";
                    contentItems.Add(newItem);
                    helpPanel.GetComponentInChildren<TextMeshProUGUI>().SetText("No tienes psinergía.");
                }


                contentCursorIndex = 0;
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

        if (turnController.CurrentFighter.GetComponent<FighterStats>().Psynergies.Count != 0)
        {
            helpPanel.GetComponentInChildren<TextMeshProUGUI>().SetText(turnController.CurrentFighter.GetComponent<FighterStats>().Psynergies[contentCursorIndex].Description);
        }



    }

    public string GetContentItem ()
    {
        switch (lastContent)
        {
            case "Psynergy":
                if (contentItems[contentCursorIndex].GetComponentInChildren<TextMeshProUGUI>().color != Color.grey && contentItems[contentCursorIndex].name != "")
                {
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlaySFX(SFXType.UI_Confirm);
                    }
                    return contentItems[contentCursorIndex].name;
                } else
                {
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlaySFX(SFXType.UI_Cancel);
                    }
                    return null;
                }
            default:
                return null;
        }
    }
}
