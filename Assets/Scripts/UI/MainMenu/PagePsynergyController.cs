using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

#region Shortcut & Drag Data Structures

/// <summary>
/// Identifies the four directions of the D-Pad exploration shortcuts.
/// </summary>
public enum DPadDirection
{
    Up,
    Left,
    Right,
    Down
}

/// <summary>
/// Context payload during an active Psynergy drag operation.
/// </summary>
public class PsynergyDragContext
{
    public PsynergySO Psynergy { get; private set; }
    public VisualElement SourceCardUI { get; private set; }
    public int SourceShortcutIndex { get; private set; } // -1 if dragged from list card

    public PsynergyDragContext(PsynergySO psynergy, VisualElement sourceCardUI, int sourceShortcutIndex = -1)
    {
        Psynergy = psynergy;
        SourceCardUI = sourceCardUI;
        SourceShortcutIndex = sourceShortcutIndex;
    }
}

/// <summary>
/// Target drop information for D-Pad shortcut slots or hero header cards.
/// </summary>
public struct PsynergyDropTargetInfo
{
    public bool IsShortcutSlot;
    public DPadDirection Direction;
    public bool IsHeroCard;
    public int HeroIndex;

    public PsynergyDropTargetInfo(bool isShortcutSlot, DPadDirection direction, bool isHeroCard, int heroIndex)
    {
        IsShortcutSlot = isShortcutSlot;
        Direction = direction;
        IsHeroCard = isHeroCard;
        HeroIndex = heroIndex;
    }
}

#endregion

/// <summary>
/// Controller for the Main Menu Psynergy Page UI.
/// Manages exploration shortcuts, filtering tabs, details panel, and implements IMenuPageController.
/// </summary>
public class PagePsynergyController : IMenuPageController
{
    #region Events & Properties

    /// <summary>
    /// Event triggered when a menu Psynergy modifies HP/PP of party members.
    /// Used by MainMenuController to refresh top header hero cards.
    /// </summary>
    public event Action OnPsynergyUsedOnHero;

    /// <summary>
    /// Indicates if a Psynergy is currently in targeting mode to be cast on a hero.
    /// Implements IMenuPageController contract.
    /// </summary>
    public bool IsTargeting => isTargetingPsynergy;

    #endregion

    #region Private Fields

    private VisualElement rootElement;

    // Local Party Cache
    private IReadOnlyList<GameObject> cachedActiveParty;

    // Psynergy Card Template
    private VisualTreeAsset psynergyCardTemplate;

    // Left Panel: Hero Portrait & D-Pad Shortcuts
    private VisualElement heroPortraitImage;
    private Label labelHeroName;

    private readonly VisualElement[] dpadSlotUI = new VisualElement[4]; // Up, Left, Right, Down
    private readonly VisualElement[] dpadIconUI = new VisualElement[4];
    private PsynergySO[] assignedShortcuts = new PsynergySO[4];

    // Center Panel: Filter Tabs & Scroll View
    private Button btnTabAll;
    private Button btnTabWorld;
    private Button btnTabMenu;
    private Button btnTabBattle;
    private ScrollView psynergyCardsScroll;

    // Right Panel: Detailed Info
    private VisualElement psynergyIconPreview;
    private VisualElement detailPsynergyIcon;
    private Label labelPsynergyName;
    private Label labelPsynergyType;
    private Label labelPsynergyCost;
    private Label labelPsynergyRange;
    private Label labelPsynergyDesc;
    private VisualElement psynergyExtraDetailsBox;

    // Data Sources
    private HeroStats currentHeroStats;
    private GameObject currentHeroObj;
    private readonly List<PsynergySO> availablePsynergies = new List<PsynergySO>();

    // Callback Memory tracking to prevent duplicate listener accumulation
    private readonly Dictionary<VisualElement, EventCallback<PointerDownEvent>> registeredPointerDownCallbacks = new Dictionary<VisualElement, EventCallback<PointerDownEvent>>();

    // Drag & Drop State Tracking
    private PsynergyDragContext currentDragContext;
    private VisualElement ghostElement;
    private bool isDragging = false;
    // Click suppression flag to swallow synthetic click events right after consuming the last resource/action
    private bool suppressClickThisFrame = false;

    // Pointer Tracking & Drag Threshold
    private Vector2 pointerStartPosition;
    private int activePointerId = -1;
    private const float DRAG_THRESHOLD = 5f;
    private const float CARD_SIZE = 52f;

    // Manual Double-Click Tracking
    private float lastClickTime = 0f;
    private int lastClickIndex = -1;
    private const float DOUBLE_CLICK_TIME_THRESHOLD = 0.3f; // 300 ms window

    // Psynergy Targeting State Tracking
    private bool isTargetingPsynergy = false;
    private PsynergySO targetingPsynergy;
    private VisualElement targetingGhostElement;

    // Active Selection State Tracking
    private PsynergySO selectedPsynergy;
    private VisualElement currentlySelectedCardUI;

    // Filter index memory (0: All, 1: World, 2: Menu, 3: Battle)
    private int currentFilterIndex = 0;

    #endregion

    #region Constructor & IMenuPageController Implementation

    public PagePsynergyController(VisualElement root)
    {
        rootElement = root;
        BindUIElements();
        BindFilterTabs();
        BindDPadSlotsInteractions();
        SubscribeEvents();
    }

    /// <summary>
    /// Populates data for the active hero based on the global menu selection.
    /// Implements IMenuPageController contract.
    /// </summary>
    public void PopulateData(IReadOnlyList<GameObject> activeParty, int selectedHeroIndex)
    {
        cachedActiveParty = activeParty;

        if (activeParty != null && selectedHeroIndex >= 0 && selectedHeroIndex < activeParty.Count)
        {
            currentHeroObj = activeParty[selectedHeroIndex];
            if (currentHeroObj != null)
            {
                currentHeroStats = currentHeroObj.GetComponent<HeroStats>();
            }
        }

        UpdateLeftPanelHeroInfo();
        FetchHeroPsynergies();
        LoadShortcutsFromWorldController();
        FilterCards(currentFilterIndex);
        ClearInfoPanel();
    }

    public void CancelTargeting()
    {
        CancelPsynergyTargeting();
    }

    /// <summary>
    /// Implements IMenuPageController contract. Executed by MainMenuController when 
    /// a header hero card is clicked while this page is actively in Psynergy targeting mode.
    /// </summary>
    public void ExecuteTargetingOnHero(int heroIndex)
    {
        if (!isTargetingPsynergy) return;
        ExecutePsynergyOnHero(heroIndex);
    }

    public void Dispose()
    {
        UnsubscribeEvents();
        CancelPsynergyTargeting();
        RemoveGhostElement();

        foreach (var pair in registeredPointerDownCallbacks)
        {
            if (pair.Key != null)
            {
                pair.Key.UnregisterCallback(pair.Value);
            }
        }
        registeredPointerDownCallbacks.Clear();

        rootElement = null;
        currentHeroStats = null;
        currentHeroObj = null;
        cachedActiveParty = null;
    }

    #endregion

    #region Configuration Helpers

    public void SetPsynergyCardTemplate(VisualTreeAsset template)
    {
        psynergyCardTemplate = template;
    }

    private VisualElement GetTopRootElement()
    {
        return rootElement?.panel?.visualTree ?? rootElement;
    }

    private void SetSelectedCardUI(VisualElement newSelectedCard)
    {
        if (currentlySelectedCardUI != null)
        {
            currentlySelectedCardUI.RemoveFromClassList("active");
        }

        currentlySelectedCardUI = newSelectedCard;

        if (currentlySelectedCardUI != null)
        {
            currentlySelectedCardUI.AddToClassList("active");
        }
    }

    private void RestoreSelectionUI()
    {
        if (selectedPsynergy == null) return;

        for (int i = 0; i < assignedShortcuts.Length; i++)
        {
            if (assignedShortcuts[i] == selectedPsynergy && dpadSlotUI[i] != null)
            {
                SetSelectedCardUI(dpadSlotUI[i]);
                return;
            }
        }

        if (psynergyCardsScroll != null)
        {
            foreach (VisualElement card in psynergyCardsScroll.Children())
            {
                if (card.userData is PsynergySO cardPsynergy && cardPsynergy == selectedPsynergy)
                {
                    SetSelectedCardUI(card);
                    return;
                }
            }
        }
    }

    #endregion

    #region Event Management

    private void SubscribeEvents()
    {
        GameEvents.Psynergy.OnShortcutsChanged -= HandleShortcutsChanged;
        GameEvents.Psynergy.OnShortcutsChanged += HandleShortcutsChanged;
    }

    private void UnsubscribeEvents()
    {
        GameEvents.Psynergy.OnShortcutsChanged -= HandleShortcutsChanged;
    }

    private void HandleShortcutsChanged(GameObject hero, PsynergySO[] shortcuts)
    {
        if (currentHeroObj != null && hero == currentHeroObj)
        {
            assignedShortcuts = shortcuts ?? new PsynergySO[4];
            RefreshShortcutsDisplay();
        }
    }

    #endregion

    #region UI Binding

    private void BindUIElements()
    {
        if (rootElement == null) return;

        heroPortraitImage = rootElement.Q<VisualElement>("hero_portrait_image");
        labelHeroName = rootElement.Q<Label>("label_hero_name");

        dpadSlotUI[(int)DPadDirection.Up] = rootElement.Q<VisualElement>("slot_shortcut_up");
        dpadSlotUI[(int)DPadDirection.Left] = rootElement.Q<VisualElement>("slot_shortcut_left");
        dpadSlotUI[(int)DPadDirection.Right] = rootElement.Q<VisualElement>("slot_shortcut_right");
        dpadSlotUI[(int)DPadDirection.Down] = rootElement.Q<VisualElement>("slot_shortcut_down");

        dpadIconUI[(int)DPadDirection.Up] = rootElement.Q<VisualElement>("slot_shortcut_up_icon");
        dpadIconUI[(int)DPadDirection.Left] = rootElement.Q<VisualElement>("slot_shortcut_left_icon");
        dpadIconUI[(int)DPadDirection.Right] = rootElement.Q<VisualElement>("slot_shortcut_right_icon");
        dpadIconUI[(int)DPadDirection.Down] = rootElement.Q<VisualElement>("slot_shortcut_down_icon");

        btnTabAll = rootElement.Q<Button>("btn_tab_all");
        btnTabWorld = rootElement.Q<Button>("btn_tab_world");
        btnTabMenu = rootElement.Q<Button>("btn_tab_menu");
        btnTabBattle = rootElement.Q<Button>("btn_tab_battle");
        psynergyCardsScroll = rootElement.Q<ScrollView>("psynergy_cards_scroll");

        psynergyIconPreview = rootElement.Q<VisualElement>("psynergy_icon_preview");
        detailPsynergyIcon = rootElement.Q<VisualElement>("detail_psynergy_icon");
        labelPsynergyName = rootElement.Q<Label>("label_psynergy_name");
        labelPsynergyType = rootElement.Q<Label>("label_psynergy_type");
        labelPsynergyCost = rootElement.Q<Label>("label_psynergy_cost");
        labelPsynergyRange = rootElement.Q<Label>("label_psynergy_range");
        labelPsynergyDesc = rootElement.Q<Label>("label_psynergy_desc");
        psynergyExtraDetailsBox = rootElement.Q<VisualElement>("psynergy_extra_details_box");
    }

    private void BindFilterTabs()
    {
        if (btnTabAll != null) btnTabAll.clicked += () => FilterCards(0);
        if (btnTabWorld != null) btnTabWorld.clicked += () => FilterCards(1);
        if (btnTabMenu != null) btnTabMenu.clicked += () => FilterCards(2);
        if (btnTabBattle != null) btnTabBattle.clicked += () => FilterCards(3);
    }

    private void BindDPadSlotsInteractions()
    {
        for (int i = 0; i < dpadSlotUI.Length; i++)
        {
            int slotIdx = i;
            VisualElement slot = dpadSlotUI[i];
            if (slot == null) continue;

            slot.AddToClassList("shortcut-slot");
            slot.AddToClassList("ui-interactive");
            slot.AddToClassList("ui-hover-subtle");

            slot.RegisterCallback<PointerDownEvent>(evt => OnDPadSlotPointerDown(evt, slotIdx, slot));
            slot.RegisterCallback<PointerMoveEvent>(OnCardPointerMove);
            slot.RegisterCallback<PointerUpEvent>(OnCardPointerUp);
            slot.RegisterCallback<PointerCaptureOutEvent>(OnCardPointerCancel);
        }
    }

    private void OnDPadSlotPointerDown(PointerDownEvent evt, int slotIdx, VisualElement slotUI)
    {
        if (evt.button == 1)
        {
            ClearShortcut(slotIdx);
            ClearInfoPanel();
            evt.StopPropagation();
            return;
        }

        if (evt.button == 0)
        {
            if (assignedShortcuts != null && slotIdx < assignedShortcuts.Length)
            {
                PsynergySO psynergy = assignedShortcuts[slotIdx];
                if (psynergy != null)
                {
                    SetSelectedCardUI(slotUI);
                    UpdateInfoPanel(psynergy);

                    if (isTargetingPsynergy)
                    {
                        CancelPsynergyTargeting();
                    }

                    pointerStartPosition = evt.position;
                    activePointerId = evt.pointerId;

                    currentDragContext = new PsynergyDragContext(psynergy, slotUI, slotIdx);
                    isDragging = false;
                }
            }
        }
    }

    #endregion

    #region Data Population & Initialization

    private void UpdateLeftPanelHeroInfo()
    {
        if (currentHeroStats != null)
        {
            if (labelHeroName != null) labelHeroName.text = currentHeroStats.Name.ToUpper();
            if (heroPortraitImage != null && currentHeroStats.TurnBarImage != null)
            {
                heroPortraitImage.style.backgroundImage = new StyleBackground(currentHeroStats.TurnBarImage);
            }
        }
    }

    private void FetchHeroPsynergies()
    {
        availablePsynergies.Clear();

        if (currentHeroStats != null)
        {
            IReadOnlyList<PsynergySO> psyList = currentHeroStats.Psynergies;
            if (psyList != null)
            {
                availablePsynergies.AddRange(psyList);
            }
        }
    }

    private void LoadShortcutsFromWorldController()
    {
        if (currentHeroObj != null)
        {
            GameEvents.Psynergy.RequestGetShortcuts(currentHeroObj, shortcuts =>
            {
                assignedShortcuts = shortcuts ?? new PsynergySO[4];
                RefreshShortcutsDisplay();
            });
        }
        else
        {
            assignedShortcuts = new PsynergySO[4];
            RefreshShortcutsDisplay();
        }
    }

    private void BuildPsynergyCardsList(Func<PsynergySO, bool> filterPredicate = null)
    {
        if (psynergyCardsScroll == null) return;

        psynergyCardsScroll.Clear();

        for (int i = 0; i < availablePsynergies.Count; i++)
        {
            PsynergySO psynergy = availablePsynergies[i];
            if (psynergy == null) continue;

            if (filterPredicate != null && !filterPredicate(psynergy)) continue;

            VisualElement card = CreatePsynergyCard(psynergy, i);
            psynergyCardsScroll.Add(card);
        }

        RestoreSelectionUI();
    }

    private VisualElement CreatePsynergyCard(PsynergySO psynergy, int index)
    {
        if (psynergyCardTemplate == null)
        {
            Debug.LogError("[PagePsynergy] psynergyCardTemplate is null. Cannot instantiate card.");
            return new VisualElement();
        }

        TemplateContainer cardInstance = psynergyCardTemplate.Instantiate();
        VisualElement card = cardInstance.Q<VisualElement>("psynergy_card") ?? cardInstance;
        card.userData = psynergy;
        card.AddToClassList("psynergy-card");
        card.AddToClassList("ui-interactive");
        card.AddToClassList("ui-hover-subtle");

        card.AddToClassList(GetElementCssClass(psynergy.Element));

        VisualElement cardIcon = card.Q<VisualElement>("psynergy_card_icon");
        if (cardIcon != null && psynergy.Icon != null)
        {
            cardIcon.style.backgroundImage = new StyleBackground(psynergy.Icon);
        }

        VisualElement elementPipIcon = card.Q<VisualElement>("element_pip_icon");
        if (elementPipIcon != null)
        {
            elementPipIcon.AddToClassList(GetElementPipCssClass(psynergy.Element));
        }

        Label nameLabel = card.Q<Label>("label_card_name");
        if (nameLabel != null)
        {
            nameLabel.text = psynergy.PsynergyName;
        }

        VisualElement badgesRow = card.Q<VisualElement>("card_badges_row");
        if (badgesRow != null)
        {
            badgesRow.Clear();

            if (psynergy.WorldData.CanBeUsedInWorld)
            {
                Label worldBadge = new Label("Mundo");
                worldBadge.AddToClassList("psynergy-badge");
                worldBadge.AddToClassList("badge-world");
                badgesRow.Add(worldBadge);
            }

            if (psynergy.MenuData.CanBeUsedInMenu)
            {
                Label menuBadge = new Label("Menú");
                menuBadge.AddToClassList("psynergy-badge");
                menuBadge.AddToClassList("badge-menu");
                badgesRow.Add(menuBadge);
            }

            if (psynergy.IsBattlePsynergy)
            {
                Label battleBadge = new Label("Combate");
                battleBadge.AddToClassList("psynergy-badge");
                battleBadge.AddToClassList("badge-battle");
                badgesRow.Add(battleBadge);
            }
        }

        Label ppLabel = card.Q<Label>("label_card_pp");
        if (ppLabel != null)
        {
            ppLabel.text = $"{psynergy.PpCost} PP";
        }

        if (currentHeroStats != null && currentHeroStats.CurrPp < psynergy.PpCost)
        {
            if (ppLabel != null) ppLabel.style.color = new StyleColor(new Color(0.8f, 0.3f, 0.3f));
            card.style.opacity = 0.6f;
        }

        RegisterPointerCallbacks(card, psynergy, index);

        return card;
    }

    private string GetElementPipCssClass(ElementalType element)
    {
        return element switch
        {
            ElementalType.Venus => "icon-venus",
            ElementalType.Mars => "icon-mars",
            ElementalType.Jupiter => "icon-jupiter",
            ElementalType.Mercury => "icon-mercury",
            _ => "icon-none"
        };
    }

    private string GetElementCssClass(ElementalType element)
    {
        return element switch
        {
            ElementalType.Venus => "element-venus",
            ElementalType.Mars => "element-mars",
            ElementalType.Jupiter => "element-jupiter",
            ElementalType.Mercury => "element-mercury",
            _ => "element-venus"
        };
    }

    #endregion

    #region Pointer & Drag Events

    private void RegisterPointerCallbacks(VisualElement cardUI, PsynergySO psynergy, int index)
    {
        UnregisterPointerCallbacks(cardUI);

        EventCallback<PointerDownEvent> pointerDownCallback = evt => OnCardPointerDown(evt, psynergy, index, cardUI);
        registeredPointerDownCallbacks[cardUI] = pointerDownCallback;

        cardUI.RegisterCallback(pointerDownCallback);
        cardUI.RegisterCallback<PointerMoveEvent>(OnCardPointerMove);
        cardUI.RegisterCallback<PointerUpEvent>(OnCardPointerUp);
        cardUI.RegisterCallback<PointerCaptureOutEvent>(OnCardPointerCancel);
    }

    private void UnregisterPointerCallbacks(VisualElement cardUI)
    {
        if (cardUI == null) return;

        if (registeredPointerDownCallbacks.TryGetValue(cardUI, out var existingCallback))
        {
            cardUI.UnregisterCallback(existingCallback);
            registeredPointerDownCallbacks.Remove(cardUI);
        }

        cardUI.UnregisterCallback<PointerMoveEvent>(OnCardPointerMove);
        cardUI.UnregisterCallback<PointerUpEvent>(OnCardPointerUp);
        cardUI.UnregisterCallback<PointerCaptureOutEvent>(OnCardPointerCancel);
    }

    private void OnCardPointerDown(PointerDownEvent evt, PsynergySO psynergy, int index, VisualElement cardUI)
    {
        if (evt.button != 0 || psynergy == null) return;

        float currentTime = Time.unscaledTime;
        bool isDoubleClick = (currentTime - lastClickTime <= DOUBLE_CLICK_TIME_THRESHOLD) && (lastClickIndex == index);

        if (isDoubleClick)
        {
            lastClickTime = 0f;
            lastClickIndex = -1;

            RemoveGhostElement();
            currentDragContext = null;
            activePointerId = -1;
            isDragging = false;

            SetSelectedCardUI(cardUI);
            HandleCardDoubleClick(psynergy);
            return;
        }

        lastClickTime = currentTime;
        lastClickIndex = index;

        if (isTargetingPsynergy)
        {
            CancelPsynergyTargeting();
        }

        pointerStartPosition = evt.position;
        activePointerId = evt.pointerId;

        currentDragContext = new PsynergyDragContext(psynergy, cardUI, -1);
        isDragging = false;

        SetSelectedCardUI(cardUI);
        UpdateInfoPanel(psynergy);
    }

    private void OnCardPointerMove(PointerMoveEvent evt)
    {
        if (currentDragContext == null || evt.pointerId != activePointerId) return;

        Vector2 currentPosition = evt.position;

        if (!isDragging)
        {
            float distance = Vector2.Distance(pointerStartPosition, currentPosition);
            if (distance >= DRAG_THRESHOLD)
            {
                if (!currentDragContext.Psynergy.WorldData.CanBeUsedInWorld)
                {
                    return;
                }

                isDragging = true;

                if (currentDragContext.SourceCardUI != null)
                {
                    currentDragContext.SourceCardUI.style.opacity = 0.3f;
                }

                HighlightShortcutSlots(true);
                CreateGhostElement(currentDragContext.Psynergy.Icon, currentPosition);
                currentDragContext.SourceCardUI.CapturePointer(activePointerId);
            }
        }
        else
        {
            UpdateGhostPosition(currentPosition);
        }
    }

    private void OnCardPointerUp(PointerUpEvent evt)
    {
        if (currentDragContext == null || evt.pointerId != activePointerId) return;

        if (currentDragContext.SourceCardUI != null && currentDragContext.SourceCardUI.HasPointerCapture(activePointerId))
        {
            currentDragContext.SourceCardUI.ReleasePointer(activePointerId);
        }

        if (isDragging)
        {
            ProcessDrop(evt.position);
            RemoveGhostElement();
        }
        else
        {
            currentDragContext = null;
            activePointerId = -1;
        }
    }

    private void OnCardPointerCancel(PointerCaptureOutEvent evt)
    {
        if (isDragging)
        {
            RemoveGhostElement();
        }
    }

    #endregion

    #region Double Click & Targeting Mode

    private void HandleCardDoubleClick(PsynergySO psynergy)
    {
        if (psynergy == null) return;

        if (!psynergy.MenuData.CanBeUsedInMenu)
        {
            Debug.LogWarning($"[PagePsynergy] '{psynergy.PsynergyName}' cannot be used in the main menu.");
            return;
        }

        if (currentHeroStats != null && currentHeroStats.CurrPp < psynergy.PpCost)
        {
            Debug.LogWarning($"[PagePsynergy] Insufficient PP to use '{psynergy.PsynergyName}'. Required: {psynergy.PpCost}, Current: {currentHeroStats.CurrPp}");
            return;
        }

        StartPsynergyTargeting(psynergy);
    }

    private void StartPsynergyTargeting(PsynergySO psynergy)
    {
        CancelPsynergyTargeting();

        isTargetingPsynergy = true;
        targetingPsynergy = psynergy;

        CreateTargetingGhost(psynergy.Icon);

        VisualElement topRoot = GetTopRootElement();
        if (topRoot != null)
        {
            topRoot.RegisterCallback<PointerMoveEvent>(OnGlobalPointerMoveTargeting, TrickleDown.TrickleDown);
        }
    }

    private void OnGlobalPointerMoveTargeting(PointerMoveEvent evt)
    {
        VisualElement topRoot = GetTopRootElement();
        if (!isTargetingPsynergy || targetingGhostElement == null || topRoot == null) return;

        Vector2 localPos = topRoot.WorldToLocal(evt.position);
        targetingGhostElement.style.left = localPos.x - (CARD_SIZE / 2f);
        targetingGhostElement.style.top = localPos.y - (CARD_SIZE / 2f);
    }

    private void ExecutePsynergyOnHero(int targetHeroIdx)
    {
        if (targetingPsynergy == null || currentHeroStats == null)
        {
            CancelPsynergyTargeting();
            ClearInfoPanel();
            return;
        }

        HeroStats targetHero = GetHeroStatsByIdx(targetHeroIdx);
        if (targetHero == null)
        {
            Debug.LogWarning($"[PagePsynergy] HeroStats not found for index {targetHeroIdx}.");
            return;
        }

        bool effectApplied = targetingPsynergy.ExecuteMenuEffect(currentHeroStats, new List<FighterStats> { targetHero });

        if (!effectApplied)
        {
            return;
        }

        GameEvents.Audio.RequestPlaySFXClip(targetingPsynergy.MenuData.MenuSoundEffect);
        currentHeroStats.ConsumePP(targetingPsynergy.PpCost);

        OnPsynergyUsedOnHero?.Invoke();

        FilterCards(currentFilterIndex);

        if (currentHeroStats.CurrPp >= targetingPsynergy.PpCost)
        {
            return;
        }

        ClearInfoPanel();
        CancelPsynergyTargetingDeferred();
    }

    private HeroStats GetHeroStatsByIdx(int index)
    {
        if (cachedActiveParty == null) return null;

        if (index < 0 || index >= cachedActiveParty.Count) return null;

        GameObject heroObj = cachedActiveParty[index];
        if (heroObj == null) return null;

        return heroObj.GetComponent<HeroStats>();
    }

    private void CancelPsynergyTargeting()
    {
        VisualElement topRoot = GetTopRootElement();
        if (topRoot != null)
        {
            topRoot.UnregisterCallback<PointerMoveEvent>(OnGlobalPointerMoveTargeting, TrickleDown.TrickleDown);
        }

        DestroyTargetingGhost();

        isTargetingPsynergy = false;
        targetingPsynergy = null;
    }

    private void CancelPsynergyTargetingDeferred()
    {
        suppressClickThisFrame = true;

        if (rootElement != null)
        {
            rootElement.schedule.Execute(() =>
            {
                CancelPsynergyTargeting();
                suppressClickThisFrame = false;
            }).ExecuteLater(50);
        }
        else
        {
            CancelPsynergyTargeting();
            suppressClickThisFrame = false;
        }
    }

    private void CreateTargetingGhost(Sprite icon)
    {
        DestroyTargetingGhost();

        targetingGhostElement = new VisualElement
        {
            name = "targeting_psynergy_ghost",
            pickingMode = PickingMode.Ignore
        };

        targetingGhostElement.style.position = Position.Absolute;
        targetingGhostElement.style.width = CARD_SIZE;
        targetingGhostElement.style.height = CARD_SIZE;
        if (icon != null) targetingGhostElement.style.backgroundImage = new StyleBackground(icon);
        targetingGhostElement.style.opacity = 0.85f;
        targetingGhostElement.style.left = -9999f;
        targetingGhostElement.style.top = -9999f;

        GetTopRootElement()?.Add(targetingGhostElement);
    }

    private void DestroyTargetingGhost()
    {
        if (targetingGhostElement != null)
        {
            targetingGhostElement.RemoveFromHierarchy();
            targetingGhostElement = null;
        }
    }

    #endregion

    #region Drop Processing & D-Pad Shortcuts Assignment

    private void ProcessDrop(Vector2 pointerPosition)
    {
        if (currentDragContext == null || currentDragContext.Psynergy == null) return;

        if (TryGetDropTarget(pointerPosition, out PsynergyDropTargetInfo targetInfo))
        {
            if (targetInfo.IsShortcutSlot)
            {
                int targetSlotIdx = (int)targetInfo.Direction;
                int sourceSlotIdx = currentDragContext.SourceShortcutIndex;

                if (sourceSlotIdx != -1 && sourceSlotIdx != targetSlotIdx)
                {
                    if (currentHeroObj != null)
                    {
                        GameEvents.Psynergy.RequestGetShortcuts(currentHeroObj, currentShortcuts =>
                        {
                            PsynergySO targetPsynergy = (currentShortcuts != null && targetSlotIdx < currentShortcuts.Length)
                                ? currentShortcuts[targetSlotIdx]
                                : null;

                            GameEvents.Psynergy.RequestAssignShortcut(currentHeroObj, targetPsynergy, sourceSlotIdx);
                            GameEvents.Psynergy.RequestAssignShortcut(currentHeroObj, currentDragContext.Psynergy, targetSlotIdx);
                        });

                        if (targetSlotIdx >= 0 && targetSlotIdx < dpadSlotUI.Length && dpadSlotUI[targetSlotIdx] != null)
                        {
                            SetSelectedCardUI(dpadSlotUI[targetSlotIdx]);
                        }
                    }
                    return;
                }

                AssignShortcut(targetInfo.Direction, currentDragContext.Psynergy);

                if (targetSlotIdx >= 0 && targetSlotIdx < dpadSlotUI.Length && dpadSlotUI[targetSlotIdx] != null)
                {
                    SetSelectedCardUI(dpadSlotUI[targetSlotIdx]);
                }
                return;
            }
        }

        if (currentDragContext.SourceShortcutIndex != -1)
        {
            ClearShortcut(currentDragContext.SourceShortcutIndex);
            ClearInfoPanel();
        }
    }

    private void AssignShortcut(DPadDirection direction, PsynergySO psynergy)
    {
        if (psynergy == null || !psynergy.WorldData.CanBeUsedInWorld) return;

        if (currentHeroObj == null) return;

        int slotIndex = (int)direction;

        if (assignedShortcuts != null)
        {
            for (int i = 0; i < assignedShortcuts.Length; i++)
            {
                if (assignedShortcuts[i] == psynergy)
                {
                    GameEvents.Psynergy.RequestAssignShortcut(currentHeroObj, null, i);
                }
            }
        }

        GameEvents.Psynergy.RequestAssignShortcut(currentHeroObj, psynergy, slotIndex);
    }

    private void ClearShortcut(int slotIndex)
    {
        if (currentHeroObj == null) return;

        GameEvents.Psynergy.RequestAssignShortcut(currentHeroObj, null, slotIndex);
    }

    private void RefreshShortcutsDisplay()
    {
        for (int i = 0; i < 4; i++)
        {
            VisualElement iconSlot = dpadIconUI[i];
            if (iconSlot == null) continue;

            PsynergySO assigned = (assignedShortcuts != null && i < assignedShortcuts.Length) ? assignedShortcuts[i] : null;

            if (assigned != null && assigned.Icon != null)
            {
                iconSlot.style.backgroundImage = new StyleBackground(assigned.Icon);
                iconSlot.style.display = DisplayStyle.Flex;
            }
            else
            {
                iconSlot.style.backgroundImage = null;
                iconSlot.style.display = DisplayStyle.None;
            }
        }
    }

    private void HighlightShortcutSlots(bool enable)
    {
        for (int i = 0; i < dpadSlotUI.Length; i++)
        {
            VisualElement slot = dpadSlotUI[i];
            if (slot == null) continue;

            if (enable)
            {
                Color highlightColor = new Color(0.83f, 0.68f, 0.21f, 0.9f);
                slot.style.borderTopColor = new StyleColor(highlightColor);
                slot.style.borderRightColor = new StyleColor(highlightColor);
                slot.style.borderBottomColor = new StyleColor(highlightColor);
                slot.style.borderLeftColor = new StyleColor(highlightColor);
            }
            else
            {
                slot.style.borderTopColor = StyleKeyword.Null;
                slot.style.borderRightColor = StyleKeyword.Null;
                slot.style.borderBottomColor = StyleKeyword.Null;
                slot.style.borderLeftColor = StyleKeyword.Null;
            }
        }
    }

    private bool TryGetDropTarget(Vector2 pointerPosition, out PsynergyDropTargetInfo targetInfo)
    {
        targetInfo = new PsynergyDropTargetInfo(false, DPadDirection.Up, false, -1);
        if (rootElement?.panel == null) return false;

        VisualElement picked = rootElement.panel.Pick(pointerPosition);
        if (picked == null) return false;

        for (int i = 0; i < dpadSlotUI.Length; i++)
        {
            if (dpadSlotUI[i] != null && IsElementOrChildOf(picked, dpadSlotUI[i]))
            {
                targetInfo = new PsynergyDropTargetInfo(true, (DPadDirection)i, false, -1);
                return true;
            }
        }

        return false;
    }

    private bool IsElementOrChildOf(VisualElement child, VisualElement parent)
    {
        VisualElement current = child;
        while (current != null)
        {
            if (current == parent) return true;
            current = current.parent;
        }
        return false;
    }

    private VisualElement FindParentMatching(VisualElement element, Predicate<VisualElement> predicate)
    {
        VisualElement current = element;
        while (current != null)
        {
            if (predicate(current)) return current;
            current = current.parent;
        }
        return null;
    }

    #endregion

    #region Ghost Element Management

    private void CreateGhostElement(Sprite icon, Vector2 startPosition)
    {
        DestroyGhostVisual();

        ghostElement = new VisualElement
        {
            name = "psynergy_drag_ghost",
            pickingMode = PickingMode.Ignore
        };

        ghostElement.style.position = Position.Absolute;
        ghostElement.style.width = CARD_SIZE;
        ghostElement.style.height = CARD_SIZE;
        if (icon != null) ghostElement.style.backgroundImage = new StyleBackground(icon);
        ghostElement.style.opacity = 0.85f;

        VisualElement topRoot = GetTopRootElement();
        if (topRoot != null)
        {
            Vector2 localPos = topRoot.WorldToLocal(startPosition);
            ghostElement.style.left = localPos.x - (CARD_SIZE / 2f);
            ghostElement.style.top = localPos.y - (CARD_SIZE / 2f);
            topRoot.Add(ghostElement);
        }
    }

    private void UpdateGhostPosition(Vector2 currentPosition)
    {
        VisualElement topRoot = GetTopRootElement();
        if (ghostElement == null || topRoot == null) return;

        Vector2 localPos = topRoot.WorldToLocal(currentPosition);
        ghostElement.style.left = localPos.x - (CARD_SIZE / 2f);
        ghostElement.style.top = localPos.y - (CARD_SIZE / 2f);
    }

    private void DestroyGhostVisual()
    {
        if (ghostElement != null)
        {
            ghostElement.RemoveFromHierarchy();
            ghostElement = null;
        }
    }

    private void RemoveGhostElement()
    {
        HighlightShortcutSlots(false);
        DestroyGhostVisual();

        if (currentDragContext?.SourceCardUI != null)
        {
            currentDragContext.SourceCardUI.style.opacity = 1f;
        }

        isDragging = false;
        currentDragContext = null;
        activePointerId = -1;
    }

    #endregion

    #region Filter Tabs & Info Panel Display

    private void FilterCards(int filterIndex)
    {
        currentFilterIndex = filterIndex;

        if (btnTabAll != null) btnTabAll.RemoveFromClassList("active");
        if (btnTabWorld != null) btnTabWorld.RemoveFromClassList("active");
        if (btnTabMenu != null) btnTabMenu.RemoveFromClassList("active");
        if (btnTabBattle != null) btnTabBattle.RemoveFromClassList("active");

        switch (filterIndex)
        {
            case 0:
                btnTabAll?.AddToClassList("active");
                BuildPsynergyCardsList();
                break;
            case 1:
                btnTabWorld?.AddToClassList("active");
                BuildPsynergyCardsList(p => p.WorldData.CanBeUsedInWorld);
                break;
            case 2:
                btnTabMenu?.AddToClassList("active");
                BuildPsynergyCardsList(p => p.MenuData.CanBeUsedInMenu);
                break;
            case 3:
                btnTabBattle?.AddToClassList("active");
                BuildPsynergyCardsList(p => p.IsBattlePsynergy);
                break;
        }
    }

    private void UpdateInfoPanel(PsynergySO psynergy)
    {
        selectedPsynergy = psynergy;

        if (psynergy == null)
        {
            ClearInfoPanel();
            return;
        }

        if (detailPsynergyIcon != null)
        {
            detailPsynergyIcon.RemoveFromClassList("icon-venus");
            detailPsynergyIcon.RemoveFromClassList("icon-mars");
            detailPsynergyIcon.RemoveFromClassList("icon-jupiter");
            detailPsynergyIcon.RemoveFromClassList("icon-mercury");
            detailPsynergyIcon.style.display = DisplayStyle.Flex;

            detailPsynergyIcon.AddToClassList($"icon-{psynergy.Element.ToString().ToLower()}");
        }

        if (labelPsynergyName != null) labelPsynergyName.text = psynergy.PsynergyName;
        if (labelPsynergyType != null) labelPsynergyType.text = $"Psinergía de {GetElementNameSpanish(psynergy.Element)}";
        if (labelPsynergyCost != null) labelPsynergyCost.text = $"Coste: {psynergy.PpCost} PP";
        if (labelPsynergyRange != null) labelPsynergyRange.text = $"Alcance: {FormatTargetTypeSpanish(psynergy.TargetType)}";
        if (labelPsynergyDesc != null) labelPsynergyDesc.text = psynergy.Description;

        if (psynergyIconPreview != null && psynergy.Icon != null)
        {
            psynergyIconPreview.style.backgroundImage = new StyleBackground(psynergy.Icon);
        }
    }

    private string FormatTargetTypeSpanish(ActionTargetType targetType)
    {
        return targetType switch
        {
            ActionTargetType.Self => "Mismo usuario",
            ActionTargetType.SingleAlly => "1 Aliado",
            ActionTargetType.MultiEnemyArea => "Varios enemigos",
            ActionTargetType.AllAllies => "Todos los aliados",
            ActionTargetType.SingleEnemy => "1 Enemigo",
            ActionTargetType.AllEnemies => "Todos los enemigos",
            _ => targetType.ToString()
        };
    }

    private string GetElementNameSpanish(ElementalType element)
    {
        return element switch
        {
            ElementalType.Venus => "Tierra - Venus",
            ElementalType.Mars => "Fuego - Marte",
            ElementalType.Jupiter => "Viento - Júpiter",
            ElementalType.Mercury => "Agua - Mercurio",
            _ => element.ToString()
        };
    }

    public void ClearInfoPanel()
    {
        selectedPsynergy = null;
        SetSelectedCardUI(null);

        if (detailPsynergyIcon != null) detailPsynergyIcon.style.display = DisplayStyle.None;
        if (labelPsynergyName != null) labelPsynergyName.text = "Seleccionar Psinergía";
        if (labelPsynergyType != null) labelPsynergyType.text = "---";
        if (labelPsynergyCost != null) labelPsynergyCost.text = "Coste: -- PP";
        if (labelPsynergyRange != null) labelPsynergyRange.text = "Alcance: --";
        if (labelPsynergyDesc != null) labelPsynergyDesc.text = "Selecciona una psinergía de la lista para ver su información detallada o asignarla a un atajo de exploración.";
        if (psynergyIconPreview != null) psynergyIconPreview.style.backgroundImage = null;
    }

    #endregion
}