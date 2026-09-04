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

    private List<GameObject> contentItems = new List<GameObject>();
    private int contentCursorIndex;
    private string lastContent;

    // Turn UI References & State
    private TurnController turnController;
    private List<Transform> turnsUIElements = new List<Transform>();
    private List<KeyValuePair<GameObject, int>> turnsOrdered = new List<KeyValuePair<GameObject, int>>();
    private int maxTurnBoxes;

    private int commandSelected;
    private readonly string[] commandsText = new string[8]
    {
        "Atacar", "Psinergía", "Djinn", "Invocar", "Defensa", "Objeto", "Estado", "Huir"
    };

    #region Properties

    public int CommandSelected { get => commandSelected; set => commandSelected = value; }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Cache local components
        turnController = GetComponent<TurnController>();

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

        // --- CACHE HERO PANELS ---
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

    private void OnEnable()
    {
        SubscribeToPartyEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromPartyEvents();
    }

    #endregion

    #region Event Subscriptions & Hero Panel Updates

    private void SubscribeToPartyEvents()
    {
        GameEvents.Fighter.OnHpChanged -= HandleFighterResourceChanged;
        GameEvents.Fighter.OnHpChanged += HandleFighterResourceChanged;

        GameEvents.Fighter.OnPpChanged -= HandleFighterResourceChanged;
        GameEvents.Fighter.OnPpChanged += HandleFighterResourceChanged;
    }

    private void UnsubscribeFromPartyEvents()
    {
        GameEvents.Fighter.OnHpChanged -= HandleFighterResourceChanged;
        GameEvents.Fighter.OnPpChanged -= HandleFighterResourceChanged;
    }

    private void HandleFighterResourceChanged(GameObject fighterObj, int current, int max)
    {
        if (GameManager.Instance == null || fighterObj == null) return;

        List<GameObject> heroParty = GameManager.Instance.GetParty();
        if (heroParty != null && heroParty.Contains(fighterObj))
        {
            RefreshHeroInfoPanel();
        }
    }

    public void SetHeroInfoPanel(int index)
    {
        if (index >= 0 && index < heroInfoPanels.Length && battleCanvas != null)
        {
            Transform panelTrans = battleCanvas.transform.Find($"Hero{index}Panel");
            if (panelTrans != null)
            {
                heroInfoPanels[index] = panelTrans.gameObject;
                heroInfoPanels[index].SetActive(true);
            }
        }
    }

    public void ShowBattleCanvas(bool choice)
    {
        if (battleCanvas != null) battleCanvas.SetActive(choice);
    }

    public void RefreshHeroInfoPanel()
    {
        if (GameManager.Instance == null) return;

        List<GameObject> heroParty = GameManager.Instance.GetParty();
        if (heroParty == null) return;

        for (int i = 0; i < heroParty.Count; i++)
        {
            if (i >= heroInfoPanels.Length || heroInfoPanels[i] == null) continue;

            Transform hpTrans = heroInfoPanels[i].transform.Find("Hp");
            Transform ppTrans = heroInfoPanels[i].transform.Find("Pp");
            Transform nameTrans = heroInfoPanels[i].transform.Find("Name");

            if (!heroParty[i].TryGetComponent<FighterStats>(out var heroStats)) continue;

            // HP Text & Coloring
            if (hpTrans != null && hpTrans.TryGetComponent<TextMeshProUGUI>(out var hpText))
            {
                if (heroStats.CurrHp <= 0)
                {
                    hpText.color = Color.red;
                    hpText.SetText($"HP 0 / {heroStats.MaxHp}");
                }
                else if (heroStats.CurrHp <= heroStats.MaxHp * 0.3f)
                {
                    hpText.color = Color.yellow;
                    hpText.SetText($"HP {heroStats.CurrHp} / {heroStats.MaxHp}");
                }
                else
                {
                    hpText.color = Color.white;
                    hpText.SetText($"HP {heroStats.CurrHp} / {heroStats.MaxHp}");
                }
            }

            // PP Text & Coloring
            if (ppTrans != null && ppTrans.TryGetComponent<TextMeshProUGUI>(out var ppText))
            {
                if (heroStats.CurrPp <= 0)
                {
                    ppText.color = Color.red;
                    ppText.SetText($"PP 0 / {heroStats.MaxPp}");
                }
                else if (heroStats.CurrPp <= heroStats.MaxPp * 0.3f)
                {
                    ppText.color = Color.yellow;
                    ppText.SetText($"PP {heroStats.CurrPp} / {heroStats.MaxPp}");
                }
                else
                {
                    ppText.color = Color.white;
                    ppText.SetText($"PP {heroStats.CurrPp} / {heroStats.MaxPp}");
                }
            }

            // Name
            if (nameTrans != null && nameTrans.TryGetComponent<TextMeshProUGUI>(out var nameText))
            {
                nameText.SetText($"{heroStats.Name}");
            }
        }
    }

    #endregion

    #region Turn Timeline Bar Control

    /// <summary>
    /// Updates the turn bar images based on the projected turn sequence from TurnController.
    /// </summary>
    public void SetTurnsPanel(List<KeyValuePair<GameObject, int>> newTurnsOrdered)
    {
        turnsOrdered = newTurnsOrdered ?? new List<KeyValuePair<GameObject, int>>();

        for (int i = 0; i < turnsUIElements.Count; i++)
        {
            if (i < turnsOrdered.Count && turnsOrdered[i].Key != null)
            {
                turnsUIElements[i].gameObject.SetActive(true);

                if (turnsOrdered[i].Key.TryGetComponent<FighterStats>(out var stats) && stats.TurnBarImage != null)
                {
                    Image iconImg = turnsUIElements[i].GetComponent<Image>();
                    if (iconImg != null)
                    {
                        iconImg.sprite = stats.TurnBarImage;
                        iconImg.enabled = true;
                    }
                }
            }
            else
            {
                // Disable unused UI turn slots
                turnsUIElements[i].gameObject.SetActive(false);
            }
        }
    }

    public void ShowTurnsPanel(bool choice)
    {
        if (turnsPanel != null) turnsPanel.SetActive(choice);
    }

    /// <summary>
    /// Highlights turn bar slots corresponding to targeted entities.
    /// </summary>
    public void ShowSelectedTarget(List<GameObject> targetedEntities)
    {
        if (turnsPanel == null || targetedEntities == null) return;

        for (int i = 0; i < maxTurnBoxes; i++)
        {
            if (i >= turnsPanel.transform.childCount) break;

            Transform highlightTrans = turnsPanel.transform.GetChild(i).Find("Highlight");
            if (highlightTrans == null) continue;

            bool isTargeted = false;
            if (i < turnsOrdered.Count && turnsOrdered[i].Key != null)
            {
                isTargeted = targetedEntities.Contains(turnsOrdered[i].Key);
            }

            highlightTrans.gameObject.SetActive(isTargeted);
        }
    }

    public void HideSelectedTarget()
    {
        if (turnsPanel == null) return;

        for (int i = 0; i < maxTurnBoxes; i++)
        {
            if (i >= turnsPanel.transform.childCount) break;

            Transform highlightTrans = turnsPanel.transform.GetChild(i).Find("Highlight");
            if (highlightTrans != null)
            {
                highlightTrans.gameObject.SetActive(false);
            }
        }
    }

    #endregion

    #region Orders & Submenu Navigation

    public void ordersPanelMove(string direction)
    {
        if (ordersPanel == null || ordersPanel.transform.childCount < 2) return;

        int totalCommands = ordersPanel.transform.childCount - 1;

        if (direction == "RIGHT")
        {
            commandSelected = (commandSelected + 1) % totalCommands;
        }
        else if (direction == "LEFT")
        {
            commandSelected = (commandSelected - 1 + totalCommands) % totalCommands;
        }

        SetInfoText();
        RefreshButtonAnimation();

        GameEvents.Audio.RequestPlaySFX(SFXType.UI_Move);
    }

    public void ShowOrdersPanel(bool choice)
    {
        if (ordersPanel != null)
        {
            ordersPanel.SetActive(choice);

            if (choice)
            {
                commandSelected = 0;
                SetInfoText();
                RefreshButtonAnimation();
            }
        }
    }

    private void SetInfoText()
    {
        if (commandInfoText != null && commandSelected >= 0 && commandSelected < commandsText.Length)
        {
            commandInfoText.SetText(commandsText[commandSelected]);
        }
    }

    private void RefreshButtonAnimation()
    {
        for (int i = 0; i < ordersButtons.Count; i++)
        {
            if (ordersButtons[i] != null && ordersButtons[i].TryGetComponent<Animator>(out var anim))
            {
                anim.SetBool("Selected", i == commandSelected);
            }
        }
    }

    public void SetContentPanel(string order)
    {
        if (turnController == null) turnController = GetComponent<TurnController>();

        // Clear existing content items
        ClearContentItems();

        switch (order)
        {
            case "Psynergy":
                lastContent = "Psynergy";

                FighterController activeFighter = turnController != null ? turnController.CurrentFighter : null;
                FighterStats activeStats = activeFighter != null ? activeFighter.GetComponent<FighterStats>() : null;

                if (activeStats != null && activeStats.Psynergies != null && activeStats.Psynergies.Count > 0)
                {
                    foreach (PsynergySO psynergy in activeStats.Psynergies)
                    {
                        if (psynergy == null || !psynergy.IsBattlePsynergy) continue;

                        GameObject newItem = Instantiate(contentItemPrefab, contentScrollView.transform);
                        newItem.name = psynergy.SlugName;

                        newItem.transform.Find("Name")?.GetComponent<TextMeshProUGUI>()?.SetText(psynergy.PsynergyName);
                        newItem.transform.Find("Number")?.GetComponent<TextMeshProUGUI>()?.SetText($"{psynergy.PpCost} PP");

                        Transform iconTrans = newItem.transform.Find("Icon");
                        if (iconTrans != null && iconTrans.TryGetComponent<Image>(out var iconImg))
                        {
                            iconImg.sprite = psynergy.Icon;
                        }

                        Transform elemTrans = newItem.transform.Find("ElementIcon");
                        if (elemTrans != null && elemTrans.TryGetComponent<Image>(out var elemImg))
                        {
                            if (psynergy != null)
                            {
                                GameEvents.Database.RequestElementIcon(psynergy.Element, (iconSprite) =>
                                {
                                    elemImg.sprite = iconSprite;
                                });
                            }
                        }

                        // Grey out text if not enough PP
                        if (activeStats.CurrPp < psynergy.PpCost)
                        {
                            foreach (TextMeshProUGUI text in newItem.GetComponentsInChildren<TextMeshProUGUI>())
                            {
                                text.color = Color.grey;
                            }
                        }

                        contentItems.Add(newItem);
                    }

                    if (helpPanel != null && activeStats.Psynergies.Count > 0)
                    {
                        helpPanel.GetComponentInChildren<TextMeshProUGUI>()?.SetText(activeStats.Psynergies[0].Description);
                    }
                }
                else
                {
                    // Empty entry when no Psynergy available
                    GameObject newItem = Instantiate(contentItemPrefab, contentScrollView.transform);
                    newItem.name = "";

                    newItem.transform.Find("Name")?.GetComponent<TextMeshProUGUI>()?.SetText("");
                    newItem.transform.Find("Number")?.GetComponent<TextMeshProUGUI>()?.SetText("");

                    Transform iconTrans = newItem.transform.Find("Icon");
                    if (iconTrans != null && iconTrans.TryGetComponent<Image>(out var iconImg))
                    {
                        iconImg.enabled = false;
                    }

                    Transform elemTrans = newItem.transform.Find("ElementIcon");
                    if (elemTrans != null && elemTrans.TryGetComponent<Image>(out var elemImg))
                    {
                        elemImg.enabled = false;
                    }

                    contentItems.Add(newItem);

                    if (helpPanel != null)
                    {
                        helpPanel.GetComponentInChildren<TextMeshProUGUI>()?.SetText("No tienes psinergía.");
                    }
                }

                contentCursorIndex = 0;
                if (contentItems.Count > 0 && contentItems[0] != null)
                {
                    Transform highlightTrans = contentItems[0].transform.Find("Highlight");
                    if (highlightTrans != null && highlightTrans.TryGetComponent<Image>(out var hlImg))
                    {
                        hlImg.enabled = true;
                    }
                }
                break;
        }
    }

    public void ShowContentPanel(bool choice)
    {
        if (contentScrollView != null)
        {
            Transform rootContainer = contentScrollView.transform.parent?.parent?.parent;
            if (rootContainer != null) rootContainer.gameObject.SetActive(choice);
        }

        if (helpPanel != null) helpPanel.SetActive(choice);

        if (!choice)
        {
            ClearContentItems();
        }
    }

    private void ClearContentItems()
    {
        if (contentScrollView != null)
        {
            for (int i = contentScrollView.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(contentScrollView.transform.GetChild(i).gameObject);
            }
        }
        contentItems.Clear();
    }

    public void MoveContentPanel(string direction)
    {
        if (contentItems.Count == 0) return;

        if (direction == "UP")
        {
            contentCursorIndex = (contentCursorIndex == 0) ? contentItems.Count - 1 : contentCursorIndex - 1;
        }
        else if (direction == "DOWN")
        {
            contentCursorIndex = (contentCursorIndex == contentItems.Count - 1) ? 0 : contentCursorIndex + 1;
        }

        // Update highlight cursor position
        for (int i = 0; i < contentItems.Count; i++)
        {
            if (contentItems[i] == null) continue;

            Transform hlTrans = contentItems[i].transform.Find("Highlight");
            if (hlTrans != null && hlTrans.TryGetComponent<Image>(out var hlImg))
            {
                hlImg.enabled = (i == contentCursorIndex);
            }
        }

        // Update description panel
        if (turnController != null && turnController.CurrentFighter != null)
        {
            if (turnController.CurrentFighter.TryGetComponent<FighterStats>(out var stats))
            {
                if (stats.Psynergies != null && contentCursorIndex < stats.Psynergies.Count && helpPanel != null)
                {
                    helpPanel.GetComponentInChildren<TextMeshProUGUI>()?.SetText(stats.Psynergies[contentCursorIndex].Description);
                }
            }
        }
    }

    public string GetContentItem()
    {
        if (lastContent == "Psynergy" && contentItems.Count > contentCursorIndex && contentItems[contentCursorIndex] != null)
        {
            TextMeshProUGUI itemText = contentItems[contentCursorIndex].GetComponentInChildren<TextMeshProUGUI>();

            if (itemText != null && itemText.color != Color.grey && !string.IsNullOrEmpty(contentItems[contentCursorIndex].name))
            {
                GameEvents.Audio.RequestPlaySFX(SFXType.UI_Confirm);
                return contentItems[contentCursorIndex].name;
            }
            else
            {
                GameEvents.Audio.RequestPlaySFX(SFXType.UI_Cancel);
                return null;
            }
        }

        return null;
    }

    #endregion
}