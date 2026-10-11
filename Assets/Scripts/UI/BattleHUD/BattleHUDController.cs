using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Controller for the Battle HUD overlay using UI Toolkit.
/// Orchestrates viewport transitions, turn timeline updates, floating target overlays,
/// action submenus, top action banners, layer visibility, results sequences, and projects floating menus over active heroes in 3D world space.
/// Enforces complete pointer-transparency as all battle HUD elements ignore raycasts declaratively.
/// Uses FighterBattleAnchors for precise 3D spatial position calculations.
/// Dynamically queries Camera.main for seamless Cinemachine integration.
/// </summary>
public class BattleHUDController : MonoBehaviour
{
    private const string FULLSCREEN_EXPAND_CLASS = "expanded-fullscreen";
    private const string ACTIVE_HERO_CLASS = "active-hero";
    private const string TARGET_INDICATOR_HIDDEN_CLASS = "timeline-target-indicator--hidden";
    private const string PORTRAIT_HIDDEN_CLASS = "portrait-viewport-frame--hidden";
    private const string LAYER_HIDDEN_CLASS = "hud-layer--hidden";
    private const string ACTION_BANNER_HIDDEN_CLASS = "action-banner-container--hidden";

    #region Serialized Fields

    [Header("Core Local References")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private TargetSelectionController targetSelectionController;

    [Header("UI Document & Templates")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset heroStatusCardTemplate;
    [SerializeField] private VisualTreeAsset targetInfoOverlayTemplate;

    [Header("SubMenu Card Templates")]
    [SerializeField] private VisualTreeAsset psynergyCardTemplate;
    [SerializeField] private VisualTreeAsset itemCardTemplate;
    [SerializeField] private VisualTreeAsset djinnCardTemplate;

    [Header("Sub-Presenters")]
    [SerializeField] private DamageNumbersPresenter damageNumbersPresenter;

    [Header("Close-Up Viewport Setup")]
    [SerializeField] private RenderTexture heroPortraitRenderTexture;
    [SerializeField] private Material heroPortraitMaterial;

    [Header("Banner Timings")]
    [Tooltip("Duration in milliseconds for the action banner to stay visible before auto-hiding.")]
    [SerializeField] private long bannerDisplayDurationMs = 2500;

    #endregion

    #region Private Local Cache State & Dynamic Helpers

    private GameObject _currentActiveFighter;
    private IReadOnlyList<GameObject> _cachedParty;

    /// <summary>
    /// Dynamic accessor for the currently active main rendering camera.
    /// Eliminates static inspector bindings and guarantees alignment with CinemachineBrain output.
    /// </summary>
    private Camera ActiveCamera => Camera.main;

    // 2D Screen Space Offset for SubMenu positioning relative to Command Cross
    private readonly Vector2 _subMenuScreenOffset = new Vector2(180f, 0f);

    // Presenter instances
    private CommandCrossPresenter _commandCrossPresenter;
    private BattleSubMenuPresenter _subMenuPresenter;

    // Floating Target Overlay Presenters
    private readonly List<TargetInfoPresenter> _targetPresenters = new List<TargetInfoPresenter>();

    // Cache mapping fighters in current timeline to their UI target indicator elements
    private readonly List<KeyValuePair<GameObject, VisualElement>> _timelineTargetIndicators = new List<KeyValuePair<GameObject, VisualElement>>();

    private BattleCommandType _currentSelectedCommand = BattleCommandType.Attack;

    // Schedules for animations and auto-hide logic
    private IVisualElementScheduledItem _bannerAutoCloseSchedule;
    private IVisualElementScheduledItem _bannerHideTransitionSchedule;

    private Coroutine _resultsSequenceCoroutine;

    #endregion

    #region Properties & Presenters

    /// <summary>
    /// Presenter managing sequential Dragon Quest / Golden Sun victory results flow.
    /// </summary>
    public ResultsScreenPresenter ResultsPresenter { get; private set; }

    #endregion

    #region UI Elements & Layer References

    private VisualElement _root;

    // --- HUD LAYERS ---
    private VisualElement _layerGameplay;
    private VisualElement _layerResults;
    private VisualElement _layerGameOver;

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

    // 5. Floating Action SubMenu Container & Instance
    private VisualElement _floatingSubMenuContainer;
    private VisualElement _subMenuInstance;

    // 6. Party Status Cards Container
    private VisualElement _bottomLeftPartyContainer;
    private readonly List<HeroStatusCardPresenter> _partyCardPresenters = new List<HeroStatusCardPresenter>();

    #endregion

    #region Unity Lifecycle & Updates

    private void Awake()
    {
        if (battleManager == null)
        {
            battleManager = GetComponent<BattleManager>() ?? GetComponentInParent<BattleManager>();
        }

        InitializeUIElements();
    }

    private void Start()
    {
        if (damageNumbersPresenter != null)
        {
            damageNumbersPresenter.Initialize(uiDocument, ActiveCamera);
        }
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
        UpdateFloatingUIElementsPosition();
        UpdateTargetOverlaysPosition();
    }

    private void OnDestroy()
    {
        ReleaseMaskedRenderTexture();

        if (_commandCrossPresenter != null)
        {
            _commandCrossPresenter.OnCommandConfirmed -= OnCommandSelected;
        }

        if (_subMenuPresenter != null)
        {
            _subMenuPresenter.OnActionSelected -= OnSubMenuActionSelected;
            _subMenuPresenter.OnSubMenuCancelled -= HandleSubMenuCancelled;
        }
    }

    #endregion

    #region Initialization Methods

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

        // --- LAYER RESOLUTION ---
        _layerGameplay = _root.Q<VisualElement>("layer_gameplay_hud");
        _layerResults = _root.Q<VisualElement>("layer_results_hud");
        _layerGameOver = _root.Q<VisualElement>("layer_gameover_hud");

        if (_layerResults != null)
        {
            ResultsPresenter = new ResultsScreenPresenter(_layerResults);
        }

        // Viewport Elements
        _portraitViewportFrame = _root.Q<VisualElement>("portrait_viewport_frame");
        _labelActiveHeroName = _root.Q<Label>("label_active_hero_name");
        _portraitRenderElement = _root.Q<VisualElement>("portrait_viewport_render");

        if (_portraitViewportFrame != null)
        {
            _portraitViewportFrame.AddToClassList(PORTRAIT_HIDDEN_CLASS);
        }

        // Action Banner Elements
        _actionBannerContainer = _root.Q<VisualElement>("action_banner_container");
        _labelActionBannerTitle = _root.Q<Label>("label_action_banner_title");
        _labelActionBannerSubtext = _root.Q<Label>("label_action_banner_subtext");

        if (_actionBannerContainer != null)
        {
            _actionBannerContainer.AddToClassList(ACTION_BANNER_HIDDEN_CLASS);
            _actionBannerContainer.style.display = DisplayStyle.None;
        }

        SetupPortraitMaskedRenderTexture();

        // Turn Timeline
        _turnLeaderSlot = _root.Q<VisualElement>("turn_leader_slot");
        _turnQueueList = _root.Q<VisualElement>("turn_queue_list");

        // Floating Command Cross Container Setup
        _floatingCommandMenuContainer = _root.Q<VisualElement>("floating_command_menu_container");
        _commandCrossOverlayInstance = _root.Q<VisualElement>("command_cross_overlay");

        if (_floatingCommandMenuContainer != null)
        {
            _floatingCommandMenuContainer.style.display = DisplayStyle.Flex;
            _floatingCommandMenuContainer.style.visibility = Visibility.Hidden;
        }

        InitializeCommandCrossPresenter();

        // Floating Action SubMenu Container Setup
        _floatingSubMenuContainer = _root.Q<VisualElement>("floating_submenu_container");
        _subMenuInstance = _root.Q<VisualElement>("battle_submenu_instance");

        if (_floatingSubMenuContainer != null)
        {
            _floatingSubMenuContainer.style.display = DisplayStyle.Flex;
            _floatingSubMenuContainer.style.visibility = Visibility.Hidden;
        }

        InitializeSubMenuPresenter();

        // Party Cards Container
        _bottomLeftPartyContainer = _root.Q<VisualElement>("bottom_left_party_container");

        // Initial Visibility Reset
        SetHUDVisible(false);
        SetResultsLayerVisible(false);
        SetGameOverLayerVisible(false);

        SetHeroTurnUIElementsVisible(false);
        SetSubMenuVisible(false);
        HideActionBannerInternal(immediate: true);
    }

    private void InitializeCommandCrossPresenter()
    {
        VisualElement targetElement = _commandCrossOverlayInstance ?? _root.Q<VisualElement>("command_cross_root");
        if (targetElement != null)
        {
            _commandCrossPresenter = new CommandCrossPresenter(targetElement);
            _commandCrossPresenter.OnCommandConfirmed += OnCommandSelected;
        }
    }

    private void InitializeSubMenuPresenter()
    {
        VisualElement rootElement = _subMenuInstance ?? _floatingSubMenuContainer;
        if (rootElement != null)
        {
            _subMenuPresenter = new BattleSubMenuPresenter(
                rootElement,
                psynergyCardTemplate,
                itemCardTemplate,
                djinnCardTemplate
            );

            _subMenuPresenter.OnActionSelected += OnSubMenuActionSelected;
            _subMenuPresenter.OnSubMenuCancelled += HandleSubMenuCancelled;
        }
    }

    #endregion

    #region Close-Up Viewport Mask Processing

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

        heroPortraitMaterial.SetTexture("_MainTex", heroPortraitRenderTexture);
        Graphics.Blit(heroPortraitRenderTexture, _maskedRenderTexture, heroPortraitMaterial);
        _portraitRenderElement.style.backgroundImage = Background.FromRenderTexture(_maskedRenderTexture);
    }

    private void UpdatePortraitRenderTexture()
    {
        if (_portraitViewportFrame == null || _portraitViewportFrame.ClassListContains(PORTRAIT_HIDDEN_CLASS)) return;

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

        GameEvents.Camera.OnActiveCameraChanged -= HandleActiveCameraChanged;
        GameEvents.Camera.OnActiveCameraChanged += HandleActiveCameraChanged;

        GameEvents.Battle.OnTurnQueueUpdated -= HandleTurnQueueUpdated;
        GameEvents.Battle.OnTurnQueueUpdated += HandleTurnQueueUpdated;

        GameEvents.Battle.OnRequestActionBanner -= HandleRequestActionBanner;
        GameEvents.Battle.OnRequestActionBanner += HandleRequestActionBanner;

        GameEvents.Input.OnBattleConfirmPressed -= HandleConfirmPressed;
        GameEvents.Input.OnBattleConfirmPressed += HandleConfirmPressed;

        GameEvents.Input.OnBattleCancelPressed -= HandleCancelPressed;
        GameEvents.Input.OnBattleCancelPressed += HandleCancelPressed;

        GameEvents.Input.OnBattleCommandUpPressed += HandleHotkeyAttack;
        GameEvents.Input.OnBattleCommandLeftPressed += HandleHotkeyPsynergy;
        GameEvents.Input.OnBattleCommandRightPressed += HandleHotkeyItem;
        GameEvents.Input.OnBattleCommandDownPressed += HandleHotkeyDefend;
        GameEvents.Input.OnBattleWingLeftPressed += HandleHotkeyDjinn;
        GameEvents.Input.OnBattleWingRightPressed += HandleHotkeySummon;

        if (battleManager != null)
        {
            battleManager.OnBattleStateChanged -= HandleBattleStateChanged;
            battleManager.OnBattleStateChanged += HandleBattleStateChanged;

            battleManager.OnBattleResultsReady -= HandleBattleResultsReady;
            battleManager.OnBattleResultsReady += HandleBattleResultsReady;

            if (_subMenuPresenter != null)
            {
                battleManager.OnBattleStateChanged -= _subMenuPresenter.HandleBattleStateChanged;
                battleManager.OnBattleStateChanged += _subMenuPresenter.HandleBattleStateChanged;
            }

            battleManager.OnCommandTypeSelected -= HandleCommandTypeSelected;
            battleManager.OnCommandTypeSelected += HandleCommandTypeSelected;

            battleManager.OnTargetSelectionChanged -= HandleTargetSelectionChanged;
            battleManager.OnTargetSelectionChanged += HandleTargetSelectionChanged;
        }

        if (targetSelectionController != null)
        {
            targetSelectionController.OnTargetSelectionChanged -= HandleTargetSelectionChanged;
            targetSelectionController.OnTargetSelectionChanged += HandleTargetSelectionChanged;
        }
    }

    private void UnsubscribeEvents()
    {
        GameEvents.Party.OnPartyChanged -= HandlePartyChanged;
        GameEvents.Hero.OnHeroStatusChanged -= HandleHeroStatusChanged;
        GameEvents.GameState.OnGameStateChanged -= HandleGameStateChanged;
        GameEvents.Camera.OnActiveCameraChanged -= HandleActiveCameraChanged;
        GameEvents.Battle.OnTurnQueueUpdated -= HandleTurnQueueUpdated;
        GameEvents.Battle.OnRequestActionBanner -= HandleRequestActionBanner;

        GameEvents.Input.OnBattleConfirmPressed -= HandleConfirmPressed;
        GameEvents.Input.OnBattleCancelPressed -= HandleCancelPressed;

        GameEvents.Input.OnBattleCommandUpPressed -= HandleHotkeyAttack;
        GameEvents.Input.OnBattleCommandLeftPressed -= HandleHotkeyPsynergy;
        GameEvents.Input.OnBattleCommandRightPressed -= HandleHotkeyItem;
        GameEvents.Input.OnBattleCommandDownPressed -= HandleHotkeyDefend;
        GameEvents.Input.OnBattleWingLeftPressed -= HandleHotkeyDjinn;
        GameEvents.Input.OnBattleWingRightPressed -= HandleHotkeySummon;

        if (battleManager != null)
        {
            battleManager.OnBattleStateChanged -= HandleBattleStateChanged;
            battleManager.OnBattleResultsReady -= HandleBattleResultsReady;
            battleManager.OnCommandTypeSelected -= HandleCommandTypeSelected;
            battleManager.OnTargetSelectionChanged -= HandleTargetSelectionChanged;

            if (_subMenuPresenter != null)
            {
                battleManager.OnBattleStateChanged -= _subMenuPresenter.HandleBattleStateChanged;
            }
        }

        if (targetSelectionController != null)
        {
            targetSelectionController.OnTargetSelectionChanged -= HandleTargetSelectionChanged;
        }
    }

    private void HandleActiveCameraChanged(Camera newCamera)
    {
        if (damageNumbersPresenter != null && newCamera != null)
        {
            damageNumbersPresenter.Initialize(uiDocument, newCamera);
        }

        UpdateFloatingUIElementsPosition();
        UpdateTargetOverlaysPosition();
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
            SetResultsLayerVisible(false);
            SetGameOverLayerVisible(false);
            RebuildPartyCards();
        }
        else
        {
            if (_resultsSequenceCoroutine != null)
            {
                StopCoroutine(_resultsSequenceCoroutine);
                _resultsSequenceCoroutine = null;
            }

            SetHUDVisible(false);
            SetResultsLayerVisible(false);
            SetGameOverLayerVisible(false);
            SetHeroTurnUIElementsVisible(false);
            SetSubMenuVisible(false);
            HideActionBannerInternal(immediate: true);
            ClearTargetOverlays();

            ResultsPresenter?.HideAll();
        }
    }

    private void HandleConfirmPressed(InputEventArgs args)
    {
        if (args.Handled) return;

        if (battleManager != null && battleManager.CurrentBattleState == BattleState.ResultsScreen)
        {
            if (ResultsPresenter != null)
            {
                args.Use();
                ResultsPresenter.HandleConfirmInput();
            }
        }
    }

    private void HandleCancelPressed(InputEventArgs args)
    {
        if (args.Handled) return;

        if (battleManager != null)
        {
            if (battleManager.CurrentBattleState == BattleState.CommandSelection)
            {
                args.Use();
                battleManager.HandleCancelCommandSelection();
            }
        }
    }

    private void HandleCommandTypeSelected(BattleCommandType commandType)
    {
        _currentSelectedCommand = commandType;
    }

    private void HandleBattleResultsReady(IReadOnlyList<GameObject> heroes, BattleRewardSummary rewards, Action onCompleted)
    {
        if (_resultsSequenceCoroutine != null)
        {
            StopCoroutine(_resultsSequenceCoroutine);
        }

        _resultsSequenceCoroutine = StartCoroutine(PlayResultsSequenceRoutine(heroes, rewards, onCompleted));
    }

    private IEnumerator PlayResultsSequenceRoutine(IReadOnlyList<GameObject> heroes, BattleRewardSummary rewards, Action onCompleted)
    {
        if (ResultsPresenter != null)
        {
            BattleCameraController cameraController = null;
            yield return ResultsPresenter.PlayResultsSequence(heroes, rewards, cameraController);
        }
        else
        {
            Debug.LogWarning($"[{nameof(BattleHUDController)}] ResultsPresenter is missing. Transitioning out immediately.");
        }

        _resultsSequenceCoroutine = null;
        onCompleted?.Invoke();
    }

    private void HandleBattleStateChanged(BattleState newState)
    {
        HeroStats heroStats = _currentActiveFighter != null ? _currentActiveFighter.GetComponent<HeroStats>() : null;
        bool isHeroTurn = heroStats != null;

        switch (newState)
        {
            case BattleState.CommandSelection:
                SetHUDVisible(true);
                SetResultsLayerVisible(false);
                SetGameOverLayerVisible(false);
                ClearTargetOverlays();
                SetSubMenuVisible(false);
                HideActionBannerInternal(immediate: true);

                if (isHeroTurn)
                {
                    UpdateFloatingUIElementsPosition();
                    SetHeroTurnUIElementsVisible(true);

                    bool hasPsynergyAvailable = heroStats.Psynergies != null &&
                                                heroStats.Psynergies.Exists(p => p != null && p.IsBattlePsynergy);

                    bool hasItemsAvailable = heroStats.HeroInventory != null &&
                                             heroStats.HeroInventory.HasUsableBattleItems();

                    bool hasDjinnsUnlocked = false;
                    bool hasSummonsUnlocked = false;

                    _commandCrossPresenter?.Show(hasPsynergyAvailable, hasItemsAvailable, hasDjinnsUnlocked, hasSummonsUnlocked);
                }
                else
                {
                    SetHeroTurnUIElementsVisible(false);
                }
                break;

            case BattleState.SubMenuSelection:
                _commandCrossPresenter?.Hide();
                ClearTargetOverlays();
                OpenSubMenuForActiveHero();
                break;

            case BattleState.TargetSelection:
                _commandCrossPresenter?.Hide();
                SetSubMenuVisible(false);
                break;

            case BattleState.WaitingTurnExecution:
            case BattleState.IntroTransition:
                _commandCrossPresenter?.Hide();
                SetHeroTurnUIElementsVisible(false);
                SetSubMenuVisible(false);
                HideActionBannerInternal(immediate: true);
                ClearTargetOverlays();
                SetResultsLayerVisible(false);
                break;

            case BattleState.VictoryFanfare:
                SetHUDVisible(false);
                SetHeroTurnUIElementsVisible(false);
                SetSubMenuVisible(false);
                HideActionBannerInternal(immediate: true);
                ClearTargetOverlays();
                break;

            case BattleState.ResultsScreen:
                SetHUDVisible(false);
                SetHeroTurnUIElementsVisible(false);
                SetSubMenuVisible(false);
                HideActionBannerInternal(immediate: true);
                ClearTargetOverlays();
                SetResultsLayerVisible(true);
                break;

            case BattleState.GameOverScreen:
                SetHUDVisible(false);
                SetHeroTurnUIElementsVisible(false);
                SetSubMenuVisible(false);
                HideActionBannerInternal(immediate: true);
                ClearTargetOverlays();
                SetResultsLayerVisible(false);
                SetGameOverLayerVisible(true);
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

                presenter.UpdateData(fighterObj.GetComponent<FighterStats>().Name, level);
                presenter.SetVisible(true);

                _targetPresenters.Add(presenter);
                _root.Add(overlayInstance);
            }
        }

        UpdateTimelineTargetMarkers(targets);
        UpdateTargetOverlaysPosition();
    }

    private void HandleRequestActionBanner(string title, string subtext, long? customDurationMs)
    {
        if (_actionBannerContainer == null) return;

        _bannerAutoCloseSchedule?.Pause();
        _bannerHideTransitionSchedule?.Pause();

        if (_labelActionBannerTitle != null)
        {
            _labelActionBannerTitle.text = title.ToUpper();
        }

        if (_labelActionBannerSubtext != null)
        {
            _labelActionBannerSubtext.text = subtext;
            _labelActionBannerSubtext.style.display = string.IsNullOrEmpty(subtext)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        _actionBannerContainer.style.display = DisplayStyle.Flex;
        _actionBannerContainer.RemoveFromClassList(ACTION_BANNER_HIDDEN_CLASS);

        long effectiveDuration = (customDurationMs.HasValue && customDurationMs.Value > 0)
            ? customDurationMs.Value
            : bannerDisplayDurationMs;

        _bannerAutoCloseSchedule = _actionBannerContainer.schedule.Execute(() =>
        {
            HideActionBannerInternal(immediate: false);
        }).StartingIn(effectiveDuration);
    }

    private void HideActionBannerInternal(bool immediate = false)
    {
        if (_actionBannerContainer == null) return;

        _bannerAutoCloseSchedule?.Pause();
        _bannerHideTransitionSchedule?.Pause();

        if (immediate)
        {
            _actionBannerContainer.AddToClassList(ACTION_BANNER_HIDDEN_CLASS);
            _actionBannerContainer.style.display = DisplayStyle.None;
        }
        else
        {
            _actionBannerContainer.AddToClassList(ACTION_BANNER_HIDDEN_CLASS);

            _bannerHideTransitionSchedule = _actionBannerContainer.schedule.Execute(() =>
            {
                if (_actionBannerContainer.ClassListContains(ACTION_BANNER_HIDDEN_CLASS))
                {
                    _actionBannerContainer.style.display = DisplayStyle.None;
                }
            }).StartingIn(300);
        }
    }

    #endregion

    #region Command Cross Hotkey & Action Processing

    private void HandleHotkeyAttack() => TryTriggerCommand(BattleCommandType.Attack);
    private void HandleHotkeyPsynergy() => TryTriggerCommand(BattleCommandType.Psynergy);
    private void HandleHotkeyItem() => TryTriggerCommand(BattleCommandType.Item);
    private void HandleHotkeyDefend() => TryTriggerCommand(BattleCommandType.Defend);
    private void HandleHotkeyDjinn() => TryTriggerCommand(BattleCommandType.Djinn);
    private void HandleHotkeySummon() => TryTriggerCommand(BattleCommandType.Summon);

    private void TryTriggerCommand(BattleCommandType commandType)
    {
        if (battleManager == null || battleManager.CurrentBattleState != BattleState.CommandSelection) return;

        HeroStats heroStats = _currentActiveFighter != null ? _currentActiveFighter.GetComponent<HeroStats>() : null;
        if (heroStats == null) return;

        if (commandType == BattleCommandType.Psynergy)
        {
            bool hasPsynergy = heroStats.Psynergies != null && heroStats.Psynergies.Exists(p => p != null && p.IsBattlePsynergy);
            if (!hasPsynergy)
            {
                GameEvents.Audio.RequestPlaySFX(SFXType.UI_Cancel);
                return;
            }
        }
        else if (commandType == BattleCommandType.Item)
        {
            bool hasItems = heroStats.HeroInventory != null && heroStats.HeroInventory.HasUsableBattleItems();
            if (!hasItems)
            {
                GameEvents.Audio.RequestPlaySFX(SFXType.UI_Cancel);
                return;
            }
        }
        else if (commandType == BattleCommandType.Djinn)
        {
            bool hasDjinnsUnlocked = false;
            if (!hasDjinnsUnlocked) return;
        }
        else if (commandType == BattleCommandType.Summon)
        {
            bool hasSummonsUnlocked = false;
            if (!hasSummonsUnlocked) return;
        }

        _currentSelectedCommand = commandType;
        GameEvents.Audio.RequestPlaySFX(SFXType.UI_Confirm);
        battleManager.ExecuteCommandFromUI(commandType);
    }

    #endregion

    #region SubMenu Population Logic

    private void OpenSubMenuForActiveHero()
    {
        if (_subMenuPresenter == null || _currentActiveFighter == null) return;

        HeroStats heroStats = _currentActiveFighter.GetComponent<HeroStats>();
        if (heroStats == null) return;

        switch (_currentSelectedCommand)
        {
            case BattleCommandType.Psynergy:
                _subMenuPresenter.ShowPsynergies(heroStats.Psynergies, heroStats.CurrPp);
                break;

            case BattleCommandType.Item:
                HeroInventory inventory = _currentActiveFighter.GetComponent<HeroInventory>();
                if (inventory != null)
                {
                    _subMenuPresenter.ShowItems(inventory.Slots);
                }
                break;

            case BattleCommandType.Djinn:
                HeroDjinnController djinnController = heroStats.DjinnController;
                if (djinnController != null)
                {
                    _subMenuPresenter.ShowDjinns(djinnController.AssignedDjinns);
                }
                break;
        }

        SetSubMenuVisible(true);
        UpdateFloatingUIElementsPosition();
    }

    #endregion

    #region Universal Projection Helper (3D World -> 2D Panel)

    public bool TryProjectWorldToPanel(Vector3 worldPosition, Vector3 worldOffset, out Vector2 panelPosition)
    {
        panelPosition = Vector2.zero;

        Camera currentCam = ActiveCamera;
        if (currentCam == null || _root == null || _root.panel == null)
            return false;

        Vector3 targetWorldPos = worldPosition + worldOffset;
        Vector3 screenPoint = currentCam.WorldToScreenPoint(targetWorldPos);

        if (screenPoint.z < 0) return false;

        panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, targetWorldPos, currentCam);
        return true;
    }

    private void UpdateFloatingUIElementsPosition()
    {
        if (_currentActiveFighter == null) return;

        Vector3 crossWorldPos = _currentActiveFighter.TryGetComponent<FighterBattleAnchors>(out var anchors)
            ? anchors.GetCommandCrossWorldPosition()
            : _currentActiveFighter.transform.position;

        if (!TryProjectWorldToPanel(crossWorldPos, Vector3.zero, out Vector2 basePanelPos))
        {
            if (_floatingCommandMenuContainer != null) _floatingCommandMenuContainer.style.visibility = Visibility.Hidden;
            if (_floatingSubMenuContainer != null) _floatingSubMenuContainer.style.visibility = Visibility.Hidden;
            return;
        }

        if (_floatingCommandMenuContainer != null && _floatingCommandMenuContainer.style.visibility == Visibility.Visible)
        {
            _floatingCommandMenuContainer.style.left = basePanelPos.x;
            _floatingCommandMenuContainer.style.top = basePanelPos.y;
        }

        if (_floatingSubMenuContainer != null && _floatingSubMenuContainer.style.visibility == Visibility.Visible)
        {
            Vector3 subMenuWorldPos = anchors != null
                ? anchors.GetSubMenuWorldPosition()
                : crossWorldPos;

            if (TryProjectWorldToPanel(subMenuWorldPos, Vector3.zero, out Vector2 subMenuPanelPos))
            {
                float panelWidth = _root.panel.visualTree.layout.width;
                float offsetX = _subMenuScreenOffset.x;

                if (subMenuPanelPos.x + offsetX + 210f > panelWidth)
                {
                    offsetX = -offsetX;
                }

                _floatingSubMenuContainer.style.left = subMenuPanelPos.x + offsetX;
                _floatingSubMenuContainer.style.top = subMenuPanelPos.y + _subMenuScreenOffset.y;
            }
        }
    }

    #endregion

    #region Timeline & Overlays Population

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
        if (_targetPresenters.Count == 0) return;

        Camera currentCam = ActiveCamera;
        if (currentCam == null) return;

        for (int i = 0; i < _targetPresenters.Count; i++)
        {
            _targetPresenters[i].UpdatePanelPosition(_root.panel, currentCam, Vector3.zero);
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
            presenter.UpdateData(stats, heroObj.name, keyNumber: i + 1, isActiveHero: isActive, inBattle: true);

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
                presenter.UpdateData(stats, presenter.BoundHero.name, keyNumber: i + 1, isActiveHero: isActive, inBattle: true);
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

    #region Action Logic & Layer Visibility API

    private void OnCommandSelected(BattleCommandType commandType)
    {
        TryTriggerCommand(commandType);
    }

    private void OnSubMenuActionSelected(BattleCommandType commandType, object selectedData)
    {
        if (battleManager != null)
        {
            battleManager.HandleSubMenuActionSelected(commandType, selectedData);
        }
    }

    private void HandleSubMenuCancelled()
    {
        if (battleManager != null)
        {
            battleManager.HandleSubMenuCancelled();
        }
    }

    public void SetHUDVisible(bool visible)
    {
        if (_layerGameplay != null)
        {
            if (!visible && _root != null)
            {
                _root.focusController?.focusedElement?.Blur();
            }

            if (visible)
            {
                _layerGameplay.RemoveFromClassList(LAYER_HIDDEN_CLASS);
                _layerGameplay.style.display = DisplayStyle.Flex;
            }
            else
            {
                _layerGameplay.AddToClassList(LAYER_HIDDEN_CLASS);
                _layerGameplay.style.display = DisplayStyle.None;
            }
        }
    }

    public void SetResultsLayerVisible(bool visible)
    {
        if (_layerResults != null)
        {
            if (visible)
            {
                _layerResults.RemoveFromClassList(LAYER_HIDDEN_CLASS);
                _layerResults.style.display = DisplayStyle.Flex;
            }
            else
            {
                _layerResults.AddToClassList(LAYER_HIDDEN_CLASS);
                _layerResults.style.display = DisplayStyle.None;
            }
        }
    }

    public void SetGameOverLayerVisible(bool visible)
    {
        if (_layerGameOver != null)
        {
            if (visible)
            {
                _layerGameOver.RemoveFromClassList(LAYER_HIDDEN_CLASS);
                _layerGameOver.style.display = DisplayStyle.Flex;
            }
            else
            {
                _layerGameOver.AddToClassList(LAYER_HIDDEN_CLASS);
                _layerGameOver.style.display = DisplayStyle.None;
            }
        }
    }

    public void SetHeroTurnUIElementsVisible(bool visible)
    {
        if (_portraitViewportFrame != null)
        {
            if (visible)
            {
                _portraitViewportFrame.RemoveFromClassList(PORTRAIT_HIDDEN_CLASS);
            }
            else
            {
                _portraitViewportFrame.AddToClassList(PORTRAIT_HIDDEN_CLASS);
            }
        }

        if (_floatingCommandMenuContainer != null)
        {
            if (!visible && _root != null)
            {
                _floatingCommandMenuContainer.focusController?.focusedElement?.Blur();
            }

            _floatingCommandMenuContainer.style.display = DisplayStyle.Flex;
            _floatingCommandMenuContainer.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
        }
    }

    public void SetSubMenuVisible(bool visible)
    {
        if (!visible && _root != null)
        {
            _root.focusController?.focusedElement?.Blur();
        }

        if (_floatingSubMenuContainer != null)
        {
            _floatingSubMenuContainer.style.display = DisplayStyle.Flex;
            _floatingSubMenuContainer.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;

            if (visible)
            {
                _floatingSubMenuContainer.schedule.Execute(UpdateFloatingUIElementsPosition);
                _subMenuPresenter?.Show();
            }
            else
            {
                _subMenuPresenter?.Hide();
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

    #endregion
}