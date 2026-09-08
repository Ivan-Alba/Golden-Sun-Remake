using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Interface contract for all modular main menu sub-page controllers.
/// Ensures consistent lifecycle and data binding across status, items, psynergy, and djinn pages.
/// </summary>
public interface IMenuPageController : IDisposable
{
    void PopulateData(IReadOnlyList<GameObject> activeParty, int selectedHeroIndex);
    bool IsTargeting { get; }
    void CancelTargeting();
}

/// <summary>
/// Controls the main menu UI overlay, handling tab navigation, hero selection, 
/// sub-page lifecycle management, and centralized global header refresh.
/// </summary>
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
    [SerializeField] private UIDocument uiDocument;
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
    [SerializeField] private VisualTreeAsset djinnSlotCardAAsset;
    [SerializeField] private VisualTreeAsset djinnSlotCardBAsset;
    [SerializeField] private Sprite targetSingleIcon;
    [SerializeField] private Sprite targetGroupIcon;

    [Header("Common Templates")]
    [SerializeField] private VisualTreeAsset psynergyCardAsset;

    // Header Elements
    private VisualElement headerHeroesBar;
    private Label labelGold;
    private Label labelLocation;
    private Label labelTime;

    // Content Area
    private VisualElement pageContent;

    // Djinn Views Containers
    private VisualElement djinnViewAContainer;
    private VisualElement djinnViewBContainer;

    // Bookmarks (Tabs)
    private readonly Dictionary<MenuTab, Button> bookmarkDictionary = new Dictionary<MenuTab, Button>();

    // Hero Selection Header Cards UI References & Cached Delegates
    private readonly Button[] btnHeroes = new Button[4];
    private readonly VisualElement[] imgHeroPortraits = new VisualElement[4];
    private readonly Label[] labelHeroNames = new Label[4];
    private readonly Label[] labelHeroLevels = new Label[4];
    private readonly Label[] labelHeroHpVals = new Label[4];
    private readonly Label[] labelHeroPpVals = new Label[4];
    private readonly VisualElement[] barHeroHpFills = new VisualElement[4];
    private readonly VisualElement[] barHeroPpFills = new VisualElement[4];

    private readonly EventCallback<ClickEvent>[] heroClickCallbacks = new EventCallback<ClickEvent>[4];

    // Active Sub-Page Controller
    private IMenuPageController activePageController;

    // State Trackers
    private MenuTab currentTab = MenuTab.Status;
    private int selectedHeroIndex = 0;
    private bool isMenuOpen = false;

    // Local Cache State
    private IReadOnlyList<GameObject> _cachedActiveParty;
    private GameManager.GameState _cachedGameState;
    private int _cachedGold;
    private float _cachedPlayTime;
    private int _lastDisplayedSecond = -1;

    private const string ACTIVE_CLASS = "active";

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }
    }

    private void OnEnable()
    {
        if (uiDocument == null) return;

        rootElement = uiDocument.rootVisualElement;
        if (rootElement == null) return;

        BindUIElements();
        RegisterUICallbacks();
        SubscribeToInputEvents();

        rootElement.style.display = DisplayStyle.None;
        isMenuOpen = false;
    }

    private void OnDisable()
    {
        UnsubscribeFromInputEvents();
        UnregisterUICallbacks();
        activePageController?.Dispose();
        activePageController = null;
    }

    private void Update()
    {
        if (!isMenuOpen || labelTime == null) return;

        _cachedPlayTime += Time.unscaledDeltaTime;
        int currentSecond = Mathf.FloorToInt(_cachedPlayTime);

        if (currentSecond != _lastDisplayedSecond)
        {
            _lastDisplayedSecond = currentSecond;
            UpdatePlayTimeText();
        }
    }

    private void UpdatePlayTimeText()
    {
        TimeSpan timeSpan = TimeSpan.FromSeconds(_cachedPlayTime);
        labelTime.text = string.Format("{0:D2}:{1:D2}:{2:D2}", (int)timeSpan.TotalHours, timeSpan.Minutes, timeSpan.Seconds);
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

        bookmarkDictionary.Clear();
        AddBookmark(MenuTab.Status, "btn_status");
        AddBookmark(MenuTab.Items, "btn_items");
        AddBookmark(MenuTab.Psynergy, "btn_psynergy");
        AddBookmark(MenuTab.Djinn, "btn_djinn");
        AddBookmark(MenuTab.Encyclopedia, "btn_encyclopedia");

        foreach (var btn in bookmarkDictionary.Values)
        {
            if (btn != null) btn.focusable = false;
        }

        for (int i = 0; i < 4; i++)
        {
            btnHeroes[i] = rootElement.Q<Button>($"btn_hero{i}");
            if (btnHeroes[i] != null) btnHeroes[i].focusable = false;

            imgHeroPortraits[i] = rootElement.Q<VisualElement>($"header_hero_portrait_{i}") ?? btnHeroes[i]?.Q<VisualElement>(className: "header-hero-portrait");
            labelHeroNames[i] = rootElement.Q<Label>($"label_hero_name_{i}");
            labelHeroLevels[i] = rootElement.Q<Label>($"label_hero_level_{i}");
            labelHeroHpVals[i] = rootElement.Q<Label>($"label_hero_hp_val_{i}");
            labelHeroPpVals[i] = rootElement.Q<Label>($"label_hero_pp_val_{i}");
            barHeroHpFills[i] = rootElement.Q<VisualElement>($"bar_hero_hp_{i}");
            barHeroPpFills[i] = rootElement.Q<VisualElement>($"bar_hero_pp_{i}");
        }
    }

    private void AddBookmark(MenuTab tab, string elementName)
    {
        Button btn = rootElement.Q<Button>(elementName);
        if (btn != null) bookmarkDictionary.Add(tab, btn);
    }

    private void RegisterUICallbacks()
    {
        foreach (var kvp in bookmarkDictionary)
        {
            MenuTab tab = kvp.Key;
            kvp.Value?.RegisterCallback<ClickEvent>(e => SelectTab(tab));
        }

        for (int i = 0; i < btnHeroes.Length; i++)
        {
            int index = i;
            heroClickCallbacks[i] = e => SelectHero(index);
            btnHeroes[i]?.RegisterCallback(heroClickCallbacks[i]);
        }
    }

    private void UnregisterUICallbacks()
    {
        foreach (var kvp in bookmarkDictionary)
        {
            MenuTab tab = kvp.Key;
            kvp.Value?.UnregisterCallback<ClickEvent>(e => SelectTab(tab));
        }

        for (int i = 0; i < btnHeroes.Length; i++)
        {
            if (btnHeroes[i] != null && heroClickCallbacks[i] != null)
            {
                btnHeroes[i].UnregisterCallback(heroClickCallbacks[i]);
            }
        }
    }

    #endregion

    #region Input Event Subscriptions & Handlers

    private void SubscribeToInputEvents()
    {
        UnsubscribeFromInputEvents();

        GameEvents.Input.OnToggleMenuPressed += HandleToggleMenuInput;
        GameEvents.Input.OnMenuNextTabPressed += HandleNextTab;
        GameEvents.Input.OnMenuPreviousTabPressed += HandlePreviousTab;
        GameEvents.Input.OnMenuCancelPressed += HandleCancelInput;
        GameEvents.Input.OnMenuNextHeroPressed += HandleNextHero;
        GameEvents.Input.OnMenuPreviousHeroPressed += HandlePreviousHero;

        GameEvents.Party.OnPartyChanged += HandlePartyChanged;
        GameEvents.Party.OnGoldChanged += HandleGoldChanged;
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;
    }

    private void UnsubscribeFromInputEvents()
    {
        GameEvents.Input.OnToggleMenuPressed -= HandleToggleMenuInput;
        GameEvents.Input.OnMenuNextTabPressed -= HandleNextTab;
        GameEvents.Input.OnMenuPreviousTabPressed -= HandlePreviousTab;
        GameEvents.Input.OnMenuCancelPressed -= HandleCancelInput;
        GameEvents.Input.OnMenuNextHeroPressed -= HandleNextHero;
        GameEvents.Input.OnMenuPreviousHeroPressed -= HandlePreviousHero;

        GameEvents.Party.OnPartyChanged -= HandlePartyChanged;
        GameEvents.Party.OnGoldChanged -= HandleGoldChanged;
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        _cachedGameState = newState;
    }

    private void HandlePartyChanged(IReadOnlyList<GameObject> activeParty)
    {
        _cachedActiveParty = activeParty;
        if (isMenuOpen)
        {
            RefreshGlobalHeaderInfo();
            activePageController?.PopulateData(_cachedActiveParty, selectedHeroIndex);
        }
    }

    private void HandleGoldChanged(int newGold)
    {
        _cachedGold = newGold;
        if (isMenuOpen && labelGold != null)
        {
            labelGold.text = $"{_cachedGold:N0} Monedas";
        }
    }

    private void HandleToggleMenuInput()
    {
        if (!isMenuOpen)
        {
            if (_cachedGameState == GameManager.GameState.WorldExploration)
            {
                SetMenuVisibility(true);
            }
        }
        else
        {
            HandleCancelInput();
        }
    }

    private void HandleCancelInput()
    {
        if (!isMenuOpen) return;

        if (activePageController != null && activePageController.IsTargeting)
        {
            activePageController.CancelTargeting();
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Cancel);
            return;
        }

        SetMenuVisibility(false);
    }

    private void HandleNextTab()
    {
        if (IsAnySubPageTargeting()) return;
        CycleTab(1);
    }

    private void HandlePreviousTab()
    {
        if (IsAnySubPageTargeting()) return;
        CycleTab(-1);
    }

    private void HandleNextHero()
    {
        if (IsAnySubPageTargeting()) return;
        CycleHero(1);
    }

    private void HandlePreviousHero()
    {
        if (IsAnySubPageTargeting()) return;
        CycleHero(-1);
    }

    private bool IsAnySubPageTargeting()
    {
        return activePageController != null && activePageController.IsTargeting;
    }

    #endregion

    #region Navigation & Page Management

    public void SetMenuVisibility(bool visible)
    {
        isMenuOpen = visible;
        rootElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        GameEvents.GameState.RequestGameStateChange(visible ? GameManager.GameState.InMenu : GameManager.GameState.WorldExploration);

        if (visible)
        {
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Confirm);
            _lastDisplayedSecond = -1;

            GameEvents.GameState.RequestPlayTime(time =>
            {
                _cachedPlayTime = time;
                UpdatePlayTimeText();
            });

            GameEvents.Party.RequestGold(gold =>
            {
                _cachedGold = gold;
                if (labelGold != null) labelGold.text = $"{_cachedGold:N0} Monedas";
            });

            RefreshGlobalHeaderInfo();
            SelectTab(currentTab, playSound: false);
        }
        else
        {
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Cancel);
            activePageController?.CancelTargeting();
            activePageController?.Dispose();
            activePageController = null;
        }
    }

    public void SelectTab(MenuTab tab, bool playSound = true)
    {
        if (IsAnySubPageTargeting()) return;

        if (playSound && currentTab != tab)
        {
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Move);
        }

        activePageController?.CancelTargeting();
        activePageController?.Dispose();
        activePageController = null;

        currentTab = tab;

        bool needsHeroSelector = IsHeroSelectorVisible();
        if (headerHeroesBar != null)
        {
            headerHeroesBar.style.display = needsHeroSelector ? DisplayStyle.Flex : DisplayStyle.None;
        }

        LoadTabContent(tab);
        UpdateBookmarksHighlight();
        UpdateHeroCardsHighlight();
    }

    public void SelectHero(int heroIndex)
    {
        if (IsAnySubPageTargeting()) return;

        List<HeroStats> party = GetPartyHeroStatsList();
        int targetIndex = 0;
        if (party != null && party.Count > 0)
        {
            targetIndex = Mathf.Clamp(heroIndex, 0, party.Count - 1);
        }

        if (selectedHeroIndex != targetIndex)
        {
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Move);
        }

        selectedHeroIndex = targetIndex;

        UpdateHeroCardsHighlight();
        RefreshActiveTabHeroData();
    }

    private void CycleHero(int direction)
    {
        if (!IsHeroSelectorVisible() || IsAnySubPageTargeting()) return;

        List<HeroStats> party = GetPartyHeroStatsList();
        if (party == null || party.Count <= 1) return;

        int nextHero = (selectedHeroIndex + direction + party.Count) % party.Count;
        SelectHero(nextHero);
    }

    private void UpdateBookmarksHighlight()
    {
        foreach (var kvp in bookmarkDictionary)
        {
            if (kvp.Value == null) continue;
            bool isCurrent = (kvp.Key == currentTab);
            kvp.Value.EnableInClassList(ACTIVE_CLASS, isCurrent);
        }
    }

    private void UpdateHeroCardsHighlight()
    {
        for (int i = 0; i < btnHeroes.Length; i++)
        {
            if (btnHeroes[i] == null) continue;
            bool isSelected = (i == selectedHeroIndex);
            btnHeroes[i].EnableInClassList(ACTIVE_CLASS, isSelected);
        }
    }

    private void CycleTab(int direction)
    {
        if (IsAnySubPageTargeting()) return;
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
        activePageController?.Dispose();
        activePageController = null;

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

        var pageController = new PageStatusController(statusInstance);
        pageController.PopulateData(_cachedActiveParty, selectedHeroIndex);
        activePageController = pageController;
    }

    private void LoadItemsPage()
    {
        if (pageItemsAsset == null) return;

        TemplateContainer itemsInstance = pageItemsAsset.Instantiate();
        itemsInstance.style.flexGrow = 1;
        pageContent.Add(itemsInstance);

        var pageController = new PageItemsController(itemsInstance);

        // AQUÍ ESTABA FALTANDO ASIGNAR EL TEMPLATE DEL SLOT DE ITEMS:
        pageController.SetItemSlotAsset(itemSlotAsset);

        pageController.SetExtraInfoTemplates(templateInfoConsumable, templateInfoEquipable, templateInfoWeapon, templateInfoPsynergyRelic, templateInfoKeyItem);
        pageController.OnItemUsedOnHero += RefreshHeaderHeroCards;

        pageController.PopulateData(_cachedActiveParty, selectedHeroIndex);
        activePageController = pageController;
    }

    private void LoadPsynergyPage()
    {
        if (pagePsynergyAsset == null) return;

        TemplateContainer psynergyInstance = pagePsynergyAsset.Instantiate();
        psynergyInstance.style.flexGrow = 1;
        pageContent.Add(psynergyInstance);

        var pageController = new PagePsynergyController(psynergyInstance);
        pageController.SetPsynergyCardTemplate(psynergyCardAsset);
        pageController.OnPsynergyUsedOnHero += RefreshHeaderHeroCards;

        pageController.PopulateData(_cachedActiveParty, selectedHeroIndex);
        activePageController = pageController;
    }

    private void LoadDjinnPage()
    {
        if (pageDjinnAsset == null || globalViewAsset == null) return;

        TemplateContainer instanceA = pageDjinnAsset.Instantiate();
        instanceA.style.flexGrow = 1;
        instanceA.style.display = DisplayStyle.Flex;

        TemplateContainer instanceB = globalViewAsset.Instantiate();
        instanceB.style.flexGrow = 1;
        instanceB.style.display = DisplayStyle.None;

        pageContent.Add(instanceA);
        pageContent.Add(instanceB);

        djinnViewAContainer = instanceA;
        djinnViewBContainer = instanceB;

        var pageController = new PageDjinnController(instanceA, instanceB);
        pageController.SetTemplates(psynergyCardAsset, djinnSlotCardAAsset, djinnSlotCardBAsset);
        pageController.SetTargetIcons(targetSingleIcon, targetGroupIcon);

        pageController.OnDjinnStateChanged += RefreshHeaderHeroCards;
        pageController.OnToggleTopHeroesBar += SetHeaderHeroesBarVisibility;
        pageController.OnRequestViewChange += HandleDjinnViewChange;

        pageController.PopulateData(_cachedActiveParty, selectedHeroIndex);
        activePageController = pageController;
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
        activePageController = null;
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
        if (_cachedActiveParty != null)
        {
            foreach (GameObject memberObj in _cachedActiveParty)
            {
                if (memberObj != null && memberObj.TryGetComponent<HeroStats>(out HeroStats heroStats))
                {
                    partyHeroStats.Add(heroStats);
                }
            }
        }
        return partyHeroStats;
    }

    #endregion

    #region Header Information Refresh

    private void RefreshGlobalHeaderInfo()
    {
        if (labelGold != null)
        {
            labelGold.text = $"{_cachedGold:N0} Monedas";
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