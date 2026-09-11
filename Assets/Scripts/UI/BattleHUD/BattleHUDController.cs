using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UI.BattleHUD.CommandCross;

/// <summary>
/// Controller for the Battle HUD overlay using UI Toolkit.
/// Orchestrates viewport transitions, turn timeline updates, floating target overlays,
/// action submenus, top action banners, and projects the floating command cross over active heroes in 3D world space.
/// </summary>
public class BattleHUDController : MonoBehaviour
{
    private const string FULLSCREEN_EXPAND_CLASS = "expanded-fullscreen";
    private const string ACTIVE_HERO_CLASS = "active-hero";
    private const string TARGET_INDICATOR_HIDDEN_CLASS = "timeline-target-indicator--hidden";
    private const string PORTRAIT_HIDDEN_CLASS = "portrait-viewport-frame--hidden";

    #region Serialized Fields

    [Header("UI Document & Templates")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset heroStatusCardTemplate;
    [SerializeField] private VisualTreeAsset targetInfoOverlayTemplate;

    [Header("3D Scene Setup")]
    [SerializeField] private Camera mainBattleCamera;

    [Header("Close-Up Viewport Setup")]
    [SerializeField] private RenderTexture heroPortraitRenderTexture;
    [SerializeField] private Material heroPortraitMaterial;

    #endregion

    #region Private Local Cache State

    private GameObject _currentActiveFighter;
    private IReadOnlyList<GameObject> _cachedParty;
    private Vector3 _commandCrossOffset = Vector3.zero; // Exactly centered over active hero transform origin
    private Vector3 _targetOverlayOffset = new Vector3(0f, 2.0f, 0f); // Height offset over target head

    // Presenter instance for modular battle command cross
    private CommandCrossPresenter _commandCrossPresenter;

    // Floating Target Overlay Presenters
    private readonly List<TargetInfoPresenter> _targetPresenters = new List<TargetInfoPresenter>();

    // Cache mapping fighters in current timeline to their UI target indicator elements
    private readonly List<KeyValuePair<GameObject, VisualElement>> _timelineTargetIndicators = new List<KeyValuePair<GameObject, VisualElement>>();

    #endregion

    #region UI Elements References

    private VisualElement _root;

    // 1. Active Hero Close-up Viewport
    private VisualElement _portraitViewportFrame;
    private VisualElement _portraitRenderElement;
    private RenderTexture _maskedRenderTexture;
    private Label _labelActiveHeroName;

    // 2. Top-Center Active Action Banner
    private VisualElement _actionBannerContainer;
    private Label _labelActionBannerTitle;
    private Label _labelActionBannerSubtext;

    // 3. Turn Timeline
    private VisualElement _turnLeaderSlot;
    private VisualElement _turnQueueList;

    // 4. Floating Command Cross Container & Overlay Instance
    private VisualElement _floatingCommandMenuContainer;
    private VisualElement _commandCrossOverlayInstance;

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
        UpdatePortraitRenderTexture();
        UpdateFloatingCommandMenuPosition();
        UpdateTargetOverlaysPosition();
    }

    private void OnDestroy()
    {
        ReleaseMaskedRenderTexture();

        if (_commandCrossPresenter != null)
        {
            _commandCrossPresenter.OnCommandConfirmed -= OnCommandSelected;
        }
    }

    #endregion

    #region Initialization Methods

    /// <summary>
    /// Queries all UXML visual elements and initializes module presenters.
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

        // Query Viewport Elements
        _portraitViewportFrame = _root.Q<VisualElement>("portrait_viewport_frame");
        _labelActiveHeroName = _root.Q<Label>("label_active_hero_name");
        _portraitRenderElement = _root.Q<VisualElement>("portrait_viewport_render");

        if (_portraitViewportFrame != null)
        {
            _portraitViewportFrame.AddToClassList(PORTRAIT_HIDDEN_CLASS);
        }

        // Query Action Banner Elements
        _actionBannerContainer = _root.Q<VisualElement>("action_banner_container");
        _labelActionBannerTitle = _root.Q<Label>("label_action_banner_title");
        _labelActionBannerSubtext = _root.Q<Label>("label_action_banner_subtext");

        // Setup process for camera render texture masking
        SetupPortraitMaskedRenderTexture();

        // Query Turn Timeline
        _turnLeaderSlot = _root.Q<VisualElement>("turn_leader_slot");
        _turnQueueList = _root.Q<VisualElement>("turn_queue_list");

        // Query Floating Command Cross Container & Instance
        _floatingCommandMenuContainer = _root.Q<VisualElement>("floating_command_menu_container");
        _commandCrossOverlayInstance = _root.Q<VisualElement>("command_cross_overlay");

        InitializeCommandCrossPresenter();

        // Query Containers
        _bottomLeftPartyContainer = _root.Q<VisualElement>("bottom_left_party_container");
        _actionDetailsPanel = _root.Q<VisualElement>("action_details_panel");
        _labelDetailsTitle = _root.Q<Label>("label_details_title");
        _detailsListScroll = _root.Q<ScrollView>("details_list_scroll");
        _labelActionDescription = _root.Q<Label>("label_action_description");
        _labelActionCost = _root.Q<Label>("label_action_cost");

        // Direct visibility reset on Awake for turn-dependent elements
        SetHUDVisible(false);
        SetHeroTurnUIElementsVisible(false);
        SetActionDetailsPanelVisible(false);
        SetActionBannerVisible(false);
    }

    /// <summary>
    /// Initializes the decoupled command cross presenter and hooks into its selection events.
    /// </summary>
    private void InitializeCommandCrossPresenter()
    {
        if (_commandCrossOverlayInstance != null)
        {
            _commandCrossPresenter = new CommandCrossPresenter(_commandCrossOverlayInstance);
            _commandCrossPresenter.OnCommandConfirmed += OnCommandSelected;
        }
        else
        {
            Debug.LogWarning($"[{nameof(BattleHUDController)}] CommandCrossOverlay instance not found in UXML hierarchy.");
        }
    }

    #endregion

    #region Close-Up Viewport Mask Processing

    /// <summary>
    /// Creates and binds the target RenderTexture processed through the cut-in shader.
    /// </summary>
    private void SetupPortraitMaskedRenderTexture()
    {
        if (_portraitRenderElement == null || heroPortraitRenderTexture == null || heroPortraitMaterial == null) return;

        if (_maskedRenderTexture == null || _maskedRenderTexture.width != heroPortraitRenderTexture.width || _maskedRenderTexture.height != heroPortraitRenderTexture.height)
        {
            ReleaseMaskedRenderTexture();

            _maskedRenderTexture = new RenderTexture(heroPortraitRenderTexture.width, heroPortraitRenderTexture.height, 0, RenderTextureFormat.ARGB32)
            {
                antiAliasing = heroPortraitRenderTexture.antiAliasing
            };
            _maskedRenderTexture.Create();
        }

        // Assign source texture to material
        heroPortraitMaterial.SetTexture("_MainTex", heroPortraitRenderTexture);

        // Blit initial frame and bind to UI Toolkit element
        Graphics.Blit(heroPortraitRenderTexture, _maskedRenderTexture, heroPortraitMaterial);
        _portraitRenderElement.style.backgroundImage = Background.FromRenderTexture(_maskedRenderTexture);
    }

    /// <summary>
    /// Continuously processes camera RenderTexture frames into the masked output buffer during hero turns.
    /// </summary>
    private void UpdatePortraitRenderTexture()
    {
        if (_portraitViewportFrame == null || _portraitViewportFrame.style.display == DisplayStyle.None) return;

        if (heroPortraitRenderTexture != null && _maskedRenderTexture != null && heroPortraitMaterial != null)
        {
            Graphics.Blit(heroPortraitRenderTexture, _maskedRenderTexture, heroPortraitMaterial);
        }
    }

    private void ReleaseMaskedRenderTexture()
    {
        if (_maskedRenderTexture != null)
        {
            _maskedRenderTexture.Release();
            Destroy(_maskedRenderTexture);
            _maskedRenderTexture = null;
        }
    }

    #endregion

    #region Event Subscriptions & Handlers

    private void SubscribeEvents()
    {
        GameEvents.Party.OnPartyChanged -= HandlePartyChanged;
        GameEvents.Party.OnPartyChanged += HandlePartyChanged;

        GameEvents.Hero.OnHeroStatusChanged -= HandleHeroStatusChanged;
        GameEvents.Hero.OnHeroStatusChanged += HandleHeroStatusChanged;

        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        GameEvents.GameState.OnGameStateChanged += HandleGameStateChanged;

        GameEvents.Battle.OnBattleStateChanged -= HandleBattleStateChanged;
        GameEvents.Battle.OnBattleStateChanged += HandleBattleStateChanged;

        GameEvents.Battle.OnTurnQueueUpdated -= HandleTurnQueueUpdated;
        GameEvents.Battle.OnTurnQueueUpdated += HandleTurnQueueUpdated;

        GameEvents.Battle.OnTargetSelectionChanged -= HandleTargetSelectionChanged;
        GameEvents.Battle.OnTargetSelectionChanged += HandleTargetSelectionChanged;

        GameEvents.Battle.OnRequestActionBanner -= HandleRequestActionBanner;
        GameEvents.Battle.OnRequestActionBanner += HandleRequestActionBanner;

        GameEvents.Battle.OnHideActionBanner -= HandleHideActionBanner;
        GameEvents.Battle.OnHideActionBanner += HandleHideActionBanner;
    }

    private void UnsubscribeEvents()
    {
        GameEvents.Party.OnPartyChanged -= HandlePartyChanged;
        GameEvents.Hero.OnHeroStatusChanged -= HandleHeroStatusChanged;
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        GameEvents.Battle.OnBattleStateChanged -= HandleBattleStateChanged;
        GameEvents.Battle.OnTurnQueueUpdated -= HandleTurnQueueUpdated;
        GameEvents.Battle.OnTargetSelectionChanged -= HandleTargetSelectionChanged;
        GameEvents.Battle.OnRequestActionBanner -= HandleRequestActionBanner;
        GameEvents.Battle.OnHideActionBanner -= HandleHideActionBanner;
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
            SetHUDVisible(true);
            RebuildPartyCards();
        }
        else
        {
            SetHUDVisible(false);
            SetHeroTurnUIElementsVisible(false);
            SetActionDetailsPanelVisible(false);
            SetActionBannerVisible(false);
            ClearTargetOverlays();
        }
    }

    private void HandleBattleStateChanged(BattleState newState)
    {
        bool isHeroTurn = _currentActiveFighter != null && _currentActiveFighter.GetComponent<HeroStats>() != null;

        switch (newState)
        {
            case BattleState.CommandSelection:
                SetHeroTurnUIElementsVisible(isHeroTurn);
                SetActionDetailsPanelVisible(false);
                SetActionBannerVisible(false);

                if (isHeroTurn)
                {
                    bool hasDjinnsUnlocked = true;
                    bool hasSummonsUnlocked = true;

                    _commandCrossPresenter?.Show(hasDjinnsUnlocked, hasSummonsUnlocked);
                    UpdateFloatingCommandMenuPosition();
                }
                break;

            case BattleState.SubMenuSelection:
                _commandCrossPresenter?.Hide();
                SetActionDetailsPanelVisible(true);
                break;

            case BattleState.TargetSelection:
                _commandCrossPresenter?.Hide();
                SetActionDetailsPanelVisible(false);
                break;

            case BattleState.WaitingTurnExecution:
            case BattleState.IntroTransition:
            case BattleState.ResultsScreen:
            case BattleState.GameOverScreen:
                _commandCrossPresenter?.Hide();
                SetHeroTurnUIElementsVisible(false);
                SetActionDetailsPanelVisible(false);
                break;
        }
    }

    private void HandleTurnQueueUpdated(IReadOnlyList<GameObject> turnQueue)
    {
        if (turnQueue == null || turnQueue.Count == 0)
        {
            SetHeroTurnUIElementsVisible(false);
            return;
        }

        _currentActiveFighter = turnQueue[0];
        PopulateTurnTimeline(turnQueue);

        if (_labelActiveHeroName != null && _currentActiveFighter != null)
        {
            _labelActiveHeroName.text = _currentActiveFighter.name.ToUpper();
        }

        UpdateActiveHeroHighlight();
    }

    private void HandleTargetSelectionChanged(IReadOnlyList<GameObject> targets, GameObject mainTarget)
    {
        ClearTargetOverlays();

        if (targets != null && targets.Count > 0 && targetInfoOverlayTemplate != null)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                GameObject fighterObj = targets[i];
                if (fighterObj == null) continue;

                TemplateContainer overlayInstance = targetInfoOverlayTemplate.Instantiate();
                VisualElement overlayRoot = overlayInstance.Q<VisualElement>("target_overlay_root") ?? overlayInstance;

                TargetInfoPresenter presenter = new TargetInfoPresenter(overlayRoot, fighterObj);

                int level = -1;
                if (fighterObj.TryGetComponent<FighterStats>(out var stats))
                {
                    // level = stats.Level;
                }

                presenter.UpdateData(fighterObj.name, level);

                bool isPrimary = (mainTarget == null) ? (i == 0) : (fighterObj == mainTarget);
                presenter.SetPrimaryTarget(isPrimary);
                presenter.SetVisible(true);

                _targetPresenters.Add(presenter);
                _root.Add(overlayInstance);
            }
        }

        UpdateTimelineTargetMarkers(targets);
        UpdateTargetOverlaysPosition();
    }

    private void HandleRequestActionBanner(string title, string subtext)
    {
        if (_labelActionBannerTitle != null) _labelActionBannerTitle.text = title.ToUpper();
        if (_labelActionBannerSubtext != null)
        {
            _labelActionBannerSubtext.text = subtext;
            _labelActionBannerSubtext.style.display = string.IsNullOrEmpty(subtext) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        SetActionBannerVisible(true);
    }

    private void HandleHideActionBanner()
    {
        SetActionBannerVisible(false);
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
        if (_targetPresenters.Count == 0 || mainBattleCamera == null || _root == null) return;

        for (int i = 0; i < _targetPresenters.Count; i++)
        {
            _targetPresenters[i].UpdatePanelPosition(_root.panel, mainBattleCamera, _targetOverlayOffset);
        }
    }

    private void PopulateTurnTimeline(IReadOnlyList<GameObject> turnQueue)
    {
        if (_turnLeaderSlot == null || _turnQueueList == null) return;

        _turnLeaderSlot.Clear();
        _turnQueueList.Clear();
        _timelineTargetIndicators.Clear();

        for (int i = 0; i < turnQueue.Count; i++)
        {
            GameObject fighterObj = turnQueue[i];
            if (fighterObj == null) continue;

            VisualElement slotWrapper = new VisualElement();
            slotWrapper.AddToClassList("timeline-slot-wrapper");

            VisualElement iconBox = new VisualElement();
            VisualElement portrait = new VisualElement();
            portrait.AddToClassList("timeline-portrait-img");

            FighterStats stats = fighterObj.GetComponent<FighterStats>();
            if (stats != null && stats.TurnBarImage != null)
            {
                portrait.style.backgroundImage = Background.FromSprite(stats.TurnBarImage);
            }
            else
            {
                Label fallbackLabel = new Label(fighterObj.name.Substring(0, Mathf.Min(3, fighterObj.name.Length)).ToUpper());
                fallbackLabel.style.color = Color.white;
                fallbackLabel.style.fontSize = 10;
                fallbackLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                portrait.Add(fallbackLabel);
            }

            iconBox.Add(portrait);
            slotWrapper.Add(iconBox);

            VisualElement targetIndicator = new VisualElement();
            targetIndicator.AddToClassList("timeline-target-indicator");
            targetIndicator.AddToClassList(TARGET_INDICATOR_HIDDEN_CLASS);

            targetIndicator.schedule.Execute(() =>
            {
                targetIndicator.ToggleInClassList("timeline-target-indicator--bounce");
            }).Every(450);

            slotWrapper.Add(targetIndicator);

            _timelineTargetIndicators.Add(new KeyValuePair<GameObject, VisualElement>(fighterObj, targetIndicator));

            if (i == 0)
            {
                slotWrapper.AddToClassList("leader-wrapper");
                iconBox.AddToClassList("current-turn-leader");

                iconBox.schedule.Execute(() =>
                {
                    iconBox.ToggleInClassList("current-turn-leader--pulse");
                }).Every(600);

                _turnLeaderSlot.Add(slotWrapper);
            }
            else
            {
                slotWrapper.AddToClassList("queue-wrapper");
                iconBox.AddToClassList("timeline-icon-box");
                _turnQueueList.Add(slotWrapper);
            }
        }
    }

    private void UpdateTimelineTargetMarkers(IReadOnlyList<GameObject> targetedFighters)
    {
        if (_timelineTargetIndicators.Count == 0) return;

        HashSet<GameObject> targetSet = (targetedFighters != null)
            ? new HashSet<GameObject>(targetedFighters)
            : new HashSet<GameObject>();

        for (int i = 0; i < _timelineTargetIndicators.Count; i++)
        {
            var pair = _timelineTargetIndicators[i];
            GameObject fighter = pair.Key;
            VisualElement indicator = pair.Value;

            if (indicator == null) continue;

            if (fighter != null && targetSet.Contains(fighter))
            {
                indicator.RemoveFromClassList(TARGET_INDICATOR_HIDDEN_CLASS);
            }
            else
            {
                indicator.AddToClassList(TARGET_INDICATOR_HIDDEN_CLASS);
            }
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

    private void UpdateFloatingCommandMenuPosition()
    {
        if (_floatingCommandMenuContainer == null || _currentActiveFighter == null || mainBattleCamera == null || _root == null) return;

        if (_floatingCommandMenuContainer.style.display == DisplayStyle.None) return;

        // Projection position centered on active hero transform origin
        Vector3 worldPos = _currentActiveFighter.transform.position + _commandCrossOffset;

        Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, worldPos, mainBattleCamera);

        Vector3 screenPoint = mainBattleCamera.WorldToScreenPoint(worldPos);
        if (screenPoint.z < 0)
        {
            _floatingCommandMenuContainer.style.visibility = Visibility.Hidden;
            return;
        }

        _floatingCommandMenuContainer.style.visibility = Visibility.Visible;

        // Assign world-to-panel position; CSS handles exact center alignment
        _floatingCommandMenuContainer.style.left = panelPos.x;
        _floatingCommandMenuContainer.style.top = panelPos.y;
    }

    #endregion

    #region Action Logic & Controls

    private void OnCommandSelected(BattleCommandType commandType)
    {
        Debug.Log($"[{nameof(BattleHUDController)}] Selected Battle Command: {commandType}");
    }

    public void SetHUDVisible(bool visible)
    {
        if (_root != null)
        {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    /// <summary>
    /// Controls visibility and plays entrance/exit USS animations for hero turn UI elements.
    /// </summary>
    public void SetHeroTurnUIElementsVisible(bool visible)
    {
        if (_portraitViewportFrame != null)
        {
            if (visible)
            {
                _portraitViewportFrame.style.display = DisplayStyle.Flex;
                _portraitViewportFrame.RemoveFromClassList(PORTRAIT_HIDDEN_CLASS);
            }
            else
            {
                _portraitViewportFrame.AddToClassList(PORTRAIT_HIDDEN_CLASS);
                _portraitViewportFrame.schedule.Execute(() =>
                {
                    if (_portraitViewportFrame.ClassListContains(PORTRAIT_HIDDEN_CLASS))
                    {
                        _portraitViewportFrame.style.display = DisplayStyle.None;
                    }
                }).StartingIn(350);
            }
        }

        if (_floatingCommandMenuContainer != null)
        {
            _floatingCommandMenuContainer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
            {
                _floatingCommandMenuContainer.schedule.Execute(UpdateFloatingCommandMenuPosition);
            }
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

    public void SetActionDetailsPanelVisible(bool visible)
    {
        if (_actionDetailsPanel != null)
        {
            _actionDetailsPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    public void SetActionBannerVisible(bool visible)
    {
        if (_actionBannerContainer != null)
        {
            _actionBannerContainer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    #endregion
}