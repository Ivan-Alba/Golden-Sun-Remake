using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    #region Enums

    public enum MenuTab
    {
        Status,
        Items,
        Psynergy,
        Djinn,
        Encyclopedia
    }

    #endregion

    #region Serialized & Private Fields

    [Header("UI Document Reference")]
    private UIDocument uiDocument;
    private VisualElement rootElement;

    [Header("Page Assets")]
    [SerializeField] private VisualTreeAsset pageStatusAsset;
    [SerializeField] private VisualTreeAsset pageItemsAsset;
    [SerializeField] private VisualTreeAsset pagePsynergyAsset;
    [SerializeField] private VisualTreeAsset pageDjinnAsset;

    [Header("Item Page Templates")]
    [SerializeField] private VisualTreeAsset templateInfoConsumable;
    [SerializeField] private VisualTreeAsset templateInfoEquipable;
    [SerializeField] private VisualTreeAsset templateInfoWeapon;
    [SerializeField] private VisualTreeAsset templateInfoPsynergyRelic;
    [SerializeField] private VisualTreeAsset templateInfoKeyItem;
    [SerializeField] private VisualTreeAsset itemSlotAsset;

    [Header("Djinn Page Templates")]
    [SerializeField] private VisualTreeAsset globalViewAsset;
    [SerializeField] private VisualTreeAsset miniPsynergyCardAsset;
    [SerializeField] private VisualTreeAsset djinnSlotCardAAsset;
    [SerializeField] private VisualTreeAsset djinnSlotCardBAsset;
    [SerializeField] private Sprite targetSingleIcon;
    [SerializeField] private Sprite targetGroupIcon;

    // Header Elements
    private VisualElement headerHeroesBar;
    private Label labelGold;
    private Label labelLocation;
    private Label labelTime;
    private float timeUpdateTimer = 0f;

    // Content Area
    private VisualElement pageContent;

    // Djinn Views Containers (Sister Elements in DOM)
    private VisualElement djinnViewAContainer;
    private VisualElement djinnViewBContainer;

    // Bookmarks (Tabs)
    private Button btnStatus;
    private Button btnItems;
    private Button btnPsynergy;
    private Button btnDjinn;
    private Button btnEncyclopedia;

    // Hero Selection Header Cards
    private Button[] btnHeroes = new Button[4];
    private VisualElement[] imgHeroPortraits = new VisualElement[4];
    private Label[] labelHeroNames = new Label[4];
    private Label[] labelHeroLevels = new Label[4];
    private Label[] labelHeroHpVals = new Label[4];
    private Label[] labelHeroPpVals = new Label[4];
    private VisualElement[] barHeroHpFills = new VisualElement[4];
    private VisualElement[] barHeroPpFills = new VisualElement[4];

    // Page Controllers
    private PageStatusController statusPageController;
    private PageItemsController itemsPageController;
    private PagePsynergyController psynergyPageController;
    private PageDjinnController djinnPageController;

    // State Trackers
    private MenuTab currentTab = MenuTab.Status;
    private int selectedHeroIndex = 0;
    private bool isMenuOpen = false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        rootElement = uiDocument.rootVisualElement;
        if (rootElement == null) return;

        BindUIElements();
        RegisterUICallbacks();

        rootElement.style.display = DisplayStyle.None;
        isMenuOpen = false;
    }

    private void OnDisable()
    {
        UnregisterUICallbacks();
    }

    private void Start()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnToggleMenuPressed += HandleToggleMenuInput;
        }
    }

    private void Update()
    {
        if (!isMenuOpen || GameManager.Instance == null || labelTime == null) return;

        timeUpdateTimer += Time.deltaTime;

        if (timeUpdateTimer >= 1.0f)
        {
            timeUpdateTimer = 0f;
            UpdatePlayTimeText();
        }
    }

    private void UpdatePlayTimeText()
    {
        TimeSpan timeSpan = TimeSpan.FromSeconds(GameManager.Instance.TotalPlayed);
        labelTime.text = string.Format("{0:D2}:{1:D2}:{2:D2}", (int)timeSpan.TotalHours, timeSpan.Minutes, timeSpan.Seconds);
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnToggleMenuPressed -= HandleToggleMenuInput;
            UnsubscribeFromMenuInputs();
        }
    }

    #endregion

    #region UI Binding & Callbacks

    private void BindUIElements()
    {
        headerHeroesBar = rootElement.Q<VisualElement>("header_heroes_bar");
        labelGold = rootElement.Q<Label>("label_gold");
        labelLocation = rootElement.Q<Label>("label_location");
        labelTime = rootElement.Q<Label>("label_time");

        pageContent = rootElement.Q<VisualElement>("page_content");

        btnStatus = rootElement.Q<Button>("btn_status");
        btnItems = rootElement.Q<Button>("btn_items");
        btnPsynergy = rootElement.Q<Button>("btn_psynergy");
        btnDjinn = rootElement.Q<Button>("btn_djinn");
        btnEncyclopedia = rootElement.Q<Button>("btn_encyclopedia");

        for (int i = 0; i < 4; i++)
        {
            btnHeroes[i] = rootElement.Q<Button>($"btn_hero{i}");

            imgHeroPortraits[i] = rootElement.Q<VisualElement>($"header_hero_portrait_{i}")
                                ?? btnHeroes[i]?.Q<VisualElement>(className: "header-hero-portrait");

            labelHeroNames[i] = rootElement.Q<Label>($"label_hero_name_{i}");
            labelHeroLevels[i] = rootElement.Q<Label>($"label_hero_level_{i}");
            labelHeroHpVals[i] = rootElement.Q<Label>($"label_hero_hp_val_{i}");
            labelHeroPpVals[i] = rootElement.Q<Label>($"label_hero_pp_val_{i}");
            barHeroHpFills[i] = rootElement.Q<VisualElement>($"bar_hero_hp_{i}");
            barHeroPpFills[i] = rootElement.Q<VisualElement>($"bar_hero_pp_{i}");
        }
    }

    private void RegisterUICallbacks()
    {
        btnStatus?.RegisterCallback<ClickEvent>(e => SelectTab(MenuTab.Status));
        btnItems?.RegisterCallback<ClickEvent>(e => SelectTab(MenuTab.Items));
        btnPsynergy?.RegisterCallback<ClickEvent>(e => SelectTab(MenuTab.Psynergy));
        btnDjinn?.RegisterCallback<ClickEvent>(e => SelectTab(MenuTab.Djinn));
        btnEncyclopedia?.RegisterCallback<ClickEvent>(e => SelectTab(MenuTab.Encyclopedia));

        for (int i = 0; i < btnHeroes.Length; i++)
        {
            int index = i;
            btnHeroes[i]?.RegisterCallback<ClickEvent>(e => SelectHero(index));
        }
    }

    private void UnregisterUICallbacks()
    {
        btnStatus?.UnregisterCallback<ClickEvent>(e => SelectTab(MenuTab.Status));
        btnItems?.UnregisterCallback<ClickEvent>(e => SelectTab(MenuTab.Items));
        btnPsynergy?.UnregisterCallback<ClickEvent>(e => SelectTab(MenuTab.Psynergy));
        btnDjinn?.UnregisterCallback<ClickEvent>(e => SelectTab(MenuTab.Djinn));
        btnEncyclopedia?.UnregisterCallback<ClickEvent>(e => SelectTab(MenuTab.Encyclopedia));

        for (int i = 0; i < btnHeroes.Length; i++)
        {
            int index = i;
            btnHeroes[i]?.UnregisterCallback<ClickEvent>(e => SelectHero(index));
        }
    }

    #endregion

    #region Input Event Subscriptions & Handlers

    private void SubscribeToMenuInputs()
    {
        if (InputManager.Instance == null) return;

        InputManager.Instance.OnMenuNextTabPressed += HandleNextTab;
        InputManager.Instance.OnMenuPreviousTabPressed += HandlePreviousTab;
        InputManager.Instance.OnMenuCancelPressed += HandleCancelInput;
    }

    private void UnsubscribeFromMenuInputs()
    {
        if (InputManager.Instance == null) return;

        InputManager.Instance.OnMenuNextTabPressed -= HandleNextTab;
        InputManager.Instance.OnMenuPreviousTabPressed -= HandlePreviousTab;
        InputManager.Instance.OnMenuCancelPressed -= HandleCancelInput;
    }

    private void HandleToggleMenuInput()
    {
        if (!isMenuOpen)
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.WorldExploration)
            {
                SetMenuVisibility(true);
            }
        }
        else
        {
            SetMenuVisibility(false);
        }
    }

    private void HandleCancelInput()
    {
        if (!isMenuOpen) return;

        if (itemsPageController != null && itemsPageController.IsTargetingItem)
        {
            itemsPageController.CancelItemTargeting();
            return;
        }

        SetMenuVisibility(false);
    }

    private void HandleNextTab() => CycleTab(1);
    private void HandlePreviousTab() => CycleTab(-1);

    #endregion

    #region Navigation & Page Management

    public void SetMenuVisibility(bool visible)
    {
        isMenuOpen = visible;
        rootElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(visible ? GameManager.GameState.InMenu : GameManager.GameState.WorldExploration);
        }

        if (visible)
        {
            timeUpdateTimer = 0f;
            UpdatePlayTimeText();

            SubscribeToMenuInputs();
            RefreshGlobalHeaderInfo();
            SelectTab(currentTab);
        }
        else
        {
            itemsPageController?.CancelItemTargeting();
            psynergyPageController?.CancelPsynergyTargeting();
            UnsubscribeFromMenuInputs();
        }
    }

    public void SelectTab(MenuTab tab)
    {
        itemsPageController?.CancelItemTargeting();
        psynergyPageController?.CancelPsynergyTargeting();

        currentTab = tab;

        bool needsHeroSelector = IsHeroSelectorVisible();
        if (headerHeroesBar != null)
        {
            headerHeroesBar.style.display = needsHeroSelector ? DisplayStyle.Flex : DisplayStyle.None;
        }

        Debug.Log($"[MainMenu] Active Tab: {tab}");
        LoadTabContent(tab);
        UpdateHeroCardsHighlight();
    }

    public void SelectHero(int heroIndex)
    {
        if ((itemsPageController != null && itemsPageController.IsTargetingItem) ||
            (psynergyPageController != null && psynergyPageController.IsTargetingPsynergy))
        {
            return;
        }

        List<HeroStats> party = GetPartyHeroStatsList();
        if (party != null && party.Count > 0)
        {
            selectedHeroIndex = Mathf.Clamp(heroIndex, 0, party.Count - 1);
        }
        else
        {
            selectedHeroIndex = 0;
        }

        Debug.Log($"[MainMenu] Selected Hero Index: {selectedHeroIndex}");
        UpdateHeroCardsHighlight();
        RefreshActiveTabHeroData();
    }

    private void UpdateHeroCardsHighlight()
    {
        for (int i = 0; i < btnHeroes.Length; i++)
        {
            if (btnHeroes[i] == null) continue;

            if (i == selectedHeroIndex)
            {
                btnHeroes[i].AddToClassList("active");
            }
            else
            {
                btnHeroes[i].RemoveFromClassList("active");
            }
        }
    }

    private void CycleTab(int direction)
    {
        int totalTabs = Enum.GetValues(typeof(MenuTab)).Length;
        int nextTab = ((int)currentTab + direction + totalTabs) % totalTabs;
        SelectTab((MenuTab)nextTab);
    }

    private bool IsHeroSelectorVisible()
    {
        return currentTab == MenuTab.Items || currentTab == MenuTab.Psynergy || currentTab == MenuTab.Djinn;
    }

    #endregion

    #region Sub-page Content Loading

    private void LoadTabContent(MenuTab tab)
    {
        if (pageContent == null) return;

        pageContent.Clear();
        statusPageController = null;
        itemsPageController = null;
        psynergyPageController = null;
        djinnPageController = null;

        switch (tab)
        {
            case MenuTab.Status:
                LoadStatusPage();
                break;
            case MenuTab.Items:
                LoadItemsPage();
                break;
            case MenuTab.Psynergy:
                LoadPsynergyPage();
                break;
            case MenuTab.Djinn:
                LoadDjinnPage();
                break;
            case MenuTab.Encyclopedia:
                LoadEncyclopediaPage();
                break;
        }
    }

    private void LoadStatusPage()
    {
        if (pageStatusAsset == null) return;

        TemplateContainer statusInstance = pageStatusAsset.Instantiate();
        statusInstance.style.flexGrow = 1;
        pageContent.Add(statusInstance);

        statusPageController = new PageStatusController(statusInstance);
        statusPageController.PopulateData(GetPartyHeroStatsList());
    }

    private void LoadItemsPage()
    {
        if (pageItemsAsset == null) return;

        TemplateContainer itemsInstance = pageItemsAsset.Instantiate();
        itemsInstance.style.flexGrow = 1;
        pageContent.Add(itemsInstance);

        itemsPageController = new PageItemsController(itemsInstance);
        itemsPageController.SetParty(GetPartyHeroStatsList());
        itemsPageController.SetExtraInfoTemplates(
            templateInfoConsumable,
            templateInfoEquipable,
            templateInfoWeapon,
            templateInfoPsynergyRelic,
            templateInfoKeyItem
        );

        itemsPageController.OnItemUsedOnHero += RefreshHeaderHeroCards;

        HeroStats selectedHeroStats = GetPartyMemberComponent<HeroStats>(selectedHeroIndex);
        HeroInventory selectedHeroInventory = GetPartyMemberComponent<HeroInventory>(selectedHeroIndex);
        HeroEquipment selectedHeroEquipment = GetPartyMemberComponent<HeroEquipment>(selectedHeroIndex);

        itemsPageController.PopulateData(selectedHeroStats, selectedHeroInventory, selectedHeroEquipment, itemSlotAsset);
    }

    private void LoadPsynergyPage()
    {
        if (pagePsynergyAsset == null) return;

        TemplateContainer psynergyInstance = pagePsynergyAsset.Instantiate();
        psynergyInstance.style.flexGrow = 1;
        pageContent.Add(psynergyInstance);

        psynergyPageController = new PagePsynergyController(psynergyInstance);
        psynergyPageController.OnPsynergyUsedOnHero += RefreshHeaderHeroCards;

        HeroStats selectedHeroStats = GetPartyMemberComponent<HeroStats>(selectedHeroIndex);
        GameObject selectedHeroObj = GetPartyMemberGameObject(selectedHeroIndex);

        psynergyPageController.PopulateData(selectedHeroStats, selectedHeroObj);
    }

    private void LoadDjinnPage()
    {
        if (pageDjinnAsset == null || globalViewAsset == null) return;

        // English comment: Instantiate Vista A and Vista B as sister containers inside pageContent
        TemplateContainer instanceA = pageDjinnAsset.Instantiate();
        instanceA.style.flexGrow = 1;
        instanceA.style.display = DisplayStyle.Flex; // Default Vista A visible

        TemplateContainer instanceB = globalViewAsset.Instantiate();
        instanceB.style.flexGrow = 1;
        instanceB.style.display = DisplayStyle.None; // Default Vista B hidden

        pageContent.Add(instanceA);
        pageContent.Add(instanceB);

        djinnViewAContainer = instanceA;
        djinnViewBContainer = instanceB;


        // English comment: Pass both root containers to PageDjinnController
        djinnPageController = new PageDjinnController(instanceA, instanceB);
        djinnPageController.SetParty(GetPartyHeroStatsList());
        djinnPageController.SetTemplates(miniPsynergyCardAsset, djinnSlotCardAAsset, djinnSlotCardBAsset);
        djinnPageController.SetTargetIcons(targetSingleIcon, targetGroupIcon);

        djinnPageController.OnDjinnStateChanged += RefreshHeaderHeroCards;
        djinnPageController.OnToggleTopHeroesBar += SetHeaderHeroesBarVisibility;
        djinnPageController.OnRequestViewChange += HandleDjinnViewChange;

        HeroStats selectedHeroStats = GetPartyMemberComponent<HeroStats>(selectedHeroIndex);
        djinnPageController.PopulateData(selectedHeroStats);
    }

    private void SetHeaderHeroesBarVisibility(bool visible)
    {
        if (headerHeroesBar != null)
        {
            headerHeroesBar.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void HandleDjinnViewChange(DjinnViewMode targetMode)
    {
        if (djinnViewAContainer != null)
        {
            djinnViewAContainer.style.display = (targetMode == DjinnViewMode.Individual) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (djinnViewBContainer != null)
        {
            djinnViewBContainer.style.display = (targetMode == DjinnViewMode.Global) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        SetHeaderHeroesBarVisibility(targetMode == DjinnViewMode.Individual);
    }

    private void LoadEncyclopediaPage()
    {
        Debug.Log("[MainMenu] Rendering Encyclopedia Page...");
    }

    private void RefreshActiveTabHeroData()
    {
        if (!IsHeroSelectorVisible()) return;

        LoadTabContent(currentTab);
    }

    #endregion

    #region Helper Methods

    private List<HeroStats> GetPartyHeroStatsList()
    {
        List<HeroStats> partyHeroStats = new List<HeroStats>();
        if (GameManager.Instance != null)
        {
            List<GameObject> partyGameObjects = GameManager.Instance.GetParty();
            if (partyGameObjects != null)
            {
                foreach (GameObject memberObj in partyGameObjects)
                {
                    if (memberObj != null && memberObj.TryGetComponent<HeroStats>(out HeroStats heroStats))
                    {
                        partyHeroStats.Add(heroStats);
                    }
                }
            }
        }
        return partyHeroStats;
    }

    private T GetPartyMemberComponent<T>(int index) where T : Component
    {
        if (GameManager.Instance == null) return null;

        List<GameObject> party = GameManager.Instance.GetParty();
        if (party != null && index >= 0 && index < party.Count)
        {
            GameObject memberObj = party[index];
            if (memberObj != null && memberObj.TryGetComponent<T>(out T component))
            {
                return component;
            }
        }
        return null;
    }

    private GameObject GetPartyMemberGameObject(int index)
    {
        if (GameManager.Instance == null) return null;

        List<GameObject> party = GameManager.Instance.GetParty();
        if (party != null && index >= 0 && index < party.Count)
        {
            return party[index];
        }
        return null;
    }

    #endregion

    #region Header Information Refresh

    private void RefreshGlobalHeaderInfo()
    {
        if (GameManager.Instance == null) return;

        if (labelGold != null)
        {
            labelGold.text = $"{GameManager.Instance.TotalGold:N0} Monedas";
        }

        if (labelLocation != null)
        {
            labelLocation.text = SceneManager.GetActiveScene().name;
        }

        UpdatePlayTimeText();
        RefreshHeaderHeroCards();
    }

    private void RefreshHeaderHeroCards()
    {
        List<HeroStats> partyStats = GetPartyHeroStatsList();
        int partyCount = partyStats.Count;

        for (int i = 0; i < btnHeroes.Length; i++)
        {
            if (btnHeroes[i] == null) continue;

            if (i < partyCount)
            {
                btnHeroes[i].style.display = DisplayStyle.Flex;

                HeroStats hero = partyStats[i];
                if (hero != null)
                {
                    if (imgHeroPortraits[i] != null && hero.TurnBarImage != null)
                    {
                        imgHeroPortraits[i].style.backgroundImage = new StyleBackground(hero.TurnBarImage);
                    }

                    if (labelHeroNames[i] != null) labelHeroNames[i].text = hero.Name;
                    if (labelHeroLevels[i] != null) labelHeroLevels[i].text = $"Nv. {hero.Level}";
                    if (labelHeroHpVals[i] != null) labelHeroHpVals[i].text = $"{hero.CurrHp}/{hero.MaxHp}";
                    if (labelHeroPpVals[i] != null) labelHeroPpVals[i].text = $"{hero.CurrPp}/{hero.MaxPp}";

                    if (barHeroHpFills[i] != null)
                    {
                        float hpPercent = (hero.MaxHp > 0) ? Mathf.Clamp01((float)hero.CurrHp / hero.MaxHp) * 100f : 0f;
                        barHeroHpFills[i].style.width = Length.Percent(hpPercent);
                    }

                    if (barHeroPpFills[i] != null)
                    {
                        float ppPercent = (hero.MaxPp > 0) ? Mathf.Clamp01((float)hero.CurrPp / hero.MaxPp) * 100f : 0f;
                        barHeroPpFills[i].style.width = Length.Percent(ppPercent);
                    }
                }
            }
            else
            {
                btnHeroes[i].style.display = DisplayStyle.None;
            }
        }

        UpdateHeroCardsHighlight();
    }

    #endregion
}