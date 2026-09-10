using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Controller for the Battle HUD overlay using UI Toolkit.
/// Manages active hero viewport frame, turn timeline, floating target overlays,
/// submenus, and projects the floating radial menu over the active hero in 3D world space.
/// </summary>
public class BattleHUDController : MonoBehaviour
{
    private const string FULLSCREEN_EXPAND_CLASS = "expanded-fullscreen";
    private const string ACTIVE_HERO_CLASS = "active-hero";

    #region Serialized Fields

    [Header("UI Document & Templates")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset heroStatusCardTemplate;
    [SerializeField] private VisualTreeAsset targetInfoOverlayTemplate;

    [Header("3D Scene Setup")]
    [SerializeField] private Camera mainBattleCamera;

    #endregion

    #region Private Local Cache State

    private GameObject _currentActiveFighter;
    private IReadOnlyList<GameObject> _cachedParty;
    private Vector3 _radialMenuOffset = new Vector3(0f, 1.8f, 0f); // Height offset over active hero
    private Vector3 _targetOverlayOffset = new Vector3(0f, 2.0f, 0f); // Height offset over target head

    // Floating Target Overlay Presenters
    private readonly List<TargetInfoPresenter> _targetPresenters = new List<TargetInfoPresenter>();

    #endregion

    #region UI Elements References

    private VisualElement _root;

    // 1. Active Hero Close-up Viewport
    private VisualElement _portraitViewportFrame;
    private Label _labelActiveHeroName;

    // 3. Turn Timeline ScrollView
    private ScrollView _turnTimelineScroll;

    // 4. Floating Radial Command Menu
    private VisualElement _floatingRadialMenuContainer;
    private Label _labelSelectedCmdName;
    private Button _btnCmdAttack;
    private Button _btnCmdPsynergy;
    private Button _btnCmdDjinn;
    private Button _btnCmdItem;
    private Button _btnCmdDefend;
    private Button _btnCmdFlee;

    // 6. Party Status Cards Container
    private VisualElement _bottomLeftPartyContainer;
    private readonly List<HeroStatusCardPresenter> _partyCardPresenters = new List<HeroStatusCardPresenter>();

    // 7. Action Details Submenu Panel
    private VisualElement _actionDetailsPanel;
    private Label _labelDetailsTitle;
    private ScrollView _detailsListScroll;
    private Label _labelActionDescription;
    private Label _labelActionCost;

    #endregion

    #region Unity Lifecycle & Updates

    private void Awake()
    {
        InitializeUIElements();
    }

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void LateUpdate()
    {
        UpdateFloatingRadialMenuPosition();
        UpdateTargetOverlaysPosition();
    }

    #endregion

    #region Initialization Methods

    /// <summary>
    /// Queries all UXML visual elements and registers button callbacks.
    /// </summary>
    private void InitializeUIElements()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError($"[{nameof(BattleHUDController)}] CRITICAL: UIDocument reference is missing!");
            return;
        }

        _root = uiDocument.rootVisualElement;
        if (_root == null) return;

        _root.pickingMode = PickingMode.Ignore;

        if (mainBattleCamera == null)
        {
            mainBattleCamera = Camera.main;
        }

        // Query Viewport
        _portraitViewportFrame = _root.Q<VisualElement>("portrait_viewport_frame");
        _labelActiveHeroName = _root.Q<Label>("label_active_hero_name");

        // Query Turn Timeline
        _turnTimelineScroll = _root.Q<ScrollView>("turn_timeline_scroll");

        // Query Radial Command Menu & Buttons
        _floatingRadialMenuContainer = _root.Q<VisualElement>("floating_radial_menu_container");
        _labelSelectedCmdName = _root.Q<Label>("label_selected_cmd_name");

        _btnCmdAttack = _root.Q<Button>("btn_cmd_attack");
        _btnCmdPsynergy = _root.Q<Button>("btn_cmd_psynergy");
        _btnCmdDjinn = _root.Q<Button>("btn_cmd_djinn");
        _btnCmdItem = _root.Q<Button>("btn_cmd_item");
        _btnCmdDefend = _root.Q<Button>("btn_cmd_defend");
        _btnCmdFlee = _root.Q<Button>("btn_cmd_flee");

        RegisterRadialButtonsCallbacks();

        // Query Containers
        _bottomLeftPartyContainer = _root.Q<VisualElement>("bottom_left_party_container");
        _actionDetailsPanel = _root.Q<VisualElement>("action_details_panel");
        _labelDetailsTitle = _root.Q<Label>("label_details_title");
        _detailsListScroll = _root.Q<ScrollView>("details_list_scroll");
        _labelActionDescription = _root.Q<Label>("label_action_description");
        _labelActionCost = _root.Q<Label>("label_action_cost");

        SetRadialMenuVisible(false);
        SetActionDetailsPanelVisible(false);
    }

    private void RegisterRadialButtonsCallbacks()
    {
        RegisterCommandEvents(_btnCmdAttack, "ATTACK", () => OnCommandSelected("Attack"));
        RegisterCommandEvents(_btnCmdPsynergy, "PSYNERGY", () => OnCommandSelected("Psynergy"));
        RegisterCommandEvents(_btnCmdDjinn, "DJINN", () => OnCommandSelected("Djinn"));
        RegisterCommandEvents(_btnCmdItem, "ITEM", () => OnCommandSelected("Item"));
        RegisterCommandEvents(_btnCmdDefend, "DEFEND", () => OnCommandSelected("Defend"));
        RegisterCommandEvents(_btnCmdFlee, "FLEE", () => OnCommandSelected("Flee"));
    }

    private void RegisterCommandEvents(Button button, string labelText, System.Action onClickAction)
    {
        if (button == null) return;

        button.RegisterCallback<MouseEnterEvent>(evt =>
        {
            if (_labelSelectedCmdName != null) _labelSelectedCmdName.text = labelText;
        });

        button.RegisterCallback<ClickEvent>(evt =>
        {
            GameEvents.Audio.RequestPlaySFX(SFXType.UI_Confirm);
            onClickAction?.Invoke();
        });
    }

    #endregion

    #region Event Subscriptions & Handlers

    private void SubscribeEvents()
    {
        // Passive party caching from global events
        GameEvents.Party.OnPartyChanged -= HandlePartyChanged;
        GameEvents.Party.OnPartyChanged += HandlePartyChanged;

        GameEvents.Hero.OnHeroStatusChanged -= HandleHeroStatusChanged;
        GameEvents.Hero.OnHeroStatusChanged += HandleHeroStatusChanged;

        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;

        GameEvents.Battle.OnTurnQueueUpdated -= HandleTurnQueueUpdated;
        GameEvents.Battle.OnTurnQueueUpdated += HandleTurnQueueUpdated;

        GameEvents.Battle.OnTargetSelectionChanged -= HandleTargetSelectionChanged;
        GameEvents.Battle.OnTargetSelectionChanged += HandleTargetSelectionChanged;
    }

    private void UnsubscribeEvents()
    {
        GameEvents.Party.OnPartyChanged -= HandlePartyChanged;
        GameEvents.Hero.OnHeroStatusChanged -= HandleHeroStatusChanged;
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        GameEvents.Battle.OnTurnQueueUpdated -= HandleTurnQueueUpdated;
        GameEvents.Battle.OnTargetSelectionChanged -= HandleTargetSelectionChanged;
    }

    private void HandlePartyChanged(IReadOnlyList<GameObject> activeParty)
    {
        _cachedParty = activeParty;
    }

    private void HandleHeroStatusChanged()
    {
        RefreshPartyCardsData();
    }

    private void HandleGameStateChanged(GameManager.GameState previousState, GameManager.GameState newState)
    {
        if (newState == GameManager.GameState.InBattle)
        {
            RebuildPartyCards();
        }
        else
        {
            SetRadialMenuVisible(false);
            SetActionDetailsPanelVisible(false);
            ClearTargetOverlays();
        }
    }

    private void HandleTurnQueueUpdated(IReadOnlyList<GameObject> turnQueue)
    {
        if (turnQueue == null || turnQueue.Count == 0)
        {
            SetRadialMenuVisible(false);
            return;
        }

        _currentActiveFighter = turnQueue[0];
        PopulateTurnTimeline(turnQueue);

        bool isHeroTurn = _currentActiveFighter != null && _currentActiveFighter.GetComponent<HeroStats>() != null;

        if (_labelActiveHeroName != null && _currentActiveFighter != null)
        {
            _labelActiveHeroName.text = _currentActiveFighter.name.ToUpper();
        }

        UpdateActiveHeroHighlight();
        SetRadialMenuVisible(isHeroTurn);
    }

    private void HandleTargetSelectionChanged(IReadOnlyList<GameObject> targets, GameObject mainTarget)
    {
        ClearTargetOverlays();

        if (targets == null || targets.Count == 0 || targetInfoOverlayTemplate == null) return;

        for (int i = 0; i < targets.Count; i++)
        {
            GameObject fighterObj = targets[i];
            if (fighterObj == null) continue;

            TemplateContainer overlayInstance = targetInfoOverlayTemplate.Instantiate();
            VisualElement overlayRoot = overlayInstance.Q<VisualElement>("target_overlay_root") ?? overlayInstance;

            TargetInfoPresenter presenter = new TargetInfoPresenter(overlayRoot, fighterObj);

            // Determine level if FighterStats is attached
            int level = -1;
            if (fighterObj.TryGetComponent<FighterStats>(out var stats))
            {
                //level = stats.Level;
            }

            presenter.UpdateData(fighterObj.name, level);

            // Determine if this is the primary target or secondary (AoE spreading)
            bool isPrimary = (mainTarget == null) ? (i == 0) : (fighterObj == mainTarget);
            presenter.SetPrimaryTarget(isPrimary);
            presenter.SetVisible(true);

            _targetPresenters.Add(presenter);
            _root.Add(overlayInstance);
        }
    }

    #endregion

    #region Floating Overlays & Timeline Population

    private void ClearTargetOverlays()
    {
        for (int i = 0; i < _targetPresenters.Count; i++)
        {
            var presenter = _targetPresenters[i];
            if (presenter != null && presenter.RootElement != null)
            {
                presenter.RootElement.RemoveFromHierarchy();
            }
        }
        _targetPresenters.Clear();
    }

    private void UpdateTargetOverlaysPosition()
    {
        if (_targetPresenters.Count == 0 || mainBattleCamera == null) return;

        for (int i = 0; i < _targetPresenters.Count; i++)
        {
            _targetPresenters[i].UpdateScreenPosition(mainBattleCamera, _targetOverlayOffset);
        }
    }

    private void PopulateTurnTimeline(IReadOnlyList<GameObject> turnQueue)
    {
        if (_turnTimelineScroll == null) return;

        _turnTimelineScroll.Clear();

        for (int i = 0; i < turnQueue.Count; i++)
        {
            GameObject fighterObj = turnQueue[i];
            if (fighterObj == null) continue;

            VisualElement iconBox = new VisualElement();
            iconBox.AddToClassList("timeline-icon-box");

            VisualElement portrait = new VisualElement();
            portrait.AddToClassList("timeline-portrait-img");

            FighterStats stats = fighterObj.GetComponent<FighterStats>();
            if (stats != null && stats.TurnBarImage != null)
            {
                portrait.style.backgroundImage = Background.FromSprite(stats.TurnBarImage);
            }

            if (i == 0)
            {
                iconBox.AddToClassList("current-turn-leader");
            }

            iconBox.Add(portrait);
            _turnTimelineScroll.Add(iconBox);
        }
    }

    private void RebuildPartyCards()
    {
        _partyCardPresenters.Clear();
        if (_bottomLeftPartyContainer == null) return;

        _bottomLeftPartyContainer.Clear();
        if (heroStatusCardTemplate == null) return;

        if (_cachedParty == null || _cachedParty.Count == 0) return;

        for (int i = 0; i < _cachedParty.Count; i++)
        {
            GameObject heroObj = _cachedParty[i];
            if (heroObj == null) continue;

            TemplateContainer cardInstance = heroStatusCardTemplate.Instantiate();
            VisualElement cardRoot = cardInstance.Q<VisualElement>("hero_status_card") ?? cardInstance;

            HeroStatusCardPresenter presenter = new HeroStatusCardPresenter(cardRoot, heroObj);
            _partyCardPresenters.Add(presenter);

            HeroStats stats = heroObj.GetComponent<HeroStats>();
            bool isActive = (heroObj == _currentActiveFighter);
            presenter.UpdateData(stats, heroObj.name, keyNumber: i + 1, isActiveHero: isActive);

            _bottomLeftPartyContainer.Add(cardInstance);
        }
    }

    /// <summary>
    /// Updates the stats data on existing party status cards without re-instantiating elements.
    /// </summary>
    private void RefreshPartyCardsData()
    {
        if (_partyCardPresenters.Count == 0)
        {
            RebuildPartyCards();
            return;
        }

        for (int i = 0; i < _partyCardPresenters.Count; i++)
        {
            var presenter = _partyCardPresenters[i];
            if (presenter != null && presenter.BoundHero != null)
            {
                HeroStats stats = presenter.BoundHero.GetComponent<HeroStats>();
                bool isActive = (presenter.BoundHero == _currentActiveFighter);
                presenter.UpdateData(stats, presenter.BoundHero.name, keyNumber: i + 1, isActiveHero: isActive);
            }
        }
    }

    public void SetPartyData(IReadOnlyList<GameObject> party)
    {
        _cachedParty = party;
        RebuildPartyCards();
    }

    private void UpdateActiveHeroHighlight()
    {
        for (int i = 0; i < _partyCardPresenters.Count; i++)
        {
            var presenter = _partyCardPresenters[i];
            bool isActive = (presenter.BoundHero == _currentActiveFighter);

            if (presenter.RootElement != null)
            {
                if (isActive) presenter.RootElement.AddToClassList(ACTIVE_HERO_CLASS);
                else presenter.RootElement.RemoveFromClassList(ACTIVE_HERO_CLASS);
            }
        }
    }

    #endregion

    #region World-To-Screen Projection

    private void UpdateFloatingRadialMenuPosition()
    {
        if (_floatingRadialMenuContainer == null || _currentActiveFighter == null || mainBattleCamera == null) return;

        if (_floatingRadialMenuContainer.style.display == DisplayStyle.None) return;

        Vector3 worldPos = _currentActiveFighter.transform.position + _radialMenuOffset;
        Vector3 screenPos = mainBattleCamera.WorldToScreenPoint(worldPos);

        if (screenPos.z < 0)
        {
            _floatingRadialMenuContainer.style.display = DisplayStyle.None;
            return;
        }

        float uiX = screenPos.x - (_floatingRadialMenuContainer.resolvedStyle.width * 0.5f);
        float uiY = (Screen.height - screenPos.y) - (_floatingRadialMenuContainer.resolvedStyle.height * 0.5f);

        _floatingRadialMenuContainer.style.left = uiX;
        _floatingRadialMenuContainer.style.top = uiY;
    }

    #endregion

    #region Action Logic & Controls

    private void OnCommandSelected(string commandType)
    {
        Debug.Log($"[{nameof(BattleHUDController)}] Selected Battle Command: {commandType}");

        switch (commandType)
        {
            case "Psynergy":
            case "Djinn":
            case "Item":
                SetActionDetailsPanelVisible(true);
                break;

            default:
                SetActionDetailsPanelVisible(false);
                break;
        }
    }

    public void SetViewportExpanded(bool expanded)
    {
        if (_portraitViewportFrame == null) return;

        if (expanded)
        {
            _portraitViewportFrame.AddToClassList(FULLSCREEN_EXPAND_CLASS);
        }
        else
        {
            _portraitViewportFrame.RemoveFromClassList(FULLSCREEN_EXPAND_CLASS);
        }
    }

    public void SetRadialMenuVisible(bool visible)
    {
        if (_floatingRadialMenuContainer != null)
        {
            _floatingRadialMenuContainer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    public void SetActionDetailsPanelVisible(bool visible)
    {
        if (_actionDetailsPanel != null)
        {
            _actionDetailsPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    #endregion
}